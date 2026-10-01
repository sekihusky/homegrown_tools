using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static class GlobalUrlChecker
{
    internal sealed class City
    {
        public string Name, English, Country;
        public City(string name, string english, string country) { Name = name; English = english; Country = country; }
    }
    internal static readonly City[] Cities = {
        new City("台北", "Taipei", "TW"), new City("東京", "Tokyo", "JP"), new City("首爾", "Seoul", "KR"),
        new City("香港", "Hong Kong", "HK"), new City("新加坡", "Singapore", "SG"), new City("孟買", "Mumbai", "IN"),
        new City("曼谷", "Bangkok", "TH"), new City("雅加達", "Jakarta", "ID"), new City("雪梨", "Sydney", "AU"),
        new City("奧克蘭", "Auckland", "NZ"), new City("杜拜", "Dubai", "AE"), new City("倫敦", "London", "GB"),
        new City("巴黎", "Paris", "FR"), new City("法蘭克福", "Frankfurt", "DE"), new City("阿姆斯特丹", "Amsterdam", "NL"),
        new City("紐約", "New York", "US"), new City("洛杉磯", "Los Angeles", "US"), new City("芝加哥", "Chicago", "US"),
        new City("多倫多", "Toronto", "CA"), new City("聖保羅", "Sao Paulo", "BR"), new City("聖地牙哥", "Santiago", "CL"),
        new City("約翰尼斯堡", "Johannesburg", "ZA")
    };
    [STAThread]
    static void Main()
    {
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new CheckerForm());
    }
    internal static Uri ValidateUrl(string text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0) throw new ArgumentException("請輸入網址。");
        if (!text.Contains("://")) text = "https://" + text;
        Uri uri;
        if (!Uri.TryCreate(text, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https") || string.IsNullOrEmpty(uri.Host))
            throw new ArgumentException("請輸入有效的 HTTP 或 HTTPS 網址。");
        if (uri.UserInfo.Length > 0) throw new ArgumentException("不支援含帳號或密碼的網址。");
        if (uri.IsLoopback || uri.Host.IndexOf('.') < 0 && uri.HostNameType != UriHostNameType.IPv6)
            throw new ArgumentException("請使用全球節點可存取的公開網域或 IP。");
        IPAddress ip;
        if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out ip))
        {
            byte[] b = ip.GetAddressBytes();
            if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal ||
                (b.Length == 4 && (b[0] == 10 || b[0] == 127 || b[0] == 0 || b[0] >= 224 || b[0] == 169 && b[1] == 254 || b[0] == 192 && b[1] == 168 || b[0] == 172 && b[1] >= 16 && b[1] <= 31)) ||
                (b.Length == 16 && (b[0] & 254) == 252))
                throw new ArgumentException("全球節點無法測量本機或內部網路位址。");
        }
        return uri;
    }
    internal static Dictionary<string, object> Map(object value) { return value as Dictionary<string, object> ?? new Dictionary<string, object>(); }
    internal static object Get(Dictionary<string, object> map, string key) { object value; return map.TryGetValue(key, out value) ? value : null; }
    internal static string Str(Dictionary<string, object> map, string key) { return Convert.ToString(Get(map, key), CultureInfo.InvariantCulture); }
    internal static string Time(Dictionary<string, object> map, string key)
    {
        object value = Get(map, key); return value == null ? "—" : Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("0.0", CultureInfo.InvariantCulture);
    }
    internal static string Classify(Dictionary<string, object> result)
    {
        string status = Str(result, "status");
        if (status == "in-progress") return "測量中";
        if (status == "offline") return "節點離線";
        if (status == "finished")
        {
            int code; if (!int.TryParse(Str(result, "statusCode"), out code) || code == 0) return "無 HTTP 回應";
            return code >= 400 ? "HTTP 錯誤" : code >= 300 ? "重新導向" : "成功";
        }
        if (Str(result, "failureSource") == "internal") return "節點／服務錯誤";
        // Failure status alone has no timeout subtype; retain the complete diagnostic for review.
        string raw = Str(result, "rawOutput").ToLowerInvariant();
        if (raw.Contains("timed out") || raw.Contains("timeout") || raw.Contains("time-out")) return "逾時";
        if (Str(result, "failureSource") == "resolver") return "DNS 解析失敗";
        return "連線失敗";
    }
    internal static object RequestBody(Uri uri, City city, int timeout, string method)
    {
        var request = new Dictionary<string, object> { { "method", method }, { "path", uri.AbsolutePath } };
        if (uri.Query.Length > 1) request["query"] = uri.Query.Substring(1);
        return new { type = "http", target = uri.IdnHost.Trim('[', ']'), timeout = timeout, inProgressUpdates = true,
            locations = new[] { new { city = city.English, country = city.Country, limit = 1 } },
            measurementOptions = new { protocol = uri.Scheme == "https" ? "HTTPS" : "HTTP", port = uri.Port,
                request = request } };
    }
    sealed class ApiException : Exception
    {
        public int Code;
        public ApiException(int code, string text) : base(text) { Code = code; }
    }
    static async Task<Dictionary<string, object>> Api(HttpClient client, HttpMethod method, string path, object body, CancellationToken token)
    {
        using (var request = new HttpRequestMessage(method, "https://api.globalping.io/v1/measurements" + path))
        {
            if (body != null) request.Content = new StringContent(new JavaScriptSerializer().Serialize(body), Encoding.UTF8, "application/json");
            using (var response = await client.SendAsync(request, token))
            {
                string text = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    string message = text;
                    try { message = Str(Map(Get(new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(text), "error")), "message"); } catch { }
                    if ((int)response.StatusCode == 429) message = "測量額度或速率已達上限；請稍後再試、減少城市或填入 API Token。 " + message;
                    throw new ApiException((int)response.StatusCode, message);
                }
                return new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Deserialize<Dictionary<string, object>>(text);
            }
        }
    }
    sealed class CheckerForm : Form
    {
        readonly TextBox url = new TextBox(), token = new TextBox(), detail = new TextBox();
        readonly NumericUpDown timeout = new NumericUpDown();
        readonly ComboBox method = new ComboBox();
        readonly Button start = new Button(), cancel = new Button(), export = new Button();
        readonly Label summary = new Label();
        readonly DataGridView grid = new DataGridView();
        readonly CheckedListBox cities = new CheckedListBox();
        CancellationTokenSource running;
        string testedUrl = "", testedMethod = "", testedAt = "";
        int completed;
        public CheckerForm()
        {
            Text = "全球網址回應測試";
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Font = new Font("Microsoft JhengHei UI", 10);
            ClientSize = new Size(1160, 760); MinimumSize = new Size(970, 650);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 6 };
            foreach (int height in new[] { 44, 44, 55, 0, 130, 34 }) layout.RowStyles.Add(new RowStyle(height == 0 ? SizeType.Percent : SizeType.Absolute, height == 0 ? 100 : height));
            var first = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
            first.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60)); first.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            first.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115)); first.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            first.Controls.Add(new Label { Text = "網址", AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, 0);
            url.Dock = DockStyle.Fill; url.Text = "https://example.com/"; first.Controls.Add(url, 1, 0);
            start.Text = "開始測試"; start.Dock = DockStyle.Fill; cancel.Text = "取消"; cancel.Dock = DockStyle.Fill; cancel.Enabled = false;
            first.Controls.Add(start, 2, 0); first.Controls.Add(cancel, 3, 0); layout.Controls.Add(first, 0, 0);
            var settings = new FlowLayoutPanel { Dock = DockStyle.Fill };
            settings.Controls.Add(new Label { Text = "節點逾時（秒）", AutoSize = true, Margin = new Padding(0, 7, 0, 0) });
            timeout.Minimum = 5; timeout.Maximum = 30; timeout.Value = 15; timeout.Width = 58; settings.Controls.Add(timeout);
            method.DropDownStyle = ComboBoxStyle.DropDownList; method.Items.AddRange(new object[] { "GET", "HEAD" }); method.SelectedIndex = 0; method.Width = 80; settings.Controls.Add(method);
            settings.Controls.Add(new Label { Text = "API Token（選填）", AutoSize = true, Margin = new Padding(10, 7, 0, 0) });
            token.UseSystemPasswordChar = true; token.Width = 190; settings.Controls.Add(token);
            export.Text = "匯出 CSV"; export.AutoSize = true; export.Enabled = false; settings.Controls.Add(export); layout.Controls.Add(settings, 0, 1);
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, ForeColor = Color.DimGray, Text = "由 Globalping 遠端節點測量；網址（含路徑與查詢字串）及結果會送交第三方服務，請勿輸入敏感資料。\r\n單次 HTTP 測量，非完整網頁載入時間。無節點與服務錯誤會另外標示；重新導向請查看詳細結果。" }, 0, 2);
            var middle = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
            middle.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var cityPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
            cityPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); cityPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var select = new FlowLayoutPanel { Dock = DockStyle.Fill }; var all = new Button { Text = "全選", Width = 70 }; var none = new Button { Text = "清除", Width = 70 };
            select.Controls.Add(all); select.Controls.Add(none); cityPanel.Controls.Add(select, 0, 0);
            cities.Dock = DockStyle.Fill; cities.CheckOnClick = true; foreach (City city in Cities) cities.Items.Add(city.Name + " " + city.Country, true); cityPanel.Controls.Add(cities, 0, 1);
            all.Click += (s, e) => { if (running == null) for (int i = 0; i < cities.Items.Count; i++) cities.SetItemChecked(i, true); };
            none.Click += (s, e) => { if (running == null) for (int i = 0; i < cities.Items.Count; i++) cities.SetItemChecked(i, false); };
            grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible = false; grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeight = 44; grid.BackgroundColor = Color.White;
            grid.ColumnHeadersHeight = 44; grid.BackgroundColor = Color.White;
            string[] headers = { "城市", "實際節點 / 網路", "狀態", "HTTP", "總耗時 ms", "DNS ms", "TCP ms", "TLS ms", "首位元組 ms", "目標 IP" };
            for (int i = 0; i < headers.Length; i++) { grid.Columns.Add("c" + i, headers[i]); grid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable; }
            grid.Columns[1].FillWeight = 180; grid.Columns[2].FillWeight = 130; grid.Columns[9].FillWeight = 150;
            middle.Controls.Add(cityPanel, 0, 0); middle.Controls.Add(grid, 1, 0); layout.Controls.Add(middle, 0, 3);
            detail.Dock = DockStyle.Fill; detail.Multiline = true; detail.ReadOnly = true; detail.ScrollBars = ScrollBars.Both; detail.WordWrap = false;
            detail.Text = "選取城市可查看測量時間、測量 ID 與原始診斷。"; layout.Controls.Add(detail, 0, 4);
            summary.Dock = DockStyle.Fill; summary.AutoEllipsis = true; summary.Text = "選取城市後開始測試。"; layout.Controls.Add(summary, 0, 5); Controls.Add(layout);
            start.Click += async (s, e) => await Run(); cancel.Click += (s, e) => { if (running != null) running.Cancel(); };
            export.Click += (s, e) => Export(); grid.SelectionChanged += (s, e) => { if (grid.SelectedRows.Count > 0) detail.Text = Convert.ToString(grid.SelectedRows[0].Tag); };
            FormClosing += (s, e) => { if (running != null) running.Cancel(); }; AcceptButton = start;
        }
        async Task Run()
        {
            if (running != null) return;
            Uri uri;
            try { uri = ValidateUrl(url.Text); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "網址格式"); return; }
            int[] selected = cities.CheckedIndices.Cast<int>().ToArray();
            if (selected.Length == 0) { MessageBox.Show(this, "請至少選擇一個城市。"); return; }
            running = new CancellationTokenSource(); var source = running;
            start.Enabled = url.Enabled = cities.Enabled = timeout.Enabled = method.Enabled = token.Enabled = export.Enabled = false; cancel.Enabled = true;
            testedUrl = uri.AbsoluteUri; testedAt = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"); testedMethod = method.Text; completed = 0;
            grid.Rows.Clear(); int seconds = (int)timeout.Value;
            var jobs = new List<Tuple<City, DataGridViewRow>>();
            foreach (int index in selected) { int row = grid.Rows.Add(Cities[index].Name, "—", "等待中"); jobs.Add(Tuple.Create(Cities[index], grid.Rows[row])); }
            summary.Text = "測量中 0 / " + jobs.Count;
            using (var client = new HttpClient())
            using (var gate = new SemaphoreSlim(4))
            {
                client.Timeout = TimeSpan.FromSeconds(20); client.DefaultRequestHeaders.UserAgent.ParseAdd("GlobalUrlChecker/1.0");
                try
                {
                    if (token.Text.Trim().Length > 0) client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Text.Trim());
                    await Task.WhenAll(jobs.Select(async job => {
                        bool entered = false;
                        try { await gate.WaitAsync(source.Token); entered = true; await Measure(client, uri, job.Item1, job.Item2, seconds, testedMethod, source.Token); }
                        catch (OperationCanceledException) { SetStatus(job.Item2, "已取消", "使用者取消；遠端已提交的測量可能仍會完成。"); }
                        catch (Exception ex) { SetStatus(job.Item2, "服務錯誤", ex.Message); }
                        finally { if (entered) gate.Release(); completed++; if (!IsDisposed) summary.Text = "已處理 " + completed + " / " + jobs.Count; }
                    }));
                    if (!IsDisposed) summary.Text = (source.IsCancellationRequested ? "已取消" : "測量完成") + " · " + testedAt + " · " + string.Join("　", jobs.GroupBy(j => Convert.ToString(j.Item2.Cells[2].Value)).Select(g => g.Key + " " + g.Count()));
                }
                catch (Exception ex) { if (!IsDisposed) summary.Text = "無法開始：" + ex.Message; }
                finally
                {
                    running = null; source.Dispose();
                    if (!IsDisposed) { start.Enabled = url.Enabled = cities.Enabled = timeout.Enabled = method.Enabled = token.Enabled = true; cancel.Enabled = false; export.Enabled = grid.Rows.Count > 0; }
                }
            }
        }
        void SetStatus(DataGridViewRow row, string status, string message)
        {
            if (IsDisposed) return;
            row.Cells[2].Value = status; row.Tag = "網址：" + testedUrl + "\r\n時間：" + testedAt + "\r\n" + message;
            row.DefaultCellStyle.ForeColor = status == "成功" ? Color.DarkGreen : status == "等待中" || status == "測量中" ? Color.DimGray : Color.DarkRed;
            if (row.Selected) detail.Text = Convert.ToString(row.Tag);
        }
        async Task Measure(HttpClient client, Uri uri, City city, DataGridViewRow row, int seconds, string verb, CancellationToken tokenValue)
        {
            SetStatus(row, "測量中", "正在向 " + city.English + " 節點送出請求。");
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(tokenValue))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(seconds + 40));
                string id = "";
                try
                {
                    var created = await Api(client, HttpMethod.Post, "", RequestBody(uri, city, seconds, verb), deadline.Token);
                    id = Str(created, "id");
                    if (id.Length == 0) throw new Exception("測量服務未回傳測量 ID。");
                    if (Str(created, "probesCount") == "0") { SetStatus(row, "無可用節點", "此城市目前沒有可用節點。測量 ID：" + id); return; }
                    while (true)
                    {
                        var measurement = await Api(client, HttpMethod.Get, "/" + Uri.EscapeDataString(id), null, deadline.Token);
                        var sequence = Get(measurement, "results") as System.Collections.IEnumerable;
                        object[] results = sequence == null ? null : sequence.Cast<object>().ToArray();
                        bool done = Str(measurement, "status") != "in-progress";
                        if (results != null && results.Length > 0)
                        {
                            var item = Map(results[0]); var result = Map(Get(item, "result")); var probe = Map(Get(item, "probe"));
                            row.Cells[1].Value = Str(probe, "city") + " / " + Str(probe, "network");
                            if (Str(result, "status") != "in-progress")
                            {
                                var times = Map(Get(result, "timings"));
                                row.Cells[3].Value = Str(result, "statusCode"); row.Cells[4].Value = Time(times, "total");
                                row.Cells[5].Value = Time(times, "dns"); row.Cells[6].Value = Time(times, "tcp"); row.Cells[7].Value = Time(times, "tls");
                                row.Cells[8].Value = Time(times, "firstByte"); row.Cells[9].Value = Str(result, "resolvedAddress");
                                SetStatus(row, Classify(result), "測量 ID：" + id + "\r\n節點：" + Str(probe, "city") + ", " + Str(probe, "country") + " / " + Str(probe, "network") +
                                    "\r\n方法：" + verb + "　節點逾時：" + seconds + " 秒\r\n失敗來源：" + Str(result, "failureSource") + "\r\nHTTP：" + Str(result, "statusCode") + " " + Str(result, "statusCodeName") +
                                    "\r\n\r\n" + Str(result, "rawOutput") + "\r\n" + Str(result, "rawHeaders"));
                                return;
                            }
                        }
                        if (done) { SetStatus(row, "無可用節點", "服務未提供此城市測量結果。測量 ID：" + id); return; }
                        await Task.Delay(500, deadline.Token);
                    }
                }
                catch (ApiException ex)
                {
                    SetStatus(row, ex.Code == 422 ? "無可用節點／請求拒絕" : ex.Code == 429 ? "服務額度限制" : "服務錯誤", "API HTTP " + ex.Code + "：" + ex.Message + "\r\n測量 ID：" + id);
                }
                catch (OperationCanceledException)
                {
                    if (tokenValue.IsCancellationRequested) throw;
                    SetStatus(row, "服務等待逾時", "無法及時取得測量服務結果；無法判定目標是否逾時。\r\n測量 ID：" + id);
                }
                catch (HttpRequestException ex) { SetStatus(row, "測量服務無法連線", ex.Message + "\r\n測量 ID：" + id); }
            }
        }
        internal static string Csv(string value)
        {
            value = value ?? "";
            if (value.TrimStart().Length > 0 && "=+-@".IndexOf(value.TrimStart()[0]) >= 0) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        void Export()
        {
            using (var dialog = new SaveFileDialog { Filter = "CSV 檔案|*.csv", FileName = "全球網址測試-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var lines = new List<string>(); lines.Add(string.Join(",", new[] { "測量時間", "網址", "方法" }.Concat(grid.Columns.Cast<DataGridViewColumn>().Select(c => c.HeaderText)).Concat(new[] { "診斷" }).Select(Csv)));
                    foreach (DataGridViewRow row in grid.Rows) lines.Add(string.Join(",", new[] { testedAt, testedUrl, testedMethod }.Concat(row.Cells.Cast<DataGridViewCell>().Select(c => Convert.ToString(c.Value))).Concat(new[] { Convert.ToString(row.Tag) }).Select(Csv)));
                    File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
                    summary.Text = "已匯出：" + dialog.FileName;
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "匯出失敗"); }
            }
        }
    }
}

