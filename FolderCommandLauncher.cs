using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace FolderCommandLauncher
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly TextBox folderBox = new TextBox();
        private readonly Button chooseButton = new Button();
        private readonly Button normalButton = new Button();
        private readonly Button adminButton = new Button();
        private readonly Label statusLabel = new Label();

        public MainForm()
        {
            Text = "資料夾命令提示字元";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(640, 310);
            MinimumSize = new Size(550, 310);
            MaximizeBox = false;
            Font = new Font("Microsoft JhengHei UI", 10F);
            Icon = SystemIcons.Application;
            BackColor = Color.FromArgb(246, 248, 251);

            var title = new Label
            {
                Text = "在資料夾中開啟命令提示字元",
                Font = new Font("Microsoft JhengHei UI", 17F, FontStyle.Bold),
                ForeColor = Color.FromArgb(28, 38, 55),
                AutoSize = true,
                Location = new Point(24, 21)
            };
            var subtitle = new Label
            {
                Text = "先選擇資料夾，再依需要選擇一般或系統管理員權限。",
                ForeColor = Color.DimGray,
                AutoSize = true,
                Location = new Point(27, 58)
            };

            var stepOne = MakeStepLabel("1", "選擇資料夾", 24, 98);
            folderBox.Location = new Point(24, 130);
            folderBox.Size = new Size(478, 30);
            folderBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            folderBox.ReadOnly = true;
            folderBox.BackColor = Color.White;

            chooseButton.Text = "瀏覽…";
            chooseButton.Location = new Point(514, 128);
            chooseButton.Size = new Size(102, 34);
            chooseButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            chooseButton.Click += delegate { ChooseFolder(); };

            var stepTwo = MakeStepLabel("2", "選擇開啟方式", 24, 187);
            normalButton.Text = "一般方式開啟";
            normalButton.Location = new Point(24, 220);
            normalButton.Size = new Size(190, 42);
            normalButton.Enabled = false;
            normalButton.Click += delegate { OpenCommandPrompt(false); };

            adminButton.Text = "以系統管理員身分開啟";
            adminButton.Location = new Point(226, 220);
            adminButton.Size = new Size(235, 42);
            adminButton.Enabled = false;
            adminButton.BackColor = Color.FromArgb(37, 99, 235);
            adminButton.ForeColor = Color.White;
            adminButton.FlatStyle = FlatStyle.Flat;
            adminButton.FlatAppearance.BorderSize = 0;
            adminButton.Click += delegate { OpenCommandPrompt(true); };

            statusLabel.Text = "請先選擇一個資料夾。";
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Location = new Point(24, 274);
            statusLabel.Size = new Size(590, 24);
            statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            Controls.AddRange(new Control[] { title, subtitle, stepOne, folderBox, chooseButton,
                stepTwo, normalButton, adminButton, statusLabel });
            Shown += delegate { chooseButton.Focus(); };
        }

        private Label MakeStepLabel(string number, string text, int x, int y)
        {
            return new Label
            {
                Text = number + "  " + text,
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = Color.FromArgb(55, 65, 81),
                AutoSize = true,
                Location = new Point(x, y)
            };
        }

        private void ChooseFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "選擇要開啟命令提示字元的資料夾";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(folderBox.Text))
                    dialog.SelectedPath = folderBox.Text;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;

                folderBox.Text = dialog.SelectedPath;
                normalButton.Enabled = true;
                adminButton.Enabled = true;
                statusLabel.Text = "已選擇：" + dialog.SelectedPath;
                statusLabel.ForeColor = Color.FromArgb(22, 101, 52);
            }
        }

        private void OpenCommandPrompt(bool asAdministrator)
        {
            string folder = folderBox.Text;
            if (!Directory.Exists(folder))
            {
                MessageBox.Show(this, "選擇的資料夾已不存在，請重新選擇。", "找不到資料夾",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ChooseFolder();
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo();
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = "/K cd /d \"" + folder + "\"";
                startInfo.WorkingDirectory = folder;
                startInfo.UseShellExecute = true;
                if (asAdministrator)
                    startInfo.Verb = "runas";

                Process.Start(startInfo);
                statusLabel.Text = asAdministrator ? "已要求以系統管理員身分開啟命令提示字元。" : "已開啟命令提示字元。";
                statusLabel.ForeColor = Color.FromArgb(22, 101, 52);
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (asAdministrator && ex.NativeErrorCode == 1223)
                {
                    statusLabel.Text = "已取消系統管理員權限確認。";
                    statusLabel.ForeColor = Color.DimGray;
                    return;
                }
                ShowOpenError(ex);
            }
            catch (Exception ex)
            {
                ShowOpenError(ex);
            }
        }

        private void ShowOpenError(Exception ex)
        {
            statusLabel.Text = "無法開啟命令提示字元。";
            statusLabel.ForeColor = Color.FromArgb(185, 28, 28);
            MessageBox.Show(this, "無法開啟命令提示字元。\r\n\r\n" + ex.Message,
                "開啟失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
