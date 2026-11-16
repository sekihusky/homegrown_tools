using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HttpProtocolChecker
{
    public sealed class ProbeResult
    {
        public string Protocol, State, Actual, Status, Seconds, Detail;
    }

    public static class Detector
    {
        public static Uri ParseUrl(string input)
        {
            string value = input.Trim();
            if (value.Length == 0 || value.Any(char.IsControl)) throw new ArgumentException("請輸入有效的網址。");
            if (!value.Contains("://")) value = "https://" + value;
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) ||
                (uri.Scheme != "https" && uri.Scheme != "http") || string.IsNullOrEmpty(uri.Host) || uri.UserInfo.Length > 0)
                throw new ArgumentException("僅接受 http:// 或 https:// 網址，且不可包含帳號密碼。");
            return new UriBuilder(uri) { Fragment = "" }.Uri;
        }

        public static ProbeResult Classify(string protocol, int exit, string output, string error)
        {
            string[] parts = output.Trim().Split('|');
            var r = new ProbeResult { Protocol = protocol, State = "未確認", Actual = "—", Status = "—", Seconds = "—", Detail = error.Trim() };
            if (parts.Length == 3)
            {
                r.Actual = parts[0]; r.Status = parts[1]; r.Seconds = parts[2];
                if (parts[1] != "000" && parts[1].Length == 3)
                {
                    r.State = parts[0] == protocol ? "支援（實測成功）" : "本次未協商到";
                    r.Detail = "實際回應 HTTP/" + parts[0] + "，狀態碼 " + parts[1] + "。" +
                        (parts[0] != protocol ? "目標協定未獲確認。" : "") +
                        (exit != 0 ? " curl 診斷：" + error.Trim() : "");
                }
            }
            if (r.State == "未確認") r.Detail = "連線未完成（curl " + exit + "）。可能是伺服器、DNS、憑證或網路限制。\r\n" + error.Trim();
            return r;
        }

        public static async Task<ProbeResult> Probe(string executable, Uri uri, string protocol)
        {
            if (protocol == "3" && uri.Scheme != "https")
                return new ProbeResult { Protocol = protocol, State = "不適用", Actual = "—", Status = "—", Seconds = "—", Detail = "HTTP/3 需要 HTTPS；請改輸入 https:// 網址再測。" };
            string option = protocol == "3" ? "--http3-only" : protocol == "2" ? "--http2" : "--http1.1";
            var start = new ProcessStartInfo(executable,
                "--disable --silent --show-error --globoff --noproxy \"*\" --proto =http,https --head --output NUL --connect-timeout 10 --max-time 20 " +
                option + " --write-out \"%{http_version}|%{response_code}|%{time_total}\" --url \"" + uri.AbsoluteUri.Replace("\"", "%22") + "\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            try
            {
                using (var process = Process.Start(start))
                {
                    Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                    Task<string> stderr = process.StandardError.ReadToEndAsync();
                    bool ended = await Task.Run(() => process.WaitForExit(25000));
                    if (!ended) { process.Kill(); process.WaitForExit(); }
                    return Classify(protocol, process.ExitCode, await stdout, await stderr);
                }
            }
            catch (Exception ex)
            {
                return new ProbeResult { Protocol = protocol, State = "無法檢測", Actual = "—", Status = "—", Seconds = "—", Detail = ex.Message };
            }
        }
    }

    internal static class Program
    {
        [STAThread] static void Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        readonly TextBox url = new TextBox { Dock = DockStyle.Fill };
        readonly Button run = new Button { Text = "開始檢測", AutoSize = true };
        readonly Label status = new Label { Text = "輸入網址後按 Enter，未填協定時使用 HTTPS。", Dock = DockStyle.Fill, AutoSize = true };
        readonly TextBox detail = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Color.White };
        readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        bool running;
        public MainForm()
        {
            Text = "HTTP 協定檢測"; ClientSize = new Size(900, 590); MinimumSize = new Size(780, 550);
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Microsoft JhengHei UI", 10);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            BackColor = Color.FromArgb(244, 247, 251);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 6 };
            foreach (int height in new[] { 45, 45, 52, 170, 45 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(new Label { Text = "網站使用哪些 HTTP 協定？", Font = new Font(Font.FontFamily, 18, FontStyle.Bold), AutoSize = true }, 0, 0);
            var input = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            input.Controls.Add(url, 0, 0); input.Controls.Add(run, 1, 0); layout.Controls.Add(input, 0, 1);
            layout.Controls.Add(new Label { Text = "分別測試 HTTP/1.1、HTTP/2、HTTP/3。僅測輸入網址，不跟隨轉址。\r\nHTTP/3 失敗可能與 UDP／防火牆有關，不代表網站一定不支援。", Dock = DockStyle.Fill }, 0, 2);
            foreach (string column in new[] { "測試協定", "檢測結果", "實際協定", "HTTP 狀態碼", "耗時（秒）" }) grid.Columns.Add(column, column);
            grid.Columns[1].FillWeight = 160; grid.RowTemplate.Height = 35;
            layout.Controls.Add(grid, 0, 3);
            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            var copy = new Button { Text = "複製結果", AutoSize = true };
            copy.Click += delegate { if (detail.Text.Length > 0) { try { Clipboard.SetText(detail.Text); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } } };
            bottom.Controls.Add(status, 0, 0); bottom.Controls.Add(copy, 1, 0); layout.Controls.Add(bottom, 0, 4); layout.Controls.Add(detail, 0, 5);
            Controls.Add(layout); AcceptButton = run; run.Click += async delegate { await Check(); };
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (running) { e.Cancel = true; status.Text = "檢測即將完成，最多約 25 秒後即可關閉。"; } };
        }

        async Task Check()
        {
            Uri target;
            try { target = Detector.ParseUrl(url.Text); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "網址格式"); return; }
            running = true; run.Enabled = false; url.Enabled = false; grid.Rows.Clear(); detail.Clear(); status.Text = "正在連線測試，約需 1～25 秒…";
            try
            {
                url.Text = target.AbsoluteUri;
                string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "curl.exe");
                var results = await Task.WhenAll(new[] { "1.1", "2", "3" }.Select(p => Detector.Probe(executable, target, p)));
                var report = new StringBuilder("檢測網址：" + target.AbsoluteUri + "\r\n時間：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n方式：直接連線、HEAD、不跟隨轉址；耗時包含完整請求。\r\n\r\n");
                foreach (var r in results)
                {
                    int index = grid.Rows.Add("HTTP/" + r.Protocol, r.State, r.Actual == "—" ? "—" : "HTTP/" + r.Actual, r.Status, r.Seconds);
                    grid.Rows[index].DefaultCellStyle.ForeColor = r.State.StartsWith("支援") ? Color.DarkGreen : Color.FromArgb(140, 85, 20);
                    report.AppendLine("HTTP/" + r.Protocol + "：" + r.State + "（" + r.Seconds + " 秒）\r\n" + r.Detail + "\r\n");
                }
                detail.Text = report.ToString(); status.Text = "檢測完成。4xx／5xx 回應仍可確認使用的協定。";
            }
            catch (Exception ex) { status.Text = "檢測失敗"; detail.Text = ex.Message; }
            finally { running = false; run.Enabled = true; url.Enabled = true; }
        }
    }
}
