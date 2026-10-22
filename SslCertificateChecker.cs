using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SslCertificateChecker
{
    public static class CertificateReader
    {
        public static X509Certificate2Collection Read(string path, string password)
        {
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("檔案過大，請選擇 16 MB 以下的憑證檔案。");
            byte[] bytes = File.ReadAllBytes(path);
            string text = Encoding.ASCII.GetString(bytes);
            var result = new X509Certificate2Collection();
            try
            {
                if (text.Contains("-----BEGIN"))
                {
                    var matches = Regex.Matches(text, "-----BEGIN CERTIFICATE-----([\\s\\S]*?)-----END CERTIFICATE-----");
                    if (matches.Count == 0) throw new InvalidDataException("找不到憑證。私鑰（.key）本身沒有到期日，請選擇憑證檔案。");
                    foreach (Match match in matches) result.Add(new X509Certificate2(Convert.FromBase64String(match.Groups[1].Value)));
                }
                else
                {
                    // EphemeralKeySet avoids persisting imported PFX private keys to disk.
                    result.Import(bytes, password, (X509KeyStorageFlags)32);
                }
                if (result.Count == 0) throw new InvalidDataException("檔案中沒有憑證。");
                return result;
            }
            catch { foreach (X509Certificate2 cert in result) cert.Reset(); throw; }
        }

        public static string Status(DateTime start, DateTime end, DateTime now)
        {
            if (now > end) return "已過期";
            if (now < start) return "尚未生效";
            return (end - now).TotalDays <= 30 ? "30 天內到期" : "有效";
        }

        public static string Taiwan(DateTime value)
        {
            return value.ToUniversalTime().AddHours(8).ToString("yyyy-MM-dd HH:mm:ss") + " UTC+08:00";
        }
    }

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

    public sealed class MainForm : Form
    {
        readonly ListView list = new ListView();
        readonly TextBox details = new TextBox();
        readonly Label fileLabel = new Label();
        readonly Label summary = new Label();
        X509Certificate2Collection certificates = new X509Certificate2Collection();
        readonly Timer timer = new Timer();

        public MainForm()
        {
            Text = "SSL 憑證到期檢測";
            ClientSize = new Size(1000, 660);
            MinimumSize = new Size(850, 580);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft JhengHei UI", 10);
            BackColor = Color.FromArgb(246, 248, 251);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 7 };
            foreach (int height in new[] { 45, 35, 45, 35, 38 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            layout.Controls.Add(new Label { Text = "SSL 憑證到期檢測", Font = new Font(Font.FontFamily, 20, FontStyle.Bold), Dock = DockStyle.Fill }, 0, 0);
            layout.Controls.Add(new Label { Text = "離線讀取本機憑證 · 所有日期使用台灣時間（UTC+08:00）", Dock = DockStyle.Fill }, 0, 1);
            var choose = new Button { Text = "選取憑證檔案…", AutoSize = true, Height = 36 };
            choose.Click += delegate { ChooseFile(); };
            layout.Controls.Add(choose, 0, 2);
            fileLabel.Text = "支援 .crt、.cer、.pem、.pfx、.p12、.p7b";
            fileLabel.Dock = DockStyle.Fill;
            fileLabel.AutoEllipsis = true;
            layout.Controls.Add(fileLabel, 0, 3);
            summary.Text = "請選取檔案；此工具僅檢查日期，不驗證信任鏈、撤銷狀態或網站部署。";
            summary.Dock = DockStyle.Fill;
            layout.Controls.Add(summary, 0, 4);
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.MultiSelect = false;
            list.HideSelection = false;
            foreach (var column in new[] { "憑證名稱", "日期狀態", "到期日（台灣時間）", "剩餘時間" }) list.Columns.Add(column);
            list.Columns[0].Width = 300; list.Columns[1].Width = 125; list.Columns[2].Width = 275; list.Columns[3].Width = 180;
            list.SelectedIndexChanged += delegate { ShowDetails(); };
            layout.Controls.Add(list, 0, 5);
            details.Dock = DockStyle.Fill;
            details.Multiline = true;
            details.ReadOnly = true;
            details.ScrollBars = ScrollBars.Both;
            details.WordWrap = false;
            details.BackColor = Color.White;
            layout.Controls.Add(details, 0, 6);
            Controls.Add(layout);
            timer.Interval = 30000;
            timer.Tick += delegate { RefreshDates(); };
            timer.Start();
        }

        void ChooseFile()
        {
            using (var dialog = new OpenFileDialog { Title = "選取 SSL 憑證", Filter = "憑證檔案|*.crt;*.cer;*.pem;*.pfx;*.p12;*.p7b;*.p7c|所有檔案|*.*" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                ClearCertificates();
                fileLabel.Text = dialog.FileName;
                try
                {
                    try { certificates = CertificateReader.Read(dialog.FileName, ""); }
                    catch (System.Security.Cryptography.CryptographicException)
                    {
                        if (X509Certificate2.GetCertContentType(dialog.FileName) != X509ContentType.Pkcs12) throw;
                        using (var prompt = new PasswordForm())
                        {
                            if (prompt.ShowDialog(this) != DialogResult.OK) { summary.Text = "已取消讀取。"; return; }
                            certificates = CertificateReader.Read(dialog.FileName, prompt.Password);
                        }
                    }
                    foreach (X509Certificate2 cert in certificates)
                    {
                        var item = new ListViewItem(cert.GetNameInfo(X509NameType.SimpleName, false));
                        item.Tag = cert;
                        item.SubItems.AddRange(new[] { "", "", "" });
                        list.Items.Add(item);
                    }
                    RefreshDates();
                    list.Items[0].Selected = true;
                }
                catch (Exception ex)
                {
                    ClearCertificates();
                    summary.Text = "讀取失敗，請確認檔案格式及密碼。";
                    MessageBox.Show(this, "無法讀取憑證。檔案可能損毀、格式不支援，或 PFX 密碼不正確。\r\n\r\n" + ex.Message, "讀取失敗", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        void RefreshDates()
        {
            DateTime now = DateTime.UtcNow;
            foreach (ListViewItem item in list.Items)
            {
                var cert = (X509Certificate2)item.Tag;
                DateTime end = cert.NotAfter.ToUniversalTime();
                string status = CertificateReader.Status(cert.NotBefore.ToUniversalTime(), end, now);
                item.SubItems[1].Text = status;
                item.SubItems[2].Text = CertificateReader.Taiwan(cert.NotAfter);
                double days = (end - now).TotalDays;
                item.SubItems[3].Text = days < 0 ? "已過期 " + Math.Abs(days).ToString("0.0") + " 天" : days < 1 ? "不足 1 天" : Math.Floor(days) + " 天";
                item.ForeColor = status == "有效" ? Color.DarkGreen : status == "30 天內到期" ? Color.FromArgb(160, 90, 0) : Color.Firebrick;
            }
            if (certificates.Count > 0) summary.Text = "已讀取 " + certificates.Count + " 張憑證；請選取一列查看詳細資訊。日期有效不代表憑證受信任。";
        }

        void ShowDetails()
        {
            if (list.SelectedItems.Count == 0) { details.Clear(); return; }
            var cert = (X509Certificate2)list.SelectedItems[0].Tag;
            var text = new StringBuilder();
            text.AppendLine("主體：" + cert.Subject);
            text.AppendLine("簽發機構：" + cert.Issuer);
            text.AppendLine("生效日期：" + CertificateReader.Taiwan(cert.NotBefore));
            text.AppendLine("到期日期：" + CertificateReader.Taiwan(cert.NotAfter));
            foreach (X509Extension extension in cert.Extensions)
                if (extension.Oid.Value == "2.5.29.17") text.AppendLine("適用名稱（SAN）：\r\n" + extension.Format(true));
            text.AppendLine("序號：" + cert.SerialNumber);
            text.AppendLine("SHA-1 指紋（識別用）：" + cert.Thumbprint);
            details.Text = text.ToString();
        }

        void ClearCertificates()
        {
            list.Items.Clear(); details.Clear();
            foreach (X509Certificate2 cert in certificates) cert.Reset();
            certificates.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { timer.Dispose(); ClearCertificates(); }
            base.Dispose(disposing);
        }
    }

    internal sealed class PasswordForm : Form
    {
        readonly TextBox input = new TextBox { UseSystemPasswordChar = true, Dock = DockStyle.Top };
        public string Password { get { return input.Text; } }
        public PasswordForm()
        {
            Text = "PFX / P12 憑證密碼";
            ClientSize = new Size(390, 140);
            Padding = new Padding(16);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 35 };
            var ok = new Button { Text = "讀取", DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel };
            buttons.Controls.AddRange(new Control[] { ok, cancel });
            Controls.Add(input);
            Controls.Add(new Label { Text = "請輸入密碼（不會儲存）", Dock = DockStyle.Top, Height = 30 });
            Controls.Add(buttons);
            AcceptButton = ok; CancelButton = cancel;
        }
    }
}
