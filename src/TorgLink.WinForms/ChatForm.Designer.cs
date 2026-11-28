namespace TorgLink.WinForms;

partial class ChatForm
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.SplitContainer _split;
    private System.Windows.Forms.ListBox _sidebar;
    private System.Windows.Forms.Panel _right;
    private System.Windows.Forms.ListBox _messages;
    private System.Windows.Forms.TableLayoutPanel _bottom;
    private System.Windows.Forms.TextBox _input;
    private System.Windows.Forms.Button _attachVoice;
    private System.Windows.Forms.Button _attachImage;
    private System.Windows.Forms.Button _attachDocument;
    private System.Windows.Forms.Button _botCommands;
    private System.Windows.Forms.Button _deliveryPath;
    private System.Windows.Forms.Button _blockPeer;
    private System.Windows.Forms.Button _send;

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
        this._split = new System.Windows.Forms.SplitContainer();
        this._sidebar = new System.Windows.Forms.ListBox();
        this._right = new System.Windows.Forms.Panel();
        this._messages = new System.Windows.Forms.ListBox();
        this._bottom = new System.Windows.Forms.TableLayoutPanel();
        this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
        this._attachVoice = new System.Windows.Forms.Button();
        this._attachDocument = new System.Windows.Forms.Button();
        this._attachImage = new System.Windows.Forms.Button();
        this._botCommands = new System.Windows.Forms.Button();
        this._deliveryPath = new System.Windows.Forms.Button();
        this._blockPeer = new System.Windows.Forms.Button();
        this._send = new System.Windows.Forms.Button();
        this._input = new System.Windows.Forms.TextBox();
        ((System.ComponentModel.ISupportInitialize)(this._split)).BeginInit();
        this._split.Panel1.SuspendLayout();
        this._split.Panel2.SuspendLayout();
        this._split.SuspendLayout();
        this._right.SuspendLayout();
        this._bottom.SuspendLayout();
        this.tableLayoutPanel1.SuspendLayout();
        this.SuspendLayout();
        // 
        // _split
        // 
        this._split.Dock = System.Windows.Forms.DockStyle.Fill;
        this._split.Location = new System.Drawing.Point(0, 0);
        this._split.Name = "_split";
        // 
        // _split.Panel1
        // 
        this._split.Panel1.Controls.Add(this._sidebar);
        this._split.Panel1MinSize = 0;
        // 
        // _split.Panel2
        // 
        this._split.Panel2.Controls.Add(this._right);
        this._split.Panel2MinSize = 0;
        this._split.Size = new System.Drawing.Size(1011, 702);
        this._split.SplitterDistance = 380;
        this._split.TabIndex = 0;
        // 
        // _sidebar
        // 
        this._sidebar.Dock = System.Windows.Forms.DockStyle.Fill;
        this._sidebar.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
        this._sidebar.IntegralHeight = false;
        this._sidebar.Location = new System.Drawing.Point(0, 0);
        this._sidebar.Name = "_sidebar";
        this._sidebar.Size = new System.Drawing.Size(380, 702);
        this._sidebar.TabIndex = 0;
        // 
        // _right
        // 
        this._right.Controls.Add(this._messages);
        this._right.Controls.Add(this._bottom);
        this._right.Dock = System.Windows.Forms.DockStyle.Fill;
        this._right.Location = new System.Drawing.Point(0, 0);
        this._right.Name = "_right";
        this._right.Size = new System.Drawing.Size(627, 702);
        this._right.TabIndex = 0;
        // 
        // _messages
        // 
        this._messages.Dock = System.Windows.Forms.DockStyle.Fill;
        this._messages.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawVariable;
        this._messages.IntegralHeight = false;
        this._messages.Location = new System.Drawing.Point(0, 0);
        this._messages.Margin = new System.Windows.Forms.Padding(0);
        this._messages.Name = "_messages";
        this._messages.Size = new System.Drawing.Size(627, 462);
        this._messages.TabIndex = 0;
        // 
        // _bottom
        // 
        this._bottom.ColumnCount = 1;
        this._bottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._bottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
        this._bottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
        this._bottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
        this._bottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
        this._bottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._bottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
        this._bottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
        this._bottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
        this._bottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
        this._bottom.Controls.Add(this.tableLayoutPanel1, 0, 0);
        this._bottom.Controls.Add(this._input, 0, 0);
        this._bottom.Dock = System.Windows.Forms.DockStyle.Bottom;
        this._bottom.Location = new System.Drawing.Point(0, 462);
        this._bottom.MinimumSize = new System.Drawing.Size(636, 240);
        this._bottom.Name = "_bottom";
        this._bottom.RowCount = 1;
        this._bottom.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._bottom.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 69F));
        this._bottom.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._bottom.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 69F));
        this._bottom.Size = new System.Drawing.Size(636, 240);
        this._bottom.TabIndex = 1;
        // 
        // tableLayoutPanel1
        // 
        this.tableLayoutPanel1.ColumnCount = 7;
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.28F));
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.28F));
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.28F));
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.28F));
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.28F));
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.28F));
        this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 14.32F));
        this.tableLayoutPanel1.Controls.Add(this._attachVoice, 0, 0);
        this.tableLayoutPanel1.Controls.Add(this._attachImage, 1, 0);
        this.tableLayoutPanel1.Controls.Add(this._attachDocument, 2, 0);
        this.tableLayoutPanel1.Controls.Add(this._botCommands, 3, 0);
        this.tableLayoutPanel1.Controls.Add(this._deliveryPath, 4, 0);
        this.tableLayoutPanel1.Controls.Add(this._blockPeer, 5, 0);
        this.tableLayoutPanel1.Controls.Add(this._send, 6, 0);
        this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Bottom;
        this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 177);
        this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(0);
        this.tableLayoutPanel1.Name = "tableLayoutPanel1";
        this.tableLayoutPanel1.Padding = new System.Windows.Forms.Padding(6);
        this.tableLayoutPanel1.RowCount = 1;
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this.tableLayoutPanel1.Size = new System.Drawing.Size(636, 63);
        this.tableLayoutPanel1.TabIndex = 2;
        // 
        // _attachVoice
        // 
        this._attachVoice.Dock = System.Windows.Forms.DockStyle.Fill;
        this._attachVoice.Font = new System.Drawing.Font("Segoe UI Emoji", 11F);
        this._attachVoice.Location = new System.Drawing.Point(9, 9);
        this._attachVoice.Name = "_attachVoice";
        this._attachVoice.Size = new System.Drawing.Size(118, 45);
        this._attachVoice.TabIndex = 2;
        this._attachVoice.Text = "🎤";
        // 
        // _attachDocument
        // 
        this._attachDocument.Dock = System.Windows.Forms.DockStyle.Fill;
        this._attachDocument.Font = new System.Drawing.Font("Segoe UI Emoji", 11F);
        this._attachDocument.Location = new System.Drawing.Point(257, 9);
        this._attachDocument.Name = "_attachDocument";
        this._attachDocument.Size = new System.Drawing.Size(118, 45);
        this._attachDocument.TabIndex = 4;
        this._attachDocument.Text = "📄";
        // 
        // _attachImage
        // 
        this._attachImage.Dock = System.Windows.Forms.DockStyle.Fill;
        this._attachImage.Font = new System.Drawing.Font("Segoe UI Emoji", 11F);
        this._attachImage.Location = new System.Drawing.Point(133, 9);
        this._attachImage.Name = "_attachImage";
        this._attachImage.Size = new System.Drawing.Size(118, 45);
        this._attachImage.TabIndex = 3;
        this._attachImage.Text = "🖼";
        // 
        // _botCommands
        // 
        this._botCommands.Dock = System.Windows.Forms.DockStyle.Fill;
        this._botCommands.Font = new System.Drawing.Font("Segoe UI", 8F);
        this._botCommands.Location = new System.Drawing.Point(318, 9);
        this._botCommands.Name = "_botCommands";
        this._botCommands.Size = new System.Drawing.Size(98, 45);
        this._botCommands.TabIndex = 5;
        this._botCommands.Text = "Список команд";
        this._botCommands.Visible = false;
        // 
        // _deliveryPath
        // 
        this._deliveryPath.Dock = System.Windows.Forms.DockStyle.Fill;
        this._deliveryPath.Font = new System.Drawing.Font("Segoe UI", 9F);
        this._deliveryPath.Location = new System.Drawing.Point(422, 9);
        this._deliveryPath.Name = "_deliveryPath";
        this._deliveryPath.Size = new System.Drawing.Size(80, 45);
        this._deliveryPath.TabIndex = 6;
        this._deliveryPath.Text = "Путь: авто";
        // 
        // _blockPeer
        // 
        this._blockPeer.Dock = System.Windows.Forms.DockStyle.Fill;
        this._blockPeer.Font = new System.Drawing.Font("Segoe UI Emoji", 11F);
        this._blockPeer.Location = new System.Drawing.Point(508, 9);
        this._blockPeer.Name = "_blockPeer";
        this._blockPeer.Size = new System.Drawing.Size(80, 45);
        this._blockPeer.TabIndex = 7;
        this._blockPeer.Text = "🚫";
        // 
        // _send
        // 
        this._send.AutoSize = true;
        this._send.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        this._send.Dock = System.Windows.Forms.DockStyle.Fill;
        this._send.Location = new System.Drawing.Point(594, 9);
        this._send.Name = "_send";
        this._send.Size = new System.Drawing.Size(80, 45);
        this._send.TabIndex = 8;
        this._send.Text = "Отправить";
        // 
        // _input
        // 
        this._input.AcceptsReturn = true;
        this._input.Dock = System.Windows.Forms.DockStyle.Fill;
        this._input.Location = new System.Drawing.Point(0, 0);
        this._input.Margin = new System.Windows.Forms.Padding(0);
        this._input.Multiline = true;
        this._input.Name = "_input";
        this._input.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this._input.Size = new System.Drawing.Size(636, 171);
        this._input.TabIndex = 0;
        // 
        // ChatForm
        // 
        this.BackColor = System.Drawing.SystemColors.Control;
        this.ClientSize = new System.Drawing.Size(1011, 702);
        this.Controls.Add(this._split);
        this.Location = new System.Drawing.Point(15, 15);
        this.Name = "ChatForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this._split.Panel1.ResumeLayout(false);
        this._split.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)(this._split)).EndInit();
        this._split.ResumeLayout(false);
        this._right.ResumeLayout(false);
        this._bottom.ResumeLayout(false);
        this._bottom.PerformLayout();
        this.tableLayoutPanel1.ResumeLayout(false);
        this.tableLayoutPanel1.PerformLayout();
        this.ResumeLayout(false);
    }

    private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
}
