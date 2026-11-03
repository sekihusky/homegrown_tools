using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

public class VideoEngine
{
    public volatile bool Cancelled;
    public static string Tools = AppDomain.CurrentDomain.BaseDirectory;
    public static string Quote(string s) { return "\"" + s + "\""; }
    public static Process Start(string tool, string args)
    {
        var p = new Process();
        p.StartInfo = new ProcessStartInfo(Path.Combine(Tools, tool), args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        p.Start(); return p;
    }
    public static Dictionary<string, object> Probe(string path)
    {
        using (var p = Start("ffprobe.exe", "-v error -show_streams -show_format -of json " + Quote(path)))
        {
            var error = p.StandardError.ReadToEndAsync();
            string json = p.StandardOutput.ReadToEnd(); p.WaitForExit();
            if (p.ExitCode != 0) throw new Exception("無法讀取影片：" + error.Result);
            return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
        }
    }
    public static double Duration(Dictionary<string, object> data)
    {
        var f = (Dictionary<string, object>)data["format"];
        double d; return f.ContainsKey("duration") && double.TryParse(Convert.ToString(f["duration"]), NumberStyles.Float, CultureInfo.InvariantCulture, out d) ? d : 0;
    }
    public string EncoderUsed;
    public static string EncodingOptions(bool h264, int quality, bool speed, string hardware)
    {
        int q = (h264 ? new int[] { 18, 21, 24 } : new int[] { 20, 23, 26 })[quality];
        string encoder = hardware == "cpu" ? (h264 ? "libx264" : "libx265") : (h264 ? "h264_" : "hevc_") + hardware;
        string options = " -c:v " + encoder;
        if (hardware == "cpu") options += " -preset " + (speed ? "veryfast" : "medium") + " -crf " + q;
        else if (hardware == "nvenc") options += " -preset " + (speed ? "p2" : "p6") + " -rc vbr -cq " + q + " -b:v 0";
        else if (hardware == "qsv") options += " -preset " + (speed ? "veryfast" : "medium") + " -global_quality " + q;
        else if (hardware == "amf") options += " -quality " + (speed ? "speed" : "quality") + " -rc cqp -qp_i " + q + " -qp_p " + q;
        else throw new ArgumentException("未知的硬體編碼器。");
        return options + " -pix_fmt yuv420p" + (h264 ? "" : " -tag:v hvc1");
    }
    public static bool HardwareAvailable(bool h264, string hardware)
    {
        // A real encode checks drivers and devices, not just FFmpeg's compiled encoder list.
        try
        {
            using (var p = Start("ffmpeg.exe", "-hide_banner -nostdin -v error -f lavfi -i color=size=128x128:rate=30 -frames:v 3" + EncodingOptions(h264, 0, true, hardware) + " -f null -"))
            {
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(8000)) { try { p.Kill(); } catch {} p.WaitForExit(); return false; }
                return p.ExitCode == 0;
            }
        }
        catch { return false; }
    }
    public void Compress(string input, string output, int quality, bool h264, bool sample, Action<int> progress)
    { Compress(input, output, quality, h264, sample, false, "cpu", progress); }
    public void Compress(string input, string output, int quality, bool h264, bool sample, bool speed, string hardware, Action<int> progress)
    {
        input = Path.GetFullPath(input); output = Path.GetFullPath(output);
        if (!File.Exists(input)) throw new Exception("找不到來源影片。");
        if (String.Equals(input, output, StringComparison.OrdinalIgnoreCase) || File.Exists(output)) throw new Exception("輸出檔案已存在，請選擇新檔名；不會覆蓋原檔。");
        if (!String.Equals(Path.GetExtension(output), ".mp4", StringComparison.OrdinalIgnoreCase)) throw new Exception("輸出檔名必須以 .mp4 結尾。");
        var info = Probe(input);
        Dictionary<string, object> video = null;
        foreach (var item in (System.Collections.IEnumerable)info["streams"])
        {
            var stream = (Dictionary<string, object>)item;
            if (Convert.ToString(stream["codec_type"]) == "video") { video = stream; break; }
        }
        if (video == null) throw new Exception("檔案沒有影像軌道。");
        string transfer = video.ContainsKey("color_transfer") ? Convert.ToString(video["color_transfer"]) : "";
        if (transfer == "smpte2084" || transfer == "arib-std-b67") throw new Exception("此影片為 HDR；第一版暫不處理，避免改變色彩與亮度。");
        double duration = Duration(info); if (sample) duration = Math.Min(duration, 10);
        EncoderUsed = hardware == "cpu" ? "CPU" : hardware.ToUpperInvariant();
        string temp = Path.Combine(Path.GetDirectoryName(output), ".video-compressor-" + Guid.NewGuid().ToString("N") + ".mp4");
        string args = "-hide_banner -nostdin -n -i " + Quote(input) + (sample ? " -t 10" : "") +
            " -map 0:v:0 -map 0:a? -map_metadata 0 -map_chapters 0" + EncodingOptions(h264, quality, speed, hardware) +
            " -fps_mode passthrough -c:a copy -movflags +faststart -progress pipe:1 -nostats " + Quote(temp);
        try
        {
            if (Cancelled) throw new OperationCanceledException();
            using (var p = Start("ffmpeg.exe", args))
            {
                var errors = new System.Text.StringBuilder();
                p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) { lock(errors) { if (errors.Length > 12000) errors.Remove(0, 6000); errors.AppendLine(e.Data); } } };
                p.BeginErrorReadLine();
                using (var timer = new System.Threading.Timer(delegate { if (Cancelled) { try { if (!p.HasExited) p.Kill(); } catch {} } }, null, 0, 100))
                {
                    string line;
                    while ((line = p.StandardOutput.ReadLine()) != null)
                    {
                        long us;
                        if (line.StartsWith("out_time_us=") && long.TryParse(line.Substring(12), out us) && duration > 0)
                            progress(Math.Max(0, Math.Min(99, (int)(us / 1000000.0 / duration * 100))));
                    }
                    p.WaitForExit();
                    if (Cancelled) throw new OperationCanceledException();
                    if (p.ExitCode != 0) throw new Exception("壓縮失敗：\r\n" + errors.ToString());
                }
            }
            if (!File.Exists(temp) || new FileInfo(temp).Length == 0) throw new Exception("沒有產生有效的輸出檔。");
            Probe(temp);
            if (Cancelled) throw new OperationCanceledException();
            File.Move(temp, output); progress(100);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

public class VideoCompressor : Form
{
    TextBox source = new TextBox(), destination = new TextBox();
    ComboBox quality = new ComboBox(), codec = new ComboBox(), priority = new ComboBox(), acceleration = new ComboBox();
    Dictionary<bool, List<string>> available = new Dictionary<bool, List<string>>();
    bool detecting = true;
    Button choose = new Button(), save = new Button(), run = new Button(), preview = new Button(), cancel = new Button(), folder = new Button();
    ProgressBar bar = new ProgressBar(); Label status = new Label();
    BackgroundWorker worker; VideoEngine engine; string lastOutput;
    public VideoCompressor()
    {
        Text = "影片減肥 · MP4 壓縮工具"; ClientSize = new Size(740, 590); MinimumSize = new Size(756, 629);
        Font = new Font("Microsoft JhengHei UI", 10); BackColor = Color.FromArgb(245, 247, 250);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        AddLabel("影片減肥", 24, 18, 680, 36).Font = new Font(Font.FontFamily, 21, FontStyle.Bold);
        AddLabel("盡量維持視覺畫質，減少影片容量。原始影片會保留。", 26, 63, 680, 28);
        AddLabel("來源 MP4", 26, 105, 120, 24); source.SetBounds(26, 133, 565, 30); source.ReadOnly = true; Controls.Add(source);
        Setup(choose, "選取影片", 605, 130, 108); choose.Click += delegate { using(var d = new OpenFileDialog { Filter = "MP4 影片|*.mp4", CheckFileExists = true }) if(d.ShowDialog() == DialogResult.OK) { source.Text = d.FileName; destination.Text = Path.Combine(Path.GetDirectoryName(d.FileName), Path.GetFileNameWithoutExtension(d.FileName) + "_compressed.mp4"); } };
        AddLabel("輸出新檔", 26, 177, 120, 24); destination.SetBounds(26, 204, 565, 30); Controls.Add(destination);
        Setup(save, "另存位置", 605, 201, 108); save.Click += delegate { using(var d = new SaveFileDialog { Filter = "MP4 影片|*.mp4", FileName = destination.Text, OverwritePrompt = true }) if(d.ShowDialog() == DialogResult.OK) destination.Text = d.FileName; };
        AddLabel("壓縮程度", 26, 249, 130, 24); quality.SetBounds(26, 278, 220, 30); quality.DropDownStyle = ComboBoxStyle.DropDownList; quality.Items.AddRange(new object[] { "畫質優先（建議）", "平衡", "容量優先" }); quality.SelectedIndex = 0; Controls.Add(quality);
        AddLabel("影片編碼", 268, 249, 180, 24); codec.SetBounds(268, 278, 445, 30); codec.DropDownStyle = ComboBoxStyle.DropDownList; codec.Items.AddRange(new object[] { "H.265 · 壓縮效率優先", "H.264 · 播放相容性優先" }); codec.SelectedIndex = 0; Controls.Add(codec);
        AddLabel("保留解析度、影格時間與音訊。H.265 播放需裝置支援；HDR 暫不支援。", 26, 319, 690, 25);
        AddLabel("處理偏好", 26, 354, 180, 24); priority.SetBounds(26, 382, 220, 30); priority.DropDownStyle = ComboBoxStyle.DropDownList; priority.Items.AddRange(new object[] { "速度優先", "容量優先" }); priority.SelectedIndex = 0; Controls.Add(priority);
        AddLabel("硬體加速（編碼）", 268, 354, 300, 24); acceleration.SetBounds(268, 382, 445, 30); acceleration.DropDownStyle = ComboBoxStyle.DropDownList; acceleration.Items.Add("偵測中…"); acceleration.SelectedIndex = 0; acceleration.Enabled = false; Controls.Add(acceleration);
        codec.SelectedIndexChanged += delegate { RefreshHardware(); };
        Shown += delegate { DetectHardware(); };
        Setup(run, "開始壓縮", 26, 446, 140); Setup(preview, "試壓前 10 秒", 180, 446, 155); Setup(cancel, "取消", 349, 446, 95); Setup(folder, "開啟輸出位置", 548, 446, 165);
        cancel.Enabled = false; folder.Enabled = false;
        run.Click += delegate { Begin(false); }; preview.Click += delegate { Begin(true); }; cancel.Click += delegate { if(engine != null) { engine.Cancelled = true; cancel.Enabled = false; status.Text = "正在取消，清理未完成檔案…"; } };
        folder.Click += delegate { if(lastOutput != null) Process.Start("explorer.exe", "/select," + VideoEngine.Quote(lastOutput)); };
        bar.SetBounds(26, 498, 687, 18); Controls.Add(bar); status.SetBounds(26, 529, 687, 48); status.Text = "先試壓並播放比較；實際縮小幅度取決於原始影片。"; Controls.Add(status);
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if(worker != null && worker.IsBusy) { e.Cancel = true; engine.Cancelled = true; status.Text = "正在取消，完成後即可關閉。"; } };
    }
    Label AddLabel(string text, int x, int y, int width, int height) { var l = new Label { Text = text }; l.SetBounds(x,y,width,height); Controls.Add(l); return l; }
    void Setup(Button b, string text, int x, int y, int width) { b.Text = text; b.SetBounds(x,y,width,36); Controls.Add(b); }
    void Busy(bool busy) { foreach(Control c in new Control[] { choose,save,run,preview,source,destination,quality,codec,priority,acceleration }) c.Enabled = !busy; cancel.Enabled = busy; }
    void RefreshHardware()
    {
        if (detecting) return;
        acceleration.Items.Clear();
        acceleration.Items.Add("自動（可用時使用硬體）"); acceleration.Items.Add("CPU 軟體編碼");
        foreach (string hw in available[codec.SelectedIndex == 1]) acceleration.Items.Add(hw == "nvenc" ? "NVIDIA NVENC" : hw == "qsv" ? "Intel Quick Sync" : "AMD AMF");
        acceleration.SelectedIndex = 0;
    }
    void DetectHardware()
    {
        run.Enabled = preview.Enabled = false; status.Text = "正在測試硬體編碼器，請稍候…";
        var detector = new BackgroundWorker();
        detector.DoWork += delegate {
            foreach (bool h264 in new bool[] { false, true }) {
                var list = new List<string>();
                foreach (string hw in new string[] { "nvenc", "qsv", "amf" }) if (VideoEngine.HardwareAvailable(h264, hw)) list.Add(hw);
                available[h264] = list;
            }
        };
        detector.RunWorkerCompleted += delegate {
            detecting = false; RefreshHardware(); acceleration.Enabled = run.Enabled = preview.Enabled = true;
            status.Text = "硬體偵測完成；自動模式無可用硬體時使用 CPU。建議先試壓前 10 秒。";
        };
        detector.RunWorkerAsync();
    }
    void Begin(bool sample)
    {
        if (detecting) return;
        if (!File.Exists(source.Text)) { MessageBox.Show("請先選取 MP4 影片。"); return; }
        string input = source.Text, output;
        try { output = Path.GetFullPath(destination.Text); if(sample) output = Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "_preview_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4"); }
        catch(Exception e) { MessageBox.Show(e.Message); return; }
        int q = quality.SelectedIndex; bool h264 = codec.SelectedIndex == 1;
        bool speed = priority.SelectedIndex == 0;
        var devices = available[h264];
        string hardware = acceleration.SelectedIndex == 1 || devices.Count == 0 ? "cpu" : devices[acceleration.SelectedIndex == 0 ? 0 : acceleration.SelectedIndex - 2];
        engine = new VideoEngine(); bar.Value = 0; Busy(true); status.Text = "正在分析與壓縮，請稍候…";
        worker = new BackgroundWorker { WorkerReportsProgress = true };
        worker.DoWork += delegate { engine.Compress(input, output, q, h264, sample, speed, hardware, delegate(int n) { worker.ReportProgress(n); }); };
        worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e) { bar.Value = e.ProgressPercentage; status.Text = engine.EncoderUsed + " · 壓縮進度 " + e.ProgressPercentage + "%"; };
        worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
        {
            Busy(false);
            if(e.Error != null) { status.Text = e.Error is OperationCanceledException ? "已取消，原檔保留。" : "壓縮失敗，原檔保留。"; if(!(e.Error is OperationCanceledException)) MessageBox.Show(e.Error.Message, "無法完成壓縮"); return; }
            lastOutput = output; folder.Enabled = true; long before = new FileInfo(input).Length, after = new FileInfo(output).Length;
            status.Text = engine.EncoderUsed + " · " + (sample ? "試壓完成：" + FormatSize(after) + "。請播放比較畫質；試壓大小不能直接與整支影片比較。" : "完成：" + FormatSize(before) + " → " + FormatSize(after) + (after < before ? "，減少 " + (100.0 * (before-after)/before).ToString("F1") + "%" : "。此設定未縮小，可試用其他壓縮程度。"));
        };
        worker.RunWorkerAsync();
    }
    static string FormatSize(long bytes) { return (bytes/1048576.0).ToString("F1") + " MB"; }
    [STAThread] public static int Main(string[] args)
    {
        if(args.Length >= 3 && args[0] == "--compress") { try { new VideoEngine().Compress(args[1],args[2], args.Length > 3 ? int.Parse(args[3]) : 0, args.Length > 4 && args[4] == "h264", false, delegate(int n) {}); return 0; } catch { return 1; } }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new VideoCompressor()); return 0;
    }
}
