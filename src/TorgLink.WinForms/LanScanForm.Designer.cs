namespace TorgLink.WinForms;

partial class LanScanForm
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.TableLayoutPanel _root;
    private Label _hint;
    private Label _status;
    private System.Windows.Forms.TextBox _search;
    private System.Windows.Forms.ListView _list;
    private System.Windows.Forms.ColumnHeader _colName;
    private System.Windows.Forms.ColumnHeader _colBot;
    private System.Windows.Forms.ColumnHeader _colNetworkId;
    private System.Windows.Forms.ColumnHeader _colTransport;
    private System.Windows.Forms.ColumnHeader _colStatus;
    private System.Windows.Forms.ColumnHeader _colAbout;
    private System.Windows.Forms.ColumnHeader _colLastSeen;
    private System.Windows.Forms.FlowLayoutPanel _bottom;
    private System.Windows.Forms.Button _scan;
    private System.Windows.Forms.Button _close;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this._root = new System.Windows.Forms.TableLayoutPanel();
        this._hint = new System.Windows.Forms.Label();
        this._status = new System.Windows.Forms.Label();
        this._search = new System.Windows.Forms.TextBox();
        this._list = new System.Windows.Forms.ListView();
        this._colName = new System.Windows.Forms.ColumnHeader();
        this._colBot = new System.Windows.Forms.ColumnHeader();
        this._colNetworkId = new System.Windows.Forms.ColumnHeader();
        this._colTransport = new System.Windows.Forms.ColumnHeader();
        this._colStatus = new System.Windows.Forms.ColumnHeader();
        this._colAbout = new System.Windows.Forms.ColumnHeader();
        this._colLastSeen = new System.Windows.Forms.ColumnHeader();
        this._bottom = new System.Windows.Forms.FlowLayoutPanel();
        this._close = new System.Windows.Forms.Button();
        this._scan = new System.Windows.Forms.Button();
        this._root.SuspendLayout();
        this._bottom.SuspendLayout();
        this.SuspendLayout();
        // 
        // _root
        // 
        this._root.ColumnCount = 1;
        this._root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
        this._root.Controls.Add(this._hint, 0, 0);
        this._root.Controls.Add(this._status, 0, 1);
        this._root.Controls.Add(this._search, 0, 2);
        this._root.Controls.Add(this._list, 0, 3);
        this._root.Controls.Add(this._bottom, 0, 4);
        this._root.Dock = System.Windows.Forms.DockStyle.Fill;
        this._root.Location = new System.Drawing.Point(0, 0);
        this._root.Name = "_root";
        this._root.Padding = new System.Windows.Forms.Padding(12);
        this._root.RowCount = 5;
        this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._root.Size = new System.Drawing.Size(744, 421);
        this._root.TabIndex = 0;
        // 
        // _hint
        // 
        this._hint.AutoSize = true;
        this._hint.ForeColor = System.Drawing.SystemColors.GrayText;
        this._hint.Location = new System.Drawing.Point(15, 12);
        this._hint.MaximumSize = new System.Drawing.Size(720, 0);
        this._hint.Name = "_hint";
        this._hint.Size = new System.Drawing.Size(694, 42);
        this._hint.TabIndex = 0;
        this._hint.Text = "Как в TorgLink.Maui → Контакты: чаты, клиенты с messenger-серверов (GetClients) и" + " LAN (UDP). Двойной щелчок — открыть/создать чат. BLE не поддерживается.";
        // 
        // _status
        // 
        this._status.AutoSize = true;
        this._status.ForeColor = System.Drawing.SystemColors.GrayText;
        this._status.Location = new System.Drawing.Point(15, 54);
        this._status.Name = "_status";
        this._status.Size = new System.Drawing.Size(0, 21);
        this._status.TabIndex = 1;
        // 
        // _search
        // 
        this._search.Dock = System.Windows.Forms.DockStyle.Fill;
        this._search.Location = new System.Drawing.Point(15, 78);
        this._search.Margin = new System.Windows.Forms.Padding(0, 0, 0, 6);
        this._search.Name = "_search";
        this._search.Size = new System.Drawing.Size(714, 29);
        this._search.TabIndex = 2;
        // 
        // _list
        // 
        this._list.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { this._colName, this._colBot, this._colNetworkId, this._colTransport, this._colStatus, this._colAbout, this._colLastSeen });
        this._list.Dock = System.Windows.Forms.DockStyle.Fill;
        this._list.FullRowSelect = true;
        this._list.GridLines = true;
        this._list.HideSelection = false;
        this._list.ShowItemToolTips = true;
        this._list.Location = new System.Drawing.Point(15, 113);
        this._list.MultiSelect = false;
        this._list.Name = "_list";
        this._list.Size = new System.Drawing.Size(714, 242);
        this._list.TabIndex = 3;
        this._list.UseCompatibleStateImageBehavior = false;
        this._list.View = System.Windows.Forms.View.Details;
        // 
        // _colName
        // 
        this._colName.Text = "Имя";
        this._colName.Width = 160;
        // 
        // _colBot
        // 
        this._colBot.Text = "Бот";
        this._colBot.Width = 420;
        // 
        // _colNetworkId
        // 
        this._colNetworkId.Text = "Network id";
        this._colNetworkId.Width = 180;
        // 
        // _colTransport
        // 
        this._colTransport.Text = "Транспорт";
        this._colTransport.Width = 100;
        // 
        // _colStatus
        // 
        this._colStatus.Text = "Статус";
        this._colStatus.Width = 80;
        // 
        // _colAbout
        // 
        this._colAbout.Text = "О себе";
        this._colAbout.Width = 160;
        // 
        // _colLastSeen
        // 
        this._colLastSeen.Text = "Последний контакт";
        this._colLastSeen.Width = 140;
        // 
        // _bottom
        // 
        this._bottom.AutoSize = true;
        this._bottom.Controls.Add(this._close);
        this._bottom.Controls.Add(this._scan);
        this._bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
        this._bottom.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
        this._bottom.Location = new System.Drawing.Point(15, 361);
        this._bottom.Name = "_bottom";
        this._bottom.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
        this._bottom.Size = new System.Drawing.Size(714, 45);
        this._bottom.TabIndex = 3;
        // 
        // _close
        // 
        this._close.DialogResult = System.Windows.Forms.DialogResult.OK;
        this._close.Location = new System.Drawing.Point(624, 11);
        this._close.Name = "_close";
        this._close.Size = new System.Drawing.Size(87, 31);
        this._close.TabIndex = 1;
        this._close.Text = "Закрыть";
        // 
        // _scan
        // 
        this._scan.AutoSize = true;
        this._scan.Location = new System.Drawing.Point(505, 11);
        this._scan.Name = "_scan";
        this._scan.Size = new System.Drawing.Size(113, 31);
        this._scan.TabIndex = 0;
        this._scan.Text = "Сканировать";
        // 
        // LanScanForm
        // 
        this.AcceptButton = this._close;
        this.ClientSize = new System.Drawing.Size(744, 421);
        this.Controls.Add(this._root);
        this.MinimizeBox = false;
        this.Name = "LanScanForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Контакты";
        this._root.ResumeLayout(false);
        this._root.PerformLayout();
        this._bottom.ResumeLayout(false);
        this._bottom.PerformLayout();
        this.ResumeLayout(false);
    }
}
