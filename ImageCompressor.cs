using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ImageMagick;

public sealed class CompressionResult
{
    public string Path, Status;
    public long Before, After;
}

public static class ImageCompressionEngine
{
    public const string BackupName = "_圖片壓縮備份";
    public static bool IsCandidate(string path)
    {
        string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp";
    }
    public static string NormalizeRoot(string root)
    {
        root = System.IO.Path.GetFullPath(root);
        if (!Directory.Exists(root)) throw new IOException("資料夾不存在。");
        for (var d = new DirectoryInfo(root); d != null; d = d.Parent)
        {
            if (d.Name.Equals(BackupName, StringComparison.OrdinalIgnoreCase)) throw new IOException("請選擇網站資料夾，不能選擇備份目錄。");
            if ((d.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("請選擇實際資料夾，不支援連結目錄。");
        }
        return root.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
    }
    public static List<string> Scan(string root, CancellationToken token, Action<string> warning)
    {
        root = NormalizeRoot(root);
        var files = new List<string>();
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            string directory = pending.Pop();
            try
            {
                foreach (string path in Directory.GetFiles(directory))
                {
                    token.ThrowIfCancellationRequested();
                    if (IsCandidate(path) && !System.IO.Path.GetFileName(path).StartsWith(".image-compressor-", StringComparison.OrdinalIgnoreCase) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0) files.Add(path);
                }
                foreach (string path in Directory.GetDirectories(directory))
                    if (!System.IO.Path.GetFileName(path).Equals(BackupName, StringComparison.OrdinalIgnoreCase) &&
                        (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0) pending.Push(path);
            }
            catch (UnauthorizedAccessException ex) { warning(directory + "：" + ex.Message); }
            catch (IOException ex) { warning(directory + "：" + ex.Message); }
        }
        return files;
    }
    static string Hash(string path)
    {
        using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(stream));
    }
    public static void PrepareBackup(string root, string backup)
    {
        string basePath = System.IO.Path.Combine(NormalizeRoot(root), BackupName) + System.IO.Path.DirectorySeparatorChar;
        backup = System.IO.Path.GetFullPath(backup);
        if (!backup.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)) throw new IOException("備份必須放在網站的備份目錄內。");
        for (var d = new DirectoryInfo(backup); d != null; d = d.Parent)
            if (d.Exists && (d.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("備份路徑包含連結目錄，未修改圖片。");
        Directory.CreateDirectory(backup);
    }
    public static CompressionResult Compress(string root, string path, string backup, bool lossy, uint quality, CancellationToken token)
    {
        root = NormalizeRoot(root);
        path = System.IO.Path.GetFullPath(path);
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !IsCandidate(path)) throw new IOException("圖片不在選取的資料夾內。");
        string relative = path.Substring(root.Length);
        var result = new CompressionResult { Path = relative, Status = "保留原檔" };
        string temp = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), ".image-compressor-" + Guid.NewGuid().ToString("N") + System.IO.Path.GetExtension(path));
        try
        {
            for (var d = new DirectoryInfo(System.IO.Path.GetDirectoryName(path)); d != null; d = d.Parent)
                if ((d.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("圖片路徑包含連結目錄，未修改。");
            if ((File.GetAttributes(path) & (FileAttributes.ReadOnly | FileAttributes.ReparsePoint)) != 0) throw new IOException("唯讀或連結檔案，未修改。");
            string originalHash;
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                result.Before = result.After = input.Length;
                using (var sha = SHA256.Create()) originalHash = Convert.ToBase64String(sha.ComputeHash(input));
                input.Position = 0;
                using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write)) input.CopyTo(output);
            }
            token.ThrowIfCancellationRequested();
            using (var images = new MagickImageCollection(temp))
            {
                if (images.Count != 1) { result.Status = "跳過動畫／多頁圖片"; return result; }
                var original = (MagickImage)images[0];
                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                MagickFormat expected = ext == ".png" ? MagickFormat.Png : ext == ".webp" ? MagickFormat.WebP : MagickFormat.Jpeg;
                if (original.Format != expected) { result.Status = "跳過：格式與副檔名不符"; return result; }
                if (original.Depth > 8) { result.Status = "跳過：高位元深度圖片"; return result; }
                if (expected == MagickFormat.WebP)
                {
                    using (var candidate = new MagickImage(temp))
                    {
                        candidate.Settings.SetDefine(MagickFormat.WebP, "lossless", lossy ? "false" : "true");
                        candidate.Settings.SetDefine(MagickFormat.WebP, "method", "6");
                        candidate.Quality = lossy ? quality : 100;
                        candidate.Write(temp);
                    }
                }
                else if (lossy && expected == MagickFormat.Jpeg)
                {
                    var optimizer = new ImageMagick.ImageOptimizers.JpegOptimizer { OptimalCompression = true };
                    optimizer.Compress(temp, quality);
                }
                else
                {
                    var optimizer = new ImageOptimizer { OptimalCompression = true };
                    optimizer.LosslessCompress(temp);
                }
                using (var check = new MagickImageCollection(temp))
                {
                    var verified = (MagickImage)check[0];
                    if (check.Count != 1 || verified.Width != original.Width || verified.Height != original.Height || verified.Format != original.Format)
                        throw new IOException("驗證未通過：尺寸或格式改變，保留原檔。");
                    if (verified.Orientation != original.Orientation || verified.HasAlpha != original.HasAlpha)
                        throw new IOException("驗證未通過：方向或透明屬性改變，保留原檔。");
                    // Require identical decoded pixels in lossless mode, including alpha.
                    if ((!lossy || expected == MagickFormat.Png) && original.Compare(verified, ErrorMetric.Absolute) != 0)
                        throw new IOException("驗證未通過：無損像素檢查不一致，保留原檔。");
                    foreach (var name in original.ProfileNames)
                    {
                        var a = original.GetProfile(name); var b = verified.GetProfile(name);
                        if (b == null || !a.ToByteArray().SequenceEqual(b.ToByteArray()))
                        {
                            result.Status = "跳過：無法完整保留描述資料";
                            return result;
                        }
                    }
                }
            }
            long size = new FileInfo(temp).Length;
            if (size >= result.Before) { result.Status = "已最佳化，保留原檔"; return result; }
            token.ThrowIfCancellationRequested();
            if (Hash(path) != originalHash) throw new IOException("原檔在處理期間有變動，未替換。");
            string saved = System.IO.Path.Combine(backup, relative);
            PrepareBackup(root, System.IO.Path.GetDirectoryName(saved));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(saved));
            if (File.Exists(saved)) throw new IOException("備份已存在，未替換。");
            // Atomic replacement: original bytes become the backup as the compressed file takes their place.
            File.Replace(temp, path, saved);
            result.After = size; result.Status = "已壓縮";
            return result;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}

public sealed class ImageCompressor : Form
{
    readonly TextBox folder = new TextBox { ReadOnly = true, Dock = DockStyle.Fill };
    readonly Button choose = new Button { Text = "選取資料夾", AutoSize = true };
    readonly Button run = new Button { Text = "開始批次壓縮", AutoSize = true };
    readonly Button stop = new Button { Text = "停止", AutoSize = true, Enabled = false };
    readonly Button openBackup = new Button { Text = "開啟本次備份", AutoSize = true, Enabled = false };
    readonly CheckBox lossy = new CheckBox { Text = "允許 JPG／WebP 輕微畫質損失（PNG 仍為無損）", AutoSize = true };
    readonly NumericUpDown quality = new NumericUpDown { Minimum = 50, Maximum = 95, Value = 85, Width = 65, Enabled = false };
    readonly Label status = new Label { Text = "請選取要處理的網站資料夾。", AutoSize = true };
    readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill };
    readonly ListView results = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
    CancellationTokenSource cancellation;
    string backupPath;
    ImageCompressor()
    {
        Text = "網站圖片減肥"; ClientSize = new Size(960, 650); MinimumSize = new Size(800, 540);
        Font = new Font("Microsoft JhengHei UI", 10); AutoScaleMode = AutoScaleMode.Dpi;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 8; i++) layout.RowStyles.Add(new RowStyle(i == 6 ? SizeType.Percent : SizeType.AutoSize, i == 6 ? 100 : 0));
        layout.Controls.Add(new Label { Text = "網站圖片減肥", Font = new Font(Font.FontFamily, 20, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 0, 0, 12) }, 0, 0);
        var selector = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        selector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); selector.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        selector.Controls.Add(folder, 0, 0); selector.Controls.Add(choose, 1, 0); layout.Controls.Add(selector, 0, 1);
        layout.Controls.Add(new Label { Text = "遞迴處理 JPG、PNG、WebP；尺寸、檔名與格式不變。動畫及高位元深度圖片會跳過。", AutoSize = true, Margin = new Padding(0, 10, 0, 8) }, 0, 2);
        var options = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        options.Controls.Add(lossy); options.Controls.Add(new Label { Text = "品質", AutoSize = true, Padding = new Padding(0, 4, 0, 0) }); options.Controls.Add(quality);
        layout.Controls.Add(options, 0, 3);
        layout.Controls.Add(new Label { Text = "預設無損。只有確實變小才替換；原圖自動存到所選資料夾內的「_圖片壓縮備份」。", AutoSize = true, Margin = new Padding(0, 4, 0, 10) }, 0, 4);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        buttons.Controls.Add(run); buttons.Controls.Add(stop); buttons.Controls.Add(openBackup); layout.Controls.Add(buttons, 0, 5);
        results.Columns.Add("圖片／訊息", 430); results.Columns.Add("原始容量", 110); results.Columns.Add("處理後", 110); results.Columns.Add("結果", 235); layout.Controls.Add(results, 0, 6);
        var bottom = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 }; bottom.Controls.Add(progress); bottom.Controls.Add(status); layout.Controls.Add(bottom, 0, 7);
        Controls.Add(layout);
        choose.Click += delegate { using (var dialog = new FolderBrowserDialog { Description = "選取網站資料夾（包含所有子資料夾）", ShowNewFolderButton = false }) if (dialog.ShowDialog() == DialogResult.OK) folder.Text = dialog.SelectedPath; };
        lossy.CheckedChanged += delegate { quality.Enabled = lossy.Checked; };
        run.Click += async delegate { await Run(); };
        stop.Click += delegate { if (cancellation != null) { cancellation.Cancel(); stop.Enabled = false; status.Text = "正在停止，等待目前圖片處理完成…"; } };
        openBackup.Click += delegate { if (Directory.Exists(backupPath)) Process.Start("explorer.exe", "\"" + backupPath + "\""); };
        FormClosing += delegate(object sender, FormClosingEventArgs e) { if (cancellation != null) { cancellation.Cancel(); e.Cancel = true; status.Text = "正在停止，結束後即可關閉。"; } };
    }
    static string SizeText(long size) { return size < 1024 ? size + " B" : size < 1048576 ? (size / 1024.0).ToString("N1") + " KB" : (size / 1048576.0).ToString("N2") + " MB"; }
    static string Csv(string value) { return "\"" + value.Replace("\"", "\"\"") + "\""; }
    async Task Run()
    {
        string root;
        try { root = ImageCompressionEngine.NormalizeRoot(folder.Text); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "請選取資料夾"); return; }
        bool allowLoss = lossy.Checked; uint q = (uint)quality.Value;
        cancellation = new CancellationTokenSource(); var token = cancellation.Token;
        run.Enabled = choose.Enabled = lossy.Enabled = quality.Enabled = openBackup.Enabled = false; stop.Enabled = true;
        results.Items.Clear(); progress.Style = ProgressBarStyle.Marquee; status.Text = "正在掃描圖片…";
        int completed = 0, changed = 0, failed = 0, warnings = 0; long before = 0, after = 0;
        var ui = new Progress<CompressionResult>(r => {
            results.Items.Add(new ListViewItem(new[] { r.Path, SizeText(r.Before), SizeText(r.After), r.Status }));
            if (progress.Style != ProgressBarStyle.Marquee) progress.Value = Math.Min(progress.Maximum, ++completed);
            status.Text = "處理 " + completed + " / " + progress.Maximum + "；" + r.Path;
        });
        var scanUi = new Progress<string>(s => { warnings++; results.Items.Add(new ListViewItem(new[] { s, "", "", "掃描略過" })); });
        bool stopped = false;
        try
        {
            var files = await Task.Run(() => ImageCompressionEngine.Scan(root, token, s => ((IProgress<string>)scanUi).Report(s)));
            progress.Style = ProgressBarStyle.Blocks; progress.Value = 0; progress.Maximum = Math.Max(1, files.Count);
            if (files.Count == 0) { status.Text = "沒有找到支援的圖片。掃描略過 " + warnings + " 處。"; return; }
            backupPath = Path.Combine(root, ImageCompressionEngine.BackupName, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
            await Task.Run(() => {
                ImageCompressionEngine.PrepareBackup(root, backupPath);
                using (var log = new StreamWriter(Path.Combine(backupPath, "處理報告.csv"), false, new UTF8Encoding(true)))
                {
                    log.WriteLine("圖片,原始位元組,處理後位元組,結果"); log.AutoFlush = true;
                    foreach (string path in files)
                    {
                        if (token.IsCancellationRequested) { stopped = true; break; }
                        CompressionResult r;
                        try { r = ImageCompressionEngine.Compress(root, path, backupPath, allowLoss, q, token); }
                        catch (OperationCanceledException) { stopped = true; break; }
                        catch (Exception ex) { failed++; r = new CompressionResult { Path = path.Substring(root.Length), Status = "失敗：" + ex.Message }; }
                        before += r.Before; after += r.After; if (r.Status == "已壓縮") changed++;
                        log.WriteLine(Csv(r.Path) + "," + r.Before + "," + r.After + "," + Csv(r.Status));
                        ((IProgress<CompressionResult>)ui).Report(r);
                    }
                    log.WriteLine(Csv(stopped ? "已停止" : "已完成") + ",,," + Csv("節省 " + (before - after) + " 位元組；失敗 " + failed));
                }
            });
            status.Text = (stopped ? "已停止" : "完成") + "：壓縮 " + changed + " 張，節省 " + SizeText(before - after) + "；失敗 " + failed + " 張；掃描略過 " + warnings + " 處。";
        }
        catch (OperationCanceledException) { status.Text = "已停止掃描，未修改圖片。"; }
        catch (Exception ex) { status.Text = "處理中斷：" + ex.Message; MessageBox.Show(ex.Message, "處理中斷"); }
        finally
        {
            progress.Style = ProgressBarStyle.Blocks;
            cancellation.Dispose(); cancellation = null; run.Enabled = choose.Enabled = lossy.Enabled = true;
            quality.Enabled = lossy.Checked; stop.Enabled = false; openBackup.Enabled = Directory.Exists(backupPath);
        }
    }
    [STAThread] public static void Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new ImageCompressor());
    }
}
