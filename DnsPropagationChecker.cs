using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class DnsPropagationChecker
{
    private static readonly Tuple<string, string>[] Servers = {
        Tuple.Create("Google", "8.8.8.8"), Tuple.Create("Cloudflare", "1.1.1.1"),
        Tuple.Create("Quad9", "9.9.9.9"), Tuple.Create("Cisco OpenDNS 主要", "208.67.222.222"),
        Tuple.Create("Cisco OpenDNS 備用", "208.67.220.220"),
        Tuple.Create("AdGuard", "94.140.14.14"), Tuple.Create("CleanBrowsing", "185.228.168.9"),
        Tuple.Create("中華電信 HiNet 主要", "168.95.1.1"), Tuple.Create("中華電信 HiNet 備用", "168.95.192.1"),
        Tuple.Create("NTT Communications", "220.110.210.114"), Tuple.Create("KDDI", "124.209.159.91"),
        Tuple.Create("SoftBank", "61.196.248.113"),
        Tuple.Create("AliDNS 主要", "223.5.5.5"), Tuple.Create("AliDNS 備用", "223.6.6.6"),
        Tuple.Create("114DNS 主要", "114.114.114.114"), Tuple.Create("114DNS 備用", "114.114.115.115"),
        Tuple.Create("SK Broadband", "210.220.163.82"), Tuple.Create("KT Corp", "168.126.63.1"),
        Tuple.Create("DNS4EU", "86.54.11.1"),
        Tuple.Create("ThaiNS IPv4", "203.159.77.77"), Tuple.Create("ThaiNS IPv6", "2405:3340:e000::77:77")
    };

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new CheckerForm());
    }

    private sealed class CheckerForm : Form
    {
        readonly TextBox domain = new TextBox();
        readonly Button query = new Button();
        readonly Label summary = new Label();
        readonly DataGridView grid = new DataGridView();

        public CheckerForm()
        {
            Text = "DNS 更新檢查";
            Icon = SystemIcons.Application;
            Font = new Font("Microsoft JhengHei UI", 10);
            ClientSize = new Size(820, 430);
            MinimumSize = new Size(650, 330);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 3 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            domain.Dock = DockStyle.Fill;
            domain.AccessibleDescription = "輸入完整網域，例如 example.com";
            query.Text = "查詢";
            query.Dock = DockStyle.Fill;
            query.Click += async (s, e) => await RunQuery();
            domain.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await RunQuery(); } };
            summary.Dock = DockStyle.Fill;
            summary.Text = "輸入網域，查詢多個公共 DNS 的 A / AAAA 回應。結果僅代表這些解析器在查詢當下的狀態。";
            summary.ForeColor = Color.DimGray;
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.Columns.Add("server", "DNS 服務");
            grid.Columns.Add("address", "DNS 位址");
            grid.Columns.Add("ipv4", "IPv4 (A)");
            grid.Columns.Add("ipv6", "IPv6 (AAAA)");
            grid.Columns.Add("status", "狀態");
            grid.Columns[0].FillWeight = 90;
            grid.Columns[1].FillWeight = 105;
            grid.Columns[2].FillWeight = 170;
            grid.Columns[3].FillWeight = 170;
            grid.Columns[4].FillWeight = 95;
            layout.Controls.Add(domain, 0, 0);
            layout.Controls.Add(query, 1, 0);
            layout.Controls.Add(summary, 0, 1);
            layout.SetColumnSpan(summary, 2);
            layout.Controls.Add(grid, 0, 2);
            layout.SetColumnSpan(grid, 2);
            Controls.Add(layout);
            AcceptButton = query;
        }

        async Task RunQuery()
        {
            string host;
            try { host = Validate(domain.Text); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "網域格式錯誤"); return; }
            query.Enabled = false;
            grid.Rows.Clear();
            summary.Text = "查詢中：" + host;
            try
            {
                var tasks = Servers.Select(s => Task.Run(() => Check(s.Item1, s.Item2, host))).ToArray();
                Result[] results = await Task.WhenAll(tasks);
                foreach (Result result in results)
                    grid.Rows.Add(result.Name, result.Server, Display(result.A), Display(result.Aaaa), result.Error ?? "成功");
                var good = results.Where(r => r.Error == null).ToArray();
                var variants = good.Select(r => string.Join(",", r.A) + "|" + string.Join(",", r.Aaaa)).Distinct().Count();
                summary.Text = good.Length == 0 ? "所有 DNS 查詢皆失敗；請檢查網路或防火牆是否允許 DNS (UDP 53)。" :
                    good.Length < results.Length ? string.Format("{0}/{1} 個 DNS 查詢成功；部分失敗，無法判定是否一致。", good.Length, results.Length) :
                    variants == 1 ? "所列 DNS 回應一致；這不保證全球所有解析器已更新。" :
                    "所列 DNS 回應不一致，可能仍在更新，或受地理位置／負載平衡影響。";
            }
            finally { query.Enabled = true; }
        }

        static string Display(string[] values) { return values.Length == 0 ? "（無紀錄）" : string.Join(", ", values); }

        static string Validate(string input)
        {
            string host = (input ?? "").Trim().TrimEnd('.');
            if (host.Length == 0) throw new ArgumentException("請輸入完整網域。");
            try { host = new IdnMapping().GetAscii(host); }
            catch { throw new ArgumentException("網域包含無效字元。"); }
            if (host.Length > 253 || !host.Contains(".") || host.Split('.').Any(x => x.Length == 0 || x.Length > 63 ||
                x.StartsWith("-") || x.EndsWith("-") || x.Any(c => !(char.IsLetterOrDigit(c) || c == '-'))))
                throw new ArgumentException("請輸入完整網域，例如 example.com。");
            return host.ToLowerInvariant();
        }

        static Result Check(string name, string server, string host)
        {
            var result = new Result { Name = name, Server = server, A = new string[0], Aaaa = new string[0] };
            try
            {
                result.A = Ask(server, host, 1);
                result.Aaaa = Ask(server, host, 28);
            }
            catch (SocketException) { result.Error = "逾時／無法連線"; }
            catch (Exception ex) { result.Error = ex.Message; }
            return result;
        }

        static string[] Ask(string server, string host, ushort type)
        {
            var packet = new List<byte>();
            ushort id = (ushort)new Random(Guid.NewGuid().GetHashCode()).Next(1, 65536);
            Add(packet, id); Add(packet, 0x0100); Add(packet, 1);
            Add(packet, 0); Add(packet, 0); Add(packet, 0);
            foreach (string label in host.Split('.')) { packet.Add((byte)label.Length); packet.AddRange(Encoding.ASCII.GetBytes(label)); }
            packet.Add(0); Add(packet, type); Add(packet, 1);
            byte[] data;
            IPAddress resolver = IPAddress.Parse(server);
            using (var udp = new UdpClient(resolver.AddressFamily))
            {
                udp.Client.ReceiveTimeout = 3000;
                udp.Client.SendTimeout = 3000;
                udp.Connect(new IPEndPoint(resolver, 53));
                udp.Send(packet.ToArray(), packet.Count);
                IPEndPoint sender = null;
                data = udp.Receive(ref sender);
            }
            if (data.Length < 12 || Read(data, 0) != id || (Read(data, 2) & 0x8000) == 0)
                throw new Exception("DNS 回應無效");
            if ((Read(data, 2) & 0x0200) != 0) throw new Exception("回應過大（需 TCP）");
            int code = Read(data, 2) & 15;
            if (code == 3) return new string[0];
            if (code != 0) throw new Exception("DNS 錯誤 " + code);
            int offset = 12;
            for (int i = 0; i < Read(data, 4); i++) { SkipName(data, ref offset); offset += 4; CheckBounds(data, offset, 0); }
            var values = new List<string>();
            for (int i = 0; i < Read(data, 6); i++)
            {
                SkipName(data, ref offset);
                CheckBounds(data, offset, 10);
                int recordType = Read(data, offset);
                int recordClass = Read(data, offset + 2);
                int length = Read(data, offset + 8);
                offset += 10;
                CheckBounds(data, offset, length);
                if (recordClass == 1 && recordType == type && length == (type == 1 ? 4 : 16))
                {
                    byte[] bytes = new byte[length];
                    Buffer.BlockCopy(data, offset, bytes, 0, length);
                    values.Add(new IPAddress(bytes).ToString());
                }
                offset += length;
            }
            return values.Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        static void SkipName(byte[] data, ref int offset)
        {
            while (true)
            {
                CheckBounds(data, offset, 1);
                int length = data[offset++];
                if (length == 0) return;
                if ((length & 0xC0) == 0xC0) { CheckBounds(data, offset, 1); offset++; return; }
                if (length > 63) throw new Exception("DNS 名稱無效");
                CheckBounds(data, offset, length);
                offset += length;
            }
        }

        static void CheckBounds(byte[] data, int offset, int length)
        { if (offset < 0 || length < 0 || offset > data.Length - length) throw new Exception("DNS 回應不完整"); }
        static int Read(byte[] data, int offset) { CheckBounds(data, offset, 2); return (data[offset] << 8) | data[offset + 1]; }
        static void Add(List<byte> data, int value) { data.Add((byte)(value >> 8)); data.Add((byte)value); }

        sealed class Result
        {
            public string Name, Server, Error;
            public string[] A, Aaaa;
        }
    }
}
