using System;
using System.Drawing;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
class CheckerTests {
 static int checks;
 static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
 static void Reject(string text) { try { GlobalUrlChecker.ValidateUrl(text); } catch (ArgumentException) { checks++; return; } throw new Exception("Accepted invalid URL: " + text); }
 [STAThread] static void Main(string[] args) { try { Run(args); } catch (Exception ex) { System.IO.File.WriteAllText(".build/global-url-checker/test-error.txt", ex.GetType().FullName + " " + ex.Message + "\n" + ex.StackTrace); Environment.ExitCode=1; } } static void Run(string[] args) {
  ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
  Application.EnableVisualStyles();
  Assert(GlobalUrlChecker.ValidateUrl("example.com/a?x=1").AbsoluteUri == "https://example.com/a?x=1", "Normalize URL");
  foreach (string text in new[] { "", "ftp://example.com", "http://localhost", "http://127.0.0.1", "http://192.168.1.1", "http://[::1]", "https://u:p@example.com" }) Reject(text);
  var js = new JavaScriptSerializer();
  foreach (var item in new[] {
   new[] { "{\"status\":\"finished\",\"statusCode\":200}", "成功" },
   new[] { "{\"status\":\"finished\",\"statusCode\":301}", "重新導向" },
   new[] { "{\"status\":\"finished\",\"statusCode\":503}", "HTTP 錯誤" },
   new[] { "{\"status\":\"failed\",\"rawOutput\":\"Connection timed out\"}", "逾時" },
   new[] { "{\"status\":\"failed\",\"rawOutput\":\"Connection refused\"}", "連線失敗" },
   new[] { "{\"status\":\"failed\",\"failureSource\":\"resolver\"}", "DNS 解析失敗" },
   new[] { "{\"status\":\"failed\",\"failureSource\":\"internal\",\"rawOutput\":\"timeout\"}", "節點／服務錯誤" },
   new[] { "{\"status\":\"offline\"}", "節點離線" } }) {
    Assert(GlobalUrlChecker.Classify(js.Deserialize<System.Collections.Generic.Dictionary<string,object>>(item[0])) == item[1], "Result classification " + item[1]);
  }
  string body = js.Serialize(GlobalUrlChecker.RequestBody(new Uri("https://example.com/"), GlobalUrlChecker.Cities[1], 5, "GET"));
  Assert(!body.Contains("query"), "Empty query must be omitted for API validation");
  body = js.Serialize(GlobalUrlChecker.RequestBody(new Uri("http://example.com:8080/a?x=1&y=2"), GlobalUrlChecker.Cities[1], 5, "GET"));
  Assert(body.Contains("8080") && GlobalUrlChecker.Str(GlobalUrlChecker.Map(GlobalUrlChecker.Get(GlobalUrlChecker.Map(GlobalUrlChecker.Get(js.Deserialize<System.Collections.Generic.Dictionary<string,object>>(body), "measurementOptions")), "request")), "query") == "x=1&y=2" && body.Contains("/a"), "Preserve port/path/query");
  var parsed = js.Deserialize<System.Collections.Generic.Dictionary<string,object>>("{\"results\":[{\"result\":{\"status\":\"finished\"}}]}");
  Assert(GlobalUrlChecker.Get(parsed, "results") is System.Collections.IEnumerable, "API result array deserialization");
  var type = typeof(GlobalUrlChecker).GetNestedType("CheckerForm", BindingFlags.NonPublic);
  using (var form = (Form)Activator.CreateInstance(type, true)) {
    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
    var cities = (CheckedListBox)type.GetField("cities", flags).GetValue(form);
    var grid = (DataGridView)type.GetField("grid", flags).GetValue(form);
    Assert(cities.Items.Count == 22 && grid.Columns.Count == 10, "City and result controls");
    form.Show(); Application.DoEvents();
    using (var image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(0,0,image.Width,image.Height)); image.Save(".build/global-url-checker/preview.png"); }
    if (args.Length > 0 && args[0] == "--live") {
     for (int i=0;i<cities.Items.Count;i++) cities.SetItemChecked(i,i==1);
     Task run = (Task)type.GetMethod("Run",flags).Invoke(form,null);
     var end = DateTime.UtcNow.AddSeconds(90);
     while (!run.IsCompleted && DateTime.UtcNow<end) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
     Assert(run.IsCompleted, "Live UI run completed"); run.GetAwaiter().GetResult();
     string status = Convert.ToString(grid.Rows[0].Cells[2].Value);
     Console.WriteLine("Live Tokyo status: " + status + "; HTTP " + grid.Rows[0].Cells[3].Value + "; total " + grid.Rows[0].Cells[4].Value + " ms");
     Assert(status == "成功", "Real Tokyo HTTP test: " + grid.Rows[0].Tag);
     var url = (TextBox)type.GetField("url",flags).GetValue(form); url.Text="https://example.com:81/";
     var timeout = (NumericUpDown)type.GetField("timeout",flags).GetValue(form); timeout.Value=5;
     run = (Task)type.GetMethod("Run",flags).Invoke(form,null); end=DateTime.UtcNow.AddSeconds(70);
     while(!run.IsCompleted && DateTime.UtcNow<end) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
     Assert(run.IsCompleted, "Live connection failure run completed"); run.GetAwaiter().GetResult();
     status = Convert.ToString(grid.Rows[0].Cells[2].Value); Console.WriteLine("Live unreachable port status: " + status);
     Assert(status == "逾時" || status == "連線失敗", "Real unreachable port: " + grid.Rows[0].Tag);
     run = (Task)type.GetMethod("Run",flags).Invoke(form,null);
     ((System.Threading.CancellationTokenSource)type.GetField("running",flags).GetValue(form)).Cancel();
     while(!run.IsCompleted) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
     run.GetAwaiter().GetResult();
     Assert(Convert.ToString(grid.Rows[0].Cells[2].Value)=="已取消", "User cancellation");
    }
    form.Close();
  }
  Console.WriteLine("PASS " + checks + " checks");
 }
}



