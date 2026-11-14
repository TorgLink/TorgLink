namespace TorgLink.WinForms;

partial class SettingsForm
{
    private System.ComponentModel.IContainer components = null;

    private System.Windows.Forms.TableLayoutPanel _root;
    private System.Windows.Forms.TabControl _tabs;

    private System.Windows.Forms.TabPage _tabProfile;
    private System.Windows.Forms.TableLayoutPanel _pageProfile;
    private System.Windows.Forms.Label _profile;
    private System.Windows.Forms.FlowLayoutPanel _avatarRow;
    private System.Windows.Forms.PictureBox _avatarPreview;
    private System.Windows.Forms.FlowLayoutPanel _avatarBtns;
    private System.Windows.Forms.Button _loadAvatar;
    private System.Windows.Forms.Button _clearAvatar;
    private System.Windows.Forms.Label _aboutLabel;
    private System.Windows.Forms.TextBox _aboutMe;
    private System.Windows.Forms.Label _aboutCounter;

    private System.Windows.Forms.TabPage _tabNetwork;
    private System.Windows.Forms.TableLayoutPanel _pageNetwork;
    private System.Windows.Forms.Label _lblUdpCaption;
    private System.Windows.Forms.Label _udpPort;
    private System.Windows.Forms.CheckBox _lan;
    private System.Windows.Forms.CheckBox _shareRoutes;
    private System.Windows.Forms.CheckBox _visibleForSearch;
    private System.Windows.Forms.Label _lblEconomy;
    private System.Windows.Forms.ComboBox _economy;
    private System.Windows.Forms.Label _economyHint;
    private System.Windows.Forms.Label _lblNoBle;

    private System.Windows.Forms.TabPage _tabRouting;
    private System.Windows.Forms.TableLayoutPanel _pageRouting;
    private System.Windows.Forms.FlowLayoutPanel _rowHops;
    private System.Windows.Forms.Label _lblHops;
    private System.Windows.Forms.NumericUpDown _hops;
    private System.Windows.Forms.FlowLayoutPanel _rowAttempts;
    private System.Windows.Forms.Label _lblAttempts;
    private System.Windows.Forms.NumericUpDown _attempts;
    private System.Windows.Forms.FlowLayoutPanel _rowDelay;
    private System.Windows.Forms.Label _lblDelay;
    private System.Windows.Forms.NumericUpDown _delayMs;
    private System.Windows.Forms.FlowLayoutPanel _rowTimeout;
    private System.Windows.Forms.Label _lblTimeout;
    private System.Windows.Forms.NumericUpDown _timeoutMs;
    private System.Windows.Forms.Label _lblLink;
    private System.Windows.Forms.ComboBox _link;

    private System.Windows.Forms.FlowLayoutPanel _buttons;
    private System.Windows.Forms.Button _save;
    private System.Windows.Forms.Button _keys;
    private System.Windows.Forms.Button _exportProfile;
    private System.Windows.Forms.Button _shareProfile;
    private System.Windows.Forms.Button _about;
    private System.Windows.Forms.Button _close;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_avatarPreview != null)
            {
                var img = _avatarPreview.Image;
                _avatarPreview.Image = null;
                img?.Dispose();
            }

            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this._root = new System.Windows.Forms.TableLayoutPanel();
        this._tabs = new System.Windows.Forms.TabControl();
        this._tabProfile = new System.Windows.Forms.TabPage();
        this._pageProfile = new System.Windows.Forms.TableLayoutPanel();
        this._profile = new System.Windows.Forms.Label();
        this._avatarRow = new System.Windows.Forms.FlowLayoutPanel();
        this._avatarPreview = new System.Windows.Forms.PictureBox();
        this._avatarBtns = new System.Windows.Forms.FlowLayoutPanel();
        this._loadAvatar = new System.Windows.Forms.Button();
        this._clearAvatar = new System.Windows.Forms.Button();
        this._aboutLabel = new System.Windows.Forms.Label();
        this._aboutCounter = new System.Windows.Forms.Label();
        this._aboutMe = new System.Windows.Forms.TextBox();
        this._tabNetwork = new System.Windows.Forms.TabPage();
        this._pageNetwork = new System.Windows.Forms.TableLayoutPanel();
        this._lan = new System.Windows.Forms.CheckBox();
        this._shareRoutes = new System.Windows.Forms.CheckBox();
        this._visibleForSearch = new System.Windows.Forms.CheckBox();
        this._lblUdpCaption = new System.Windows.Forms.Label();
        this._udpPort = new System.Windows.Forms.Label();
        this._lblEconomy = new System.Windows.Forms.Label();
        this._economy = new System.Windows.Forms.ComboBox();
        this._economyHint = new System.Windows.Forms.Label();
        this._lblNoBle = new System.Windows.Forms.Label();
        this._tabRouting = new System.Windows.Forms.TabPage();
        this._pageRouting = new System.Windows.Forms.TableLayoutPanel();
        this._rowHops = new System.Windows.Forms.FlowLayoutPanel();
        this._lblHops = new System.Windows.Forms.Label();
        this._hops = new System.Windows.Forms.NumericUpDown();
        this._rowAttempts = new System.Windows.Forms.FlowLayoutPanel();
        this._lblAttempts = new System.Windows.Forms.Label();
        this._attempts = new System.Windows.Forms.NumericUpDown();
        this._rowDelay = new System.Windows.Forms.FlowLayoutPanel();
        this._lblDelay = new System.Windows.Forms.Label();
        this._delayMs = new System.Windows.Forms.NumericUpDown();
        this._rowTimeout = new System.Windows.Forms.FlowLayoutPanel();
        this._lblTimeout = new System.Windows.Forms.Label();
        this._timeoutMs = new System.Windows.Forms.NumericUpDown();
        this._lblLink = new System.Windows.Forms.Label();
        this._link = new System.Windows.Forms.ComboBox();
        this._buttons = new System.Windows.Forms.FlowLayoutPanel();
        this._save = new System.Windows.Forms.Button();
        this._keys = new System.Windows.Forms.Button();
        this._exportProfile = new System.Windows.Forms.Button();
        this._shareProfile = new System.Windows.Forms.Button();
        this._about = new System.Windows.Forms.Button();
        this._close = new System.Windows.Forms.Button();
        this._root.SuspendLayout();
        this._tabs.SuspendLayout();
        this._tabProfile.SuspendLayout();
        this._pageProfile.SuspendLayout();
        this._avatarRow.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this._avatarPreview)).BeginInit();
        this._avatarBtns.SuspendLayout();
        this._tabNetwork.SuspendLayout();
        this._pageNetwork.SuspendLayout();
        this._tabRouting.SuspendLayout();
        this._pageRouting.SuspendLayout();
        this._rowHops.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this._hops)).BeginInit();
        this._rowAttempts.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this._attempts)).BeginInit();
        this._rowDelay.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this._delayMs)).BeginInit();
        this._rowTimeout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this._timeoutMs)).BeginInit();
        this._buttons.SuspendLayout();
        this.SuspendLayout();
        // 
        // _root
        // 
        this._root.ColumnCount = 1;
        this._root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._root.Controls.Add(this._tabs, 0, 0);
        this._root.Controls.Add(this._buttons, 0, 1);
        this._root.Dock = System.Windows.Forms.DockStyle.Fill;
        this._root.Location = new System.Drawing.Point(0, 0);
        this._root.Name = "_root";
        this._root.Padding = new System.Windows.Forms.Padding(12);
        this._root.RowCount = 2;
        this._root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._root.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._root.Size = new System.Drawing.Size(780, 520);
        this._root.TabIndex = 0;
        // 
        // _tabs
        // 
        this._tabs.Controls.Add(this._tabProfile);
        this._tabs.Controls.Add(this._tabNetwork);
        this._tabs.Controls.Add(this._tabRouting);
        this._tabs.Dock = System.Windows.Forms.DockStyle.Fill;
        this._tabs.Location = new System.Drawing.Point(15, 15);
        this._tabs.Name = "_tabs";
        this._tabs.SelectedIndex = 0;
        this._tabs.Size = new System.Drawing.Size(750, 396);
        this._tabs.TabIndex = 0;
        // 
        // _tabProfile
        // 
        this._tabProfile.Controls.Add(this._pageProfile);
        this._tabProfile.Location = new System.Drawing.Point(4, 30);
        this._tabProfile.Name = "_tabProfile";
        this._tabProfile.Size = new System.Drawing.Size(742, 362);
        this._tabProfile.TabIndex = 0;
        this._tabProfile.Text = "Профиль";
        this._tabProfile.UseVisualStyleBackColor = true;
        // 
        // _pageProfile
        // 
        this._pageProfile.AutoScroll = true;
        this._pageProfile.ColumnCount = 1;
        this._pageProfile.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._pageProfile.Controls.Add(this._profile);
        this._pageProfile.Controls.Add(this._avatarRow);
        this._pageProfile.Controls.Add(this._aboutLabel);
        this._pageProfile.Controls.Add(this._aboutCounter);
        this._pageProfile.Controls.Add(this._aboutMe, 0, 3);
        this._pageProfile.Dock = System.Windows.Forms.DockStyle.Fill;
        this._pageProfile.Location = new System.Drawing.Point(0, 0);
        this._pageProfile.Name = "_pageProfile";
        this._pageProfile.Padding = new System.Windows.Forms.Padding(5);
        this._pageProfile.RowCount = 6;
        this._pageProfile.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageProfile.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageProfile.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageProfile.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageProfile.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageProfile.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._pageProfile.Size = new System.Drawing.Size(742, 362);
        this._pageProfile.TabIndex = 0;
        // 
        // _profile
        // 
        this._profile.AutoSize = true;
        this._profile.Location = new System.Drawing.Point(8, 5);
        this._profile.Margin = new System.Windows.Forms.Padding(3, 0, 3, 12);
        this._profile.Name = "_profile";
        this._profile.Size = new System.Drawing.Size(0, 21);
        this._profile.TabIndex = 0;
        // 
        // _avatarRow
        // 
        this._avatarRow.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
        this._avatarRow.AutoSize = true;
        this._avatarRow.Controls.Add(this._avatarPreview);
        this._avatarRow.Controls.Add(this._avatarBtns);
        this._avatarRow.Location = new System.Drawing.Point(8, 38);
        this._avatarRow.Margin = new System.Windows.Forms.Padding(3, 0, 3, 5);
        this._avatarRow.Name = "_avatarRow";
        this._avatarRow.Size = new System.Drawing.Size(726, 128);
        this._avatarRow.TabIndex = 1;
        this._avatarRow.WrapContents = false;
        // 
        // _avatarPreview
        // 
        this._avatarPreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
        this._avatarPreview.Location = new System.Drawing.Point(3, 3);
        this._avatarPreview.Name = "_avatarPreview";
        this._avatarPreview.Size = new System.Drawing.Size(129, 122);
        this._avatarPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        this._avatarPreview.TabIndex = 0;
        this._avatarPreview.TabStop = false;
        // 
        // _avatarBtns
        // 
        this._avatarBtns.AutoSize = true;
        this._avatarBtns.Controls.Add(this._loadAvatar);
        this._avatarBtns.Controls.Add(this._clearAvatar);
        this._avatarBtns.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
        this._avatarBtns.Location = new System.Drawing.Point(138, 3);
        this._avatarBtns.Name = "_avatarBtns";
        this._avatarBtns.Padding = new System.Windows.Forms.Padding(12, 0, 0, 0);
        this._avatarBtns.Size = new System.Drawing.Size(178, 82);
        this._avatarBtns.TabIndex = 1;
        this._avatarBtns.WrapContents = false;
        // 
        // _loadAvatar
        // 
        this._loadAvatar.AutoSize = true;
        this._loadAvatar.Location = new System.Drawing.Point(15, 3);
        this._loadAvatar.Name = "_loadAvatar";
        this._loadAvatar.Size = new System.Drawing.Size(160, 35);
        this._loadAvatar.TabIndex = 0;
        this._loadAvatar.Text = "Выбрать аватар…";
        // 
        // _clearAvatar
        // 
        this._clearAvatar.AutoSize = true;
        this._clearAvatar.Location = new System.Drawing.Point(15, 44);
        this._clearAvatar.Name = "_clearAvatar";
        this._clearAvatar.Size = new System.Drawing.Size(160, 35);
        this._clearAvatar.TabIndex = 1;
        this._clearAvatar.Text = "Убрать аватар";
        // 
        // _aboutLabel
        // 
        this._aboutLabel.AutoSize = true;
        this._aboutLabel.Location = new System.Drawing.Point(8, 171);
        this._aboutLabel.Name = "_aboutLabel";
        this._aboutLabel.Size = new System.Drawing.Size(197, 21);
        this._aboutLabel.TabIndex = 2;
        this._aboutLabel.Text = "О себе (до 250 символов):";
        // 
        // _aboutCounter
        // 
        this._aboutCounter.AutoSize = true;
        this._aboutCounter.ForeColor = System.Drawing.SystemColors.GrayText;
        this._aboutCounter.Location = new System.Drawing.Point(8, 355);
        this._aboutCounter.Name = "_aboutCounter";
        this._aboutCounter.Size = new System.Drawing.Size(0, 21);
        this._aboutCounter.TabIndex = 4;
        // 
        // _aboutMe
        // 
        this._aboutMe.Location = new System.Drawing.Point(8, 195);
        this._aboutMe.MaxLength = 250;
        this._aboutMe.Multiline = true;
        this._aboutMe.Name = "_aboutMe";
        this._aboutMe.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this._aboutMe.Size = new System.Drawing.Size(726, 157);
        this._aboutMe.TabIndex = 3;
        // 
        // _tabNetwork
        // 
        this._tabNetwork.Controls.Add(this._pageNetwork);
        this._tabNetwork.Location = new System.Drawing.Point(4, 30);
        this._tabNetwork.Name = "_tabNetwork";
        this._tabNetwork.Size = new System.Drawing.Size(742, 362);
        this._tabNetwork.TabIndex = 1;
        this._tabNetwork.Text = "Сеть";
        this._tabNetwork.UseVisualStyleBackColor = true;
        // 
        // _pageNetwork
        // 
        this._pageNetwork.AutoScroll = true;
        this._pageNetwork.ColumnCount = 1;
        this._pageNetwork.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._pageNetwork.Controls.Add(this._lan);
        this._pageNetwork.Controls.Add(this._shareRoutes);
        this._pageNetwork.Controls.Add(this._visibleForSearch);
        this._pageNetwork.Controls.Add(this._lblUdpCaption);
        this._pageNetwork.Controls.Add(this._udpPort);
        this._pageNetwork.Controls.Add(this._lblEconomy);
        this._pageNetwork.Controls.Add(this._economy);
        this._pageNetwork.Controls.Add(this._economyHint);
        this._pageNetwork.Controls.Add(this._lblNoBle);
        this._pageNetwork.Dock = System.Windows.Forms.DockStyle.Fill;
        this._pageNetwork.Location = new System.Drawing.Point(0, 0);
        this._pageNetwork.Name = "_pageNetwork";
        this._pageNetwork.Padding = new System.Windows.Forms.Padding(12);
        this._pageNetwork.RowCount = 10;
        this._pageNetwork.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageNetwork.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageNetwork.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageNetwork.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageNetwork.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageNetwork.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageNetwork.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageNetwork.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageNetwork.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageNetwork.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._pageNetwork.Size = new System.Drawing.Size(742, 362);
        this._pageNetwork.TabIndex = 0;
        // 
        // _lan
        // 
        this._lan.AutoSize = true;
        this._lan.Location = new System.Drawing.Point(15, 15);
        this._lan.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this._lan.Name = "_lan";
        this._lan.Size = new System.Drawing.Size(104, 25);
        this._lan.TabIndex = 0;
        this._lan.Text = "LAN (UDP)";
        // 
        // _shareRoutes
        // 
        this._shareRoutes.AutoSize = true;
        this._shareRoutes.Location = new System.Drawing.Point(15, 51);
        this._shareRoutes.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this._shareRoutes.Name = "_shareRoutes";
        this._shareRoutes.Size = new System.Drawing.Size(283, 25);
        this._shareRoutes.TabIndex = 1;
        this._shareRoutes.Text = "Делиться маршрутами (PeerSearch)";
        // 
        // _visibleForSearch
        // 
        this._visibleForSearch.AutoSize = true;
        this._visibleForSearch.Checked = true;
        this._visibleForSearch.CheckState = System.Windows.Forms.CheckState.Checked;
        this._visibleForSearch.Location = new System.Drawing.Point(15, 87);
        this._visibleForSearch.Margin = new System.Windows.Forms.Padding(3, 3, 3, 20);
        this._visibleForSearch.Name = "_visibleForSearch";
        this._visibleForSearch.Size = new System.Drawing.Size(180, 25);
        this._visibleForSearch.TabIndex = 2;
        this._visibleForSearch.Text = "Visible for search";
        // 
        // _lblUdpCaption
        // 
        this._lblUdpCaption.AutoSize = true;
        this._lblUdpCaption.Location = new System.Drawing.Point(15, 132);
        this._lblUdpCaption.Name = "_lblUdpCaption";
        this._lblUdpCaption.Size = new System.Drawing.Size(138, 21);
        this._lblUdpCaption.TabIndex = 2;
        this._lblUdpCaption.Text = "UDP-порт данных";
        // 
        // _udpPort
        // 
        this._udpPort.AutoSize = true;
        this._udpPort.Location = new System.Drawing.Point(15, 117);
        this._udpPort.Margin = new System.Windows.Forms.Padding(3, 0, 3, 16);
        this._udpPort.Name = "_udpPort";
        this._udpPort.Size = new System.Drawing.Size(0, 21);
        this._udpPort.TabIndex = 3;
        // 
        // _lblEconomy
        // 
        this._lblEconomy.AutoSize = true;
        this._lblEconomy.Location = new System.Drawing.Point(15, 154);
        this._lblEconomy.Name = "_lblEconomy";
        this._lblEconomy.Size = new System.Drawing.Size(147, 21);
        this._lblEconomy.TabIndex = 4;
        this._lblEconomy.Text = "Экономия трафика";
        // 
        // _economy
        // 
        this._economy.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this._economy.Location = new System.Drawing.Point(15, 178);
        this._economy.Margin = new System.Windows.Forms.Padding(3, 3, 3, 16);
        this._economy.Name = "_economy";
        this._economy.Size = new System.Drawing.Size(440, 29);
        this._economy.TabIndex = 5;
        // 
        // _economyHint
        // 
        this._economyHint.AutoSize = true;
        this._economyHint.ForeColor = System.Drawing.SystemColors.GrayText;
        this._economyHint.Location = new System.Drawing.Point(15, 231);
        this._economyHint.Margin = new System.Windows.Forms.Padding(3, 8, 3, 4);
        this._economyHint.MaximumSize = new System.Drawing.Size(680, 0);
        this._economyHint.Name = "_economyHint";
        this._economyHint.Size = new System.Drawing.Size(0, 21);
        this._economyHint.TabIndex = 6;
        // 
        // _lblNoBle
        // 
        this._lblNoBle.AutoSize = true;
        this._lblNoBle.ForeColor = System.Drawing.SystemColors.GrayText;
        this._lblNoBle.Location = new System.Drawing.Point(15, 264);
        this._lblNoBle.Margin = new System.Windows.Forms.Padding(3, 8, 3, 3);
        this._lblNoBle.MaximumSize = new System.Drawing.Size(680, 0);
        this._lblNoBle.Name = "_lblNoBle";
        this._lblNoBle.Size = new System.Drawing.Size(436, 21);
        this._lblNoBle.TabIndex = 7;
        this._lblNoBle.Text = "Bluetooth недоступен в версии для старых Windows-систем";
        // 
        // _tabRouting
        // 
        this._tabRouting.Controls.Add(this._pageRouting);
        this._tabRouting.Location = new System.Drawing.Point(4, 30);
        this._tabRouting.Name = "_tabRouting";
        this._tabRouting.Size = new System.Drawing.Size(742, 362);
        this._tabRouting.TabIndex = 2;
        this._tabRouting.Text = "Маршрутизация";
        this._tabRouting.UseVisualStyleBackColor = true;
        // 
        // _pageRouting
        // 
        this._pageRouting.AutoScroll = true;
        this._pageRouting.ColumnCount = 1;
        this._pageRouting.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._pageRouting.Controls.Add(this._rowHops);
        this._pageRouting.Controls.Add(this._rowAttempts);
        this._pageRouting.Controls.Add(this._rowDelay);
        this._pageRouting.Controls.Add(this._rowTimeout);
        this._pageRouting.Controls.Add(this._lblLink);
        this._pageRouting.Controls.Add(this._link);
        this._pageRouting.Dock = System.Windows.Forms.DockStyle.Fill;
        this._pageRouting.Location = new System.Drawing.Point(0, 0);
        this._pageRouting.Name = "_pageRouting";
        this._pageRouting.Padding = new System.Windows.Forms.Padding(12);
        this._pageRouting.RowCount = 7;
        this._pageRouting.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageRouting.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageRouting.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageRouting.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageRouting.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageRouting.RowStyles.Add(new System.Windows.Forms.RowStyle());
        this._pageRouting.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._pageRouting.Size = new System.Drawing.Size(742, 362);
        this._pageRouting.TabIndex = 0;
        // 
        // _rowHops
        // 
        this._rowHops.AutoSize = true;
        this._rowHops.Controls.Add(this._lblHops);
        this._rowHops.Controls.Add(this._hops);
        this._rowHops.Location = new System.Drawing.Point(15, 15);
        this._rowHops.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this._rowHops.Name = "_rowHops";
        this._rowHops.Size = new System.Drawing.Size(305, 35);
        this._rowHops.TabIndex = 0;
        // 
        // _lblHops
        // 
        this._lblHops.AutoSize = true;
        this._lblHops.Location = new System.Drawing.Point(3, 0);
        this._lblHops.Name = "_lblHops";
        this._lblHops.Padding = new System.Windows.Forms.Padding(0, 6, 8, 0);
        this._lblHops.Size = new System.Drawing.Size(213, 27);
        this._lblHops.TabIndex = 0;
        this._lblHops.Text = "Макс. глубина поиска (1–3)";
        // 
        // _hops
        // 
        this._hops.Location = new System.Drawing.Point(222, 3);
        this._hops.Maximum = new decimal(new int[] { 3, 0, 0, 0 });
        this._hops.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        this._hops.Name = "_hops";
        this._hops.Size = new System.Drawing.Size(80, 29);
        this._hops.TabIndex = 1;
        this._hops.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // _rowAttempts
        // 
        this._rowAttempts.AutoSize = true;
        this._rowAttempts.Controls.Add(this._lblAttempts);
        this._rowAttempts.Controls.Add(this._attempts);
        this._rowAttempts.Location = new System.Drawing.Point(15, 61);
        this._rowAttempts.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this._rowAttempts.Name = "_rowAttempts";
        this._rowAttempts.Size = new System.Drawing.Size(319, 35);
        this._rowAttempts.TabIndex = 1;
        // 
        // _lblAttempts
        // 
        this._lblAttempts.AutoSize = true;
        this._lblAttempts.Location = new System.Drawing.Point(3, 0);
        this._lblAttempts.Name = "_lblAttempts";
        this._lblAttempts.Padding = new System.Windows.Forms.Padding(0, 6, 8, 0);
        this._lblAttempts.Size = new System.Drawing.Size(227, 27);
        this._lblAttempts.TabIndex = 0;
        this._lblAttempts.Text = "Повторы поиска при ошибке";
        // 
        // _attempts
        // 
        this._attempts.Location = new System.Drawing.Point(236, 3);
        this._attempts.Maximum = new decimal(new int[] { 20, 0, 0, 0 });
        this._attempts.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
        this._attempts.Name = "_attempts";
        this._attempts.Size = new System.Drawing.Size(80, 29);
        this._attempts.TabIndex = 1;
        this._attempts.Value = new decimal(new int[] { 1, 0, 0, 0 });
        // 
        // _rowDelay
        // 
        this._rowDelay.AutoSize = true;
        this._rowDelay.Controls.Add(this._lblDelay);
        this._rowDelay.Controls.Add(this._delayMs);
        this._rowDelay.Location = new System.Drawing.Point(15, 107);
        this._rowDelay.Margin = new System.Windows.Forms.Padding(3, 3, 3, 8);
        this._rowDelay.Name = "_rowDelay";
        this._rowDelay.Size = new System.Drawing.Size(354, 35);
        this._rowDelay.TabIndex = 2;
        // 
        // _lblDelay
        // 
        this._lblDelay.AutoSize = true;
        this._lblDelay.Location = new System.Drawing.Point(3, 0);
        this._lblDelay.Name = "_lblDelay";
        this._lblDelay.Padding = new System.Windows.Forms.Padding(0, 6, 8, 0);
        this._lblDelay.Size = new System.Drawing.Size(222, 27);
        this._lblDelay.TabIndex = 0;
        this._lblDelay.Text = "Пауза между попытками, мс";
        // 
        // _delayMs
        // 
        this._delayMs.Increment = new decimal(new int[] { 1000, 0, 0, 0 });
        this._delayMs.Location = new System.Drawing.Point(231, 3);
        this._delayMs.Maximum = new decimal(new int[] { 3600000, 0, 0, 0 });
        this._delayMs.Name = "_delayMs";
        this._delayMs.Size = new System.Drawing.Size(120, 29);
        this._delayMs.TabIndex = 1;
        // 
        // _rowTimeout
        // 
        this._rowTimeout.AutoSize = true;
        this._rowTimeout.Controls.Add(this._lblTimeout);
        this._rowTimeout.Controls.Add(this._timeoutMs);
        this._rowTimeout.Location = new System.Drawing.Point(15, 153);
        this._rowTimeout.Margin = new System.Windows.Forms.Padding(3, 3, 3, 20);
        this._rowTimeout.Name = "_rowTimeout";
        this._rowTimeout.Size = new System.Drawing.Size(273, 35);
        this._rowTimeout.TabIndex = 3;
        // 
        // _lblTimeout
        // 
        this._lblTimeout.AutoSize = true;
        this._lblTimeout.Location = new System.Drawing.Point(3, 0);
        this._lblTimeout.Name = "_lblTimeout";
        this._lblTimeout.Padding = new System.Windows.Forms.Padding(0, 6, 8, 0);
        this._lblTimeout.Size = new System.Drawing.Size(141, 27);
        this._lblTimeout.TabIndex = 0;
        this._lblTimeout.Text = "Таймаут FIND, мс";
        // 
        // _timeoutMs
        // 
        this._timeoutMs.Increment = new decimal(new int[] { 500, 0, 0, 0 });
        this._timeoutMs.Location = new System.Drawing.Point(150, 3);
        this._timeoutMs.Maximum = new decimal(new int[] { 120000, 0, 0, 0 });
        this._timeoutMs.Minimum = new decimal(new int[] { 500, 0, 0, 0 });
        this._timeoutMs.Name = "_timeoutMs";
        this._timeoutMs.Size = new System.Drawing.Size(120, 29);
        this._timeoutMs.TabIndex = 1;
        this._timeoutMs.Value = new decimal(new int[] { 500, 0, 0, 0 });
        // 
        // _lblLink
        // 
        this._lblLink.AutoSize = true;
        this._lblLink.Location = new System.Drawing.Point(15, 208);
        this._lblLink.Name = "_lblLink";
        this._lblLink.Size = new System.Drawing.Size(182, 21);
        this._lblLink.TabIndex = 4;
        this._lblLink.Text = "Пресет скорости канала";
        // 
        // _link
        // 
        this._link.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        this._link.Location = new System.Drawing.Point(15, 232);
        this._link.Name = "_link";
        this._link.Size = new System.Drawing.Size(440, 29);
        this._link.TabIndex = 5;
        // 
        // _buttons
        // 
        this._buttons.AutoSize = true;
        this._buttons.Controls.Add(this._save);
        this._buttons.Controls.Add(this._keys);
        this._buttons.Controls.Add(this._exportProfile);
        this._buttons.Controls.Add(this._shareProfile);
        this._buttons.Controls.Add(this._about);
        this._buttons.Controls.Add(this._close);
        this._buttons.Dock = System.Windows.Forms.DockStyle.Fill;
        this._buttons.Location = new System.Drawing.Point(15, 423);
        this._buttons.Margin = new System.Windows.Forms.Padding(3, 9, 3, 3);
        this._buttons.Name = "_buttons";
        this._buttons.Size = new System.Drawing.Size(750, 82);
        this._buttons.TabIndex = 1;
        // 
        // _save
        // 
        this._save.AutoSize = true;
        this._save.Location = new System.Drawing.Point(3, 3);
        this._save.Name = "_save";
        this._save.Size = new System.Drawing.Size(110, 35);
        this._save.TabIndex = 0;
        this._save.Text = "Сохранить";
        // 
        // _keys
        // 
        this._keys.AutoSize = true;
        this._keys.Location = new System.Drawing.Point(119, 3);
        this._keys.Name = "_keys";
        this._keys.Size = new System.Drawing.Size(170, 35);
        this._keys.TabIndex = 1;
        this._keys.Text = "Копировать ключи";
        // 
        // _exportProfile
        // 
        this._exportProfile.AutoSize = true;
        this._exportProfile.Location = new System.Drawing.Point(295, 3);
        this._exportProfile.Name = "_exportProfile";
        this._exportProfile.Size = new System.Drawing.Size(193, 35);
        this._exportProfile.TabIndex = 2;
        this._exportProfile.Text = "Экспорт профиля (.tlp)…";
        // 
        // _shareProfile
        // 
        this._shareProfile.AutoSize = true;
        this._shareProfile.Location = new System.Drawing.Point(494, 3);
        this._shareProfile.Name = "_shareProfile";
        this._shareProfile.Size = new System.Drawing.Size(194, 35);
        this._shareProfile.TabIndex = 3;
        this._shareProfile.Text = "Поделиться профилем…";
        // 
        // _about
        // 
        this._about.AutoSize = true;
        this._about.Location = new System.Drawing.Point(3, 44);
        this._about.Name = "_about";
        this._about.Size = new System.Drawing.Size(130, 35);
        this._about.TabIndex = 4;
        this._about.Text = "О программе";
        // 
        // _close
        // 
        this._close.AutoSize = true;
        this._close.DialogResult = System.Windows.Forms.DialogResult.OK;
        this._close.Location = new System.Drawing.Point(139, 44);
        this._close.Name = "_close";
        this._close.Size = new System.Drawing.Size(100, 35);
        this._close.TabIndex = 5;
        this._close.Text = "Закрыть";
        // 
        // SettingsForm
        // 
        this.AcceptButton = this._close;
        this.ClientSize = new System.Drawing.Size(780, 520);
        this.Controls.Add(this._root);
        this.MinimizeBox = false;
        this.MinimumSize = new System.Drawing.Size(640, 460);
        this.Name = "SettingsForm";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Настройки";
        this._root.ResumeLayout(false);
        this._root.PerformLayout();
        this._tabs.ResumeLayout(false);
        this._tabProfile.ResumeLayout(false);
        this._pageProfile.ResumeLayout(false);
        this._pageProfile.PerformLayout();
        this._avatarRow.ResumeLayout(false);
        this._avatarRow.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this._avatarPreview)).EndInit();
        this._avatarBtns.ResumeLayout(false);
        this._avatarBtns.PerformLayout();
        this._tabNetwork.ResumeLayout(false);
        this._pageNetwork.ResumeLayout(false);
        this._pageNetwork.PerformLayout();
        this._tabRouting.ResumeLayout(false);
        this._pageRouting.ResumeLayout(false);
        this._pageRouting.PerformLayout();
        this._rowHops.ResumeLayout(false);
        this._rowHops.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this._hops)).EndInit();
        this._rowAttempts.ResumeLayout(false);
        this._rowAttempts.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this._attempts)).EndInit();
        this._rowDelay.ResumeLayout(false);
        this._rowDelay.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this._delayMs)).EndInit();
        this._rowTimeout.ResumeLayout(false);
        this._rowTimeout.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)(this._timeoutMs)).EndInit();
        this._buttons.ResumeLayout(false);
        this._buttons.PerformLayout();
        this.ResumeLayout(false);
    }
}
