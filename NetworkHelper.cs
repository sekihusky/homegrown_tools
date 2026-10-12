using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetworkHelper
{
    internal static class Program
    {
        [STAThread] static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        readonly FlowLayoutPanel actions = new FlowLayoutPanel();
        readonly TextBox output = new TextBox();
        readonly TextBox host = new TextBox { Text = "example.com", Width = 220 };
        readonly Label status = new Label();
        bool busy;
        [DllImport("kernel32.dll")] static extern uint GetOEMCP();

        public MainForm()
        {
            Text = "網路修復小幫手";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Microsoft JhengHei UI", 10);
            ClientSize = new Size(940, 650); MinimumSize = new Size(800, 580);
            StartPosition = FormStartPosition.CenterScreen;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 155));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "網站換了 IP 或突然打不開？先試試「清除 DNS 快取」。\r\n清除後請重新開啟網頁；這不會清除瀏覽紀錄，也不會更改 DNS 伺服器。" });
            actions.Dock = DockStyle.Fill; actions.AutoScroll = true;
            AddAction("一鍵清除 DNS 快取", "ipconfig.exe", "/flushdns", false);
            AddAction("查看 DNS 快取", "ipconfig.exe", "/displaydns", false);
            AddAction("查看網路設定", "ipconfig.exe", "/all", false);
            AddAction("查看路由表", "route.exe", "print", false);
            AddAction("查看 WinHTTP Proxy", "netsh.exe", "winhttp show proxy", false);
            AddAction("重新取得 IP（DHCP）", "ipconfig.exe", "/renew", true);
            var targetRow = new FlowLayoutPanel { Width = 700, Height = 42 };
            targetRow.Controls.Add(new Label { Text = "測試網域／IP", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
            targetRow.Controls.Add(host);
            targetRow.Controls.Add(MakeButton("DNS 解析測試", async delegate { await TestHost(false); }));
            targetRow.Controls.Add(MakeButton("Ping 連線測試", async delegate { await TestHost(true); }));
            actions.Controls.Add(targetRow);
            layout.Controls.Add(actions);
            status.Dock = DockStyle.Fill; status.Text = "準備就緒。請選擇功能。"; layout.Controls.Add(status);
            output.Multiline = true; output.ReadOnly = true; output.ScrollBars = ScrollBars.Both;
            output.WordWrap = false; output.Dock = DockStyle.Fill; output.BackColor = Color.White;
            layout.Controls.Add(output);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill };
            footer.Controls.Add(MakeButton("複製結果", delegate { TryExport(false); }));
            footer.Controls.Add(MakeButton("儲存結果", delegate { TryExport(true); }));
            footer.Controls.Add(new Label { AutoSize = true, Text = "結果可能含內部 IP／電腦名稱，分享前請先檢查。", Padding = new Padding(0, 8, 0, 0) });
            layout.Controls.Add(footer); Controls.Add(layout);
            FormClosing += delegate(object sender, FormClosingEventArgs e) { if (busy) { e.Cancel = true; status.Text = "指令執行中，請等待完成或逾時後再關閉。"; } };
        }

        static Button MakeButton(string title, EventHandler click)
        {
            var button = new Button { Text = title, AutoSize = true, Height = 36, Padding = new Padding(7, 3, 7, 3) };
            button.Click += click; return button;
        }

        void AddAction(string title, string exe, string args, bool confirm)
        {
            actions.Controls.Add(MakeButton(title, async delegate {
                if (confirm && MessageBox.Show(this, "將更新所有使用 DHCP 的網卡租約，可能短暫影響連線。遠端操作時請小心。\r\n固定 IP 網卡不適用。是否繼續？", title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                await RunCommand(title, exe, args);
            }));
        }

        internal static bool ValidHost(string value)
        {
            return value.Length > 0 && value.Length <= 253 && value[0] != '-' &&
                System.Text.RegularExpressions.Regex.IsMatch(value, @"\A[a-zA-Z0-9.:_-]+\z") && Uri.CheckHostName(value) != UriHostNameType.Unknown;
        }

        async Task TestHost(bool ping)
        {
            string value = host.Text.Trim();
            if (!ValidHost(value)) { MessageBox.Show(this, "請輸入網域或 IP，例如 example.com，不要包含 https:// 或路徑。", "檢查輸入"); return; }
            await RunCommand(ping ? "Ping 連線測試" : "DNS 解析測試", ping ? "ping.exe" : "nslookup.exe", ping ? "-n 4 -w 2000 " + value : value);
        }

        async Task RunCommand(string title, string exe, string args)
        {
            if (busy) return;
            busy = true; actions.Enabled = false; status.Text = title + "：執行中…";
            output.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n> " + exe + " " + args + "\r\n\r\n";
            try
            {
                var result = await Task.Run(() => Execute(exe, args, 90000));
                output.AppendText(result.Item2);
                status.Text = result.Item1 == 0 ? title + "：指令已完成。" : title + "：指令未成功（代碼 " + result.Item1 + "），請查看結果。";
                if (exe == "nslookup.exe") output.AppendText("\r\n解析是否成功請以回應中的位址或錯誤訊息為準。");
                if (exe == "ping.exe") output.AppendText("\r\nPing 無回應也可能是對方封鎖 ICMP，不代表網站一定無法使用。");
            }
            catch (Exception ex) { status.Text = title + "：未完成。"; output.AppendText("\r\n" + ex.Message); }
            finally { busy = false; actions.Enabled = true; }
        }

        internal static Tuple<int, string> Execute(string exe, string args, int timeout)
        {
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, exe), args) {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.GetEncoding((int)GetOEMCP()), StandardErrorEncoding = Encoding.GetEncoding((int)GetOEMCP())
            };
            using (var process = Process.Start(start))
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(timeout))
                {
                    try { process.Kill(); } catch (InvalidOperationException) { }
                    process.WaitForExit();
                    throw new TimeoutException("指令逾時，已停止等待。請檢查網路狀態後重試。\r\n" + stdout.Result + stderr.Result);
                }
                return Tuple.Create(process.ExitCode, stdout.Result + stderr.Result);
            }
        }

        void TryExport(bool save)
        {
            if (output.TextLength == 0) return;
            try
            {
                if (!save) { Clipboard.SetText(output.Text); return; }
                using (var dialog = new SaveFileDialog { Filter = "文字檔 (*.txt)|*.txt", FileName = "network-result.txt" })
                    if (dialog.ShowDialog(this) == DialogResult.OK) File.WriteAllText(dialog.FileName, output.Text, new UTF8Encoding(true));
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "無法匯出結果"); }
        }
    }
}
