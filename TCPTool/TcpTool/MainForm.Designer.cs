using System.ComponentModel;

namespace TcpTool;

public partial class MainForm
{
    private IContainer components = null!;

    // Common font for consistent appearance
    private readonly Font uiFont = new("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

    // Server controls
    private Label lblServerPort = null!;
    private TextBox txtServerPort = null!;
    private Button btnStartServer = null!;
    private Button btnStopServer = null!;
    private CheckBox chkServerEcho = null!;
    private CheckBox chkServerDefault = null!;
    private TextBox txtServerLog = null!;
    private TextBox txtServerSend = null!;
    private Button btnServerSend = null!;
    private CheckBox chkServerSendHex = null!;

    // Client controls
    private Label lblClientHost = null!;
    private TextBox txtClientHost = null!;
    private Label lblClientPort = null!;
    private TextBox txtClientPort = null!;
    private Button btnClientConnect = null!;
    private Button btnClientDisconnect = null!;
    private TextBox txtClientSend = null!;
    private Button btnClientSend = null!;
    private CheckBox chkClientSendHex = null!;
    private TextBox txtClientLog = null!;

    // Tabs
    private TabControl tabs = null!;
    private TabPage tabServer = null!;
    private TabPage tabClient = null!;

    // Devices UI (Server tab right side)
    private Panel pnlServerRight = null!;
    private ListBox lstDevices = null!;
    private Button btnAddDevice = null!;
    private Button btnRemoveDevice = null!;
    private Label lblActiveDevice = null!;
    private DataGridView dgvDevices = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new Container();

        // Application window title
        Text = "TCP Server-Client & Data Parsing Tool .NET 8";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = true;
        Width = 1000;
        Height = 715;
        Font = uiFont;
        AutoScaleMode = AutoScaleMode.Font;

        tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Alignment = TabAlignment.Top,
            Appearance = TabAppearance.Normal
        };

        tabServer = new TabPage("TCP Server") { Padding = new Padding(10) };
        tabClient = new TabPage("TCP Client") { Padding = new Padding(10) };
        // default positions (may be overridden per-tab)
        tabs.TabPages.Add(tabServer);
        tabs.TabPages.Add(tabClient);

        // ===== Server Tab =====
        // Strict separation via 2-column TableLayoutPanel: left content | right devices
        var tlServer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0)
        };
        tlServer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tlServer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400F));
        tabServer.Controls.Add(tlServer);

        // Left content panel
        var pnlServer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 8, 0), AutoScroll = true };

        // Right devices panel
        // Right panel should not auto-scroll so the DataGridView can provide its own scrollbars
        pnlServerRight = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            AutoScroll = false
        };

        tlServer.Controls.Add(pnlServer, 0, 0);
        tlServer.Controls.Add(pnlServerRight, 1, 0);

        lblServerPort = new Label { AutoSize = true, Text = "Port:", Left = 15, Top = 20 };
        txtServerPort = new TextBox { Left = 70, Top = 15, Width = 110, Text = "5000" };
        btnStartServer = new Button { Left = 190, Top = 13, Width = 90, Height = 30, Text = "Start" };
        btnStopServer = new Button { Left = 290, Top = 13, Width = 90, Height = 30, Text = "Stop", Enabled = false };
        chkServerEcho = new CheckBox { AutoSize = true, Left = 390, Top = 18, Text = "Echo" };
        chkServerDefault = new CheckBox { AutoSize = true, Text = "Default" };

        pnlServer.Controls.Add(lblServerPort);
        pnlServer.Controls.Add(txtServerPort);
        pnlServer.Controls.Add(btnStartServer);
        pnlServer.Controls.Add(btnStopServer);
        // Single-line input with Send button on the left
        btnServerSend = new Button { Left = 15, Top = 83, Width = 90, Height = 30, Text = "Send", Anchor = AnchorStyles.Top | AnchorStyles.Left };
        chkServerSendHex = new CheckBox { Left = 110, Top = 88, AutoSize = true, Text = "Hex", Checked = false };
        txtServerSend = new TextBox
        {
            Left = 180,
            Top = 85,
            Width = 280,
            Height = 27,
            Multiline = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BorderStyle = BorderStyle.Fixed3D
        };
        txtServerLog = new TextBox
        {
            Left = 15,
            Top = 140,
            Width = 443,
            Height = 495,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
            TabStop = true,
            HideSelection = false
        };

        pnlServer.Controls.Add(chkServerEcho);
        pnlServer.Controls.Add(chkServerSendHex);
        pnlServer.Controls.Add(txtServerSend);
        pnlServer.Controls.Add(btnServerSend);
        pnlServer.Controls.Add(txtServerLog);

        // Right panel for Devices (selector + Manage button)
        // Header label for devices
        var lblDevices = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            Text = "Devices",
            Font = new Font(uiFont, FontStyle.Bold),
            Padding = new Padding(10, 8, 8, 6),
            AutoSize = false,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };

        var pnlDevicesInner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 40, 8, 0) };

        // Active device label kept for code references but hidden in the UI
        lblActiveDevice = new Label { Text = "Active device: (none)", Dock = DockStyle.Top, Height = 22, Visible = false };

        // Numbered grid (read-only overview) - make it fill the available space above the buttons
        dgvDevices = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, Margin = new Padding(0, 4, 0, 0) };
        dgvDevices.AutoGenerateColumns = false;
        dgvDevices.RowHeadersVisible = false;
        var colMark = new DataGridViewTextBoxColumn
        {
            HeaderText = "",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 30,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Padding = new Padding(0, 4, 0, 4),
                ForeColor = Color.DodgerBlue,
                SelectionBackColor = Color.LightBlue,
                SelectionForeColor = Color.DarkBlue,
                WrapMode = DataGridViewTriState.False
            }
        };
        var colName = new DataGridViewTextBoxColumn
        {
            HeaderText = "Name",
            DataPropertyName = "Name",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 44f,
            MinimumWidth = 120,
            ReadOnly = true
        };
        
        var colFormat = new DataGridViewTextBoxColumn
        {
           HeaderText = "Format",
           DataPropertyName = "Format",
           AutoSizeMode = DataGridViewAutoSizeColumnMode.None, 
           Width = 120,
           ReadOnly = true
        };
        dgvDevices.Columns.AddRange(new DataGridViewColumn[] { colMark, colName, colFormat });
        // NOTE: do not add dgvDevices here; add the buttons first so the grid can Dock=Fill and occupy space above the buttons
        // Ensure the grid itself shows a vertical scrollbar and use slightly taller rows with padding for readability
        dgvDevices.ScrollBars = ScrollBars.Vertical;
        dgvDevices.RowTemplate.Height = 36; // slightly taller so text descenders are visible
        dgvDevices.DefaultCellStyle.Padding = new Padding(8, 6, 8, 6); // increased top/bottom padding
        dgvDevices.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
        dgvDevices.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvDevices.MultiSelect = false;
        dgvDevices.AllowUserToResizeColumns = false;
        dgvDevices.AllowUserToResizeRows = false;
        dgvDevices.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        dgvDevices.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
        dgvDevices.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        dgvDevices.BackgroundColor = SystemColors.Window;
        dgvDevices.BorderStyle = BorderStyle.Fixed3D;

        // Legacy ListBox kept but hidden; DataGridView is primary and scrollable
        lstDevices = new ListBox { Dock = DockStyle.Fill, Visible = false };
        // Bottom panel for action buttons so they remain visible inside the devices inner panel
        var pnlDeviceButtons = new Panel { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(0) };
        // Use an auto-sized FlowLayoutPanel and center it inside the bottom panel so controls remain centered
        var fl = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0), Anchor = AnchorStyles.None };
        // Align checkbox and buttons with the same vertical margin so they appear level
        chkServerDefault.Margin = new Padding(6, 6, 6, 6);
        fl.Controls.Add(chkServerDefault);
        btnAddDevice = new Button { Width = 80, Height = 28, Text = "Add" };
        btnAddDevice.Margin = new Padding(6, 6, 6, 6);
        // Make Remove wider so the full text is visible
        btnRemoveDevice = new Button { Width = 92, Height = 28, Text = "Remove" };
        btnRemoveDevice.Margin = new Padding(6, 6, 6, 6);
        fl.Controls.Add(btnAddDevice);
        fl.Controls.Add(btnRemoveDevice);
        pnlDeviceButtons.Controls.Add(fl);
        // Keep the flow panel centered when the bottom panel resizes
        pnlDeviceButtons.Resize += (s, e) => { fl.Left = Math.Max(0, (pnlDeviceButtons.ClientSize.Width - fl.Width) / 2); fl.Top = Math.Max(0, (pnlDeviceButtons.ClientSize.Height - fl.Height) / 2); };

        // Add the grid first, then the buttons so the button panel draws on top and grid does not paint over it
        pnlDevicesInner.Controls.Add(dgvDevices);
        pnlDevicesInner.Controls.Add(pnlDeviceButtons);
        // Device list double-click is handled in InitializeDevicesSystem() to open the Device Manager
        // Add header first, then inner panel containing grid+buttons
        pnlServerRight.Controls.Add(lblDevices);
        pnlServerRight.Controls.Add(pnlDevicesInner);

        // open device manager when grid row is double-clicked (wired in code-behind)
        // Send button sits to the left of the input

        // ===== Client Tab =====
        var pnlClient = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        tabClient.Controls.Add(pnlClient);

        lblClientHost = new Label { AutoSize = true, Text = "Host:", Left = 15, Top = 20 };
        // match host/port left offset used on server side for vertical alignment
        txtClientHost = new TextBox { Left = 70, Top = 15, Width = 210, Text = "127.0.0.1" };
        lblClientPort = new Label { AutoSize = true, Text = "Port:", Left = 285, Top = 20 };
        txtClientPort = new TextBox { Left = 330, Top = 15, Width = 90, Text = "5000" };
        btnClientConnect = new Button { Left = 430, Top = 13, Width = 100, Height = 30, Text = "Connect" };
        // initial value removed; final sizing applied below

        // Single-line input with Send button on the left
        btnClientSend = new Button { Left = 15, Top = 83, Width = 90, Height = 30, Text = "Send", Anchor = AnchorStyles.Top | AnchorStyles.Left };
        chkClientSendHex = new CheckBox { Left = 110, Top = 88, AutoSize = true, Text = "Hex", Checked = false };
        txtClientSend = new TextBox { Left = 180, Top = 85, Width = 760, Height = 27, Multiline = false, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, BorderStyle = BorderStyle.Fixed3D };

        txtClientLog = new TextBox
        {
            Left = 15,
            Top = 140,
            Width = 657,
            Height = 495,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
            TabStop = true,
            HideSelection = false
        };

        pnlClient.Controls.Add(lblClientHost);
        pnlClient.Controls.Add(txtClientHost);
        // ensure consistent disconnect button sizing/position
        btnClientDisconnect = new Button { Left = 550, Top = 13, Width = 120, Height = 30, Text = "Disconnect", Enabled = false };
        pnlClient.Controls.Add(lblClientPort);
        pnlClient.Controls.Add(txtClientPort);
        pnlClient.Controls.Add(btnClientConnect);
        pnlClient.Controls.Add(btnClientDisconnect);
        pnlClient.Controls.Add(txtClientSend);
        pnlClient.Controls.Add(chkClientSendHex);
        pnlClient.Controls.Add(btnClientSend);
        pnlClient.Controls.Add(txtClientLog);
        // Send button sits to the left of the input

        Controls.Add(tabs);

        // No SplitContainer; right panel is docked Right with fixed width.

        // Wire up events
        btnStartServer.Click += btnStartServer_Click;
        btnStopServer.Click += btnStopServer_Click;
        btnClientConnect.Click += btnClientConnect_Click;
        btnClientDisconnect.Click += btnClientDisconnect_Click;
        btnClientSend.Click += btnClientSend_Click;
        // Server checkbox: when checked, send as Hex; when unchecked, send as Text
        chkServerSendHex.CheckedChanged += (s, e) => { btnServerSend.Text = chkServerSendHex.Checked ? "Send (Hex)" : "Send"; };
        chkClientSendHex.CheckedChanged += (s, e) => { btnClientSend.Text = chkClientSendHex.Checked ? "Send (Hex)" : "Send"; };
        txtServerSend.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true; e.SuppressKeyPress = true;
                btnServerSend.PerformClick();
                return;
            }
            if (e.Control && e.KeyCode == Keys.H)
            {
                chkServerSendHex.Checked = !chkServerSendHex.Checked; e.Handled = true; e.SuppressKeyPress = true;
            }
        };
        txtClientSend.KeyDown += txtClientSend_KeyDown;
        btnServerSend.Click += (s, e) =>
        {
            var text = txtServerSend.Text;
            if (string.IsNullOrEmpty(text)) return;
            // Checked = Hex string, Unchecked = Text/ASCII
            byte[] data = chkServerSendHex.Checked ? HexStringToBytes(text) : System.Text.Encoding.UTF8.GetBytes(text);
            foreach (var c in _clients.ToArray())
            {
                try { c.GetStream().Write(data, 0, data.Length); } catch { }
            }
            LogServer($"TX broadcast: {(chkServerSendHex.Checked ? MainForm.BytesToHex(data, data.Length) : text)}");
        };

        // Wire device UI events
        btnAddDevice.Click += (s, e) => OpenDeviceManagerForNew();
        btnRemoveDevice.Click += (s, e) => { RemoveSelectedDevice(); PopulateDeviceList(); };
        lstDevices.SelectedIndexChanged += (s, e) => LoadSelectedDeviceToUI();
        chkServerDefault.CheckedChanged += (s, e) => OnDefaultModeChanged(s, e);

        // ToolTip
        var tip = new ToolTip();
        tip.SetToolTip(chkServerDefault, "When checked, app runs in default TCP mode and device selection is disabled");
    }
}

