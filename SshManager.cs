using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace SshManager
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        private static Assembly ResolveEmbeddedAssembly(object sender, ResolveEventArgs args)
        {
            var requested = new AssemblyName(args.Name).Name;
            if (!String.Equals(requested, "Renci.SshNet", StringComparison.OrdinalIgnoreCase)) return null;
            const string resourceName = "SshManager.Renci.SshNet.dll";
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                int offset = 0, read;
                while (offset < bytes.Length && (read = stream.Read(bytes, offset, bytes.Length - offset)) > 0) offset += read;
                return Assembly.Load(bytes);
            }
        }
    }

    [DataContract]
    internal sealed class SiteProfile
    {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string Name = "";
        [DataMember] public string Host = "";
        [DataMember] public int Port = 22;
        [DataMember] public string UserName = "";
        [DataMember] public string AuthType = "password";
        [DataMember] public string EncryptedSecret = "";
        [DataMember] public string KeyPath = "";
        [DataMember] public string Note = "";
        [DataMember] public string HostKeyFingerprint = "";
        public override string ToString() { return Name + "  (" + Host + ")"; }
    }

    internal static class ProfileStore
    {
        private static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomegrownTools", "SshManager");
        private static readonly string FilePath = Path.Combine(DirectoryPath, "sites.json");
        public static List<SiteProfile> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<SiteProfile>();
                using (var stream = File.OpenRead(FilePath))
                    return (List<SiteProfile>)new DataContractJsonSerializer(typeof(List<SiteProfile>)).ReadObject(stream);
            }
            catch { return new List<SiteProfile>(); }
        }
        public static void Save(List<SiteProfile> profiles)
        {
            Directory.CreateDirectory(DirectoryPath);
            using (var stream = File.Create(FilePath))
                new DataContractJsonSerializer(typeof(List<SiteProfile>)).WriteObject(stream, profiles);
        }
        public static void SaveFingerprint(SiteProfile profile, string fingerprint)
        {
            profile.HostKeyFingerprint = fingerprint;
            var profiles = Load();
            var stored = profiles.FirstOrDefault(p => p.Id == profile.Id);
            if (stored != null) stored.HostKeyFingerprint = fingerprint;
            else profiles.Add(profile);
            Save(profiles);
        }
        public static string Encrypt(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
        }
        public static string Decrypt(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser));
        }
    }

    internal sealed class TerminalView : UserControl
    {
        private readonly RichTextBox output = new RichTextBox();
        private readonly TextBox command = new TextBox();
        private readonly Label state = new Label();
        private readonly Button reconnect = new Button();
        private readonly Button disconnect = new Button();
        private SshClient client;
        private ShellStream shell;
        private CancellationTokenSource readerCancellation;
        private readonly SiteProfile profile;

        public string Title { get { return profile.Name; } }
        public bool IsConnected { get { return client != null && client.IsConnected; } }

        public TerminalView(SiteProfile profile)
        {
            this.profile = profile;
            BackColor = Color.FromArgb(20, 24, 30);
            Dock = DockStyle.Fill;
            var top = new Panel { Dock = DockStyle.Top, Height = 35, BackColor = Color.FromArgb(35, 42, 52) };
            state.AutoSize = true; state.ForeColor = Color.Gainsboro; state.Location = new Point(10, 9); state.Text = "準備連線";
            reconnect.Text = "重新連線"; reconnect.Size = new Size(78, 26); reconnect.Location = new Point(260, 4); reconnect.Click += delegate { Connect(); };
            disconnect.Text = "中斷"; disconnect.Size = new Size(60, 26); disconnect.Location = new Point(344, 4); disconnect.Click += delegate { Disconnect(); };
            foreach (var button in new Button[] { reconnect, disconnect })
            {
                button.UseVisualStyleBackColor = false;
                button.BackColor = Color.FromArgb(238, 242, 247);
                button.ForeColor = Color.FromArgb(20, 28, 40);
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Color.FromArgb(160, 175, 195);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(215, 230, 250);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(190, 215, 245);
            }
            top.Controls.AddRange(new Control[] { state, reconnect, disconnect });
            output.Dock = DockStyle.Fill; output.ReadOnly = true; output.BorderStyle = BorderStyle.None; output.BackColor = Color.FromArgb(13, 17, 23); output.ForeColor = Color.FromArgb(222, 235, 223); output.Font = new Font("Consolas", 10F); output.WordWrap = false;
            command.Dock = DockStyle.Bottom; command.Height = 31; command.BackColor = Color.FromArgb(25, 31, 40); command.ForeColor = Color.White; command.Font = new Font("Consolas", 10F); command.BorderStyle = BorderStyle.FixedSingle;
            command.KeyDown += CommandKeyDown;
            Controls.Add(output); Controls.Add(command); Controls.Add(top);
            Connect();
        }

        private void CommandKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                var text = command.Text;
                command.Clear();
                Send(text + "\n");
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up) { Send("\x1b[A"); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Down) { Send("\x1b[B"); e.SuppressKeyPress = true; }
        }

        public void Connect()
        {
            Disconnect();
            Append("\r\n--- 正在連線至 " + profile.Host + ":" + profile.Port + " ---\r\n");
            state.Text = "連線中：" + profile.Host + ":" + profile.Port;
            Task.Factory.StartNew(ConnectWorker);
        }

        private void ConnectWorker()
        {
            try
            {
                AuthenticationMethod auth;
                string secret = ProfileStore.Decrypt(profile.EncryptedSecret);
                if (profile.AuthType == "key")
                {
                    if (!File.Exists(profile.KeyPath)) throw new FileNotFoundException("找不到私鑰檔", profile.KeyPath);
                    var key = String.IsNullOrEmpty(secret) ? new PrivateKeyFile(profile.KeyPath) : new PrivateKeyFile(profile.KeyPath, secret);
                    auth = new PrivateKeyAuthenticationMethod(profile.UserName, key);
                }
                else auth = new PasswordAuthenticationMethod(profile.UserName, secret);
                var info = new ConnectionInfo(profile.Host, profile.Port, profile.UserName, auth) { Timeout = TimeSpan.FromSeconds(15) };
                var newClient = new SshClient(info);
                newClient.HostKeyReceived += delegate(object sender, HostKeyEventArgs e)
                {
                    var fingerprint = Convert.ToBase64String(e.FingerPrint);
                    if (String.IsNullOrEmpty(profile.HostKeyFingerprint))
                    {
                        ProfileStore.SaveFingerprint(profile, fingerprint);
                        e.CanTrust = true;
                    }
                    else e.CanTrust = String.Equals(profile.HostKeyFingerprint, fingerprint, StringComparison.Ordinal);
                };
                newClient.Connect();
                var newShell = newClient.CreateShellStream("xterm", 120, 40, 900, 600, 4096);
                if (IsDisposed) { newShell.Dispose(); newClient.Dispose(); return; }
                BeginInvoke((Action)delegate
                {
                    client = newClient; shell = newShell; state.Text = "已連線：" + profile.Host + ":" + profile.Port; Append("--- 已連線 ---\r\n");
                    readerCancellation = new CancellationTokenSource(); Task.Factory.StartNew(() => ReadWorker(readerCancellation.Token)); command.Focus();
                });
            }
            catch (Exception ex) { if (!IsDisposed) BeginInvoke((Action)delegate { state.Text = "連線失敗"; Append("連線失敗：" + ex.Message + "\r\n"); }); }
        }

        private void ReadWorker(CancellationToken cancellation)
        {
            var buffer = new byte[4096];
            try
            {
                while (!cancellation.IsCancellationRequested && shell != null && shell.CanRead)
                {
                    int count = shell.Read(buffer, 0, buffer.Length);
                    if (count > 0) Append(Encoding.UTF8.GetString(buffer, 0, count));
                }
            }
            catch (Exception ex) { if (!cancellation.IsCancellationRequested) Append("\r\n連線已中斷：" + ex.Message + "\r\n"); }
        }

        private void Send(string text)
        {
            try { if (shell != null && client != null && client.IsConnected) shell.Write(text); else Append("尚未連線。\r\n"); }
            catch (Exception ex) { Append("傳送失敗：" + ex.Message + "\r\n"); }
        }
        private void Append(string text)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke((Action)(() => Append(text))); return; }
            output.AppendText(StripAnsi(text)); output.SelectionStart = output.TextLength; output.ScrollToCaret();
        }
        private static string StripAnsi(string value)
        {
            var builder = new StringBuilder();
            bool escape = false;
            foreach (char c in value)
            {
                if (c == '\x1b') { escape = true; continue; }
                if (escape) { if ((c >= '@' && c <= '~')) escape = false; continue; }
                builder.Append(c);
            }
            return builder.ToString();
        }
        public void Disconnect()
        {
            try { if (readerCancellation != null) readerCancellation.Cancel(); if (shell != null) shell.Dispose(); if (client != null) { if (client.IsConnected) client.Disconnect(); client.Dispose(); } }
            catch { }
            finally { shell = null; client = null; if (!IsDisposed) state.Text = "已中斷"; }
        }
        protected override void Dispose(bool disposing) { if (disposing) Disconnect(); base.Dispose(disposing); }
    }

    internal sealed class MainForm : Form
    {
        private readonly List<SiteProfile> profiles = ProfileStore.Load();
        private readonly ListBox siteList = new ListBox();
        private readonly TextBox search = new TextBox();
        private readonly TabControl tabs = new TabControl();
        private readonly SplitContainer split = new SplitContainer();
        private readonly Button splitButton = new Button();

        public MainForm()
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Text = "SSH 站台管理器"; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(1100, 720); MinimumSize = new Size(800, 540); Font = new Font("Microsoft JhengHei UI", 9F); BackColor = Color.FromArgb(246, 248, 251);
            var header = new Panel { Dock = DockStyle.Top, Height = 105, BackColor = Color.White };
            var title = new Label { Text = "SSH 站台管理器", Font = new Font("Microsoft JhengHei UI", 16F, FontStyle.Bold), AutoSize = true, Location = new Point(18, 15), ForeColor = Color.FromArgb(28, 38, 55) };
            var add = MakeButton("+ 新增站台", 640); add.Click += delegate { EditProfile(null); };
            var edit = MakeButton("編輯", 745); edit.Click += delegate { EditProfile(SelectedProfile()); };
            var delete = MakeButton("刪除", 820); delete.Click += DeleteProfile;
            splitButton = MakeButton("並排檢視", 895); splitButton.Width = 90; splitButton.Click += ToggleSplit;
            var connect = MakeButton("連線", 0); connect.Click += delegate { OpenSelected(); };
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, Padding = new Padding(12, 3, 0, 3), WrapContents = false };
            toolbar.Controls.AddRange(new Control[] { add, edit, delete, connect, splitButton });
            header.Controls.Add(title); header.Controls.Add(toolbar);
            split.Size = new Size(1100, 615); split.Dock = DockStyle.Fill; split.FixedPanel = FixedPanel.Panel1; split.SplitterDistance = 260; split.Panel1MinSize = 180; split.Panel2MinSize = 300;
            var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = Color.FromArgb(238, 242, 247) };
            var label = new Label { Text = "已儲存站台", Dock = DockStyle.Top, Height = 26, Font = new Font(Font, FontStyle.Bold) };
            search.Dock = DockStyle.Top; search.TextChanged += delegate { RefreshSites(); }; search.PlaceholderText("搜尋站台、IP 或帳號");
            siteList.Dock = DockStyle.Fill; siteList.DoubleClick += delegate { OpenSelected(); };
            var hint = new Label { Text = "先新增站台，再選取並按「連線」。", Dock = DockStyle.Bottom, Height = 45 };
            left.Controls.Add(siteList); left.Controls.Add(search); left.Controls.Add(label); left.Controls.Add(hint); split.Panel1.Controls.Add(left);
            tabs.Dock = DockStyle.Fill; tabs.Padding = new Point(14, 4); split.Panel2.Controls.Add(tabs);
            Controls.Add(split); Controls.Add(header); RefreshSites();
        }
        private Button MakeButton(string text, int x) { return new Button { Text = text, Height = 30, MinimumSize = new Size(78, 30), AutoSize = true, Margin = new Padding(3), FlatStyle = FlatStyle.System }; }
        private void RefreshSites()
        {
            var selected = SelectedProfile(); siteList.BeginUpdate(); siteList.Items.Clear();
            var needle = search.Text.Trim();
            foreach (var profile in profiles.Where(p => String.IsNullOrEmpty(needle) || (p.Name + p.Host + p.UserName).IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0).OrderBy(p => p.Name)) siteList.Items.Add(profile);
            siteList.EndUpdate(); if (selected != null) siteList.SelectedItem = siteList.Items.Cast<SiteProfile>().FirstOrDefault(p => p.Id == selected.Id);
        }
        private SiteProfile SelectedProfile() { return siteList.SelectedItem as SiteProfile; }
        private void DeleteProfile(object sender, EventArgs e)
        {
            var profile = SelectedProfile(); if (profile == null) return;
            if (MessageBox.Show("確定刪除「" + profile.Name + "」？", "刪除站台", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            profiles.Remove(profile); ProfileStore.Save(profiles); RefreshSites();
        }
        private void OpenSelected()
        {
            var profile = SelectedProfile(); if (profile == null) return;
            foreach (TabPage page in tabs.TabPages) if ((page.Tag as SiteProfile).Id == profile.Id) { tabs.SelectedTab = page; return; }
            var terminal = new TerminalView(profile); var pageNew = new TabPage(profile.Name + "  ×") { Tag = profile }; pageNew.Controls.Add(terminal); tabs.TabPages.Add(pageNew); tabs.SelectedTab = pageNew;
        }
        private void ToggleSplit(object sender, EventArgs e)
        {
            var pages = tabs.TabPages.Cast<TabPage>().Where(p => p.Controls.OfType<TerminalView>().Any()).Take(2).ToList();
            if (pages.Count < 2) { MessageBox.Show("請先開啟至少兩台 SSH 連線，再使用並排檢視。", "SSH 站台管理器", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var pair = new Form { Text = "並排檢視：" + pages[0].Tag + " / " + pages[1].Tag, StartPosition = FormStartPosition.CenterParent, Size = new Size(1200, 700) };
            var layout = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 590 };
            var first = pages[0].Controls.OfType<TerminalView>().First(); var second = pages[1].Controls.OfType<TerminalView>().First();
            pages[0].Controls.Remove(first); pages[1].Controls.Remove(second); layout.Panel1.Controls.Add(first); layout.Panel2.Controls.Add(second); pair.Controls.Add(layout);
            pair.FormClosed += delegate { layout.Panel1.Controls.Remove(first); layout.Panel2.Controls.Remove(second); pages[0].Controls.Add(first); pages[1].Controls.Add(second); };
            pair.Show(this);
        }
        private void EditProfile(SiteProfile profile)
        {
            var dialog = new ProfileDialog(profile); if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (profile == null) profiles.Add(dialog.Profile); else { int index = profiles.IndexOf(profile); profiles[index] = dialog.Profile; }
            ProfileStore.Save(profiles); RefreshSites();
        }
    }

    internal sealed class ProfileDialog : Form
    {
        public SiteProfile Profile { get; private set; }
        private readonly TextBox name = new TextBox(), host = new TextBox(), user = new TextBox(), secret = new TextBox(), key = new TextBox(), note = new TextBox();
        private readonly NumericUpDown port = new NumericUpDown(); private readonly RadioButton password = new RadioButton(), keyAuth = new RadioButton();
        private readonly Button browseKey = new Button();
        private readonly SiteProfile original;
        public ProfileDialog(SiteProfile profile)
        {
            original = profile; Profile = Clone(profile ?? new SiteProfile()); Text = profile == null ? "新增 SSH 站台" : "編輯 SSH 站台"; StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(465, 410); Font = new Font("Microsoft JhengHei UI", 9F); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            AddField("站台名稱", name, 20, 20); AddField("IP 位址或網域", host, 20, 76); AddField("使用者名稱", user, 20, 132); AddField("Port", port, 20, 188); port.Minimum = 1; port.Maximum = 65535;
            password.Text = "密碼"; password.AutoSize = true; password.Location = new Point(20, 242); password.CheckedChanged += delegate { UpdateAuthUi(); }; keyAuth.Text = "私鑰檔"; keyAuth.AutoSize = true; keyAuth.Location = new Point(145, 242); keyAuth.CheckedChanged += delegate { UpdateAuthUi(); };
            AddField("密碼／私鑰密碼", secret, 20, 270); secret.UseSystemPasswordChar = true; AddField("私鑰檔案", key, 20, 326);
            key.Width = 200;
            browseKey.Text = "瀏覽…"; browseKey.Location = new Point(375, 320); browseKey.Size = new Size(75, 27);
            browseKey.Click += delegate
            {
                using (var picker = new OpenFileDialog())
                {
                    picker.Title = "選取 SSH 私鑰檔案";
                    picker.Filter = "所有檔案 (*.*)|*.*";
                    picker.CheckFileExists = true;
                    picker.Multiselect = false;
                    picker.RestoreDirectory = true;
                    if (File.Exists(key.Text)) picker.FileName = key.Text;
                    if (picker.ShowDialog(this) == DialogResult.OK) key.Text = picker.FileName;
                }
            };
            Controls.Add(browseKey);
            var save = new Button { Text = "儲存", DialogResult = DialogResult.OK, Location = new Point(285, 370), Width = 75 }; save.Click += Save;
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(370, 370), Width = 75 }; Controls.AddRange(new Control[] { password, keyAuth, save, cancel }); AcceptButton = save; CancelButton = cancel;
            name.Text = Profile.Name; host.Text = Profile.Host; user.Text = Profile.UserName; port.Value = Profile.Port; key.Text = Profile.KeyPath; password.Checked = Profile.AuthType != "key"; keyAuth.Checked = Profile.AuthType == "key"; try { secret.Text = ProfileStore.Decrypt(Profile.EncryptedSecret); } catch { }
            UpdateAuthUi();
        }
        private void AddField(string label, Control control, int x, int y) { Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(x, y) }); control.Location = new Point(x + 145, y - 4); control.Width = 285; Controls.Add(control); }
        private void UpdateAuthUi() { key.Enabled = keyAuth.Checked; browseKey.Enabled = keyAuth.Checked; }
        private void Save(object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace(name.Text) || String.IsNullOrWhiteSpace(host.Text) || String.IsNullOrWhiteSpace(user.Text)) { MessageBox.Show("請填寫站台名稱、IP／網域與使用者名稱。", "資料不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning); DialogResult = DialogResult.None; return; }
            if (keyAuth.Checked && !File.Exists(key.Text)) { MessageBox.Show("請指定存在的私鑰檔。", "資料不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning); DialogResult = DialogResult.None; return; }
            Profile.Name = name.Text.Trim(); Profile.Host = host.Text.Trim(); Profile.UserName = user.Text.Trim(); Profile.Port = (int)port.Value; Profile.AuthType = keyAuth.Checked ? "key" : "password"; Profile.KeyPath = key.Text.Trim(); Profile.EncryptedSecret = ProfileStore.Encrypt(secret.Text);
        }
        private static SiteProfile Clone(SiteProfile p) { return new SiteProfile { Id = p.Id, Name = p.Name, Host = p.Host, Port = p.Port, UserName = p.UserName, AuthType = p.AuthType, EncryptedSecret = p.EncryptedSecret, KeyPath = p.KeyPath, Note = p.Note, HostKeyFingerprint = p.HostKeyFingerprint }; }
    }

    internal static class ControlExtensions
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
        public static void PlaceholderText(this TextBox box, string text) { SendMessage(box.Handle, 0x1501, (IntPtr)1, text); }
    }
}
