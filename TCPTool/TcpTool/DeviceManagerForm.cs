using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Text;

namespace TcpTool;

// Simple modal form for managing devices & parameters
internal class DeviceManagerForm : Form
{
    private readonly List<DeviceDefinition> _devices;
    private readonly Action<List<DeviceDefinition>> _onSave;
    private readonly Func<byte[]>? _getLastBytes;

    private ComboBox cboDevices = null!;
    private TextBox txtDeviceName = null!;
    private Button btnSave = null!;
    private ComboBox cboFormat = null!;
    private ComboBox cboEndian = null!;
    private DataGridView grid = null!;
    private Button btnCapture = null!;
    private Button btnClearExpected = null!;
    private TabControl tabControl = null!;
    private TabPage tabParsing = null!;
    private TabPage tabCases = null!;
    private TabPage tabReports = null!;
    private Button btnImportJson = null!;
    private Label lblImportJson = null!;
    
    // Reports tab controls
    private DataGridView gridReports = null!;
    private Button btnClearReports = null!;
    private Button btnExportReports = null!;

    private BindingList<FieldDef> _fields = null!;
    private DeviceDefinition? _selected;

    public DeviceManagerForm(List<DeviceDefinition> devices, Action<List<DeviceDefinition>> onSave, Func<byte[]>? getLastBytes = null)
    {
        _devices = devices;
        _onSave = onSave;
        _getLastBytes = getLastBytes;
        InitializeComponent();
        LoadDevices();
    }

    public DeviceManagerForm(List<DeviceDefinition> devices, Action<List<DeviceDefinition>> onSave, Func<byte[]>? getLastBytes, string? preselectName)
        : this(devices, onSave, getLastBytes)
    {
        if (!string.IsNullOrEmpty(preselectName))
        {
            // If the combo contains it, select the device so fields load; otherwise just prefill the name textbox
            if (cboDevices.Items.Contains(preselectName))
            {
                cboDevices.SelectedItem = preselectName;
                var d = Current();
                if (d != null) txtDeviceName.Text = d.Name;
            }
            else
            {
                txtDeviceName.Text = preselectName;
            }
        }
    }

    private void InitializeComponent()
    {
        Text = "Manage Devices";
        StartPosition = FormStartPosition.CenterParent;
        Font = new("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        // Allow the user to resize if needed; keep it primarily dialog-like
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        Width = 880;
        Height = 640;
        MinimumSize = new System.Drawing.Size(700, 420);

        // Top area: device selector + payload format using a table layout to guarantee order
        var tblTop = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Padding = new Padding(8)
        };
        tblTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var lblDevice = new Label { AutoSize = true, Text = "Device Name", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        // Editable name textbox shown at top; keep cboDevices hidden for internal indexing
        cboDevices = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Visible = false };
        txtDeviceName = new TextBox { Dock = DockStyle.Fill };

        var lblFmt = new Label { AutoSize = true, Text = "Payload Format", Anchor = AnchorStyles.Left };
        cboFormat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Anchor = AnchorStyles.Left };
        cboFormat.Items.AddRange(new object[] { PayloadFormat.HexDump, PayloadFormat.Binary });
        var lblEndian = new Label { AutoSize = true, Text = "Endianness", Anchor = AnchorStyles.Left };
        cboEndian = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Anchor = AnchorStyles.Left };
        cboEndian.Items.AddRange(new object[] { Endianness.Little, Endianness.Big });

        lblImportJson = new Label
        {
            Text = "Import JSON",
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(16, 0, 0, 0)
        };
        btnImportJson = new Button
        {
            Width = 32,
            Height = 32,
            Image = ResourceLoader.LoadImportJsonIcon(),
            ImageAlign = ContentAlignment.MiddleCenter,
            FlatStyle = FlatStyle.Standard,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(4, 0, 0, 0)
        };
        btnImportJson.Click += BtnImportJson_Click;

        var tlRow = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 6, Padding = new Padding(0) };
        tlRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Payload Format label
        tlRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Payload Format combo
        tlRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Endianness label
        tlRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Endianness combo
        tlRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Import JSON label
        tlRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); // Import JSON button
        tlRow.RowCount = 1;
        tlRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tlRow.Controls.Add(lblFmt, 0, 0);
        tlRow.Controls.Add(cboFormat, 1, 0);
        tlRow.Controls.Add(lblEndian, 2, 0);
        tlRow.Controls.Add(cboEndian, 3, 0);
        tlRow.Controls.Add(lblImportJson, 4, 0);
        tlRow.Controls.Add(btnImportJson, 5, 0);

        tblTop.RowCount = 3;
        tblTop.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tblTop.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tblTop.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tblTop.Controls.Add(lblDevice, 0, 0);
        tblTop.Controls.Add(txtDeviceName, 0, 1);
        // keep cboDevices populated but hidden
        tblTop.Controls.Add(cboDevices, 0, 1);
        tblTop.Controls.Add(tlRow, 0, 2);

        // Bottom panel with Save button (create this before TabControl so layout order is correct)
        var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(8, 6, 8, 6) };
        int btnH = 34;
        Font btnFont = new("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        btnCapture = new Button
        {
            Text = "Capture Last Received Data",
            AutoSize = false,
            Height = btnH,
            Width = 230,
            Font = btnFont,
            FlatStyle = FlatStyle.System,
            TextAlign = ContentAlignment.MiddleCenter
        };
        btnClearExpected = new Button
        {
            Text = "Clear",
            AutoSize = false,
            Height = btnH,
            Width = 140,
            Font = btnFont,
            FlatStyle = FlatStyle.System,
            TextAlign = ContentAlignment.MiddleCenter
        };
        btnSave = new Button
        {
            Text = "Save",
            AutoSize = false,
            Height = btnH,
            Width = 112,
            Font = btnFont,
            FlatStyle = FlatStyle.System,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            TextAlign = ContentAlignment.MiddleCenter
        };
        bottomPanel.Controls.Add(btnCapture);
        bottomPanel.Controls.Add(btnClearExpected);
        bottomPanel.Controls.Add(btnSave);
        void LayoutButtonsManual()
        {
            var centerY = (bottomPanel.ClientSize.Height - btnH) / 2;
            // align both buttons to same vertical position
            btnCapture.Left = 8;
            btnCapture.Top = centerY;
            btnClearExpected.Left = btnCapture.Right + 8;
            btnClearExpected.Top = centerY;
            btnSave.Left = bottomPanel.ClientSize.Width - btnSave.Width - 8;
            btnSave.Top = centerY;
        }
        bottomPanel.Resize += (s, e) => LayoutButtonsManual();
        bottomPanel.HandleCreated += (s, e) => LayoutButtonsManual();

        // Create TabControl with tabs
        tabControl = new TabControl { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 8, 0) };
        tabParsing = new TabPage("Parsing") { Padding = new Padding(8) };
        tabCases = new TabPage("Cases") { Padding = new Padding(8) };
        tabReports = new TabPage("Reports") { Padding = new Padding(8) };
        tabControl.TabPages.Add(tabParsing);
        tabControl.TabPages.Add(tabCases);
        tabControl.TabPages.Add(tabReports);

        // Main grid fills available space
        grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersWidth = 30,
            Margin = new Padding(0)
        };
        // Bind the visible "Meaning" column to the FieldDef.Meaning property so edits are saved
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Meaning", DataPropertyName = "Meaning" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Offset", DataPropertyName = "Offset" });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Length", DataPropertyName = "Length" });
        grid.Columns.Add(new DataGridViewComboBoxColumn { HeaderText = "Type", DataPropertyName = "Type", DataSource = Enum.GetValues(typeof(FieldType)) });

        // Add grid to the Parsing tab
        tabParsing.Controls.Add(grid);

        // Add placeholder for Cases tab (can be populated later with test case controls)
        var lblCasesPlaceholder = new Label
        {
            Text = "Test cases configuration will be added here",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = SystemColors.GrayText
        };
        tabCases.Controls.Add(lblCasesPlaceholder);

        // Create Reports tab content
        CreateReportsTab();

        // Add controls to the form in order (must be in this specific order)
        Controls.Add(bottomPanel);
        Controls.Add(tabControl);
        Controls.Add(tblTop);

        btnSave.Click += (s, e) => SaveAll();
        cboDevices.SelectedIndexChanged += (s, e) => LoadSelected();
        cboFormat.SelectedIndexChanged += (s, e) => { var d = Current(); if (d != null && cboFormat.SelectedItem != null) d.PayloadFormat = (PayloadFormat)cboFormat.SelectedItem; };
        cboEndian.SelectedIndexChanged += (s, e) => { var d = Current(); if (d != null && cboEndian.SelectedItem != null) d.Endian = (Endianness)cboEndian.SelectedItem; };
        btnCapture.Click += (s, e) => CaptureAndParseData();
        btnClearExpected.Click += (s, e) => ClearAllParameters();
        grid.SelectionChanged += (s, e) => UpdateCaptureButtonsState();
        UpdateCaptureButtonsState();
    }

    private void LoadDevices()
    {
        cboDevices.Items.Clear();
        foreach (var d in _devices) cboDevices.Items.Add(d.Name);
        // Do not auto-select first device to avoid pre-filling name unexpectedly
        if (_selected != null)
        {
            var name = _selected.Name;
            if (!string.IsNullOrEmpty(name) && cboDevices.Items.Contains(name))
                cboDevices.SelectedItem = name;
        }
        // If nothing is selected, keep it blank; caller may preselect by name
        if (cboDevices.SelectedIndex < 0)
        {
            LoadSelected();
        }
    }

    private void LoadSelected()
    {
        var d = Current();
        if (d == null)
        {
            if (cboFormat.Items.Count > 0) cboFormat.SelectedIndex = 0;
            grid.DataSource = null;
            if (txtDeviceName != null) txtDeviceName.Text = string.Empty;
            if (gridReports != null) gridReports.DataSource = null;
            return;
        }
        cboFormat.SelectedItem = d.PayloadFormat;
        cboEndian.SelectedItem = d.Endian;
        if (txtDeviceName != null) txtDeviceName.Text = d.Name;
        // Copy stored fields, prioritizing Meaning over Name for display, or copying Name to Meaning if Meaning is empty
        _fields = new BindingList<FieldDef>(d.Fields.Select(f => 
        {
            var newField = new FieldDef
            {
                Name = f.Name,
                // If Meaning is empty but Name is populated, use Name as Meaning for display
                Meaning = !string.IsNullOrEmpty(f.Meaning) ? f.Meaning : f.Name,
                Offset = f.Offset,
                Length = f.Length,
                Type = f.Type,
                Endian = f.Endian,
                ExpectedHex = f.ExpectedHex
            };
            return newField;
        }).ToList());
        grid.DataSource = _fields;
        
        // Refresh reports grid
        RefreshReportsGrid();
    }

    private void UpdateCaptureButtonsState()
    {
        btnCapture.Enabled = _getLastBytes != null;
        btnClearExpected.Enabled = false;
        
        if (_fields == null) return;
        
        // Enable Clear button if there are any parameters to clear
        btnClearExpected.Enabled = _fields.Count > 0;
    }

    private void ClearAllParameters()
    {
        if (_fields == null || _fields.Count == 0) return;
        
        // Show confirmation dialog
        var result = MessageBox.Show(
            "Are you sure you want to delete all parameters? This action cannot be undone.", 
            "Confirm Clear Parameters", 
            MessageBoxButtons.YesNo, 
            MessageBoxIcon.Question);
            
        if (result != DialogResult.Yes) return;
        
        // Clear all parameters
        _fields.Clear();
        grid.Refresh();
    }

    private void CreateReportsTab()
    {
        // Reports grid
        gridReports = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, // Changed from Fill to None
            RowHeadersWidth = 30,
            ReadOnly = true,
            Margin = new Padding(0, 0, 0, 40)
        };

        // Add columns to reports grid
        var timestampCol = new DataGridViewTextBoxColumn 
        { 
            HeaderText = "Timestamp", 
            DataPropertyName = "Timestamp",
            DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm:ss" },
            Width = 180,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        };
        timestampCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        gridReports.Columns.Add(timestampCol);
        
        var rawDataCol = new DataGridViewTextBoxColumn 
        { 
            HeaderText = "Raw Data", 
            DataPropertyName = "RawDataHex",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };
        rawDataCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        gridReports.Columns.Add(rawDataCol);

        // Add download icon column - fixed width
        var btnColumn = new DataGridViewImageColumn
        {
            HeaderText = "Export",
            Width = 80,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Image = ResourceLoader.LoadExportIcon(16),
            ImageLayout = DataGridViewImageCellLayout.Normal // 16px ikon net gösterim
        };
        btnColumn.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        gridReports.Columns.Add(btnColumn);

        // Handle button clicks - updated column index
        gridReports.CellClick += GridReports_CellClick;

        // Bottom panel for reports tab
        var reportsBottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            Padding = new Padding(8)
        };

        btnClearReports = new Button
        {
            Text = "Clear All Reports",
            Height = 24,
            Width = 120,
            Left = 0,
            Top = 8,
            FlatStyle = FlatStyle.System
        };
        btnClearReports.Click += BtnClearReports_Click;

        btnExportReports = new Button
        {
            Text = "Export All",
            Height = 24,
            Width = 100,
            Left = 130,
            Top = 8,
            FlatStyle = FlatStyle.System
        };
        btnExportReports.Click += BtnExportReports_Click;

        reportsBottomPanel.Controls.Add(btnClearReports);
        reportsBottomPanel.Controls.Add(btnExportReports);

        tabReports.Controls.Add(gridReports);
        tabReports.Controls.Add(reportsBottomPanel);
    }

    private void CaptureAndParseData()
    {
        if (_getLastBytes == null) 
        { 
            MessageBox.Show("No last-received data available.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Information); 
            return; 
        }

        var bytes = _getLastBytes();
        if (bytes == null || bytes.Length == 0) 
        { 
            MessageBox.Show("No recent received data available to capture from.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Information); 
            return; 
        }

        var device = Current();
        if (device == null || _fields == null || _fields.Count == 0)
        {
            MessageBox.Show("Please configure device parameters first.", "No Configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Validate device field definitions before attempting to parse
        var validationErrors = ValidateDeviceDefinition(device);
        if (validationErrors.Count > 0)
        {
            var msg = "Device configuration errors:\n" + string.Join("\n", validationErrors);
            MessageBox.Show(msg, "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var report = ParseData(bytes, device);
            device.Reports.Add(report);
            
            _onSave(_devices);
            
            RefreshReportsGrid();
            
            // Switch to Reports tab to show the result
            tabControl.SelectedTab = tabReports;
            
            MessageBox.Show($"Data parsed successfully! Found {report.ParsedFields.Count} fields.", "Parse Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error parsing data: {ex.Message}", "Parse Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private List<string> ValidateDeviceDefinition(DeviceDefinition device)
    {
        var errors = new List<string>();
        if (device.Fields == null || device.Fields.Count == 0) return errors;

        // Basic checks per field
        for (int i = 0; i < device.Fields.Count; i++)
        {
            var f = device.Fields[i];
            if (f.Offset < 0) errors.Add($"Field '{f.Meaning}' has negative offset {f.Offset}.");
            if (f.Length <= 0) errors.Add($"Field '{f.Meaning}' has invalid length {f.Length}.");

            // Type vs length consistency
            switch (f.Type)
            {
                case FieldType.UInt8:
                case FieldType.Int8:
                    if (f.Length < 1) errors.Add($"Field '{f.Meaning}' type {f.Type} requires at least 1 byte.");
                    break;
                case FieldType.UInt16:
                case FieldType.Int16:
                    if (f.Length < 2) errors.Add($"Field '{f.Meaning}' type {f.Type} requires at least 2 bytes.");
                    break;
                case FieldType.UInt32:
                case FieldType.Int32:
                case FieldType.Float32:
                    if (f.Length < 4) errors.Add($"Field '{f.Meaning}' type {f.Type} requires at least 4 bytes.");
                    break;
                case FieldType.AsciiString:
                case FieldType.Fixed:
                    // length validated above
                    break;
                default:
                    errors.Add($"Field '{f.Meaning}' has unknown type {f.Type}.");
                    break;
            }
        }

        // Check overlaps and continuity
        var ranges = device.Fields.Select(f => new { f.Meaning, Start = f.Offset, End = f.Offset + f.Length - 1 }).ToList();
        // check for overlaps
        for (int i = 0; i < ranges.Count; i++)
        {
            for (int j = i + 1; j < ranges.Count; j++)
            {
                var a = ranges[i];
                var b = ranges[j];
                if (a.Start <= b.End && b.Start <= a.End)
                {
                    errors.Add($"Fields '{a.Meaning}' (offset {a.Start}-{a.End}) and '{b.Meaning}' (offset {b.Start}-{b.End}) overlap.");
                }
            }
        }

        // Optional: check that max offset+length equals expected payload size if device.PayloadFormat==Binary and user provided expected size
        // Since we don't store expected payload length, warn if max byte index is unusually large
        var maxEnd = ranges.Max(r => r.End);
        if (maxEnd < 0) maxEnd = 0;
        if (maxEnd >= 1024) errors.Add($"Max field end {maxEnd} looks suspiciously large.");

        return errors;
    }

    private ParsedReport ParseData(byte[] data, DeviceDefinition device)
    {
        var report = new ParsedReport
        {
            Timestamp = DateTime.Now,
            RawDataHex = MainForm.BytesToHex(data, data.Length),
            DeviceName = device.Name
        };

        var dataToProcess = data;
        
        if (device.PayloadFormat == PayloadFormat.HexDump)
        {
            try
            {
                var dataString = Encoding.ASCII.GetString(data);
                if (IsValidHexString(dataString))
                {
                    dataToProcess = HexStringToBytes(dataString);
                    report.RawDataHex = MainForm.BytesToHex(dataToProcess, dataToProcess.Length);
                }
            }
            catch 
            {
            }
        }

        foreach (var field in _fields)
        {
            try
            {
                var parsedField = ParseField(dataToProcess, field);
                report.ParsedFields.Add(parsedField);
            }
            catch (Exception ex)
            {
                // Add error field
                report.ParsedFields.Add(new ParsedField
                {
                    FieldName = field.Name,
                    Meaning = field.Meaning,
                    DisplayValue = $"Error: {ex.Message}",
                    Offset = field.Offset,
                    Length = field.Length,
                    Type = field.Type
                });
            }
        }

        return report;
    }

    private bool IsValidHexString(string str)
    {
        if (string.IsNullOrWhiteSpace(str)) return false;
        
        var cleanStr = str.Replace(" ", "").Replace("-", "");
        if (cleanStr.Length % 2 != 0) return false;
        
        return cleanStr.All(c => "0123456789ABCDEFabcdef".Contains(c));
    }

    private byte[] HexStringToBytes(string hex)
    {
        var cleanHex = hex.Replace(" ", "").Replace("-", "");
        if (cleanHex.Length % 2 != 0) return Array.Empty<byte>();
        
        var bytes = new byte[cleanHex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(cleanHex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    private ParsedField ParseField(byte[] data, FieldDef field)
    {
        var parsedField = new ParsedField
        {
            FieldName = field.Name,
            Meaning = field.Meaning,
            Offset = field.Offset,
            Length = field.Length,
            Type = field.Type
        };

        if (field.Offset < 0 || field.Offset >= data.Length)
        {
            throw new ArgumentException($"Offset {field.Offset} is out of range for data length {data.Length}");
        }

        if (field.Length <= 0)
        {
            throw new ArgumentException($"Field '{field.Name}' has invalid Length {field.Length}. Length must be > 0.");
        }

        if (field.Offset + field.Length > data.Length)
        {
            throw new ArgumentException($"Field '{field.Name}' (offset {field.Offset}, length {field.Length}) extends beyond data length {data.Length}");
        }

        var fieldData = data.Skip(field.Offset).Take(field.Length).ToArray();
        bool isLittleEndian = field.Endian == Endianness.Little;

        switch (field.Type)
        {
            case FieldType.UInt8:
                if (fieldData.Length < 1) throw new ArgumentException("Insufficient data for UInt8");
                parsedField.ParsedValue = fieldData[0];
                parsedField.DisplayValue = fieldData[0].ToString();
                break;

            case FieldType.Int8:
                if (fieldData.Length < 1) throw new ArgumentException("Insufficient data for Int8");
                parsedField.ParsedValue = (sbyte)fieldData[0];
                parsedField.DisplayValue = ((sbyte)fieldData[0]).ToString();
                break;

            case FieldType.UInt16:
                if (fieldData.Length < 2) throw new ArgumentException("Insufficient data for UInt16");
                var uint16Val = isLittleEndian ? BitConverter.ToUInt16(fieldData, 0) : BitConverter.ToUInt16(fieldData.Reverse().ToArray(), 0);
                parsedField.ParsedValue = uint16Val;
                parsedField.DisplayValue = uint16Val.ToString();
                break;

            case FieldType.Int16:
                if (fieldData.Length < 2) throw new ArgumentException("Insufficient data for Int16");
                var int16Val = isLittleEndian ? BitConverter.ToInt16(fieldData, 0) : BitConverter.ToInt16(fieldData.Reverse().ToArray(), 0);
                parsedField.ParsedValue = int16Val;
                parsedField.DisplayValue = int16Val.ToString();
                break;

            case FieldType.UInt32:
                if (fieldData.Length < 4) throw new ArgumentException("Insufficient data for UInt32");
                var uint32Val = isLittleEndian ? BitConverter.ToUInt32(fieldData, 0) : BitConverter.ToUInt32(fieldData.Reverse().ToArray(), 0);
                parsedField.ParsedValue = uint32Val;
                parsedField.DisplayValue = uint32Val.ToString();
                break;

            case FieldType.Int32:
                if (fieldData.Length < 4) throw new ArgumentException("Insufficient data for Int32");
                var int32Val = isLittleEndian ? BitConverter.ToInt32(fieldData, 0) : BitConverter.ToInt32(fieldData.Reverse().ToArray(), 0);
                parsedField.ParsedValue = int32Val;
                parsedField.DisplayValue = int32Val.ToString();
                break;

            case FieldType.Float32:
                if (fieldData.Length < 4) throw new ArgumentException("Insufficient data for Float32");
                var floatVal = isLittleEndian ? BitConverter.ToSingle(fieldData, 0) : BitConverter.ToSingle(fieldData.Reverse().ToArray(), 0);
                parsedField.ParsedValue = floatVal;
                parsedField.DisplayValue = floatVal.ToString("F6");
                break;

            case FieldType.AsciiString:
                var str = Encoding.ASCII.GetString(fieldData).TrimEnd('\0');
                parsedField.ParsedValue = str;
                parsedField.DisplayValue = str;
                break;

            case FieldType.Fixed:
                var hex = MainForm.BytesToHex(fieldData, fieldData.Length);
                parsedField.ParsedValue = hex;
                parsedField.DisplayValue = hex;
                break;

            default:
                throw new ArgumentException($"Unknown field type: {field.Type}");
        }

        return parsedField;
    }

    private void RefreshReportsGrid()
    {
        var device = Current();
        if (device == null) return;

        // Order reports by timestamp descending so newest appears at top
        var orderedReports = device.Reports.OrderByDescending(r => r.Timestamp).ToList();

        var reportSummaries = orderedReports.Select(r => new
        {
            Timestamp = r.Timestamp,
            RawDataHex = r.RawDataHex
        }).ToList();

        gridReports.DataSource = reportSummaries;
        // Keep reference to the ordered ParsedReport list so clicks map correctly
        gridReports.Tag = orderedReports;
    }

    private void GridReports_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.ColumnIndex == 2 && e.RowIndex >= 0) // Export button column (updated index)
        {
            var ordered = gridReports.Tag as List<ParsedReport>;
            if (ordered != null)
            {
                if (e.RowIndex >= 0 && e.RowIndex < ordered.Count)
                {
                    var report = ordered[e.RowIndex];
                    ExportSingleReport(report);
                }
            }
            else
            {
                // Fallback to original behavior
                var device = Current();
                if (device == null || e.RowIndex >= device.Reports.Count) return;

                var report = device.Reports[e.RowIndex];
                ExportSingleReport(report);
            }
        }
    }

    private void BtnClearReports_Click(object? sender, EventArgs e)
    {
        var device = Current();
        if (device == null || device.Reports.Count == 0) return;

        var result = MessageBox.Show(
            "Are you sure you want to delete all reports? This action cannot be undone.",
            "Confirm Clear Paremeters",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            device.Reports.Clear();
            
            _onSave(_devices);
            
            RefreshReportsGrid();
        }
    }

    private void BtnExportReports_Click(object? sender, EventArgs e)
    {
        var device = Current();
        if (device == null || device.Reports.Count == 0)
        {
            MessageBox.Show("No reports to export.", "No Data", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ExportAllReports(device.Reports);
    }

    private void ExportSingleReport(ParsedReport report)
    {
        using var saveDialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt|CSV files (*.csv)|*.csv",
            FileName = $"Report_{report.Timestamp:yyyyMMdd_HHmmss}.json"
        };

        if (saveDialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var extension = Path.GetExtension(saveDialog.FileName).ToLower();
                switch (extension)
                {
                    case ".json":
                        ExportAsJson(new[] { report }, saveDialog.FileName);
                        break;
                    case ".txt":
                        ExportAsText(new[] { report }, saveDialog.FileName);
                        break;
                    case ".csv":
                        ExportAsCsv(new[] { report }, saveDialog.FileName);
                        break;
                }
                MessageBox.Show("Report exported successfully!", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting report: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ExportAllReports(List<ParsedReport> reports)
    {
        using var saveDialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|Text files (*.txt)|*.txt|CSV files (*.csv)|*.csv",
            FileName = $"AllReports_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (saveDialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var extension = Path.GetExtension(saveDialog.FileName).ToLower();
                switch (extension)
                {
                    case ".json":
                        ExportAsJson(reports, saveDialog.FileName);
                        break;
                    case ".txt":
                        ExportAsText(reports, saveDialog.FileName);
                        break;
                    case ".csv":
                        ExportAsCsv(reports, saveDialog.FileName);
                        break;
                }
                MessageBox.Show($"{reports.Count} reports exported successfully!", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting reports: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ExportAsJson(IEnumerable<ParsedReport> reports, string fileName)
    {
        // Transform reports to ensure DeviceName is the first property and omit FieldName (it's same as Meaning)
        var transformed = reports.Select(r => new
        {
            DeviceName = r.DeviceName,
            Timestamp = r.Timestamp,
            RawDataHex = r.RawDataHex,
            ParsedFields = r.ParsedFields.Select(f => new
            {
                Meaning = f.Meaning,
                ParsedValue = f.ParsedValue,
                DisplayValue = f.DisplayValue,
                Offset = f.Offset,
                Length = f.Length,
                Type = f.Type
            }).ToList()
        }).ToList();

        var json = System.Text.Json.JsonSerializer.Serialize(transformed, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(fileName, json);
    }

    private void ExportAsText(IEnumerable<ParsedReport> reports, string fileName)
    {
        using var writer = new StreamWriter(fileName);
        foreach (var report in reports)
        {
            writer.WriteLine($"Report - {report.Timestamp:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Device: {report.DeviceName}");
            writer.WriteLine($"Raw Data: {report.RawDataHex}");
            writer.WriteLine("Parsed Fields:");
            foreach (var field in report.ParsedFields)
            {
                writer.WriteLine($"  {field.Meaning} ({field.FieldName}): {field.DisplayValue}");
            }
            writer.WriteLine(new string('-', 50));
        }
    }

    private void ExportAsCsv(IEnumerable<ParsedReport> reports, string fileName)
    {
        using var writer = new StreamWriter(fileName);
        
        // Write header
        writer.WriteLine("Timestamp,Device,Raw Data,Field Name,Field Meaning,Parsed Value,Offset,Length,Type");
        
        // Write data
        foreach (var report in reports)
        {
            foreach (var field in report.ParsedFields)
            {
                writer.WriteLine($"{report.Timestamp:yyyy-MM-dd HH:mm:ss},{report.DeviceName},\"{report.RawDataHex}\",{field.FieldName},{field.Meaning},{field.DisplayValue},{field.Offset},{field.Length},{field.Type}");
            }
        }
    }

    private DeviceDefinition? Current()
    {
        // Prefer the explicitly selected device; during rename we keep this reference
        if (_selected != null) return _selected;
        var name = cboDevices.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(name)) return null;
        _selected = _devices.FirstOrDefault(d => d.Name == name);
        return _selected;
    }

    private void SaveAll()
    {
        var d = Current();
        if (d != null && _fields != null)
        {
            // Ensure any in-progress cell edits are committed to the data source
            try { grid.CommitEdit(DataGridViewDataErrorContexts.Commit); } catch { }
            try { grid.EndEdit(); } catch { }
            try { Validate(); } catch { }

            // Copy Meaning to Name on save to ensure field descriptions persist
            foreach (var f in _fields)
            {
                f.Name = f.Meaning;
            }

            d.Fields = _fields.ToList();
            // propagate device-level Endian to fields for consistency
            foreach (var f in d.Fields) f.Endian = d.Endian;
            // apply rename if text changed and unique
            var newName = (txtDeviceName.Text ?? "").Trim();
            if (!string.IsNullOrEmpty(newName) && !newName.Equals(d.Name, StringComparison.OrdinalIgnoreCase))
            {
                if (_devices.Any(x => x != d && x.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("A device with the same name already exists.", "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    d.Name = newName;
                }
            }
        }

        _selected = d;

        // Move the saved/edited device to the top of the list so it appears first in the main grid
        try
        {
            if (d != null && _devices.Contains(d))
            {
                _devices.Remove(d);
                _devices.Insert(0, d);
            }
        }
        catch { }

        LoadDevices();
        _onSave(_devices);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void BtnImportJson_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Import device fields from JSON"
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        string json;
        try
        {
            json = File.ReadAllText(dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to read file: {ex.Message}", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            System.Text.Json.JsonElement arrayEl;

            // Optional: read payload_format and endianness from top-level
            var device = Current();
            if (device == null)
            {
                MessageBox.Show("No device selected to import fields into.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // payload_format detection
            var pf = TryGetString(root, "payload_format") ?? TryGetString(root, "payloadFormat") ?? TryGetString(root, "format");
            if (!string.IsNullOrWhiteSpace(pf) && TryParsePayloadFormat(pf, out var payloadFormat))
            {
                device.PayloadFormat = payloadFormat;
                // update UI if present
                try { if (cboFormat != null) cboFormat.SelectedItem = payloadFormat; } catch { }
            }

            // endianness detection
            var ed = TryGetString(root, "endianness") ?? TryGetString(root, "endian") ?? TryGetString(root, "byte_order");
            if (!string.IsNullOrWhiteSpace(ed) && TryParseEndianness(ed, out var endian))
            {
                device.Endian = endian;
                try { if (cboEndian != null) cboEndian.SelectedItem = endian; } catch { }
            }

            if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                arrayEl = root;
            }
            else if (root.TryGetProperty("Fields", out var f))
            {
                arrayEl = f;
            }
            else if (root.TryGetProperty("ParsedFields", out var pf2))
            {
                arrayEl = pf2;
            }
            else if (root.TryGetProperty("parsedFields", out var pf3))
            {
                arrayEl = pf3;
            }
            else if (root.TryGetProperty("packet_structure", out var ps))
            {
                arrayEl = ps;
            }
            else if (root.TryGetProperty("packetStructure", out var ps2))
            {
                arrayEl = ps2;
            }
            else if (root.TryGetProperty("packet-structure", out var ps3))
            {
                arrayEl = ps3;
            }
            else
            {
                MessageBox.Show("No fields array found in JSON. Expected top-level array or a 'Fields' / 'ParsedFields' / 'packet_structure' property.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var newFields = new List<FieldDef>();

            foreach (var item in arrayEl.EnumerateArray())
            {
                string meaning = TryGetString(item, "Meaning") ?? TryGetString(item, "meaning") ?? TryGetString(item, "Name") ?? TryGetString(item, "name") ?? "";

                // Offset
                int offset = TryGetInt(item, "Offset") ?? TryGetInt(item, "offset") ?? 0;
                // Length
                int length = TryGetInt(item, "Length") ?? TryGetInt(item, "length") ?? 0;

                // Type
                var typeStr = TryGetString(item, "Type") ?? TryGetString(item, "type") ?? TryGetString(item, "FieldType") ?? TryGetString(item, "fieldType");
                FieldType ftype;
                if (!TryParseFieldType(typeStr, out ftype))
                {
                    // default to Fixed if ambiguous
                    ftype = FieldType.Fixed;
                }

                var fd = new FieldDef
                {
                    Name = meaning,
                    Meaning = meaning,
                    Offset = offset,
                    Length = length,
                    Type = ftype,
                    Endian = device.Endian
                };

                newFields.Add(fd);
            }

            if (newFields.Count == 0)
            {
                MessageBox.Show("No valid fields were found in the JSON file.", "Import Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Apply to UI and device
            _fields = new System.ComponentModel.BindingList<FieldDef>(newFields);
            grid.DataSource = _fields;

            // Persist to the device definition and save
            device.Fields = newFields;
            _onSave(_devices);

            MessageBox.Show($"Imported {newFields.Count} fields into device '{device.Name}'.", "Import Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (System.Text.Json.JsonException jex)
        {
            MessageBox.Show($"Invalid JSON: {jex.Message}", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error importing JSON: {ex.Message}", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string? TryGetString(System.Text.Json.JsonElement el, string prop)
    {
        if (el.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
        if (el.TryGetProperty(prop, out var v))
        {
            if (v.ValueKind == System.Text.Json.JsonValueKind.String) return v.GetString();
            // numbers/other types -> ToString
            return v.ToString();
        }
        return null;
    }

    private static int? TryGetInt(System.Text.Json.JsonElement el, string prop)
    {
        if (el.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
        if (!el.TryGetProperty(prop, out var v)) return null;
        try
        {
            if (v.ValueKind == System.Text.Json.JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
            if (v.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                if (int.TryParse(v.GetString(), out var parsed)) return parsed;
            }
        }
        catch { }
        return null;
    }

    private static bool TryParseFieldType(string? s, out FieldType ft)
    {
        ft = FieldType.Fixed;
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (Enum.TryParse<FieldType>(s, true, out var parsed))
        {
            ft = parsed; return true;
        }

        var key = s.Trim().ToLowerInvariant();
        switch (key)
        {
            case "uint8": case "u8": case "byte": case "uint": ft = FieldType.UInt8; return true;
            case "int8": case "s8": ft = FieldType.Int8; return true;
            case "uint16": case "u16": ft = FieldType.UInt16; return true;
            case "int16": case "s16": ft = FieldType.Int16; return true;
            case "uint32": case "u32": ft = FieldType.UInt32; return true;
            case "int32": case "s32": case "int": case "integer": ft = FieldType.Int32; return true;
            case "float32": case "float": case "f32": ft = FieldType.Float32; return true;
            case "ascii": case "string": case "asciistring": ft = FieldType.AsciiString; return true;
            case "fixed": case "bytes": ft = FieldType.Fixed; return true;
        }

        return false;
    }

    private static bool TryParsePayloadFormat(string? s, out PayloadFormat pf)
    {
        pf = PayloadFormat.HexDump;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var key = s.Trim().ToLowerInvariant();
        if (key == "hexdump" || key == "hex" || key == "hex_dump") { pf = PayloadFormat.HexDump; return true; }
        if (key == "binary" || key == "bin") { pf = PayloadFormat.Binary; return true; }
        return false;
    }

    private static bool TryParseEndianness(string? s, out Endianness e)
    {
        e = Endianness.Little;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var key = s.Trim().ToLowerInvariant();
        if (key == "big" || key == "big_endian" || key == "bigendian") { e = Endianness.Big; return true; }
        if (key == "little" || key == "little_endian" || key == "littleendian") { e = Endianness.Little; return true; }
        return false;
    }
}
