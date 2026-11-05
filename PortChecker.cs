using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
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
        private readonly CheckBox sslCheckBox = new CheckBox();
        private readonly CheckBox redirectCheckBox = new CheckBox();
        private readonly TextBox redirectResultBox = new TextBox();
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
            ClientSize = new Size(680, 680);
            MinimumSize = new Size(620, 640);
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

            sslCheckBox.Text = "附加檢查 SSL/TLS（握手與憑證）";
            sslCheckBox.AutoSize = true;
            sslCheckBox.Checked = true;
            sslCheckBox.Location = new Point(24, 158);
            sslCheckBox.ForeColor = Color.FromArgb(55, 65, 81);

            redirectCheckBox.Text = "檢查 HTTP 是否跳轉 HTTPS（同一 Port）";
            redirectCheckBox.AutoSize = true;
            redirectCheckBox.Location = new Point(24, 187);
            redirectCheckBox.ForeColor = Color.FromArgb(55, 65, 81);

            resultPanel.Location = new Point(24, 218);
            resultPanel.Size = new Size(632, 112);
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
            resultDetail.Size = new Size(590, 58);
            resultDetail.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            resultPanel.Controls.Add(resultTitle);
            resultPanel.Controls.Add(resultDetail);

            redirectResultBox.Location = new Point(24, 339);
            redirectResultBox.Size = new Size(632, 68);
            redirectResultBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            redirectResultBox.Multiline = true;
            redirectResultBox.ReadOnly = true;
            redirectResultBox.ScrollBars = ScrollBars.Vertical;
            redirectResultBox.Text = "勾選後檢查 HTTP 回應的跳轉位置。";

            var historyLabel = MakeLabel("測試紀錄", 24, 422);
            historyLabel.Font = new Font(Font, FontStyle.Bold);
            clearButton.Text = "清除";
            clearButton.Location = new Point(590, 414);
            clearButton.Size = new Size(66, 30);
            clearButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            clearButton.Click += delegate { history.Items.Clear(); };

            history.Location = new Point(24, 451);
            history.Size = new Size(632, 205);
            history.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            history.View = View.Details;
            history.FullRowSelect = true;
            history.GridLines = true;
            history.HideSelection = false;
            history.Columns.Add("時間", 85);
            history.Columns.Add("目標", 245);
            history.Columns.Add("結果", 105);
            history.Columns.Add("耗時 / 說明", 175);

            Controls.AddRange(new Control[] { header, subtitle, hostLabel, hostBox, portLabel, portBox,
                timeoutLabel, timeoutBox, testButton, sslCheckBox, redirectCheckBox, resultPanel, redirectResultBox, historyLabel, clearButton, history });
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
            bool checkSsl = sslCheckBox.Checked;
            bool checkRedirect = redirectCheckBox.Checked;
            redirectResultBox.Text = checkRedirect ? "HTTP 跳轉檢查中…" : "未啟用 HTTP 跳轉檢查。";
            SetBusy(true);
            ShowResult("測試中…", "正在連線到 " + FormatTarget(host, port) + (checkSsl ? "，接著會測試 SSL/TLS 握手" : ""), Color.FromArgb(37, 99, 235));

            TestResult result;
            try
            {
                result = await Task.Run(() =>
                {
                    TestResult current = TestConnection(host, port, timeout);
                    if (current.Success && checkSsl)
                    {
                        current.SslChecked = true;
                        CheckSsl(host, port, timeout, current);
                    }
                    if (current.Success && checkRedirect) current.RedirectMessage = CheckHttpRedirect(host, port, timeout);
                    return current;
                });
            }
            catch (Exception ex)
            {
                result = new TestResult { Success = false, Message = FriendlyError(ex) };
            }

            if (result.Success && result.SslChecked && result.SslSuccess)
                ShowResult("✓ TCP 與 SSL/TLS 均可通", FormatSslSuccess(host, port, result), Color.FromArgb(22, 163, 74));
            else if (result.Success && result.SslChecked)
                ShowResult("! TCP 可通，但 SSL/TLS 失敗", FormatTarget(host, port) + " 的 Port 可連線，但無法完成 TLS 握手。\r\n" + result.SslMessage, Color.FromArgb(217, 119, 6));
            else if (result.Success)
                ShowResult("✓ 連線成功", FormatTarget(host, port) + " 可以連線（" + result.ElapsedMilliseconds + " ms）" + AddressSuffix(result), Color.FromArgb(22, 163, 74));
            else
                ShowResult("✕ 連線失敗", FormatTarget(host, port) + " 無法連線：" + result.Message, Color.FromArgb(220, 38, 38));

            if (checkRedirect) redirectResultBox.Text = result.Success ? result.RedirectMessage : "HTTP 跳轉檢查未執行：TCP 連線失敗。";

            AddHistory(host, port, result);
            SetBusy(false);
            hostBox.Focus();
            hostBox.SelectAll();
        }

        private static string CheckHttpRedirect(string host, int port, int timeoutMilliseconds)
        {
            try
            {
                Uri source = new UriBuilder("http", host, port, "/").Uri;
                var request = (HttpWebRequest)WebRequest.Create(source);
                request.AllowAutoRedirect = false;
                request.Proxy = null;
                request.Timeout = timeoutMilliseconds;
                request.ReadWriteTimeout = timeoutMilliseconds;
                request.Method = "GET";
                HttpWebResponse response;
                try { response = (HttpWebResponse)request.GetResponse(); }
                catch (WebException ex)
                {
                    response = ex.Response as HttpWebResponse;
                    if (response == null) return "HTTP 跳轉檢查失敗：" + ex.Message;
                }
                using (response)
                {
                    int status = (int)response.StatusCode;
                    string location = response.Headers[HttpResponseHeader.Location];
                    if (status != 301 && status != 302 && status != 303 && status != 307 && status != 308)
                        return "HTTP " + status + "：未回傳 HTTP 跳轉。";
                    Uri destination;
                    if (String.IsNullOrWhiteSpace(location) || !Uri.TryCreate(source, location, out destination))
                        return "HTTP " + status + "：跳轉位置不存在或無效。";
                    bool same = destination.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
                        destination.Host.Equals(source.Host, StringComparison.OrdinalIgnoreCase) && destination.Port == port;
                    return "HTTP " + status + "：" + (same ? "有跳轉到同主機、同 Port 的 HTTPS" : "有跳轉，但目標不是同主機、同 Port 的 HTTPS") + "\r\n" + destination.AbsoluteUri;
                }
            }
            catch (Exception ex) { return "HTTP 跳轉檢查失敗：" + ex.Message; }
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

        private static void CheckSsl(string host, int port, int timeoutMilliseconds, TestResult result)
        {
            TcpClient client = null;
            SslStream sslStream = null;
            try
            {
                IPAddress address = IPAddress.Parse(result.Address);
                client = new TcpClient(address.AddressFamily);
                IAsyncResult connecting = client.BeginConnect(address, port, null, null);
                if (!connecting.AsyncWaitHandle.WaitOne(timeoutMilliseconds))
                {
                    result.SslMessage = "建立 SSL 測試連線逾時";
                    return;
                }
                client.EndConnect(connecting);

                SslPolicyErrors certificateErrors = SslPolicyErrors.None;
                X509Certificate2 certificate = null;
                sslStream = new SslStream(client.GetStream(), false, delegate(object sender, X509Certificate remoteCertificate, X509Chain chain, SslPolicyErrors errors)
                {
                    certificateErrors = errors;
                    if (remoteCertificate != null) certificate = new X509Certificate2(remoteCertificate);
                    return true;
                });

                SslProtocols protocols = SslProtocols.Tls | (SslProtocols)768 | (SslProtocols)3072;
                IAsyncResult authenticating = sslStream.BeginAuthenticateAsClient(host, new X509CertificateCollection(), protocols, false, null, null);
                if (!authenticating.AsyncWaitHandle.WaitOne(timeoutMilliseconds))
                {
                    result.SslMessage = "TLS 握手逾時（服務可能不是 SSL/TLS）";
                    return;
                }
                sslStream.EndAuthenticateAsClient(authenticating);

                result.SslSuccess = true;
                result.SslProtocol = sslStream.SslProtocol.ToString();
                result.CertificateValid = certificateErrors == SslPolicyErrors.None;
                result.CertificateErrors = CertificateErrorText(certificateErrors);
                if (certificate != null)
                {
                    result.CertificateSubject = certificate.GetNameInfo(X509NameType.SimpleName, false);
                    if (String.IsNullOrWhiteSpace(result.CertificateSubject)) result.CertificateSubject = certificate.Subject;
                    result.CertificateIssuer = certificate.GetNameInfo(X509NameType.SimpleName, true);
                    result.CertificateExpires = certificate.NotAfter;
                }
            }
            catch (AuthenticationException ex) { result.SslMessage = "TLS 握手失敗：" + FriendlySslError(ex); }
            catch (IOException ex) { result.SslMessage = "對方中斷 SSL/TLS 連線：" + FriendlySslError(ex); }
            catch (SocketException ex) { result.SslMessage = "SSL 測試連線失敗：" + ex.SocketErrorCode; }
            catch (Exception ex) { result.SslMessage = "SSL/TLS 檢查失敗：" + FriendlySslError(ex); }
            finally
            {
                if (sslStream != null) sslStream.Dispose();
                if (client != null) client.Close();
            }
        }

        private void AddHistory(string host, int port, TestResult result)
        {
            string detail;
            string status;
            if (!result.Success)
            {
                status = "TCP 失敗";
                detail = result.Message;
            }
            else if (result.SslChecked)
            {
                status = result.SslSuccess ? "SSL 可通" : "僅 TCP 可通";
                detail = result.SslSuccess ? result.SslProtocol + "，" + CertificateStatus(result) : result.SslMessage;
            }
            else
            {
                status = "TCP 成功";
                detail = result.ElapsedMilliseconds + " ms" + AddressSuffix(result);
            }
            var item = new ListViewItem(DateTime.Now.ToString("HH:mm:ss"));
            item.SubItems.Add(FormatTarget(host, port));
            item.SubItems.Add(status);
            item.SubItems.Add(detail);
            item.ForeColor = !result.Success ? Color.FromArgb(185, 28, 28) :
                (result.SslChecked && !result.SslSuccess ? Color.FromArgb(180, 83, 9) : Color.FromArgb(21, 128, 61));
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
            sslCheckBox.Enabled = !busy;
            redirectCheckBox.Enabled = !busy;
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

        private static string FormatSslSuccess(string host, int port, TestResult result)
        {
            string certificate = String.IsNullOrWhiteSpace(result.CertificateSubject) ? "未取得憑證資訊" :
                "憑證：" + result.CertificateSubject + "，到期：" + result.CertificateExpires.ToString("yyyy-MM-dd") + "，" + CertificateStatus(result);
            return FormatTarget(host, port) + " 可完成 " + result.SslProtocol + " 握手（TCP " + result.ElapsedMilliseconds + " ms）。\r\n" + certificate;
        }

        private static string CertificateStatus(TestResult result)
        {
            return result.CertificateValid ? "憑證驗證正常" : "憑證驗證有問題（" + result.CertificateErrors + "）";
        }

        private static string CertificateErrorText(SslPolicyErrors errors)
        {
            var messages = new List<string>();
            if ((errors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0) messages.Add("名稱不符");
            if ((errors & SslPolicyErrors.RemoteCertificateChainErrors) != 0) messages.Add("憑證鏈不受信任或已過期");
            if ((errors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0) messages.Add("未提供憑證");
            return messages.Count == 0 ? "無" : String.Join("、", messages.ToArray());
        }

        private static string FriendlySslError(Exception ex)
        {
            Exception current = ex;
            while (current.InnerException != null) current = current.InnerException;
            if (current is SocketException) return ((SocketException)current).SocketErrorCode.ToString();
            return current.Message;
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
        public string RedirectMessage { get; set; }
        public bool SslChecked { get; set; }
        public bool SslSuccess { get; set; }
        public string SslProtocol { get; set; }
        public string SslMessage { get; set; }
        public bool CertificateValid { get; set; }
        public string CertificateErrors { get; set; }
        public string CertificateSubject { get; set; }
        public string CertificateIssuer { get; set; }
        public DateTime CertificateExpires { get; set; }
    }
}
