namespace TorgLink.WinForms;

partial class QrPreviewForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel _layout;
    private PictureBox _box;
    private Label _label;
    private Button _close;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_box != null)
            {
                var img = _box.Image;
                _box.Image = null;
                img?.Dispose();
            }

            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _layout = new TableLayoutPanel();
        _box = new PictureBox();
        _label = new Label();
        _close = new Button();
        _layout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_box).BeginInit();
        SuspendLayout();

        _box.Dock = DockStyle.Fill;
        _box.Name = "_box";
        _box.Size = new Size(360, 360);
        _box.SizeMode = PictureBoxSizeMode.Zoom;
        _box.Margin = new Padding(0, 0, 0, 12);

        _label.AutoSize = true;
        _label.Dock = DockStyle.Fill;
        _label.Margin = new Padding(0, 0, 0, 12);
        _label.MaximumSize = new Size(420, 0);
        _label.Name = "_label";
        _label.TextAlign = ContentAlignment.MiddleLeft;

        _close.AutoSize = true;
        _close.DialogResult = DialogResult.OK;
        _close.Margin = new Padding(0);
        _close.MinimumSize = new Size(100, 32);
        _close.Name = "_close";
        _close.Text = "Закрыть";

        _layout.ColumnCount = 1;
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _layout.Controls.Add(_box, 0, 0);
        _layout.Controls.Add(_label, 0, 1);
        _layout.Controls.Add(_close, 0, 2);
        _layout.Dock = DockStyle.Fill;
        _layout.Name = "_layout";
        _layout.Padding = new Padding(16);
        _layout.RowCount = 3;
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        AcceptButton = _close;
        ClientSize = new Size(440, 520);
        Controls.Add(_layout);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(420, 500);
        Name = "QrPreviewForm";
        StartPosition = FormStartPosition.CenterParent;

        ((System.ComponentModel.ISupportInitialize)_box).EndInit();
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        ResumeLayout(false);
    }
}
