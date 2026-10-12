using System;
using System.Drawing;
using System.Windows.Forms;
using NetworkHelper;
internal static class Smoke {
 [STAThread] static void Main(string[] args) {
 foreach(var v in new[]{"example.com","127.0.0.1","::1"}) if(!MainForm.ValidHost(v)) throw new Exception("Rejected host");
 foreach(var v in new[]{"","-help","https://example.com","x & whoami","foo\r\nbar","a b"}) if(MainForm.ValidHost(v)) throw new Exception("Unsafe host");
 var r=MainForm.Execute("ipconfig.exe","/all",10000); if(r.Item1!=0 || r.Item2.Length==0) throw new Exception("Read failed");
 var f=MainForm.Execute("netsh.exe","definitely-invalid-command",10000); if(f.Item1==0) throw new Exception("Failure status lost");
 bool timeout=false; try {MainForm.Execute("ping.exe","-n 10 127.0.0.1",1);} catch(TimeoutException){timeout=true;} if(!timeout) throw new Exception("Timeout failed");
 Application.EnableVisualStyles();
 using(var form=new MainForm()) {form.Show(); Application.DoEvents(); using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(Point.Empty,form.Size));bmp.Save(args[0]);} form.ClientSize=new Size(800,580);Application.DoEvents(); using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(Point.Empty,form.Size));bmp.Save(args[1]);} form.Close();}
 Console.WriteLine("PASS: validation, read-only command, failure status, timeout, form rendering.");
 }
}
