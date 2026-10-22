using System;
using System.IO;
using SslCertificateChecker;
class Tests
{
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static void Main(string[] args)
    {
        string dir = args[0];
        foreach (string name in new[] { "cert.cer", "chain.pem", "cert.pfx" })
        {
            var certs = CertificateReader.Read(Path.Combine(dir, name), "test-password");
            Check(certs.Count == (name == "chain.pem" ? 2 : 1), name);
            foreach (var cert in certs) cert.Reset();
        }
        foreach (string name in new[] { "key.pem", "broken.cer", "cert.pfx" })
        {
            bool failed = false;
            try { CertificateReader.Read(Path.Combine(dir, name), "wrong"); } catch { failed = true; }
            Check(failed, "reject " + name);
        }
        DateTime now = new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);
        Check(CertificateReader.Status(now.AddDays(-1), now.AddDays(90), now) == "有效", "valid");
        Check(CertificateReader.Status(now.AddDays(-1), now.AddDays(30), now) == "30 天內到期", "30 day boundary");
        Check(CertificateReader.Status(now.AddDays(-90), now.AddSeconds(-1), now) == "已過期", "expired");
        Check(CertificateReader.Status(now.AddDays(1), now.AddDays(90), now) == "尚未生效", "not yet valid");
        Check(CertificateReader.Taiwan(now) == "2026-10-22 08:00:00 UTC+08:00", "Taiwan time");
        using (var form = new MainForm()) { form.Show(); System.Windows.Forms.Application.DoEvents(); Check(form.Visible, "form opens"); form.Close(); }
    }
}
