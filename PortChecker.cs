using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PortChecker
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly TextBox hostBox = new TextBox();
        private readonly NumericUpDown portBox = new NumericUpDown();
        private readonly NumericUpDown timeoutBox = new NumericUpDown();
        private readonly Button testButton = new Button();
        private readonly Button clearButton = new Button();
        private readonly Label resultTitle = new Label();
        private readonly Label resultDetail = new Label();
        private readonly Panel resultPanel = new Panel();
        private readonly ListView history = new ListView();

        public MainForm()
        {
            Text = "Port 連線測試工具";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(680, 510);
            MinimumSize = new Size(620, 470);
            Font = new Font("Microsoft JhengHei UI", 10F);
            Icon = SystemIcons.Shield;
            BackColor = Color.FromArgb(246, 248, 251);

            var header = new Label
            {
                Text = "Port 連線測試",
                Font = new Font("Microsoft JhengHei UI", 18F, FontStyle.Bold),
                ForeColor = Color.FromArgb(28, 38, 55),
                AutoSize = true,
                Location = new Point(24, 18)
            };
            var subtitle = new Label
            {
                Text = "直接測試 TCP 連線，不需要安裝或啟用 Telnet",
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(27, 55)
            };

            var hostLabel = MakeLabel("IP 或網域", 24, 88);
            hostBox.Location = new Point(24, 113);
            hostBox.Size = new Size(345, 29);
            hostBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            SetCueBanner(hostBox, "例如 192.168.1.10 或 example.com");

            var portLabel = MakeLabel("Port", 385, 88);
            portLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            portBox.Location = new Point(385, 113);
            portBox.Size = new Size(90, 29);
            portBox.Minimum = 1;
            portBox.Maximum = 65535;
            portBox.Value = 443;
            portBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            var timeoutLabel = MakeLabel("逾時（秒）", 491, 88);
            timeoutLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            timeoutBox.Location = new Point(491, 113);
            timeoutBox.Size = new Size(72, 29);
            timeoutBox.Minimum = 1;
            timeoutBox.Maximum = 30;
            timeoutBox.Value = 3;
            timeoutBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            testButton.Text = "測試";
            testButton.Location = new Point(579, 109);
            testButton.Size = new Size(77, 37);
            testButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            testButton.BackColor = Color.FromArgb(37, 99, 235);
            testButton.ForeColor = Color.White;
            testButton.FlatStyle = FlatStyle.Flat;
            testButton.FlatAppearance.BorderSize = 0;
            testButton.Click += delegate { StartTest(); };

            resultPanel.Location = new Point(24, 163);
            resultPanel.Size = new Size(632, 91);
            resultPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            resultPanel.BackColor = Color.White;
            resultPanel.BorderStyle = BorderStyle.FixedSingle;

            resultTitle.Text = "準備就緒";
            resultTitle.Font = new Font(Font, FontStyle.Bold);
            resultTitle.ForeColor = Color.FromArgb(55, 65, 81);
            resultTitle.Location = new Point(18, 14);
            resultTitle.AutoSize = true;
            resultDetail.Text = "輸入目標後按「測試」，或直接按 Enter。";
            resultDetail.ForeColor = Color.DimGray;
            resultDetail.Location = new Point(18, 44);
            resultDetail.Size = new Size(590, 35);
            resultDetail.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            resultPanel.Controls.Add(resultTitle);
            resultPanel.Controls.Add(resultDetail);

            var historyLabel = MakeLabel("測試紀錄", 24, 273);
            historyLabel.Font = new Font(Font, FontStyle.Bold);
            clearButton.Text = "清除";
            clearButton.Location = new Point(590, 265);
            clearButton.Size = new Size(66, 30);
            clearButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            clearButton.Click += delegate { history.Items.Clear(); };

            history.Location = new Point(24, 302);
            history.Size = new Size(632, 184);
            history.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            history.View = View.Details;
            history.FullRowSelect = true;
            history.GridLines = true;
            history.HideSelection = false;
            history.Columns.Add("時間", 85);
            history.Columns.Add("目標", 245);
            history.Columns.Add("結果", 85);
            history.Columns.Add("耗時 / 說明", 195);

            Controls.AddRange(new Control[] { header, subtitle, hostLabel, hostBox, portLabel, portBox,
                timeoutLabel, timeoutBox, testButton, resultPanel, historyLabel, clearButton, history });
            AcceptButton = testButton;
            Shown += delegate { hostBox.Focus(); };
        }

        private async void StartTest()
        {
            string host = NormalizeHost(hostBox.Text);
            if (host.Length == 0)
            {
                MessageBox.Show(this, "請輸入 IP 位址或網域名稱。", "缺少目標", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                hostBox.Focus();
                return;
            }

            int port = Decimal.ToInt32(portBox.Value);
            int timeout = Decimal.ToInt32(timeoutBox.Value) * 1000;
            SetBusy(true);
            ShowResult("測試中…", "正在連線到 " + FormatTarget(host, port), Color.FromArgb(37, 99, 235));

            TestResult result;
            try
            {
                result = await Task.Run(() => TestConnection(host, port, timeout));
            }
            catch (Exception ex)
            {
                result = new TestResult { Success = false, Message = FriendlyError(ex) };
            }

            if (result.Success)
                ShowResult("✓ 連線成功", FormatTarget(host, port) + " 可以連線（" + result.ElapsedMilliseconds + " ms）" + AddressSuffix(result), Color.FromArgb(22, 163, 74));
            else
                ShowResult("✕ 連線失敗", FormatTarget(host, port) + " 無法連線：" + result.Message, Color.FromArgb(220, 38, 38));

            AddHistory(host, port, result);
            SetBusy(false);
            hostBox.Focus();
            hostBox.SelectAll();
        }

        private static TestResult TestConnection(string host, int port, int timeoutMilliseconds)
        {
            IPAddress[] addresses;
            var totalWatch = Stopwatch.StartNew();
            try { addresses = Dns.GetHostAddresses(host); }
            catch (SocketException) { return new TestResult { Success = false, Message = "找不到主機或 DNS 解析失敗" }; }

            if (addresses.Length == 0)
                return new TestResult { Success = false, Message = "找不到主機的 IP 位址" };

            var errors = new List<SocketError>();
            foreach (IPAddress address in addresses)
            {
                int remaining = timeoutMilliseconds - (int)totalWatch.ElapsedMilliseconds;
                if (remaining <= 0) break;
                var watch = Stopwatch.StartNew();
                using (var client = new TcpClient(address.AddressFamily))
                {
                    try
                    {
                        IAsyncResult pending = client.BeginConnect(address, port, null, null);
                        bool connected = pending.AsyncWaitHandle.WaitOne(remaining);
                        if (!connected)
                        {
                            client.Close();
                            errors.Add(SocketError.TimedOut);
                            continue;
                        }
                        client.EndConnect(pending);
                        watch.Stop();
                        return new TestResult { Success = true, Address = address.ToString(), ElapsedMilliseconds = watch.ElapsedMilliseconds };
                    }
                    catch (SocketException ex) { errors.Add(ex.SocketErrorCode); }
                    catch (ObjectDisposedException) { errors.Add(SocketError.TimedOut); }
                }
            }

            if (errors.Contains(SocketError.ConnectionRefused))
                return new TestResult { Success = false, Message = "主機有回應，但該 Port 拒絕連線（服務可能未啟動）" };
            if (errors.Contains(SocketError.TimedOut) || totalWatch.ElapsedMilliseconds >= timeoutMilliseconds)
                return new TestResult { Success = false, Message = "連線逾時（可能被防火牆阻擋或主機無回應）" };
            if (errors.Contains(SocketError.NetworkUnreachable) || errors.Contains(SocketError.HostUnreachable))
                return new TestResult { Success = false, Message = "無法連到目標網路或主機" };
            return new TestResult { Success = false, Message = "連線無法建立" };
        }

        private void AddHistory(string host, int port, TestResult result)
        {
            string detail = result.Success ? result.ElapsedMilliseconds + " ms" + AddressSuffix(result) : result.Message;
            var item = new ListViewItem(DateTime.Now.ToString("HH:mm:ss"));
            item.SubItems.Add(FormatTarget(host, port));
            item.SubItems.Add(result.Success ? "成功" : "失敗");
            item.SubItems.Add(detail);
            item.ForeColor = result.Success ? Color.FromArgb(21, 128, 61) : Color.FromArgb(185, 28, 28);
            history.Items.Insert(0, item);
        }

        private void ShowResult(string title, string detail, Color color)
        {
            resultTitle.Text = title;
            resultTitle.ForeColor = color;
            resultDetail.Text = detail;
            resultPanel.BackColor = MixWithWhite(color, 0.94f);
        }

        private void SetBusy(bool busy)
        {
            testButton.Enabled = !busy;
            hostBox.Enabled = !busy;
            portBox.Enabled = !busy;
            timeoutBox.Enabled = !busy;
            testButton.Text = busy ? "測試中" : "測試";
            UseWaitCursor = busy;
        }

        private static string NormalizeHost(string value)
        {
            string host = (value ?? "").Trim();
            Uri uri;
            if (Uri.TryCreate(host, UriKind.Absolute, out uri) && !String.IsNullOrEmpty(uri.Host)) host = uri.Host;
            if (host.StartsWith("[") && host.EndsWith("]")) host = host.Substring(1, host.Length - 2);
            return host;
        }

        private static string FormatTarget(string host, int port)
        {
            return host.Contains(":") ? "[" + host + "]:" + port : host + ":" + port;
        }

        private static string AddressSuffix(TestResult result)
        {
            return String.IsNullOrEmpty(result.Address) ? "" : "，IP: " + result.Address;
        }

        private static string FriendlyError(Exception ex)
        {
            if (ex is ArgumentException) return "IP 或網域格式不正確";
            return ex.Message;
        }

        private static Label MakeLabel(string text, int x, int y)
        {
            return new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Color.FromArgb(55, 65, 81) };
        }

        private static Color MixWithWhite(Color color, float whiteAmount)
        {
            int r = (int)(color.R * (1 - whiteAmount) + 255 * whiteAmount);
            int g = (int)(color.G * (1 - whiteAmount) + 255 * whiteAmount);
            int b = (int)(color.B * (1 - whiteAmount) + 255 * whiteAmount);
            return Color.FromArgb(r, g, b);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private static void SetCueBanner(TextBox box, string text)
        {
            box.HandleCreated += delegate { SendMessage(box.Handle, 0x1501, (IntPtr)1, text); };
        }
    }

    internal sealed class TestResult
    {
        public bool Success { get; set; }
        public string Address { get; set; }
        public long ElapsedMilliseconds { get; set; }
        public string Message { get; set; }
    }
}
