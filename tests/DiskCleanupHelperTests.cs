using System;
using System.IO;
using System.Linq;
using System.Threading;
using DiskCleanupHelper;

internal static class DiskCleanupHelperTests
{
    static void Check(bool value, string name)
    { if (!value) throw new Exception("FAIL " + name); Console.WriteLine("PASS " + name); }
    static void Make(string path, DateTime date)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, "fixture");
        File.SetCreationTimeUtc(path, date); File.SetLastWriteTimeUtc(path, date);
    }
    [STAThread] static void Main(string[] args)
    {
        System.Windows.Forms.Application.EnableVisualStyles();
        System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
        string dir = Path.GetFullPath(args[0]);
        var root = new CleanupRoot("fixture", Path.Combine(dir, "allowed"), false);
        var dumps = new CleanupRoot("dumps", Path.Combine(dir, "dumps"), true);
        DateTime cutoff = DateTime.UtcNow.AddDays(-7), old = cutoff.AddDays(-3);
        Make(Path.Combine(root.Path, "old.tmp"), old);
        Make(Path.Combine(root.Path, "nested", "old.log"), old);
        Make(Path.Combine(root.Path, "recent.tmp"), DateTime.UtcNow);
        Make(Path.Combine(root.Path, "newly-created.tmp"), old);
        File.SetCreationTimeUtc(Path.Combine(root.Path, "newly-created.tmp"), DateTime.UtcNow);
        Make(Path.Combine(root.Path, "readonly.tmp"), old);
        File.SetAttributes(Path.Combine(root.Path, "readonly.tmp"), FileAttributes.ReadOnly);
        Make(Path.Combine(dumps.Path, "crash.dmp"), old); Make(Path.Combine(dumps.Path, "keep.txt"), old);
        Make(Path.Combine(dir, "outside", "private.log"), old);
        var allowed = new[] { root, dumps };
        var result = CleanupEngine.Scan(allowed, cutoff, CancellationToken.None);
        Check(result.Files.Count == 3, "only old temp files and dmp candidates; preserve recent, newly-created and readonly files");
        Check(!result.Files.Any(c => c.Path.Contains("junction")), "junction is not traversed");
        var c1 = result.Files.First(c => c.Path.EndsWith("old.tmp"));
        Check(CleanupEngine.Validate(c1, allowed, cutoff), "unchanged allowlisted candidate accepted");
        File.AppendAllText(c1.Path, "changed");
        Check(!CleanupEngine.Validate(c1, allowed, cutoff), "changed file rejected");
        var outside = new Candidate { Root = root, Path = Path.Combine(dir, "outside", "private.log") };
        Check(!CleanupEngine.Validate(outside, allowed, cutoff), "outside allowlist rejected");
        Check(!CleanupEngine.SafePath(root, root.Path + "-sibling\\file", true), "prefix collision rejected");
        var nested = result.Files.First(c => c.Path.EndsWith("old.log"));
        using (var locked = new FileStream(nested.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            bool rejected = false;
            try { rejected = !CleanupEngine.Validate(nested, allowed, cutoff); } catch (IOException) { rejected = true; }
            Check(rejected, "locked file rejected without force");
        }
        Check(!CleanupEngine.Validate(nested, new[] { dumps }, cutoff), "unapproved root rejected");
        File.SetAttributes(nested.Path, FileAttributes.ReadOnly);
        Check(!CleanupEngine.Validate(nested, allowed, cutoff), "newly protected candidate rejected");
        File.SetAttributes(nested.Path, FileAttributes.Normal);
        var cancel = new CancellationTokenSource(); cancel.Cancel();
        bool cancelled = false;
        try { CleanupEngine.Scan(allowed, cutoff, cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled, "scan cancellation");
        Check(File.Exists(Path.Combine(dir, "outside", "private.log")), "outside file intact");
        Check(File.Exists(c1.Path), "scan and validation do not delete files");
        // Exercise shell recycling ONLY with this disposable fixture, never machine caches.
        Recycle.Send(nested.Path, IntPtr.Zero);
        Check(!File.Exists(nested.Path), "workspace fixture sent through Windows recycle operation");
        using (var form = new MainForm())
        {
            form.Show(); System.Windows.Forms.Application.DoEvents();
            Check(form.Visible, "window opens without starting scan");
            using (var bitmap = new System.Drawing.Bitmap(form.Width, form.Height))
            { form.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, form.Width, form.Height)); bitmap.Save(Path.Combine(dir, "ui.png")); }
            form.Close();
        }
        Console.WriteLine("All checks passed. No user cache directories scanned or cleaned.");
    }
}
