using System;
using System.Linq;
using System.Threading.Tasks;
using HttpProtocolChecker;
class ProtocolTests
{
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Main(string[] args)
    {
        Assert(Detector.ParseUrl(" example.com/a?q=1#fragment ").AbsoluteUri == "https://example.com/a?q=1", "normalize");
        foreach (string bad in new[] { "", "ftp://example.com", "https://user:pass@example.com", "https://example.com\n--insecure" })
        {
            bool rejected = false;
            try { Detector.ParseUrl(bad); } catch (ArgumentException) { rejected = true; }
            Assert(rejected, "reject " + bad);
        }
        Assert(Detector.Classify("2", 0, "1.1|200|0.1", "").State == "本次未協商到", "fallback is not HTTP2");
        Assert(Detector.Classify("3", 28, "0|000|20", "timeout").State == "未確認", "timeout is inconclusive");
        Assert(Detector.Classify("2", 0, "2|403|0.2", "").State.StartsWith("支援"), "403 confirms HTTP2");
        Assert(Detector.Classify("3", 0, "3|200|0.2", "").State.StartsWith("支援"), "HTTP3 confirmed");
        Assert(Detector.Probe("missing.exe", new Uri("http://example.com"), "3").Result.State == "不適用", "HTTP3 needs HTTPS");
        Console.WriteLine("PASS: normalization, invalid input, fallback, timeout, error HTTP status, HTTP3, plain HTTP");
        if (args.Length > 0)
        {
            var results = Task.WhenAll(new[] { "1.1", "2", "3" }.Select(p => Detector.Probe(args[0], new Uri("https://www.cloudflare.com"), p))).Result;
            foreach (var r in results) Console.WriteLine("HTTP/" + r.Protocol + " " + r.State + " actual=" + r.Actual + " status=" + r.Status + " seconds=" + r.Seconds + " " + r.Detail);
            Assert(results.Any(r => r.State.StartsWith("支援")), "live connection");
        }
    }
}
