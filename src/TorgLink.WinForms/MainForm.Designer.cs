namespace TorgLink.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel _root;
    private System.Windows.Forms.FlowLayoutPanel _toolbar;
    private System.Windows.Forms.Button _btnAdd;
    private Button _btnLan;
    private Button _btnServers;
    private Button _btnMyQr;
    private Button _btnSettings;
    private System.Windows.Forms.Button _btnLogout;
    private System.Windows.Forms.TextBox _search;
    private System.Windows.Forms.ListBox _list;
    private Label _status;

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
        this._toolbar = new System.Windows.Forms.FlowLayoutPanel();
        this._btnAdd = new System.Windows.Forms.Button();
        this._btnLan = new System.Windows.Forms.Button();
        this._btnServers = new System.Windows.Forms.Button();
        this._btnMyQr = new System.Windows.Forms.Button();
        this._btnSettings = new System.Windows.Forms.Button();
        this._btnLogout = new System.Windows.Forms.Button();
        this._search = new System.Windows.Forms.TextBox();
        this._list = new System.Windows.Forms.ListBox();
        this._status = new System.Windows.Forms.Label();
        this._root.SuspendLayout();
        this._toolbar.SuspendLayout();
        this.SuspendLayout();
        // 
        // _root
        // 
        this._root.ColumnCount = 1;
        this._root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._root.Controls.Add(this._toolbar, 0, 0);
        this._root.Controls.Add(this._search, 0, 1);
        this._root.Controls.Add(this._list, 0, 2);
        this._root.Controls.Add(this._status, 0, 3);
        this._root.Dock = System.Windows.Forms.DockStyle.Fill;
        this._root.Location = new System.Drawing.Point(0, 0);
        this._root.Name = "_root";
        this._root.RowCount = 4;
        this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._root.Size = new System.Drawing.Size(640, 480);
        this._root.TabIndex = 0;
        // 
        // _toolbar
        // 
        this._toolbar.AutoSize = true;
        this._toolbar.Controls.Add(this._btnAdd);
        this._toolbar.Controls.Add(this._btnLan);
        this._toolbar.Controls.Add(this._btnServers);
        this._toolbar.Controls.Add(this._btnMyQr);
        this._toolbar.Controls.Add(this._btnSettings);
        this._toolbar.Controls.Add(this._btnLogout);
        this._toolbar.Dock = System.Windows.Forms.DockStyle.Fill;
        this._toolbar.Location = new System.Drawing.Point(3, 3);
        this._toolbar.Name = "_toolbar";
        this._toolbar.Padding = new System.Windows.Forms.Padding(8);
        this._toolbar.Size = new System.Drawing.Size(634, 53);
        this._toolbar.TabIndex = 0;
        // 
        // _btnAdd
        // 
        this._btnAdd.AutoSize = true;
        this._btnAdd.Location = new System.Drawing.Point(11, 11);
        this._btnAdd.Name = "_btnAdd";
        this._btnAdd.Size = new System.Drawing.Size(117, 31);
        this._btnAdd.TabIndex = 0;
        this._btnAdd.Text = "Добавить чат";
        // 
        // _btnLan
        // 
        this._btnLan.AutoSize = true;
        this._btnLan.Location = new System.Drawing.Point(134, 11);
        this._btnLan.Name = "_btnLan";
        this._btnLan.Size = new System.Drawing.Size(88, 31);
        this._btnLan.TabIndex = 1;
        this._btnLan.Text = "Контакты";
        // 
        // _btnServers
        // 
        this._btnServers.AutoSize = true;
        this._btnServers.Location = new System.Drawing.Point(228, 11);
        this._btnServers.Name = "_btnServers";
        this._btnServers.Size = new System.Drawing.Size(83, 31);
        this._btnServers.TabIndex = 2;
        this._btnServers.Text = "Серверы";
        // 
        // _btnMyQr
        // 
        this._btnMyQr.AutoSize = true;
        this._btnMyQr.Location = new System.Drawing.Point(317, 11);
        this._btnMyQr.Name = "_btnMyQr";
        this._btnMyQr.Size = new System.Drawing.Size(78, 31);
        this._btnMyQr.TabIndex = 3;
        this._btnMyQr.Text = "Мой QR";
        // 
        // _btnSettings
        // 
        this._btnSettings.AutoSize = true;
        this._btnSettings.Location = new System.Drawing.Point(401, 11);
        this._btnSettings.Name = "_btnSettings";
        this._btnSettings.Size = new System.Drawing.Size(97, 31);
        this._btnSettings.TabIndex = 4;
        this._btnSettings.Text = "Настройки";
        // 
        // _btnLogout
        // 
        this._btnLogout.AutoSize = true;
        this._btnLogout.Location = new System.Drawing.Point(504, 11);
        this._btnLogout.Name = "_btnLogout";
        this._btnLogout.Size = new System.Drawing.Size(75, 31);
        this._btnLogout.TabIndex = 6;
        this._btnLogout.Text = "Разлогиниться";
        // 
        // _search
        // 
        this._search.Dock = System.Windows.Forms.DockStyle.Fill;
        this._search.Location = new System.Drawing.Point(3, 59);
        this._search.Margin = new System.Windows.Forms.Padding(3, 0, 3, 6);
        this._search.Name = "_search";
        this._search.Size = new System.Drawing.Size(634, 29);
        this._search.TabIndex = 1;
        // 
        // _list
        // 
        this._list.Dock = System.Windows.Forms.DockStyle.Fill;
        this._list.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
        this._list.IntegralHeight = false;
        this._list.Location = new System.Drawing.Point(3, 94);
        this._list.Name = "_list";
        this._list.Size = new System.Drawing.Size(634, 327);
        this._list.TabIndex = 2;
        // 
        // _status
        // 
        this._status.AutoSize = true;
        this._status.Dock = System.Windows.Forms.DockStyle.Top;
        this._status.Location = new System.Drawing.Point(3, 427);
        this._status.MinimumSize = new System.Drawing.Size(0, 56);
        this._status.Name = "_status";
        this._status.Padding = new System.Windows.Forms.Padding(10, 12, 10, 12);
        this._status.Size = new System.Drawing.Size(634, 56);
        this._status.TabIndex = 3;
        this._status.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        // 
        // MainForm
        // 
        this.ClientSize = new System.Drawing.Size(640, 480);
        this.Controls.Add(this._root);
        this.Name = "MainForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "TorgLink";
        this._root.ResumeLayout(false);
        this._root.PerformLayout();
        this._toolbar.ResumeLayout(false);
        this._toolbar.PerformLayout();
        this.ResumeLayout(false);
    }
}
