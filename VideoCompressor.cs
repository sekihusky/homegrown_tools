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
    public void Compress(string input, string output, int quality, bool h264, bool sample, Action<int> progress)
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
        double duration = Duration(info); if (sample) duration = Math.Min(duration, 15);
        int crf = (h264 ? new int[] { 18, 21, 24 } : new int[] { 20, 23, 26 })[quality];
        string temp = Path.Combine(Path.GetDirectoryName(output), ".video-compressor-" + Guid.NewGuid().ToString("N") + ".mp4");
        string args = "-hide_banner -nostdin -n -i " + Quote(input) + (sample ? " -t 15" : "") +
            " -map 0:v:0 -map 0:a? -map_metadata 0 -map_chapters 0 -c:v " + (h264 ? "libx264" : "libx265") +
            " -preset medium -crf " + crf + (h264 ? " -pix_fmt yuv420p" : " -tag:v hvc1") +
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
    ComboBox quality = new ComboBox(), codec = new ComboBox();
    Button choose = new Button(), save = new Button(), run = new Button(), preview = new Button(), cancel = new Button(), folder = new Button();
    ProgressBar bar = new ProgressBar(); Label status = new Label();
    BackgroundWorker worker; VideoEngine engine; string lastOutput;
    public VideoCompressor()
    {
        Text = "影片減肥 · MP4 壓縮工具"; ClientSize = new Size(740, 500); MinimumSize = new Size(756, 539);
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
        Setup(run, "開始壓縮", 26, 356, 140); Setup(preview, "試壓前 15 秒", 180, 356, 155); Setup(cancel, "取消", 349, 356, 95); Setup(folder, "開啟輸出位置", 548, 356, 165);
        cancel.Enabled = false; folder.Enabled = false;
        run.Click += delegate { Begin(false); }; preview.Click += delegate { Begin(true); }; cancel.Click += delegate { if(engine != null) { engine.Cancelled = true; cancel.Enabled = false; status.Text = "正在取消，清理未完成檔案…"; } };
        folder.Click += delegate { if(lastOutput != null) Process.Start("explorer.exe", "/select," + VideoEngine.Quote(lastOutput)); };
        bar.SetBounds(26, 408, 687, 18); Controls.Add(bar); status.SetBounds(26, 439, 687, 48); status.Text = "先試壓並播放比較；實際縮小幅度取決於原始影片。"; Controls.Add(status);
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if(worker != null && worker.IsBusy) { e.Cancel = true; engine.Cancelled = true; status.Text = "正在取消，完成後即可關閉。"; } };
    }
    Label AddLabel(string text, int x, int y, int width, int height) { var l = new Label { Text = text }; l.SetBounds(x,y,width,height); Controls.Add(l); return l; }
    void Setup(Button b, string text, int x, int y, int width) { b.Text = text; b.SetBounds(x,y,width,36); Controls.Add(b); }
    void Busy(bool busy) { foreach(Control c in new Control[] { choose,save,run,preview,source,destination,quality,codec }) c.Enabled = !busy; cancel.Enabled = busy; }
    void Begin(bool sample)
    {
        if (!File.Exists(source.Text)) { MessageBox.Show("請先選取 MP4 影片。"); return; }
        string input = source.Text, output;
        try { output = Path.GetFullPath(destination.Text); if(sample) output = Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "_preview_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".mp4"); }
        catch(Exception e) { MessageBox.Show(e.Message); return; }
        int q = quality.SelectedIndex; bool h264 = codec.SelectedIndex == 1;
        engine = new VideoEngine(); bar.Value = 0; Busy(true); status.Text = "正在分析與壓縮，請稍候…";
        worker = new BackgroundWorker { WorkerReportsProgress = true };
        worker.DoWork += delegate { engine.Compress(input, output, q, h264, sample, delegate(int n) { worker.ReportProgress(n); }); };
        worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs e) { bar.Value = e.ProgressPercentage; status.Text = "壓縮進度 " + e.ProgressPercentage + "%"; };
        worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs e)
        {
            Busy(false);
            if(e.Error != null) { status.Text = e.Error is OperationCanceledException ? "已取消，原檔保留。" : "壓縮失敗，原檔保留。"; if(!(e.Error is OperationCanceledException)) MessageBox.Show(e.Error.Message, "無法完成壓縮"); return; }
            lastOutput = output; folder.Enabled = true; long before = new FileInfo(input).Length, after = new FileInfo(output).Length;
            status.Text = sample ? "試壓完成：" + FormatSize(after) + "。請播放比較畫質；試壓大小不能直接與整支影片比較。" : "完成：" + FormatSize(before) + " → " + FormatSize(after) + (after < before ? "，減少 " + (100.0 * (before-after)/before).ToString("F1") + "%" : "。此設定未縮小，可試用其他壓縮程度。") ;
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
