using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LanDeviceScanner
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
        private readonly ComboBox addressBox = new ComboBox();
        private readonly Button refreshButton = new Button();
        private readonly Button scanButton = new Button();
        private readonly Button stopButton = new Button();
        private readonly Label statusLabel = new Label();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly ListView devices = new ListView();
        private CancellationTokenSource cancellation;

        public MainForm()
        {
            Text = "區域網路裝置掃描器";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(850, 575);
            MinimumSize = new Size(720, 500);
            Font = new Font("Microsoft JhengHei UI", 10F);
            Icon = SystemIcons.Information;
            BackColor = Color.FromArgb(246, 248, 251);

            var title = new Label { Text = "區域網路裝置掃描器", AutoSize = true, Location = new Point(24, 18), Font = new Font(Font.FontFamily, 18F, FontStyle.Bold), ForeColor = Color.FromArgb(28, 38, 55) };
            var subtitle = new Label { Text = "找出同一個 /24（C Class）網段內正在使用的 IP", AutoSize = true, Location = new Point(27, 55), ForeColor = Color.DimGray };
            var addressLabel = new Label { Text = "選擇本機 IPv4", AutoSize = true, Location = new Point(24, 91) };

            addressBox.DropDownStyle = ComboBoxStyle.DropDownList;
            addressBox.Location = new Point(24, 116);
            addressBox.Size = new Size(390, 29);
            refreshButton.Text = "重新偵測";
            refreshButton.Location = new Point(425, 112);
            refreshButton.Size = new Size(92, 36);
            refreshButton.Click += delegate { LoadAddresses(); };
            scanButton.Text = "開始搜尋";
            scanButton.Location = new Point(626, 112);
            scanButton.Size = new Size(96, 36);
            scanButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            scanButton.BackColor = Color.FromArgb(37, 99, 235);
            scanButton.ForeColor = Color.White;
            scanButton.FlatStyle = FlatStyle.Flat;
            scanButton.FlatAppearance.BorderSize = 0;
            scanButton.Click += async delegate { await StartScan(); };
            stopButton.Text = "停止";
            stopButton.Location = new Point(732, 112);
            stopButton.Size = new Size(94, 36);
            stopButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            stopButton.Enabled = false;
            stopButton.Click += delegate { if (cancellation != null) cancellation.Cancel(); };

            statusLabel.Text = "準備就緒";
            statusLabel.Location = new Point(24, 165);
            statusLabel.Size = new Size(802, 24);
            statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progress.Location = new Point(24, 194);
            progress.Size = new Size(802, 8);
            progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progress.Minimum = 0;
            progress.Maximum = 254;

            devices.Location = new Point(24, 220);
            devices.Size = new Size(802, 325);
            devices.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            devices.View = View.Details;
            devices.FullRowSelect = true;
            devices.GridLines = true;
            devices.HideSelection = false;
            devices.Columns.Add("IP 位址", 125);
            devices.Columns.Add("裝置名稱", 225);
            devices.Columns.Add("裝置類型（推測）", 175);
            devices.Columns.Add("MAC 位址", 175);
            devices.Columns.Add("回應", 75);

            Controls.AddRange(new Control[] { title, subtitle, addressLabel, addressBox, refreshButton, scanButton, stopButton, statusLabel, progress, devices });
            Shown += delegate { LoadAddresses(); };
        }

        private void LoadAddresses()
        {
            addressBox.Items.Clear();
            foreach (LocalAddress address in GetLocalAddresses()) addressBox.Items.Add(address);
            if (addressBox.Items.Count == 1) addressBox.SelectedIndex = 0;
            statusLabel.Text = addressBox.Items.Count == 0 ? "找不到可用的私人 IPv4 網路介面。" :
                addressBox.Items.Count == 1 ? "已找到本機 IP，可開始搜尋。" : "找到多個本機 IP，請先選擇要掃描的網路。";
            scanButton.Enabled = addressBox.Items.Count > 0;
        }

        private static IEnumerable<LocalAddress> GetLocalAddresses()
        {
            var result = new List<LocalAddress>();
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (UnicastIPAddressInformation item in nic.GetIPProperties().UnicastAddresses)
                {
                    if (item.Address.AddressFamily != AddressFamily.InterNetwork || !IsPrivate(item.Address)) continue;
                    result.Add(new LocalAddress { Address = item.Address, InterfaceName = nic.Name });
                }
            }
            return result.OrderBy(x => x.Address.ToString());
        }

        private async Task StartScan()
        {
            var selected = addressBox.SelectedItem as LocalAddress;
            if (selected == null)
            {
                MessageBox.Show(this, "請先選擇一個本機 IPv4 位址。", "選擇網路", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                addressBox.DroppedDown = true;
                return;
            }

            cancellation = new CancellationTokenSource();
            SetBusy(true);
            devices.Items.Clear();
            progress.Value = 0;
            byte[] bytes = selected.Address.GetAddressBytes();
            string prefix = bytes[0] + "." + bytes[1] + "." + bytes[2] + ".";
            statusLabel.Text = "正在搜尋 " + prefix + "1 – " + prefix + "254…";

            var found = new ConcurrentBag<DeviceInfo>();
            int completed = 0;
            var gate = new SemaphoreSlim(48);
            var tasks = new List<Task>();
            for (int host = 1; host <= 254; host++)
            {
                string target = prefix + host;
                tasks.Add(Task.Run(async () =>
                {
                    await gate.WaitAsync(cancellation.Token);
                    try
                    {
                        DeviceInfo info = await Probe(target, selected.Address, cancellation.Token);
                        if (info != null) found.Add(info);
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        gate.Release();
                        int done = Interlocked.Increment(ref completed);
                        BeginInvoke((Action)(() => { progress.Value = Math.Min(254, done); statusLabel.Text = "搜尋中… " + done + "/254，已找到 " + found.Count + " 個裝置"; }));
                    }
                }, cancellation.Token));
            }

            try { await Task.WhenAll(tasks); } catch (OperationCanceledException) { }
            List<DeviceInfo> ordered = found.OrderBy(x => IpNumber(x.Address)).ToList();
            foreach (DeviceInfo info in ordered) AddDevice(info);
            bool stopped = cancellation.IsCancellationRequested;
            statusLabel.Text = stopped
                ? "搜尋已停止；已列出目前找到的 " + ordered.Count + " 個裝置。"
                : "搜尋完成：已掃描 " + prefix + "1 ～ " + prefix + "254，共找到 " + ordered.Count + " 個裝置；未列出的 IP 沒有回應。";
            SetBusy(false);
        }

        private static async Task<DeviceInfo> Probe(string target, IPAddress localAddress, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            IPAddress address = IPAddress.Parse(target);
            long elapsed = -1;
            if (address.Equals(localAddress)) elapsed = 0;
            else
            {
                try
                {
                    using (var ping = new Ping())
                    {
                        PingReply reply = await ping.SendPingAsync(address, 650);
                        if (reply.Status == IPStatus.Success) elapsed = reply.RoundtripTime;
                    }
                }
                catch { }
            }

            string mac = GetMacAddress(address);
            if (elapsed < 0 && String.IsNullOrEmpty(mac)) return null;
            string name = await ResolveName(address);
            string type = GuessType(name, address, localAddress);
            if (type == "無法判斷") type = await GuessTypeFromServices(address, token);
            return new DeviceInfo { Address = address, Name = name, Type = type, Mac = String.IsNullOrEmpty(mac) ? "—" : mac, Response = elapsed < 0 ? "ARP" : elapsed + " ms" };
        }

        private static async Task<string> ResolveName(IPAddress address)
        {
            try
            {
                Task<IPHostEntry> lookup = Dns.GetHostEntryAsync(address);
                Task finished = await Task.WhenAny(lookup, Task.Delay(700));
                if (finished == lookup) return (await lookup).HostName;
            }
            catch { }
            return "—";
        }

        private static string GuessType(string name, IPAddress address, IPAddress local)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (address.Equals(local)) return "這台電腦";
            if (n.Contains("iphone") || n.Contains("ipad") || n.Contains("apple")) return "蘋果行動裝置";
            if (n.Contains("android") || n.Contains("galaxy") || n.Contains("pixel") || n.Contains("xiaomi") || n.Contains("oppo") || n.Contains("vivo")) return "Android 裝置";
            if (n.Contains("tv") || n.Contains("chromecast") || n.Contains("appletv") || n.Contains("roku") || n.Contains("shield")) return "電視／機上盒";
            if (n.Contains("printer") || n.Contains("epson") || n.Contains("brother") || n.Contains("canon") || n.Contains("hp")) return "印表機";
            if (n.Contains("router") || n.Contains("gateway") || n.Contains("asus") || n.Contains("tplink")) return "路由器／網路設備";
            if (n != "—" && (n.Contains("desktop") || n.Contains("laptop") || n.Contains("pc-") || n.Contains("macbook") || n.Contains("imac"))) return "電腦";
            return "無法判斷";
        }

        private static async Task<string> GuessTypeFromServices(IPAddress address, CancellationToken token)
        {
            if (await PortOpen(address, 9100, token)) return "印表機（推測）";
            if (await PortOpen(address, 62078, token)) return "蘋果行動裝置（推測）";
            if (await PortOpen(address, 5555, token)) return "Android 裝置（推測）";
            if (await PortOpen(address, 8008, token) || await PortOpen(address, 8009, token)) return "電視／機上盒（推測）";
            if (await PortOpen(address, 3389, token) || await PortOpen(address, 445, token)) return "電腦（推測）";
            if (await PortOpen(address, 53, token)) return "路由器／網路設備（推測）";
            return "無法判斷";
        }

        private static async Task<bool> PortOpen(IPAddress address, int port, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (var client = new TcpClient())
            {
                try
                {
                    Task connect = client.ConnectAsync(address, port);
                    Task finished = await Task.WhenAny(connect, Task.Delay(140, token));
                    return finished == connect && client.Connected;
                }
                catch { return false; }
            }
        }

        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(uint destination, uint source, byte[] mac, ref int length);

        private static string GetMacAddress(IPAddress address)
        {
            try
            {
                byte[] mac = new byte[6];
                int length = mac.Length;
                byte[] bytes = address.GetAddressBytes();
                uint destination = BitConverter.ToUInt32(bytes, 0);
                if (SendARP(destination, 0, mac, ref length) != 0 || length == 0) return "";
                return String.Join("-", mac.Take(length).Select(b => b.ToString("X2")));
            }
            catch { return ""; }
        }

        private void AddDevice(DeviceInfo info)
        {
            var item = new ListViewItem(info.Address.ToString());
            item.SubItems.Add(info.Name);
            item.SubItems.Add(info.Type);
            item.SubItems.Add(info.Mac);
            item.SubItems.Add(info.Response);
            if (info.Type == "這台電腦") item.BackColor = Color.FromArgb(219, 234, 254);
            devices.Items.Add(item);
        }

        private void SetBusy(bool busy)
        {
            addressBox.Enabled = !busy;
            refreshButton.Enabled = !busy;
            scanButton.Enabled = !busy;
            stopButton.Enabled = busy;
            scanButton.Text = busy ? "搜尋中…" : "開始搜尋";
        }

        private static bool IsPrivate(IPAddress address)
        {
            byte[] b = address.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
        }

        private static uint IpNumber(IPAddress address)
        {
            byte[] b = address.GetAddressBytes();
            return ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        }

        private sealed class LocalAddress
        {
            public IPAddress Address;
            public string InterfaceName;
            public override string ToString() { return Address + "  —  " + InterfaceName; }
        }

        private sealed class DeviceInfo
        {
            public IPAddress Address;
            public string Name;
            public string Type;
            public string Mac;
            public string Response;
        }
    }
}
