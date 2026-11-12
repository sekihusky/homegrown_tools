using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DiskCleanupHelper
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

    internal sealed class CleanupRoot
    {
        public readonly string Name, Path;
        public readonly bool DumpsOnly;
        public CleanupRoot(string name, string path, bool dumpsOnly)
        { Name = name; Path = System.IO.Path.GetFullPath(path).TrimEnd('\\'); DumpsOnly = dumpsOnly; }
    }
    internal sealed class Candidate
    {
        public CleanupRoot Root;
        public string Path;
        public long Length;
        public DateTime Modified, Created;
    }
    internal sealed class ScanResult
    {
        public readonly List<Candidate> Files = new List<Candidate>();
        public readonly List<string> Notes = new List<string>();
        public int Skipped;
    }

    internal static class CleanupEngine
    {
        public static List<CleanupRoot> DefaultRoots()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            return new List<CleanupRoot> {
                new CleanupRoot("使用者暫存", System.IO.Path.Combine(local, "Temp"), false),
                new CleanupRoot("當機報告", System.IO.Path.Combine(local, "CrashDumps"), true),
                new CleanupRoot("Windows 暫存", System.IO.Path.Combine(windows, "Temp"), false)
            }.Where(r => System.IO.Path.GetPathRoot(r.Path).Equals("C:\\", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Never follow junctions/symlinks, including ancestors above the allowed root.
        internal static bool SafePath(CleanupRoot root, string path, bool file)
        {
            string full = System.IO.Path.GetFullPath(path);
            if (!(full.Equals(root.Path, StringComparison.OrdinalIgnoreCase) && !file) &&
                !full.StartsWith(root.Path + "\\", StringComparison.OrdinalIgnoreCase)) return false;
            string current = full;
            while (!String.IsNullOrEmpty(current))
            {
                FileAttributes a = File.GetAttributes(current);
                if ((a & FileAttributes.ReparsePoint) != 0) return false;
                current = System.IO.Path.GetDirectoryName(current);
            }
            return true;
        }

        internal static bool Eligible(CleanupRoot root, FileInfo f, DateTime cutoff)
        {
            f.Refresh();
            const FileAttributes blocked = FileAttributes.ReparsePoint | FileAttributes.System |
                FileAttributes.ReadOnly | FileAttributes.Offline | FileAttributes.Directory;
            return f.Exists && (f.Attributes & blocked) == 0 && f.LastWriteTimeUtc < cutoff &&
                f.CreationTimeUtc < cutoff && (!root.DumpsOnly || f.Extension.Equals(".dmp", StringComparison.OrdinalIgnoreCase));
        }

        public static ScanResult Scan(IEnumerable<CleanupRoot> roots, DateTime cutoff, CancellationToken token)
        {
            var result = new ScanResult();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CleanupRoot root in roots)
            {
                token.ThrowIfCancellationRequested();
                if (!Directory.Exists(root.Path)) { result.Notes.Add("不存在或無法存取：" + root.Path); continue; }
                var pending = new Stack<string>(); pending.Push(root.Path);
                while (pending.Count > 0)
                {
                    token.ThrowIfCancellationRequested();
                    string dir = pending.Pop();
                    try
                    {
                        if (!SafePath(root, dir, false)) { result.Skipped++; continue; }
                        foreach (string path in Directory.EnumerateFileSystemEntries(dir))
                        {
                            token.ThrowIfCancellationRequested();
                            try
                            {
                                FileAttributes a = File.GetAttributes(path);
                                if ((a & (FileAttributes.ReparsePoint | FileAttributes.System | FileAttributes.Offline)) != 0)
                                { result.Skipped++; continue; }
                                if ((a & FileAttributes.Directory) != 0) { pending.Push(path); continue; }
                                var f = new FileInfo(path);
                                if (!Eligible(root, f, cutoff) || !SafePath(root, path, true)) { result.Skipped++; continue; }
                                if (seen.Add(f.FullName)) result.Files.Add(new Candidate {
                                    Root = root, Path = f.FullName, Length = f.Length,
                                    Modified = f.LastWriteTimeUtc, Created = f.CreationTimeUtc });
                            }
                            catch (IOException) { result.Skipped++; }
                            catch (UnauthorizedAccessException) { result.Skipped++; }
                        }
                    }
                    catch (IOException ex) { result.Notes.Add(dir + "：" + ex.Message); }
                    catch (UnauthorizedAccessException ex) { result.Notes.Add(dir + "：" + ex.Message); }
                }
            }
            return result;
        }

        // Revalidate the allowlist, age and snapshot immediately before the shell operation.
        public static bool Validate(Candidate c, IEnumerable<CleanupRoot> allowed, DateTime cutoff)
        {
            if (!allowed.Any(r => r.Path.Equals(c.Root.Path, StringComparison.OrdinalIgnoreCase) && r.DumpsOnly == c.Root.DumpsOnly)) return false;
            if (!SafePath(c.Root, c.Path, true)) return false;
            var f = new FileInfo(c.Path);
            if (!Eligible(c.Root, f, cutoff) || f.Length != c.Length ||
                f.LastWriteTimeUtc != c.Modified || f.CreationTimeUtc != c.Created) return false;
            // Do not force removal of files currently opened by another application.
            using (new FileStream(c.Path, FileMode.Open, FileAccess.Read, FileShare.None)) { }
            return true;
        }

        public static string Size(long bytes)
        {
            if (bytes >= 1073741824) return (bytes / 1073741824.0).ToString("0.00") + " GB";
            if (bytes >= 1048576) return (bytes / 1048576.0).ToString("0.0") + " MB";
            return (bytes / 1024.0).ToString("0.0") + " KB";
        }
    }

    internal static class Recycle
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct Operation
        {
            public IntPtr hwnd;
            public uint function;
            [MarshalAs(UnmanagedType.LPWStr)] public string from;
            [MarshalAs(UnmanagedType.LPWStr)] public string to;
            public ushort flags;
            [MarshalAs(UnmanagedType.Bool)] public bool aborted;
            public IntPtr mappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string title;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHFileOperation(ref Operation operation);

        public static void Send(string path, IntPtr owner)
        {
            // ALLOWUNDO + NOCONFIRMATION + WANTNUKEWARNING + NO_CONNECTED_ELEMENTS.
            // Keep the Windows warning if recycling is impossible. Never answer it automatically.
            var op = new Operation { hwnd = owner, function = 3, from = path + '\0' + '\0', flags = 0x6050 };
            int code = SHFileOperation(ref op);
            if (code != 0 || op.aborted || File.Exists(path))
                throw new IOException("Windows 未完成回收（可能已取消或檔案使用中），代碼：" + code);
        }
    }

    internal sealed class MainForm : Form
    {
        readonly Button scan = new Button(), clean = new Button(), cancel = new Button();
        readonly ListView files = new ListView();
        readonly Label status = new Label(), capacity = new Label();
        readonly TextBox details = new TextBox();
        readonly ComboBox age = new ComboBox();
        readonly List<CleanupRoot> roots = CleanupEngine.DefaultRoots();
        CancellationTokenSource cancellation;
        DateTime cutoff;
        bool busy, selecting;

        public MainForm()
        {
            Text = "C 槽清理助手"; ClientSize = new Size(1000, 730); MinimumSize = new Size(880, 650);
            Font = new Font("Microsoft JhengHei UI", 10); BackColor = Color.FromArgb(245, 247, 250);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 8 };
            foreach (int height in new[] { 42, 32, 58, 46, 0, 32, 100, 40 })
                layout.RowStyles.Add(height == 0 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.Absolute, height));
            layout.Controls.Add(new Label { Text = "C 槽清理助手", AutoSize = true, Font = new Font(Font.FontFamily, 20, FontStyle.Bold) }, 0, 0);
            capacity.Dock = DockStyle.Fill; layout.Controls.Add(capacity, 0, 1);
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "只掃描已知暫存資料夾與 .dmp 當機報告，保留近期檔案。掃描不會刪除資料。\r\n移到資源回收筒後仍占用 C 槽；確認不需還原，再自行清空才能釋放空間。", ForeColor = Color.FromArgb(75, 85, 99) }, 0, 2);
            var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill };
            toolbar.Controls.Add(new Label { Text = "保留最近", AutoSize = true, Margin = new Padding(0, 9, 4, 0) });
            age.DropDownStyle = ComboBoxStyle.DropDownList; age.Width = 90;
            age.Items.AddRange(new object[] { "7 天", "14 天", "30 天" }); age.SelectedIndex = 0; toolbar.Controls.Add(age);
            age.SelectedIndexChanged += delegate { files.Items.Clear(); details.Text = "保留天數已變更，請重新掃描。"; UpdateSelection(); };
            Configure(scan, "掃描", delegate { StartScan(); }); toolbar.Controls.Add(scan);
            Configure(cancel, "停止", delegate { if (cancellation != null) cancellation.Cancel(); }); cancel.Enabled = false; toolbar.Controls.Add(cancel);
            Configure(clean, "回收勾選項目", delegate { StartCleanup(); }); clean.Enabled = false; toolbar.Controls.Add(clean);
            var all = new Button(); Configure(all, "全選／全不選", delegate {
                if (busy) return;
                bool check = files.CheckedItems.Count != files.Items.Count;
                selecting = true; files.BeginUpdate(); foreach (ListViewItem item in files.Items) item.Checked = check; files.EndUpdate(); selecting = false; UpdateSelection();
            }); toolbar.Controls.Add(all); layout.Controls.Add(toolbar, 0, 3);
            files.Dock = DockStyle.Fill; files.View = View.Details; files.CheckBoxes = true;
            files.FullRowSelect = true; files.GridLines = true; files.HideSelection = false;
            files.Columns.Add("類別", 135); files.Columns.Add("大小", 95); files.Columns.Add("修改時間", 155); files.Columns.Add("完整路徑", 540);
            files.ItemChecked += delegate { if (!selecting && IsHandleCreated && !IsDisposed) BeginInvoke((Action)UpdateSelection); };
            files.SelectedIndexChanged += delegate {
                if (files.SelectedItems.Count > 0) details.Text = ((Candidate)files.SelectedItems[0].Tag).Path;
            };
            layout.Controls.Add(files, 0, 4); status.Dock = DockStyle.Fill; status.Padding = new Padding(0, 5, 0, 0); layout.Controls.Add(status, 0, 5);
            details.Multiline = true; details.ReadOnly = true; details.ScrollBars = ScrollBars.Both; details.Dock = DockStyle.Fill;
            details.Text = "掃描範圍：\r\n" + String.Join("\r\n", roots.Select(r => r.Name + "：" + r.Path)); layout.Controls.Add(details, 0, 6);
            var links = new FlowLayoutPanel { Dock = DockStyle.Fill };
            AddLink(links, "Windows 儲存空間", "ms-settings:storagesense");
            AddLink(links, "系統磁碟清理", "cleanmgr.exe");
            AddLink(links, "開啟資源回收筒", "shell:RecycleBinFolder");
            layout.Controls.Add(links, 0, 7); Controls.Add(layout); RefreshCapacity(); UpdateSelection();
            FormClosing += delegate(object sender, FormClosingEventArgs e) {
                if (busy) { cancellation.Cancel(); e.Cancel = true; status.Text = "正在停止，完成目前項目後即可關閉。"; }
            };
        }
        static void Configure(Button b, string text, EventHandler handler)
        { b.Text = text; b.AutoSize = true; b.Height = 32; b.Click += handler; }
        void AddLink(FlowLayoutPanel p, string text, string target)
        {
            var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, 10, 24, 0) };
            link.LinkClicked += delegate { try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "無法開啟"); } }; p.Controls.Add(link);
        }
        void RefreshCapacity()
        {
            try { var d = new DriveInfo("C"); capacity.Text = "C 槽可用 " + CleanupEngine.Size(d.AvailableFreeSpace) + " ／ 總容量 " + CleanupEngine.Size(d.TotalSize); }
            catch (IOException) { capacity.Text = "無法讀取 C 槽容量"; }
        }
        void SetBusy(bool value)
        {
            busy = value; scan.Enabled = !value; age.Enabled = !value; cancel.Enabled = value;
            files.Enabled = !value; clean.Enabled = !value && files.CheckedItems.Count > 0;
        }
        void UpdateSelection()
        {
            clean.Enabled = !busy && files.CheckedItems.Count > 0;
            if (!busy) status.Text = String.Format("候選 {0} 個；勾選 {1} 個，共 {2}", files.Items.Count,
                files.CheckedItems.Count, CleanupEngine.Size(files.CheckedItems.Cast<ListViewItem>().Sum(i => ((Candidate)i.Tag).Length)));
        }
        async void StartScan()
        {
            if (busy) return;
            files.Items.Clear(); cutoff = DateTime.UtcNow.AddDays(-new[] { 7, 14, 30 }[age.SelectedIndex]);
            cancellation = new CancellationTokenSource(); SetBusy(true); status.Text = "正在掃描，不會修改檔案…";
            try
            {
                ScanResult result = await Task.Run(() => CleanupEngine.Scan(roots, cutoff, cancellation.Token));
                files.BeginUpdate();
                foreach (Candidate c in result.Files.OrderByDescending(c => c.Length))
                {
                    var row = new ListViewItem(c.Root.Name) { Tag = c };
                    row.SubItems.Add(CleanupEngine.Size(c.Length)); row.SubItems.Add(c.Modified.ToLocalTime().ToString("yyyy-MM-dd HH:mm")); row.SubItems.Add(c.Path); files.Items.Add(row);
                }
                files.EndUpdate();
                details.Text = "掃描完成；所有候選項目預設不勾選。略過近期、受保護或無法存取的項目：" + result.Skipped + "\r\n" +
                    String.Join("\r\n", result.Notes) + "\r\n範圍：\r\n" + String.Join("\r\n", roots.Select(r => r.Path));
            }
            catch (OperationCanceledException) { details.Text = "掃描已取消，未修改任何檔案。"; }
            catch (Exception ex) { details.Text = "掃描失敗：" + ex.Message; }
            finally { SetBusy(false); cancellation.Dispose(); cancellation = null; UpdateSelection(); RefreshCapacity(); }
        }
        async void StartCleanup()
        {
            if (busy) return;
            List<Candidate> selected = files.CheckedItems.Cast<ListViewItem>().Select(i => (Candidate)i.Tag).ToList();
            if (selected.Count == 0) return;
            if (MessageBox.Show(this, "將處理勾選的 " + selected.Count + " 個檔案（" + CleanupEngine.Size(selected.Sum(c => c.Length)) + "）。\r\n\r\n請先關閉其他應用程式。檔案將要求 Windows 移到資源回收筒。\r\n若 Windows 提示無法回收、要永久刪除，請選擇取消。\r\n回收不會立即增加 C 槽可用空間；本工具不會清空資源回收筒。\r\n\r\n是否繼續？", "確認回收項目", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.OK) return;
            cancellation = new CancellationTokenSource(); SetBusy(true); status.Text = "正在回收；可停止後續項目…";
            IntPtr owner = Handle; var completed = new List<Candidate>(); var log = new List<string>();
            var done = new TaskCompletionSource<bool>();
            var thread = new Thread(delegate() {
                try {
                    foreach (Candidate c in selected)
                    {
                        if (cancellation.IsCancellationRequested) break;
                        try {
                            if (!CleanupEngine.Validate(c, roots, cutoff)) { log.Add("略過（已變動或不符合規則）：" + c.Path); continue; }
                            Recycle.Send(c.Path, owner); completed.Add(c); log.Add("已處理：" + c.Path);
                        } catch (Exception ex) { log.Add("略過：" + c.Path + " — " + ex.Message); }
                    }
                    done.SetResult(true);
                } catch (Exception ex) { done.SetException(ex); }
            });
            thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start();
            try { await done.Task; }
            catch (Exception ex) { log.Add("處理停止：" + ex.Message); }
            finally {
                foreach (ListViewItem item in files.Items.Cast<ListViewItem>().ToArray()) if (completed.Contains((Candidate)item.Tag)) files.Items.Remove(item);
                SetBusy(false); cancellation.Dispose(); cancellation = null; UpdateSelection(); RefreshCapacity();
                details.Text = "已處理 " + completed.Count + " 個；未處理 " + (selected.Count - completed.Count) + " 個。\r\n確認不需還原後，可自行開啟資源回收筒清空。\r\n" + String.Join("\r\n", log);
            }
        }
    }
}
