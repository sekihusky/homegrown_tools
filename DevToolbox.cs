using System;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace DevToolbox
{
    internal static class Program
    {
        [STAThread] static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
    }

    internal sealed class MainForm : Form
    {
        readonly ComboBox encoding = new ComboBox();
        readonly TextBox input = new TextBox();
        readonly TextBox output = new TextBox();
        readonly ComboBox hash = new ComboBox();
        readonly Label status = new Label();

        public MainForm()
        {
            Text = "開發工具箱"; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(900, 650);
            MinimumSize = new Size(760, 520); Font = new Font("Microsoft JhengHei UI", 10F); BackColor = Color.FromArgb(246,248,251);
            var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 6) };
            tabs.TabPages.Add(EncodingPage()); tabs.TabPages.Add(HashPage()); tabs.TabPages.Add(FormatPage()); Controls.Add(tabs);
        }

        TabPage EncodingPage()
        {
            var page = new TabPage("編碼／解碼"); var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 5 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            encoding.Items.AddRange(new object[] { "Base64", "URL", "HTML", "Hex" }); encoding.SelectedIndex = 0; encoding.DropDownStyle = ComboBoxStyle.DropDownList;
            panel.Controls.Add(new Label { Text = "格式", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0); panel.Controls.Add(encoding, 1, 0);
            input.Multiline = true; input.ScrollBars = ScrollBars.Both; input.AcceptsTab = true; input.Dock = DockStyle.Fill;
            output.Multiline = true; output.ScrollBars = ScrollBars.Both; output.ReadOnly = true; output.BackColor = Color.White; output.Dock = DockStyle.Fill;
            panel.Controls.Add(new Label { Text = "輸入", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1); panel.Controls.Add(input, 1, 1);
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill }; actions.Controls.Add(Button("編碼", (s,e) => Transform(true))); actions.Controls.Add(Button("解碼", (s,e) => Transform(false))); actions.Controls.Add(Button("清除", (s,e) => { input.Clear(); output.Clear(); status.Text = ""; })); actions.Controls.Add(Button("複製結果", (s,e) => CopyOutput()));
            panel.Controls.Add(new Label { Text = "操作", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2); panel.Controls.Add(actions, 1, 2);
            panel.Controls.Add(new Label { Text = "結果", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3); panel.Controls.Add(output, 1, 3);
            panel.Controls.Add(status, 1, 4); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 45)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); panel.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
            page.Controls.Add(panel); return page;
        }

        TabPage HashPage()
        {
            var page = new TabPage("雜湊"); var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 5 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            hash.Items.AddRange(new object[] { "MD5", "SHA-1", "SHA-256", "SHA-384", "SHA-512" }); hash.SelectedIndex = 2; hash.DropDownStyle = ComboBoxStyle.DropDownList;
            var text = new TextBox { Multiline = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill };
            var result = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, BackColor = Color.White };
            var run = Button("計算雜湊", (s,e) => { try { using (var h = CreateHash()) result.Text = BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(text.Text))).Replace("-", "").ToLowerInvariant(); } catch(Exception ex) { result.Text = ex.Message; } });
            var copy = Button("複製結果", (s,e) => { if (!string.IsNullOrEmpty(result.Text)) Clipboard.SetText(result.Text); });
            panel.Controls.Add(new Label { Text = "演算法", AutoSize = true, Anchor = AnchorStyles.Left },0,0); panel.Controls.Add(hash,1,0); panel.Controls.Add(new Label { Text = "輸入文字", AutoSize = true, Anchor = AnchorStyles.Left },0,1); panel.Controls.Add(text,1,1); panel.Controls.Add(run,1,2); panel.Controls.Add(new Label { Text = "SHA 結果", AutoSize = true, Anchor = AnchorStyles.Left },0,3); panel.Controls.Add(result,1,3); panel.Controls.Add(copy,1,4); panel.RowStyles.Add(new RowStyle(SizeType.Absolute,38)); panel.RowStyles.Add(new RowStyle(SizeType.Percent,55)); panel.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); panel.RowStyles.Add(new RowStyle(SizeType.Percent,45)); page.Controls.Add(panel); return page;
        }

        TabPage FormatPage()
        {
            var page = new TabPage("格式化"); var inputBox = new TextBox { Multiline=true, ScrollBars=ScrollBars.Both, Dock=DockStyle.Fill }; var outputBox = new TextBox { Multiline=true, ScrollBars=ScrollBars.Both, ReadOnly=true, Dock=DockStyle.Fill, BackColor=Color.White }; var type = new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList, Dock=DockStyle.Left, Width=150 }; type.Items.AddRange(new object[]{"JSON","URL 元件"}); type.SelectedIndex=0; var go=Button("格式化",(s,e)=>{try{ outputBox.Text=type.SelectedIndex==0 ? PrettyJson(inputBox.Text) : Uri.UnescapeDataString(inputBox.Text); }catch(Exception ex){outputBox.Text="格式錯誤："+ex.Message;}}); var copy=Button("複製結果",(s,e)=>{if(outputBox.Text.Length>0)Clipboard.SetText(outputBox.Text);}); var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Vertical,SplitterDistance=410}; split.Panel1.Controls.Add(inputBox); split.Panel2.Controls.Add(outputBox); var top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=45}; top.Controls.Add(type);top.Controls.Add(go);top.Controls.Add(copy); page.Controls.Add(split);page.Controls.Add(top); return page;
        }

        HashAlgorithm CreateHash() { switch(hash.SelectedItem.ToString()){case "MD5":return MD5.Create();case "SHA-1":return SHA1.Create();case "SHA-384":return SHA384.Create();case "SHA-512":return SHA512.Create();default:return SHA256.Create();} }
        void Transform(bool encode) { try { string v=input.Text; switch(encoding.SelectedItem.ToString()){case "Base64": output.Text=encode?Convert.ToBase64String(Encoding.UTF8.GetBytes(v)):Encoding.UTF8.GetString(Convert.FromBase64String(v));break;case "URL":output.Text=encode?Uri.EscapeDataString(v):Uri.UnescapeDataString(v);break;case "HTML":output.Text=encode?System.Net.WebUtility.HtmlEncode(v):System.Net.WebUtility.HtmlDecode(v);break;default:output.Text=encode?BitConverter.ToString(Encoding.UTF8.GetBytes(v)).Replace("-","").ToLowerInvariant():Encoding.UTF8.GetString(Enumerable.Range(0,v.Length/2).Select(i=>Convert.ToByte(v.Substring(i*2,2),16)).ToArray());break;} status.Text="完成";}catch(Exception ex){output.Text="處理失敗："+ex.Message;status.Text="請檢查輸入格式";} }
        void CopyOutput(){if(output.Text.Length>0)Clipboard.SetText(output.Text);}
        string PrettyJson(string json) { var sb=new StringBuilder(); int indent=0; bool quoted=false; for(int i=0;i<json.Length;i++){char c=json[i]; if(c=='\"' && (i==0||json[i-1]!='\\')) quoted=!quoted; if(!quoted && (c=='{'||c=='[')){sb.Append(c);sb.AppendLine();indent++;sb.Append(new string(' ',indent*2));} else if(!quoted && (c=='}'||c==']')){sb.AppendLine();indent--;sb.Append(new string(' ',indent*2));sb.Append(c);} else if(!quoted && c==','){sb.Append(c);sb.AppendLine();sb.Append(new string(' ',indent*2));} else if(!quoted && c==':'){sb.Append(": ");} else if(!char.IsWhiteSpace(c)||quoted) sb.Append(c); } return sb.ToString(); }
        Button Button(string text, EventHandler click){var b=new Button{Text=text,AutoSize=true,Height=32,Margin=new Padding(3)};b.Click+=click;return b;}
    }
}
