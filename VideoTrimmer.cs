using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

class VideoTrimmer : Form
{
    class PlayerHost : AxHost { public PlayerHost() : base("6BF52A52-394A-11D3-B153-00C04F79FAA6") { } public object Player { get { return GetOcx(); } } }
    PlayerHost player = new PlayerHost { Width = 530, Height = 300 };
    Timer timer = new Timer { Interval = 500 };
    bool loaded;
    TextBox file = new TextBox { ReadOnly = true, Width = 410 };
    TextBox start = new TextBox { Text = "00:00:00", Width = 120 };
    TextBox end = new TextBox { Text = "00:00:00", Width = 120 };
    Label status = new Label { AutoSize = true, Text = "請選取 MP4 影片。" };
    Button choose = new Button { Text = "選取影片", AutoSize = true };
    Button run = new Button { Text = "開始剪輯", AutoSize = true };
    ProgressBar progressBar = new ProgressBar { Width = 530, Height = 22, Minimum = 0, Maximum = 100 };
    string ffmpeg = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");

    VideoTrimmer()
    {
        Text = "影片剪輯"; ClientSize = new Size(590, 650); MinimumSize = new Size(606, 689);
        Font = new Font("Microsoft JhengHei UI", 10);
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(18), WrapContents = false, AutoScroll = true };
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        row.Controls.Add(file); row.Controls.Add(choose);
        var times = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        times.Controls.Add(new Label { Text = "開始時間", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }); times.Controls.Add(start);
        times.Controls.Add(new Label { Text = "結束時間", AutoSize = true, Padding = new Padding(12, 5, 0, 0) }); times.Controls.Add(end);
        layout.Controls.Add(new Label { Text = "選取 MP4 影片", AutoSize = true }); layout.Controls.Add(row); layout.Controls.Add(player);
        var marks = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        var markStart = new Button { Text = "設為開始", AutoSize = true };
        var markEnd = new Button { Text = "設為結束", AutoSize = true };
        marks.Controls.Add(markStart); marks.Controls.Add(markEnd); layout.Controls.Add(marks);
        layout.Controls.Add(new Label { Text = "時間格式：時:分:秒，例如 00:01:30", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        layout.Controls.Add(times); layout.Controls.Add(run); layout.Controls.Add(progressBar); layout.Controls.Add(status); Controls.Add(layout);
        choose.Click += delegate { using (var dialog = new OpenFileDialog { Filter = "MP4 影片 (*.mp4)|*.mp4", CheckFileExists = true }) if (dialog.ShowDialog() == DialogResult.OK) { file.Text = dialog.FileName; start.Text = "00:00:00"; end.Text = "00:00:00"; loaded = false; try { dynamic mediaPlayer = player.Player; mediaPlayer.settings.autoStart = false; mediaPlayer.URL = dialog.FileName; timer.Start(); status.Text = "請播放或拖動進度，設定開始及結束時間。"; } catch (Exception ex) { status.Text = "無法開啟預覽，可手動輸入時間。"; MessageBox.Show(ex.Message, "預覽無法使用"); } } };
        markStart.Click += delegate { MarkTime(start); };
        markEnd.Click += delegate { MarkTime(end); };
        timer.Tick += delegate { if (loaded) return; try { dynamic p = player.Player; double duration = p.currentMedia.duration; if (duration > 0) { end.Text = FormatTime(duration); loaded = true; } } catch { } };
        run.Click += async delegate { await Trim(); };
    }

    static string FormatTime(double seconds) { var t = TimeSpan.FromSeconds(Math.Floor(seconds)); return ((int)t.TotalHours).ToString("00") + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00"); }
    void MarkTime(TextBox target) { try { if (String.IsNullOrEmpty(file.Text)) return; dynamic p = player.Player; target.Text = FormatTime((double)p.controls.currentPosition); } catch (Exception ex) { MessageBox.Show(ex.Message, "無法取得播放位置"); } }

    async Task Trim()
    {
        TimeSpan from, to;
        if (!File.Exists(file.Text)) { MessageBox.Show("請選取存在的 MP4 影片。"); return; }
        if (!TimeSpan.TryParseExact(start.Text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out from) || !TimeSpan.TryParseExact(end.Text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out to) || to <= from) { MessageBox.Show("請輸入正確的時:分:秒，結束時間必須晚於開始時間。"); return; }
        if (!File.Exists(ffmpeg)) { MessageBox.Show("找不到 ffmpeg.exe。請先執行 build-video-trimmer.cmd。 "); return; }
        string source = file.Text, directory = Path.GetDirectoryName(source), name = Path.GetFileNameWithoutExtension(source);
        string output = Path.Combine(directory, name + "_trimmed.mp4");
        for (int i = 2; File.Exists(output); i++) output = Path.Combine(directory, name + "_trimmed_" + i + ".mp4");
        run.Enabled = false; choose.Enabled = false; progressBar.Value = 0; status.Text = "剪輯中：0%"; UseWaitCursor = true;
        try
        {
            double duration = (to - from).TotalSeconds;
            string args = "-hide_banner -loglevel error -progress pipe:1 -nostats -n -ss " + from.TotalSeconds.ToString(CultureInfo.InvariantCulture) + " -i \"" + source + "\" -t " + duration.ToString(CultureInfo.InvariantCulture) + " -map 0:v:0 -map 0:a? -c:v libx264 -preset medium -crf 18 -c:a aac \"" + output + "\"";
            var progress = new Progress<int>(value => { if (!run.Enabled && !IsDisposed) { progressBar.Value = value; status.Text = "剪輯中：" + value + "%"; } });
            await Task.Run(async () => { using (var p = new Process()) { p.StartInfo = new ProcessStartInfo(ffmpeg, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true }; p.Start(); Task<string> errors = p.StandardError.ReadToEndAsync(); string line; while ((line = await p.StandardOutput.ReadLineAsync()) != null) { long microseconds; if (line.StartsWith("out_time_us=", StringComparison.Ordinal) && Int64.TryParse(line.Substring(12), NumberStyles.Integer, CultureInfo.InvariantCulture, out microseconds)) { int percent = (int)Math.Max(0, Math.Min(99, microseconds / 1000000.0 / duration * 100)); ((IProgress<int>)progress).Report(percent); } } p.WaitForExit(); string error = await errors; if (p.ExitCode != 0) throw new Exception(error); } });
            run.Enabled = true; progressBar.Value = 100;
            status.Text = "剪輯完成：" + Path.GetFileName(output); MessageBox.Show("已完成：\n" + output);
        }
        catch (Exception ex) { status.Text = "剪輯失敗。"; MessageBox.Show(ex.Message, "剪輯失敗"); }
        finally { run.Enabled = true; choose.Enabled = true; UseWaitCursor = false; }
    }
    [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new VideoTrimmer()); }
}
