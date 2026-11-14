using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace HomegrownApiTester
{
    public sealed class RequestPreset
    {
        public string Name { get; set; }
        public string Method { get; set; }
        public string Url { get; set; }
        public string Headers { get; set; }
        public string Query { get; set; }
        public string Body { get; set; }
        public string ContentType { get; set; }
        public string Auth { get; set; }
        public string User { get; set; }
        public string Secret { get; set; }
        public override string ToString() { return Name; }
    }
    static class Program
    {
        [STAThread] static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
    sealed class MainForm : Form
    {
        readonly ComboBox method = Combo(new string[] { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" });
        readonly TextBox url = new TextBox { Dock = DockStyle.Fill, Text = "https://example.com" };
        readonly TextBox body = Editor(false), response = Editor(true), responseHeaders = Editor(true);
        readonly DataGridView headers = Grid(), query = Grid();
        readonly ComboBox contentType = Combo(new string[] { "application/json", "text/plain", "application/x-www-form-urlencoded", "application/xml" });
        readonly ComboBox auth = Combo(new string[] { "無驗證", "Bearer Token", "Basic Auth" });
        readonly TextBox user = new TextBox { Width = 210 }, secret = new TextBox { Width = 350, UseSystemPasswordChar = true };
        readonly TextBox name = new TextBox { Width = 170 };
        readonly NumericUpDown timeout = new NumericUpDown { Minimum = 1, Maximum = 300, Value = 30, Width = 65 };
        readonly CheckBox redirects = new CheckBox { Text = "跟隨重新導向", Checked = true, AutoSize = true };
        readonly Label status = new Label { Text = "就緒", Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
        readonly ListBox saved = new ListBox { Dock = DockStyle.Fill }, history = new ListBox { Dock = DockStyle.Fill };
        readonly Button send = new Button { Text = "送出請求", Dock = DockStyle.Fill, BackColor = Color.FromArgb(223, 241, 255) };
        readonly Button cancel = new Button { Text = "取消", Dock = DockStyle.Fill, Enabled = false };
        readonly List<RequestPreset> presets = new List<RequestPreset>();
        readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 24 * 1024 * 1024, RecursionLimit = 128 };
        readonly string storage = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HomegrownTools", "ApiTester", "requests.dat");
        CancellationTokenSource pending;
        bool closing;
        public MainForm()
        {
            Text = "API 測試工具"; ClientSize = new Size(1200, 820); MinimumSize = new Size(950, 680);
            StartPosition = FormStartPosition.CenterScreen; Font = new Font("Microsoft JhengHei UI", 10);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            var outer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12) };
            outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); Controls.Add(outer);
            var side = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(0, 0, 10, 0) };
            side.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); side.RowStyles.Add(new RowStyle(SizeType.Percent, 55)); side.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); side.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); side.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            side.Controls.Add(new Label { Text = "我的請求（雙擊載入）", AutoSize = true }, 0, 0); side.Controls.Add(saved, 0, 1);
            side.Controls.Add(Button("刪除收藏", delegate { DeletePreset(); }), 0, 2);
            side.Controls.Add(new Label { Text = "本次紀錄（雙擊載入）", AutoSize = true }, 0, 3); side.Controls.Add(history, 0, 4); outer.Controls.Add(side, 0, 0);
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); main.RowStyles.Add(new RowStyle(SizeType.Percent, 46)); main.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); main.RowStyles.Add(new RowStyle(SizeType.Percent, 54)); outer.Controls.Add(main, 1, 0);
            var address = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
            address.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105)); address.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); address.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); address.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));
            address.Controls.Add(method, 0, 0); address.Controls.Add(url, 1, 0); address.Controls.Add(send, 2, 0); address.Controls.Add(cancel, 3, 0); main.Controls.Add(address, 0, 0);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill }; actions.Controls.Add(new Label { Text = "收藏名稱", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }); actions.Controls.Add(name); actions.Controls.Add(Button("儲存", delegate { SavePreset(); })); actions.Controls.Add(new Label { Text = "逾時(秒)", AutoSize = true, Padding = new Padding(8, 6, 0, 0) }); actions.Controls.Add(timeout); actions.Controls.Add(redirects); main.Controls.Add(actions, 0, 1);
            var requestTabs = new TabControl { Dock = DockStyle.Fill }; AddPage(requestTabs, "Query 參數", query); AddPage(requestTabs, "Headers", headers);
            var bodyPanel = new Panel { Dock = DockStyle.Fill }; contentType.Dock = DockStyle.Top; bodyPanel.Controls.Add(body); bodyPanel.Controls.Add(contentType); AddPage(requestTabs, "Body", bodyPanel);
            var authPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), FlowDirection = FlowDirection.TopDown };
            auth.Width = 210; authPanel.Controls.Add(auth); authPanel.Controls.Add(new Label { Text = "Basic 使用者名稱", AutoSize = true }); authPanel.Controls.Add(user); authPanel.Controls.Add(new Label { Text = "Token / Basic 密碼", AutoSize = true }); authPanel.Controls.Add(secret); AddPage(requestTabs, "驗證", authPanel);
            main.Controls.Add(requestTabs, 0, 2); main.Controls.Add(status, 0, 3);
            var responseTabs = new TabControl { Dock = DockStyle.Fill }; AddPage(responseTabs, "回應內容", response); AddPage(responseTabs, "回應 Headers", responseHeaders);
            var resultPanel = new Panel { Dock = DockStyle.Fill }; var resultActions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
            resultActions.Controls.Add(Button("JSON 格式化", delegate { try { response.Text = Pretty(json.Serialize(json.DeserializeObject(response.Text))); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "無法格式化 JSON"); } }));
            resultActions.Controls.Add(Button("複製內容", delegate { if (response.Text.Length > 0) Clipboard.SetText(response.Text); }));
            resultActions.Controls.Add(Button("另存回應文字", delegate { using (var dialog = new SaveFileDialog { Filter = "文字檔|*.txt|JSON|*.json|所有檔案|*.*" }) if (dialog.ShowDialog(this) == DialogResult.OK) try { File.WriteAllText(dialog.FileName, response.Text, new UTF8Encoding(false)); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }));
            resultPanel.Controls.Add(responseTabs); resultPanel.Controls.Add(resultActions); main.Controls.Add(resultPanel, 0, 4);
            send.Click += async delegate { await SendRequest(); }; cancel.Click += delegate { if (pending != null) pending.Cancel(); };
            saved.DoubleClick += delegate { LoadPreset(saved.SelectedItem as RequestPreset); }; history.DoubleClick += delegate { LoadPreset(history.SelectedItem as RequestPreset); };
            auth.SelectedIndexChanged += delegate { user.Enabled = auth.SelectedIndex == 2; secret.Enabled = auth.SelectedIndex != 0; }; user.Enabled = false; secret.Enabled = false;
            FormClosing += delegate { closing = true; if (pending != null) pending.Cancel(); }; LoadStorage();
        }
        static ComboBox Combo(string[] values) { var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.None }; c.Items.AddRange(values); c.SelectedIndex = 0; return c; }
        static TextBox Editor(bool readOnly) { return new TextBox { Multiline = true, ReadOnly = readOnly, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, WordWrap = false, AcceptsTab = true, Font = new Font("Consolas", 10), BackColor = Color.White }; }
        static Button Button(string label, EventHandler action) { var b = new Button { Text = label, AutoSize = true, Height = 32 }; b.Click += action; return b; }
        static DataGridView Grid() { var g = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.White, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false }; g.Columns.Add("Key", "名稱"); g.Columns.Add("Value", "值"); return g; }
        static void AddPage(TabControl tabs, string title, Control control) { var p = new TabPage(title) { Padding = new Padding(6) }; p.Controls.Add(control); tabs.TabPages.Add(p); }
        static List<KeyValuePair<string, string>> Pairs(DataGridView grid)
        {
            grid.EndEdit(); var pairs = new List<KeyValuePair<string, string>>();
            foreach (DataGridViewRow row in grid.Rows) if (!row.IsNewRow) { string key = Convert.ToString(row.Cells[0].Value).Trim(); if (key.Length > 0) pairs.Add(new KeyValuePair<string, string>(key, Convert.ToString(row.Cells[1].Value))); } return pairs;
        }
        string StoreGrid(DataGridView grid) { return json.Serialize(Pairs(grid)); }
        void RestoreGrid(DataGridView grid, string value) { grid.Rows.Clear(); if (string.IsNullOrEmpty(value)) return; foreach (var pair in json.Deserialize<List<KeyValuePair<string, string>>>(value)) grid.Rows.Add(pair.Key, pair.Value); }
        RequestPreset CaptureRequest() { return new RequestPreset { Name = name.Text.Trim(), Method = method.Text, Url = url.Text.Trim(), Headers = StoreGrid(headers), Query = StoreGrid(query), Body = body.Text, ContentType = contentType.Text, Auth = auth.Text, User = user.Text, Secret = secret.Text }; }
        void LoadPreset(RequestPreset p) { if (p == null) return; name.Text = p.Name; method.SelectedItem = p.Method; url.Text = p.Url; body.Text = p.Body; contentType.SelectedItem = p.ContentType; auth.SelectedItem = p.Auth; user.Text = p.User; secret.Text = p.Secret; RestoreGrid(headers, p.Headers); RestoreGrid(query, p.Query); }
        void SavePreset()
        {
            try { var p = CaptureRequest(); if (p.Name.Length == 0) throw new Exception("請先輸入收藏名稱。"); var next = new List<RequestPreset>(presets); next.RemoveAll(x => x.Name == p.Name); next.Add(p); Persist(next); presets.Clear(); presets.AddRange(next); RefreshSaved(); status.Text = "已儲存：" + p.Name; }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "儲存失敗"); }
        }
        void DeletePreset() { var p = saved.SelectedItem as RequestPreset; if (p == null) return; try { var next = new List<RequestPreset>(presets); next.Remove(p); Persist(next); presets.Clear(); presets.AddRange(next); RefreshSaved(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "刪除失敗"); } }
        void RefreshSaved() { saved.Items.Clear(); foreach (var p in presets) saved.Items.Add(p); }
        void Persist(List<RequestPreset> values) { Directory.CreateDirectory(Path.GetDirectoryName(storage)); byte[] data = ProtectedData.Protect(Encoding.UTF8.GetBytes(json.Serialize(values)), null, DataProtectionScope.CurrentUser); string temp = storage + ".tmp"; File.WriteAllBytes(temp, data); if (File.Exists(storage)) File.Replace(temp, storage, null); else File.Move(temp, storage); }
        void LoadStorage() { try { if (File.Exists(storage)) presets.AddRange(json.Deserialize<List<RequestPreset>>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(storage), null, DataProtectionScope.CurrentUser)))); RefreshSaved(); } catch (Exception ex) { status.Text = "收藏載入失敗：" + ex.Message; } }
        async Task SendRequest()
        {
            if (pending != null) return;
            pending = new CancellationTokenSource(); send.Enabled = false; cancel.Enabled = true; response.Clear(); responseHeaders.Clear(); status.Text = "正在送出…";
            var watch = Stopwatch.StartNew();
            try
            {
                var preset = CaptureRequest(); Uri target;
                if (!Uri.TryCreate(preset.Url, UriKind.Absolute, out target) || (target.Scheme != "http" && target.Scheme != "https")) throw new Exception("請輸入完整的 http:// 或 https:// 網址。");
                var builder = new UriBuilder(target); var qs = builder.Query.TrimStart('?'); foreach (var pair in Pairs(query)) qs += (qs.Length == 0 ? "" : "&") + Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value); builder.Query = qs;
                using (var handler = new HttpClientHandler { AllowAutoRedirect = redirects.Checked, AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate, UseCookies = false })
                using (var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds((double)timeout.Value), MaxResponseContentBufferSize = 10 * 1024 * 1024 })
                using (var request = new HttpRequestMessage(new HttpMethod(preset.Method), builder.Uri))
                {
                    if (preset.Body.Length > 0) request.Content = new StringContent(preset.Body, Encoding.UTF8, preset.ContentType);
                    foreach (var pair in Pairs(headers))
                    {
                        if (pair.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) { if (request.Content == null) request.Content = new StringContent(""); request.Content.Headers.Remove(pair.Key); if (!request.Content.Headers.TryAddWithoutValidation(pair.Key, pair.Value)) throw new Exception("無效 Header：" + pair.Key); }
                        else if (!request.Headers.TryAddWithoutValidation(pair.Key, pair.Value)) { if (request.Content == null) request.Content = new StringContent(""); if (!request.Content.Headers.TryAddWithoutValidation(pair.Key, pair.Value)) throw new Exception("無效 Header：" + pair.Key); }
                    }
                    if (preset.Auth == "Bearer Token") request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", preset.Secret);
                    if (preset.Auth == "Basic Auth") request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(preset.User + ":" + preset.Secret)));
                    using (var result = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, pending.Token))
                    {
                        byte[] data = await result.Content.ReadAsByteArrayAsync(); string text = await result.Content.ReadAsStringAsync(); if (closing) return;
                        response.Text = text; responseHeaders.Text = result.Headers.ToString() + result.Content.Headers.ToString();
                        status.Text = string.Format("HTTP {0} {1}  •  {2:N0} ms  •  {3:N0} bytes  •  {4}", (int)result.StatusCode, result.ReasonPhrase, watch.ElapsedMilliseconds, data.Length, result.RequestMessage.RequestUri);
                        preset.Name = DateTime.Now.ToString("HH:mm:ss") + " " + preset.Method + " " + preset.Url; history.Items.Insert(0, preset); if (history.Items.Count > 50) history.Items.RemoveAt(50);
                    }
                }
            }
            catch (OperationCanceledException) { if (!closing) status.Text = pending.IsCancellationRequested ? "請求已取消" : "請求逾時"; }
            catch (Exception ex) { if (!closing) { status.Text = "請求失敗"; response.Text = ex.Message + (ex.InnerException == null ? "" : Environment.NewLine + ex.InnerException.Message); } }
            finally { pending.Dispose(); pending = null; if (!closing) { send.Enabled = true; cancel.Enabled = false; } }
        }
        static string Pretty(string value)
        {
            var output = new StringBuilder(); bool quoted = false, escaped = false; int indent = 0;
            foreach (char c in value)
            {
                if (quoted) { output.Append(c); if (escaped) escaped = false; else if (c == '\\') escaped = true; else if (c == '"') quoted = false; continue; }
                if (c == '"') { quoted = true; output.Append(c); }
                else if (c == '{' || c == '[') { output.Append(c).AppendLine(); output.Append(' ', ++indent * 2); }
                else if (c == '}' || c == ']') { output.AppendLine(); output.Append(' ', --indent * 2).Append(c); }
                else if (c == ',') { output.Append(c).AppendLine(); output.Append(' ', indent * 2); }
                else if (c == ':') output.Append(": "); else if (!char.IsWhiteSpace(c)) output.Append(c);
            }
            return output.ToString();
        }
    }
}
