using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SimpleHostsEditor
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

    internal sealed class HostEntry
    {
        public bool Enabled;
        public string Ip = "";
        public string Domain = "";
        public string Comment = "";
        public int OriginalLine = -1;
    }

    internal sealed class MainForm : Form
    {
        private static readonly string HostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
        // Disabled entries are commonly written as "# 1.2.3.4 host" or "#!1.2.3.4 host".
        private static readonly Regex EntryPattern = new Regex(@"^\s*(#\s*!?\s*)?(?<ip>\S+)\s+(?<domain>[^\s#]+)(?<comment>\s+#.*)?\s*$", RegexOptions.Compiled);
        private readonly TextBox searchBox = new TextBox();
        private readonly DataGridView grid = new DataGridView();
        private readonly Button addButton = new Button();
        private readonly Button deleteButton = new Button();
        private readonly Button saveButton = new Button();
        private readonly Button reloadButton = new Button();
        private readonly Label statusLabel = new Label();
        private readonly List<HostEntry> entries = new List<HostEntry>();
        private readonly List<string> originalLines = new List<string>();
        private bool dirty;

        public MainForm()
        {
            Text = "Hosts 檔案編輯器";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(700, 530);
            MinimumSize = new Size(580, 430);
            Font = new Font("Microsoft JhengHei UI", 10F);
            Icon = SystemIcons.Application;

            var pathLabel = new Label { Text = "Hosts 檔案：" + HostsPath, AutoEllipsis = true, Location = new Point(18, 15), Size = new Size(664, 24), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            var searchLabel = new Label { Text = "搜尋：", AutoSize = true, Location = new Point(18, 50) };
            searchBox.Location = new Point(75, 47);
            searchBox.Size = new Size(607, 27);
            searchBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            searchBox.TextChanged += delegate { RefreshGrid(); };

            grid.Location = new Point(18, 84);
            grid.Size = new Size(664, 335);
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AutoGenerateColumns = false;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.Fixed3D;
            grid.MultiSelect = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "啟用", Width = 58 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ip", HeaderText = "IP 位址", Width = 170 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Domain", HeaderText = "網域名稱", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            grid.CellValueChanged += GridChanged;
            grid.CurrentCellDirtyStateChanged += delegate { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            grid.SelectionChanged += delegate { deleteButton.Enabled = grid.SelectedRows.Count > 0; };

            addButton.Text = "新增...";
            addButton.Size = new Size(90, 32);
            addButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            addButton.Location = new Point(492, 429);
            addButton.Click += delegate { AddEntry(); };
            deleteButton.Text = "刪除";
            deleteButton.Size = new Size(90, 32);
            deleteButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            deleteButton.Location = new Point(592, 429);
            deleteButton.Enabled = false;
            deleteButton.Click += delegate { DeleteSelected(); };

            statusLabel.Location = new Point(18, 472);
            statusLabel.Size = new Size(315, 34);
            statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.AutoEllipsis = true;

            saveButton.Text = "儲存";
            saveButton.Size = new Size(90, 34);
            saveButton.Location = new Point(392, 470);
            saveButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            saveButton.Click += delegate { SaveHosts(); };
            reloadButton.Text = "重新載入";
            reloadButton.Size = new Size(90, 34);
            reloadButton.Location = new Point(492, 470);
            reloadButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            reloadButton.Click += delegate { ReloadWithConfirmation(); };
            var closeButton = new Button { Text = "關閉", Size = new Size(90, 34), Location = new Point(592, 470), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            closeButton.Click += delegate { Close(); };

            Controls.AddRange(new Control[] { pathLabel, searchLabel, searchBox, grid, addButton, deleteButton, statusLabel, saveButton, reloadButton, closeButton });
            FormClosing += OnFormClosing;
            LoadHosts();
        }

        private void LoadHosts()
        {
            try
            {
                originalLines.Clear();
                originalLines.AddRange(File.ReadAllLines(HostsPath));
                entries.Clear();
                for (int i = 0; i < originalLines.Count; i++)
                {
                    Match match = EntryPattern.Match(originalLines[i]);
                    IPAddress parsed;
                    if (!match.Success || !IPAddress.TryParse(match.Groups["ip"].Value, out parsed)) continue;
                    entries.Add(new HostEntry {
                        Enabled = !match.Groups[1].Success,
                        Ip = match.Groups["ip"].Value,
                        Domain = match.Groups["domain"].Value,
                        Comment = match.Groups["comment"].Success ? match.Groups["comment"].Value : "",
                        OriginalLine = i
                    });
                }
                dirty = false;
                RefreshGrid();
                statusLabel.Text = string.Format("已載入 {0} 筆規則{1}", entries.Count, IsAdministrator() ? "（系統管理員）" : "（唯讀權限）");
            }
            catch (Exception ex) { ShowError("無法讀取 hosts 檔案", ex); }
        }

        private void RefreshGrid()
        {
            string filter = searchBox.Text.Trim();
            grid.CellValueChanged -= GridChanged;
            grid.Rows.Clear();
            foreach (HostEntry entry in entries.Where(x => filter.Length == 0 || x.Ip.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || x.Domain.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                int rowIndex = grid.Rows.Add(entry.Enabled, entry.Ip, entry.Domain);
                grid.Rows[rowIndex].Tag = entry;
            }
            grid.CellValueChanged += GridChanged;
            deleteButton.Enabled = grid.SelectedRows.Count > 0;
        }

        private void GridChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            DataGridViewRow row = grid.Rows[e.RowIndex];
            HostEntry entry = row.Tag as HostEntry;
            if (entry == null) return;
            entry.Enabled = Convert.ToBoolean(row.Cells["Enabled"].Value ?? false);
            entry.Ip = Convert.ToString(row.Cells["Ip"].Value ?? "").Trim();
            entry.Domain = Convert.ToString(row.Cells["Domain"].Value ?? "").Trim();
            dirty = true;
            statusLabel.Text = "有尚未儲存的變更";
        }

        private void AddEntry()
        {
            using (var dialog = new AddHostForm())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                entries.Add(new HostEntry { Enabled = true, Ip = dialog.Ip, Domain = dialog.Domain, OriginalLine = -1 });
                dirty = true;
                searchBox.Clear();
                RefreshGrid();
                grid.ClearSelection();
                if (grid.Rows.Count > 0) { grid.Rows[grid.Rows.Count - 1].Selected = true; grid.FirstDisplayedScrollingRowIndex = grid.Rows.Count - 1; }
                statusLabel.Text = "已新增規則，請按儲存套用";
            }
        }

        private void DeleteSelected()
        {
            var selected = grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag as HostEntry).Where(x => x != null).ToList();
            if (selected.Count == 0) return;
            if (MessageBox.Show(this, "確定刪除選取的 " + selected.Count + " 筆規則？", "確認刪除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (HostEntry entry in selected) entries.Remove(entry);
            dirty = true;
            RefreshGrid();
            statusLabel.Text = "已刪除規則，請按儲存套用";
        }

        private bool ValidateEntries()
        {
            foreach (HostEntry entry in entries)
            {
                IPAddress parsed;
                if (!IPAddress.TryParse(entry.Ip, out parsed)) { MessageBox.Show(this, "IP 位址格式不正確：" + entry.Ip, "無法儲存", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
                if (entry.Domain.Length == 0 || entry.Domain.Any(char.IsWhiteSpace) || entry.Domain.Contains("#")) { MessageBox.Show(this, "網域名稱格式不正確：" + entry.Domain, "無法儲存", MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            }
            return true;
        }

        private void SaveHosts()
        {
            grid.EndEdit();
            if (!ValidateEntries()) return;
            try
            {
                var byLine = entries.Where(x => x.OriginalLine >= 0).ToDictionary(x => x.OriginalLine);
                var output = new List<string>();
                for (int i = 0; i < originalLines.Count; i++)
                {
                    HostEntry entry;
                    if (byLine.TryGetValue(i, out entry)) output.Add(FormatEntry(entry));
                    else if (!WasManagedLine(i)) output.Add(originalLines[i]);
                }
                var added = entries.Where(x => x.OriginalLine < 0).ToList();
                if (added.Count > 0)
                {
                    if (output.Count > 0 && output[output.Count - 1].Length > 0) output.Add("");
                    output.Add("# Added by Hosts File Editor");
                    output.AddRange(added.Select(FormatEntry));
                }
                string backup = HostsPath + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(HostsPath, backup, true);
                File.WriteAllLines(HostsPath, output.ToArray(), new UTF8Encoding(false));
                FlushDns();
                LoadHosts();
                statusLabel.Text = "儲存完成；備份：" + Path.GetFileName(backup);
                MessageBox.Show(this, "Hosts 檔案已儲存，DNS 快取已清除。\n\n備份檔：" + backup, "儲存完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (UnauthorizedAccessException ex) { ShowError("沒有寫入權限，請以系統管理員身分執行本程式", ex); }
            catch (Exception ex) { ShowError("儲存 hosts 檔案失敗", ex); }
        }

        private bool WasManagedLine(int line)
        {
            return entries.Any(x => x.OriginalLine == line) || IsHostEntryLine(originalLines[line]);
        }

        private static bool IsHostEntryLine(string line)
        {
            Match match = EntryPattern.Match(line);
            IPAddress parsed;
            return match.Success && IPAddress.TryParse(match.Groups["ip"].Value, out parsed);
        }

        private static string FormatEntry(HostEntry entry)
        {
            return (entry.Enabled ? "" : "# ") + entry.Ip + "\t" + entry.Domain + entry.Comment;
        }

        private void ReloadWithConfirmation()
        {
            if (dirty && MessageBox.Show(this, "放棄尚未儲存的變更並重新載入？", "重新載入", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            LoadHosts();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!dirty) return;
            DialogResult result = MessageBox.Show(this, "目前有尚未儲存的變更，要直接關閉嗎？", "尚未儲存", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) e.Cancel = true;
        }

        private static void FlushDns()
        {
            try { Process.Start(new ProcessStartInfo("ipconfig.exe", "/flushdns") { CreateNoWindow = true, UseShellExecute = false }).WaitForExit(5000); }
            catch { }
        }

        private static bool IsAdministrator()
        {
            try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
            catch { return false; }
        }

        private void ShowError(string title, Exception ex)
        {
            MessageBox.Show(this, title + "。\n\n" + ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            statusLabel.Text = title;
        }
    }

    internal sealed class AddHostForm : Form
    {
        private readonly TextBox ipBox = new TextBox();
        private readonly TextBox domainBox = new TextBox();
        public string Ip { get { return ipBox.Text.Trim(); } }
        public string Domain { get { return domainBox.Text.Trim(); } }

        public AddHostForm()
        {
            Text = "新增 Hosts 規則";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(440, 175);
            Font = new Font("Microsoft JhengHei UI", 10F);
            var ipLabel = new Label { Text = "IP 位址：", AutoSize = true, Location = new Point(18, 27) };
            ipBox.Location = new Point(100, 23); ipBox.Size = new Size(315, 27);
            var domainLabel = new Label { Text = "網域名稱：", AutoSize = true, Location = new Point(18, 69) };
            domainBox.Location = new Point(100, 65); domainBox.Size = new Size(315, 27);
            var ok = new Button { Text = "確定", DialogResult = DialogResult.None, Location = new Point(235, 116), Size = new Size(85, 33) };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(330, 116), Size = new Size(85, 33) };
            ok.Click += delegate { ValidateAndClose(); };
            Controls.AddRange(new Control[] { ipLabel, ipBox, domainLabel, domainBox, ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
        }

        private void ValidateAndClose()
        {
            IPAddress parsed;
            if (!IPAddress.TryParse(Ip, out parsed)) { MessageBox.Show(this, "請輸入有效的 IPv4 或 IPv6 位址。", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning); ipBox.Focus(); return; }
            if (Domain.Length == 0 || Domain.Any(char.IsWhiteSpace) || Domain.Contains("#")) { MessageBox.Show(this, "請輸入有效的網域名稱，不可包含空白或 #。", "輸入錯誤", MessageBoxButtons.OK, MessageBoxIcon.Warning); domainBox.Focus(); return; }
            DialogResult = DialogResult.OK;
        }
    }
}
