using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
class Smoke
{
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            VideoEngine.Tools = Path.GetFullPath(args[0]);
            string dir = Path.GetFullPath(args[1]); string input = Path.Combine(dir,"來源 測試.mp4");
            string original = Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(input)));
            foreach(bool h264 in new bool[] {false,true})
            {
                string output = Path.Combine(dir,h264 ? "h264.mp4" : "h265.mp4"); int progress = 0;
                new VideoEngine().Compress(input,output,0,h264,false,delegate(int p){ progress=p; });
                Assert(progress==100,"Progress not complete");
                Assert(Math.Abs(VideoEngine.Duration(VideoEngine.Probe(output))-VideoEngine.Duration(VideoEngine.Probe(input)))<0.15,"Duration changed");
                var streams=(System.Collections.IEnumerable)VideoEngine.Probe(output)["streams"]; int videos=0,audios=0; foreach(var item in streams) {var s=(System.Collections.Generic.Dictionary<string,object>)item; if(Convert.ToString(s["codec_type"])=="video") {videos++; Assert(Convert.ToInt32(s["width"])==320 && Convert.ToInt32(s["height"])==180,"Resolution changed");} if(Convert.ToString(s["codec_type"])=="audio") audios++;} Assert(videos==1 && audios==1,"Missing media tracks"); bool refused=false; try { new VideoEngine().Compress(input,output,0,h264,false,delegate(int p){}); } catch { refused=true; }
                Assert(refused,"Existing output overwritten");
            }
            string preview=Path.Combine(dir,"preview.mp4"); new VideoEngine().Compress(input,preview,0,false,true,delegate(int p){});
            Assert(VideoEngine.Duration(VideoEngine.Probe(preview))<=15.15,"Preview too long");
            bool sameRefused=false; try { new VideoEngine().Compress(input,input,0,false,false,delegate(int p){}); } catch { sameRefused=true; }
            Assert(sameRefused,"Source overwritten");
            string cancelled=Path.Combine(dir,"cancelled.mp4"); var engine=new VideoEngine(); bool cancellation=false;
            try { engine.Compress(input,cancelled,0,false,false,delegate(int p){engine.Cancelled=true;}); } catch(OperationCanceledException) {cancellation=true;}
            Assert(cancellation && !File.Exists(cancelled),"Cancellation failed");
            Assert(Directory.GetFiles(dir,".video-compressor-*.mp4").Length==0,"Temporary file leaked");
            bool hdrRefused=false; try {new VideoEngine().Compress(Path.Combine(dir,"hdr.mp4"),Path.Combine(dir,"hdr-output.mp4"),0,false,false,delegate(int p){});}catch(Exception e){hdrRefused=e.Message.Contains("HDR");} Assert(hdrRefused,"HDR protection failed"); string bad=Path.Combine(dir,"broken.mp4"); File.WriteAllText(bad,"not video"); bool badRefused=false;
            try {new VideoEngine().Compress(bad,Path.Combine(dir,"bad-output.mp4"),0,false,false,delegate(int p){});}catch{badRefused=true;}
            Assert(badRefused,"Broken input accepted");
            Assert(original==Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(input))),"Source changed");
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new VideoCompressor()) { form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-32000,-32000); form.Show(); Application.DoEvents(); using(var bmp=new Bitmap(form.Width,form.Height)) {form.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height)); bmp.Save(Path.Combine(dir,"ui.png"));} }
            Console.WriteLine("PASS: H.264/H.265, Unicode paths, duration, preview, overwrite protection, cancellation, cleanup, invalid input, original hash, form render.");
            return 0;
        } catch(Exception e) {Console.Error.WriteLine(e);return 1;}
    }
}
