using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using ImageMagick;

class ImageCompressionSmoke
{
    static int assertions;
    static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
    [STAThread] static int Main(string[] args)
    {
        try { Run(args); return 0; }
        catch (Exception ex) { File.WriteAllText(Path.Combine(args[0], "failure.txt"), ex.GetType().FullName + ": " + ex.Message + Environment.NewLine + ex.StackTrace); return 1; }
    }
    static void Run(string[] args)
    {
        string root = Path.Combine(Path.GetFullPath(args[0]), "site-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "images"));
        string backup = Path.Combine(root, ImageCompressionEngine.BackupName, "test");
        Directory.CreateDirectory(backup);
        File.WriteAllText(Path.Combine(root, "index.html"), "<img src='images/photo.jpg'>");
        File.WriteAllText(Path.Combine(root, "script.js"), "alert('untouched');");
        string png = Path.Combine(root, "images", "透明圖片.png");
        string jpg = Path.Combine(root, "images", "photo.jpg");
        using (var bitmap = new Bitmap(320, 180))
        {
            using (var g = Graphics.FromImage(bitmap)) { g.Clear(Color.Transparent); g.FillRectangle(Brushes.CornflowerBlue, 20, 20, 200, 100); }
            bitmap.Save(png, ImageFormat.Png);
            bitmap.Save(jpg, ImageFormat.Jpeg);
        }
        // A valid PNG with extra trailing bytes lets us exercise the smaller-only and backup path.
        using (var stream = new FileStream(png, FileMode.Append)) stream.Write(new byte[50000], 0, 50000);
        using (var image = new MagickImage(jpg))
        {
            var exif = new ExifProfile(); exif.SetValue(ExifTag.Orientation, (ushort)6);
            image.SetProfile(exif); image.Write(jpg);
        }
        string webp = Path.Combine(root, "images", "image.webp");
        using (var image = new MagickImage(png)) { image.Format = MagickFormat.WebP; image.Quality = 100; image.Write(webp); }
        string animated = Path.Combine(root, "animated.webp");
        using (var images = new MagickImageCollection())
        {
            images.Add(new MagickImage(MagickColors.Red, 20, 20)); images.Add(new MagickImage(MagickColors.Blue, 20, 20));
            images.Write(animated, MagickFormat.WebP);
        }
        string bad = Path.Combine(root, "bad.jpg"); File.WriteAllText(bad, "not an image");
        string mismatch = Path.Combine(root, "wrong.jpg"); File.Copy(png, mismatch);
        File.Copy(png, Path.Combine(backup, "excluded.png"));
        var files = ImageCompressionEngine.Scan(root, CancellationToken.None, s => { throw new Exception(s); });
        Check(files.Count == 6, "Scan candidates and exclude backups");
        byte[] original = File.ReadAllBytes(png);
        var result = ImageCompressionEngine.Compress(root, png, backup, false, 85, CancellationToken.None);
        Console.WriteLine("PNG: " + result.Status + " " + result.Before + " -> " + result.After);
        Check(result.Status == "已壓縮" && result.After < result.Before, "PNG compression");
        Check(original.SequenceEqual(File.ReadAllBytes(Path.Combine(backup, "images", "透明圖片.png"))), "Backup must match original bytes");
        using (var image = new MagickImage(png)) Check(image.Width == 320 && image.Height == 180 && image.HasAlpha, "Size and alpha unchanged");
        foreach (string path in new[] { jpg, webp })
        {
            result = ImageCompressionEngine.Compress(root, path, backup, false, 85, CancellationToken.None);
            Console.WriteLine(Path.GetExtension(path) + " lossless: " + result.Status);
            Check(result.After <= result.Before, "Never grow files");
        }
        result = ImageCompressionEngine.Compress(root, animated, backup, false, 85, CancellationToken.None);
        Check(result.Status.Contains("動畫"), "Skip animation");
        result = ImageCompressionEngine.Compress(root, mismatch, backup, false, 85, CancellationToken.None);
        Check(result.Status.Contains("不符"), "Skip mismatched format");
        bool badFailed = false;
        try { ImageCompressionEngine.Compress(root, bad, backup, false, 85, CancellationToken.None); } catch { badFailed = true; }
        Check(badFailed && File.ReadAllText(bad) == "not an image", "Invalid image unchanged");
        File.SetAttributes(jpg, FileAttributes.ReadOnly);
        bool readOnlyFailed = false;
        try { ImageCompressionEngine.Compress(root, jpg, backup, false, 85, CancellationToken.None); } catch (IOException) { readOnlyFailed = true; }
        File.SetAttributes(jpg, FileAttributes.Normal); Check(readOnlyFailed, "Skip readonly files");
        var cancel = new CancellationTokenSource(); cancel.Cancel();
        bool canceled = false;
        try { ImageCompressionEngine.Compress(root, webp, backup, false, 85, cancel.Token); } catch (OperationCanceledException) { canceled = true; }
        Check(canceled, "Cancellation");
        Check(!Directory.GetFiles(root, ".image-compressor-*", SearchOption.AllDirectories).Any(), "No abandoned temp files");
        Check(File.ReadAllText(Path.Combine(root, "index.html")) == "<img src='images/photo.jpg'>" && File.ReadAllText(Path.Combine(root, "script.js")) == "alert('untouched');", "Other files unchanged");
        bool backupRejected = false; try { ImageCompressionEngine.NormalizeRoot(backup); } catch (IOException) { backupRejected = true; }
        Check(backupRejected, "Reject backup directory selection");
        string lossyBackup = Path.Combine(root, ImageCompressionEngine.BackupName, "lossy");
        foreach (string path in new[] { jpg, webp })
        {
            result = ImageCompressionEngine.Compress(root, path, lossyBackup, true, 75, CancellationToken.None);
            Check(result.After <= result.Before, "Lossy never grows file");
            using (var image = new MagickImage(path)) Check(image.Width == 320 && image.Height == 180, "Lossy keeps dimensions");
        }
        Application.EnableVisualStyles();
        using (var form = (Form)Activator.CreateInstance(typeof(ImageCompressor), true))
        {
            form.Show(); Application.DoEvents();
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(args[0], "ui.png")); }
            form.Close();
        }
        Console.WriteLine("PASS: " + assertions + " assertions. Fixtures: " + root);
    }
}
