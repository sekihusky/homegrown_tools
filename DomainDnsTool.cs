using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DomainDnsTool
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
        private readonly TextBox domainBox = new TextBox();
        private readonly Button queryButton = new Button();
        private readonly TextBox resultBox = new TextBox();
        private readonly Label statusLabel = new Label();

        public MainForm()
        {
            Text = "網域 IP / DNS 查詢工具";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(620, 420);
            MinimumSize = new Size(560, 380);
            Font = new Font("Microsoft JhengHei UI", 10F);
            Icon = SystemIcons.Information;

            var title = new Label
            {
                Text = "輸入網域名稱",
                AutoSize = true,
                Location = new Point(24, 24),
                Font = new Font(Font, FontStyle.Bold)
            };

            domainBox.Location = new Point(24, 54);
            domainBox.Size = new Size(455, 30);
            domainBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            domainBox.PlaceholderTextCompat("例如：example.com");
            domainBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    StartQuery();
                }
            };

            queryButton.Text = "查詢";
            queryButton.Location = new Point(495, 52);
            queryButton.Size = new Size(100, 34);
            queryButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            queryButton.Click += delegate { StartQuery(); };

            statusLabel.Text = "可輸入純網域，也可直接貼上網址";
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.AutoSize = false;
            statusLabel.Location = new Point(24, 96);
            statusLabel.Size = new Size(571, 26);
            statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            resultBox.Location = new Point(24, 128);
            resultBox.Size = new Size(571, 260);
            resultBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            resultBox.Multiline = true;
            resultBox.ReadOnly = true;
            resultBox.ScrollBars = ScrollBars.Vertical;
            resultBox.BackColor = Color.White;
            resultBox.Font = new Font("Consolas", 10.5F);

            Controls.Add(title);
            Controls.Add(domainBox);
            Controls.Add(queryButton);
            Controls.Add(statusLabel);
            Controls.Add(resultBox);
            AcceptButton = queryButton;
        }

        private async void StartQuery()
        {
            string domain;
            try
            {
                domain = NormalizeDomain(domainBox.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            queryButton.Enabled = false;
            domainBox.Enabled = false;
            resultBox.Text = "查詢中...";
            statusLabel.Text = "正在查詢 " + domain;

            try
            {
                var result = await Task.Run(() => Lookup(domain));
                resultBox.Text = FormatResult(result);
                statusLabel.Text = "查詢完成";
            }
            catch (Exception ex)
            {
                resultBox.Text = "查詢失敗\r\n\r\n" + FriendlyError(ex);
                statusLabel.Text = "查詢失敗";
            }
            finally
            {
                queryButton.Enabled = true;
                domainBox.Enabled = true;
                domainBox.Focus();
                domainBox.SelectAll();
            }
        }

        private static LookupResult Lookup(string domain)
        {
            IPAddress[] addresses;
            Exception ipError = null;
            try { addresses = Dns.GetHostAddresses(domain); }
            catch (Exception ex) { addresses = new IPAddress[0]; ipError = ex; }

            List<string> nameServers = new List<string>();
            Exception nsError = null;
            string nsDomain = domain;
            try
            {
                string current = domain;
                while (current.Contains("."))
                {
                    nameServers = DnsNsClient.Query(current);
                    if (nameServers.Count > 0) { nsDomain = current; break; }
                    current = current.Substring(current.IndexOf('.') + 1);
                }
            }
            catch (Exception ex) { nsError = ex; }

            if (addresses.Length == 0 && nameServers.Count == 0)
                throw new Exception("找不到此網域的 DNS 資料。請確認網域名稱與網路連線。", ipError ?? nsError);

            var ipDetails = new Dictionary<string, IpDetail>();
            foreach (IPAddress address in addresses.Distinct())
            {
                try
                {
                    IpDetail detail = IpInfoClient.Query(address);
                    if (detail != null) ipDetails[address.ToString()] = detail;
                }
                catch { }
            }

            var dnsRecords = new Dictionary<string, string[]>();
            var recordTypes = new[]
            {
                new DnsQueryType("CNAME", 5),
                new DnsQueryType("MX", 15),
                new DnsQueryType("TXT", 16),
                new DnsQueryType("SOA", 6),
                new DnsQueryType("CAA", 257),
                new DnsQueryType("SRV", 33),
                new DnsQueryType("DS", 43),
                new DnsQueryType("DNSKEY", 48)
            };
            foreach (DnsQueryType recordType in recordTypes)
            {
                try
                {
                    string[] values = DnsNsClient.Query(domain, recordType.Type)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    if (values.Length > 0) dnsRecords[recordType.Name] = values;
                }
                catch { }
            }

            return new LookupResult
            {
                Domain = domain,
                Addresses = addresses.Distinct().OrderBy(a => a.AddressFamily).ThenBy(a => a.ToString()).ToArray(),
                NameServers = nameServers.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray(),
                NameServerDomain = nsDomain,
                IpDetails = ipDetails,
                DnsRecords = dnsRecords,
                IpError = ipError,
                NsError = nsError
            };
        }

        private static string FormatResult(LookupResult result)
        {
            var lines = new List<string>();
            lines.Add("網域：" + result.Domain);
            lines.Add("");
            lines.Add("IP 位址：");
            if (result.Addresses.Length == 0) lines.Add("  （查無 A / AAAA 紀錄）");
            foreach (var address in result.Addresses)
            {
                string type = address.AddressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6";
                lines.Add("  " + address + "  (" + type + ")");
                IpDetail detail;
                if (result.IpDetails != null && result.IpDetails.TryGetValue(address.ToString(), out detail))
                {
                    if (!string.IsNullOrWhiteSpace(detail.Isp))
                        lines.Add("    ISP / 組織：" + detail.Isp);
                    if (detail.Asn > 0)
                        lines.Add("    ASN：AS" + detail.Asn);
                    string location = string.Join("、", new[] { detail.Country, detail.Region, detail.City }
                        .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray());
                    if (location.Length > 0)
                        lines.Add("    推估地區：" + location);
                }
            }
            lines.Add("");
            lines.Add("管理 DNS 主機（NS）：");
            if (result.NameServers.Length == 0) lines.Add("  （查無 NS 紀錄）");
            foreach (string server in result.NameServers) lines.Add("  " + server);
            if (!result.NameServerDomain.Equals(result.Domain, StringComparison.OrdinalIgnoreCase))
                lines.Add("  [NS 所屬網域：" + result.NameServerDomain + "]");

            if (result.DnsRecords != null && result.DnsRecords.Count > 0)
            {
                string[] displayOrder = { "CNAME", "MX", "TXT", "SOA", "CAA", "SRV", "DS", "DNSKEY" };
                foreach (string recordType in displayOrder)
                {
                    string[] values;
                    if (!result.DnsRecords.TryGetValue(recordType, out values)) continue;
                    lines.Add("");
                    lines.Add(recordType + " 紀錄：");
                    foreach (string value in values) lines.Add("  " + value);
                }
            }
            return string.Join("\r\n", lines.ToArray());
        }

        private static string NormalizeDomain(string input)
        {
            string value = (input ?? "").Trim();
            if (value.Length == 0) throw new Exception("請輸入網域名稱。");

            Uri uri;
            if (!value.Contains("://")) value = "http://" + value;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || string.IsNullOrWhiteSpace(uri.Host))
                throw new Exception("網域格式不正確。範例：example.com");

            string host = uri.Host.TrimEnd('.');
            IPAddress parsedAddress;
            if (IPAddress.TryParse(host, out parsedAddress)) throw new Exception("請輸入網域名稱，不要直接輸入 IP 位址。");
            try { host = new IdnMapping().GetAscii(host); }
            catch { throw new Exception("網域格式不正確。請檢查是否包含無效字元。"); }
            if (!host.Contains(".") || host.Length > 253) throw new Exception("請輸入完整網域，例如 example.com。");
            return host.ToLowerInvariant();
        }

        private static string FriendlyError(Exception ex)
        {
            if (ex is SocketException) return "無法連線至 DNS 服務。請檢查網路連線後再試一次。";
            return ex.Message;
        }
    }

    internal sealed class LookupResult
    {
        public string Domain;
        public IPAddress[] Addresses;
        public string[] NameServers;
        public string NameServerDomain;
        public Dictionary<string, IpDetail> IpDetails;
        public Dictionary<string, string[]> DnsRecords;
        public Exception IpError;
        public Exception NsError;
    }

    internal sealed class DnsQueryType
    {
        public readonly string Name;
        public readonly ushort Type;

        public DnsQueryType(string name, ushort type)
        {
            Name = name;
            Type = type;
        }
    }

    internal sealed class IpDetail
    {
        public string Country;
        public string Region;
        public string City;
        public string Isp;
        public long Asn;
    }

    internal static class IpInfoClient
    {
        public static IpDetail Query(IPAddress address)
        {
            using (var client = new WebClient())
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.UserAgent] = "DomainDnsTool/1.1";
                string url = "http://ipwho.is/" + Uri.EscapeDataString(address.ToString());
                byte[] json = client.DownloadData(url);
                IpWhoResponse root;
                using (var stream = new MemoryStream(json))
                    root = (IpWhoResponse)new DataContractJsonSerializer(typeof(IpWhoResponse)).ReadObject(stream);
                if (root == null || !root.success) return null;

                var detail = new IpDetail
                {
                    Country = root.country,
                    Region = root.region,
                    City = root.city
                };

                if (root.connection != null)
                {
                    detail.Isp = root.connection.isp;
                    if (string.IsNullOrWhiteSpace(detail.Isp)) detail.Isp = root.connection.org;
                    detail.Asn = root.connection.asn;
                }
                return detail;
            }
        }

        [DataContract]
        private sealed class IpWhoResponse
        {
            [DataMember]
            public bool success;
            [DataMember]
            public string country;
            [DataMember]
            public string region;
            [DataMember]
            public string city;
            [DataMember]
            public IpWhoConnection connection;
        }

        [DataContract]
        private sealed class IpWhoConnection
        {
            [DataMember]
            public long asn;
            [DataMember]
            public string isp;
            [DataMember]
            public string org;
        }
    }

    internal static class DnsNsClient
    {
        private const ushort NsType = 2;

        public static List<string> Query(string domain)
        {
            return Query(domain, NsType);
        }

        public static List<string> Query(string domain, ushort queryType)
        {
            byte[] request = BuildRequest(domain, queryType);
            Exception lastError = null;
            foreach (IPAddress dnsServer in GetDnsServers())
            {
                try
                {
                    using (var udp = new UdpClient(dnsServer.AddressFamily))
                    {
                        udp.Client.SendTimeout = 2500;
                        udp.Client.ReceiveTimeout = 2500;
                        udp.Connect(new IPEndPoint(dnsServer, 53));
                        udp.Send(request, request.Length);
                        IPEndPoint remote = null;
                        byte[] response = udp.Receive(ref remote);
                        if (IsTruncated(response)) response = QueryTcp(dnsServer, request);
                        return ParseResponse(response, queryType);
                    }
                }
                catch (Exception ex) { lastError = ex; }
            }
            if (lastError != null) throw lastError;
            return new List<string>();
        }

        private static bool IsTruncated(byte[] response)
        {
            return response != null && response.Length >= 4 && (ReadU16(response, 2) & 0x0200) != 0;
        }

        private static byte[] QueryTcp(IPAddress server, byte[] request)
        {
            using (var client = new TcpClient(server.AddressFamily))
            {
                client.SendTimeout = 3500;
                client.ReceiveTimeout = 3500;
                client.Connect(new IPEndPoint(server, 53));
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] length = { (byte)(request.Length >> 8), (byte)request.Length };
                    stream.Write(length, 0, length.Length);
                    stream.Write(request, 0, request.Length);
                    byte[] responseLength = ReadExact(stream, 2);
                    int count = (responseLength[0] << 8) | responseLength[1];
                    if (count <= 0) throw new InvalidDataException("DNS TCP 回應長度無效。");
                    return ReadExact(stream, count);
                }
            }
        }

        private static byte[] ReadExact(Stream stream, int count)
        {
            byte[] result = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int read = stream.Read(result, offset, count - offset);
                if (read <= 0) throw new EndOfStreamException("DNS TCP 回應資料不完整。");
                offset += read;
            }
            return result;
        }

        private static IEnumerable<IPAddress> GetDnsServers()
        {
            // Query public recursive resolvers first so the result represents the
            // public NS record instead of a router/ISP resolver's stale cache.
            var servers = new List<IPAddress>
            {
                IPAddress.Parse("8.8.8.8"),
                IPAddress.Parse("1.1.1.1")
            };
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var address in nic.GetIPProperties().DnsAddresses)
                        if (!servers.Contains(address)) servers.Add(address);
                }
            }
            catch { }
            return servers;
        }

        private static byte[] BuildRequest(string domain, ushort queryType)
        {
            var bytes = new List<byte>();
            ushort id = (ushort)new Random().Next(1, 65535);
            AddU16(bytes, id); AddU16(bytes, 0x0100); AddU16(bytes, 1);
            AddU16(bytes, 0); AddU16(bytes, 0); AddU16(bytes, 0);
            foreach (string label in domain.Split('.'))
            {
                byte[] labelBytes = Encoding.ASCII.GetBytes(label);
                bytes.Add((byte)labelBytes.Length);
                bytes.AddRange(labelBytes);
            }
            bytes.Add(0);
            AddU16(bytes, queryType); AddU16(bytes, 1);
            return bytes.ToArray();
        }

        private static List<string> ParseResponse(byte[] data, ushort queryType)
        {
            if (data.Length < 12) throw new InvalidDataException("DNS 回應格式不正確。");
            int questionCount = ReadU16(data, 4);
            int answerCount = ReadU16(data, 6);
            int offset = 12;
            for (int i = 0; i < questionCount; i++)
            {
                ReadName(data, ref offset);
                offset += 4;
            }

            var result = new List<string>();
            for (int i = 0; i < answerCount; i++)
            {
                ReadName(data, ref offset);
                Ensure(data, offset, 10);
                int type = ReadU16(data, offset);
                int dataLength = ReadU16(data, offset + 8);
                offset += 10;
                Ensure(data, offset, dataLength);
                if (type == queryType)
                {
                    string value = FormatRecord(data, offset, dataLength, type);
                    if (!string.IsNullOrWhiteSpace(value)) result.Add(value);
                }
                offset += dataLength;
            }
            return result;
        }

        private static string FormatRecord(byte[] data, int offset, int length, int type)
        {
            int position = offset;
            if (type == 2 || type == 5)
                return ReadName(data, ref position).TrimEnd('.');

            if (type == 15)
            {
                int preference = ReadU16(data, position);
                position += 2;
                return preference + " " + ReadName(data, ref position).TrimEnd('.');
            }

            if (type == 16)
            {
                var parts = new List<string>();
                int end = offset + length;
                while (position < end)
                {
                    int partLength = data[position++];
                    Ensure(data, position, partLength);
                    parts.Add(Encoding.UTF8.GetString(data, position, partLength));
                    position += partLength;
                }
                return "\"" + string.Join("", parts.ToArray()) + "\"";
            }

            if (type == 6)
            {
                string primary = ReadName(data, ref position).TrimEnd('.');
                string responsible = ReadName(data, ref position).TrimEnd('.');
                long serial = ReadU32(data, position); position += 4;
                long refresh = ReadU32(data, position); position += 4;
                long retry = ReadU32(data, position); position += 4;
                long expire = ReadU32(data, position); position += 4;
                long minimum = ReadU32(data, position);
                return primary + " " + responsible + " (serial=" + serial + ", refresh=" + refresh +
                    ", retry=" + retry + ", expire=" + expire + ", minimum=" + minimum + ")";
            }

            if (type == 33)
            {
                int priority = ReadU16(data, position); position += 2;
                int weight = ReadU16(data, position); position += 2;
                int port = ReadU16(data, position); position += 2;
                return priority + " " + weight + " " + port + " " + ReadName(data, ref position).TrimEnd('.');
            }

            if (type == 257)
            {
                int flags = data[position++];
                int tagLength = data[position++];
                Ensure(data, position, tagLength);
                string tag = Encoding.ASCII.GetString(data, position, tagLength);
                position += tagLength;
                string value = Encoding.UTF8.GetString(data, position, offset + length - position);
                return flags + " " + tag + " \"" + value + "\"";
            }

            if (type == 43)
            {
                int keyTag = ReadU16(data, position); position += 2;
                int algorithm = data[position++];
                int digestType = data[position++];
                return keyTag + " " + algorithm + " " + digestType + " " + ToHex(data, position, offset + length - position);
            }

            if (type == 48)
            {
                int flags = ReadU16(data, position); position += 2;
                int protocol = data[position++];
                int algorithm = data[position++];
                byte[] key = new byte[offset + length - position];
                Buffer.BlockCopy(data, position, key, 0, key.Length);
                return flags + " " + protocol + " " + algorithm + " " + Convert.ToBase64String(key);
            }
            return null;
        }

        private static long ReadU32(byte[] data, int offset)
        {
            Ensure(data, offset, 4);
            return ((long)data[offset] << 24) | ((long)data[offset + 1] << 16) |
                ((long)data[offset + 2] << 8) | data[offset + 3];
        }

        private static string ToHex(byte[] data, int offset, int count)
        {
            Ensure(data, offset, count);
            var builder = new StringBuilder(count * 2);
            for (int i = 0; i < count; i++) builder.Append(data[offset + i].ToString("X2"));
            return builder.ToString();
        }

        private static string ReadName(byte[] data, ref int offset)
        {
            var labels = new List<string>();
            int position = offset;
            bool jumped = false;
            int loops = 0;
            while (true)
            {
                Ensure(data, position, 1);
                int length = data[position];
                if (length == 0)
                {
                    if (!jumped) offset = position + 1;
                    break;
                }
                if ((length & 0xC0) == 0xC0)
                {
                    Ensure(data, position, 2);
                    int pointer = ((length & 0x3F) << 8) | data[position + 1];
                    if (!jumped) offset = position + 2;
                    position = pointer;
                    jumped = true;
                    if (++loops > 20) throw new InvalidDataException("DNS 名稱壓縮指標無效。");
                    continue;
                }
                position++;
                Ensure(data, position, length);
                labels.Add(Encoding.ASCII.GetString(data, position, length));
                position += length;
                if (!jumped) offset = position;
            }
            return string.Join(".", labels.ToArray());
        }

        private static int ReadU16(byte[] data, int offset)
        {
            Ensure(data, offset, 2);
            return (data[offset] << 8) | data[offset + 1];
        }

        private static void AddU16(List<byte> bytes, int value)
        {
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)value);
        }

        private static void Ensure(byte[] data, int offset, int count)
        {
            if (offset < 0 || count < 0 || offset + count > data.Length)
                throw new InvalidDataException("DNS 回應資料不完整。");
        }
    }

    internal static class TextBoxExtensions
    {
        // .NET Framework WinForms 沒有 PlaceholderText；以提示框內容保持介面簡潔。
        public static void PlaceholderTextCompat(this TextBox box, string text)
        {
            box.AccessibleDescription = text;
        }
    }
}
