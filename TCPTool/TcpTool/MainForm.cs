using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace TcpTool
{
    public partial class MainForm : Form
    {
        private TcpListener? _listener;
        private CancellationTokenSource? _serverCts;
        private readonly List<TcpClient> _clients = new();

        private TcpClient? _client;
        private NetworkStream? _clientStream;
        private CancellationTokenSource? _clientCts;

        // Devices & parsing state
        private List<DeviceDefinition> _devices = new();
        private DeviceDefinition? _currentDevice;
        // cache last received raw bytes (server RX)
        private byte[] _lastRx = Array.Empty<byte>();

        // Expose a safe copy of last received bytes for the DeviceManager capture
        public byte[] GetLastReceivedBytes()
        {
            // return a copy to avoid races
            var copy = new byte[_lastRx.Length];
            Array.Copy(_lastRx, copy, _lastRx.Length);
            return copy;
        }

        public MainForm()
        {
            InitializeComponent();
            InitializeDevicesSystem();
            // Default mode: unchecked (start without default selected)
            try { if (chkServerDefault != null) chkServerDefault.Checked = false; } catch { }
            
            // Ensure logs are focusable for scroll (simplified approach)
            txtServerLog.Enter += (s, e) => txtServerLog.Focus();
            txtServerLog.Click += (s, e) => txtServerLog.Focus();
            
            txtClientLog.Enter += (s, e) => txtClientLog.Focus();
            txtClientLog.Click += (s, e) => txtClientLog.Focus();
            
            // Setup after form is fully loaded
            this.Load += (s, e) => {
                try 
                {
                    txtServerLog.TabStop = true;
                    txtClientLog.TabStop = true;
                }
                catch { /* Ignore setup errors */ }
            };
        }

        private void LogServer(string msg = "") => AppendText(txtServerLog, msg);
        private void LogClient(string msg = "") => AppendText(txtClientLog, msg);

        private static void AppendText(TextBox box, string msg)
        {
            if (box.InvokeRequired)
            {
                try
                {
                    box.BeginInvoke(new Action(() => AppendText(box, msg)));
                }
                catch { /* Ignore invoke errors */ }
                return;
            }
            
            try
            {
                // Check if user is at the bottom before adding new text
                bool wasAtBottom = (box.SelectionStart + box.SelectionLength) >= (box.Text.Length - 2);
                
                // Add the new message
                box.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}");
                
                // If user was at bottom, auto-scroll to show new message
                if (wasAtBottom)
                {
                    box.SelectionStart = box.Text.Length;
                    box.ScrollToCaret();
                }
            }
            catch (Exception)
            {
                // Fallback: just add text without fancy scrolling
                try
                {
                    box.Text += $"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}";
                }
                catch { /* Final fallback: ignore */ }
            }
        }

        private async void btnStartServer_Click(object sender, EventArgs e)
        {
            if (_listener != null)
            {
                LogServer("Server already running");
                return;
            }
            if (!int.TryParse(txtServerPort.Text, out var port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Please enter a valid port (1-65535).", "Invalid Port", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                _serverCts = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Any, port);
                _listener.Start();
                btnStartServer.Enabled = false;
                btnStopServer.Enabled = true;
                LogServer($"Server started on 0.0.0.0:{port}");
                await AcceptLoopAsync(_serverCts.Token);
            }
            catch (Exception ex)
            {
                LogServer($"Error: {ex.Message}");
                StopServer();
            }
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _listener != null)
                {
                    var client = await _listener.AcceptTcpClientAsync(ct);
                    _clients.Add(client);
                    LogServer($"Client connected: {client.Client.RemoteEndPoint}");
                    _ = Task.Run(() => HandleClientAsync(client, ct));
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { LogServer($"Accept error: {ex.Message}"); }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            using var _ = client;
            var stream = client.GetStream();
            var buffer = new byte[8192];
            var messageBuffer = new List<byte>(); // Accumulate incomplete messages
            
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                    if (read == 0) break;
                    
                    // Add received bytes to message buffer
                    for (int i = 0; i < read; i++)
                    {
                        messageBuffer.Add(buffer[i]);
                    }
                    
                    // Store complete received bytes for capture in DeviceManager
                    _lastRx = messageBuffer.ToArray();

                    // Log incoming data: ASCII if printable, otherwise hex
                    if (IsPrintableAscii(messageBuffer.ToArray(), messageBuffer.Count))
                    {
                        var ascii = Encoding.ASCII.GetString(messageBuffer.ToArray());
                        LogServer($"ASCII: {ascii}");
                    }
                    else
                    {
                        var hex = BytesToHex(messageBuffer.ToArray(), messageBuffer.Count);
                        LogServer($"HEX: {hex}");
                    }

                    // Echo the complete message if enabled
                    if (chkServerEcho.Checked)
                    {
                        await stream.WriteAsync(messageBuffer.ToArray(), 0, messageBuffer.Count, ct);
                    }
                    
                    // Clear buffer for next message
                    messageBuffer.Clear();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { LogServer($"Client error: {ex.Message}"); }
            finally
            {
                LogServer($"Client disconnected: {client.Client.RemoteEndPoint}");
                _clients.Remove(client);
            }
        }

        private void btnStopServer_Click(object sender, EventArgs e) => StopServer();

        private void StopServer()
        {
            try
            {
                _serverCts?.Cancel();
                foreach (var c in _clients.ToArray()) { try { c.Close(); } catch { } }
                _clients.Clear();
            }
            catch { }
            finally
            {
                _serverCts = null;
                _listener = null;
                btnStartServer.Enabled = true;
                btnStopServer.Enabled = false;
                LogServer("Server stopped");
            }
        }

        private async void btnClientConnect_Click(object sender, EventArgs e)
        {
            if (_client != null)
            {
                LogClient("Already connected");
                return;
            }
            if (!int.TryParse(txtClientPort.Text, out var port))
            {
                MessageBox.Show("Please enter a valid port.", "Invalid Port", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                _client = new TcpClient();
                _clientCts = new CancellationTokenSource();
                await _client.ConnectAsync(txtClientHost.Text, port);
                _clientStream = _client.GetStream();
                btnClientConnect.Enabled = false;
                btnClientDisconnect.Enabled = true;
                LogClient($"Connected to {txtClientHost.Text}:{port}");
                _ = Task.Run(() => ClientReadLoopAsync(_clientCts.Token));
            }
            catch (Exception ex)
            {
                LogClient($"Connect error: {ex.Message}");
                DisconnectClient();
            }
        }

        private async Task ClientReadLoopAsync(CancellationToken ct)
        {
            if (_clientStream == null) return;
            var buffer = new byte[8192];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var read = await _clientStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                    if (read == 0) break;
                    var text = Encoding.UTF8.GetString(buffer, 0, read);
                    LogClient(text);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { LogClient($"Read error: {ex.Message}"); }
            finally { BeginInvoke(new Action(DisconnectClient)); }
        }

        private async void btnClientSend_Click(object sender, EventArgs e)
        {
            if (_clientStream == null)
            {
                LogClient("Not connected");
                return;
            }
            var input = txtClientSend.Text ?? string.Empty;
            // Client: honor only the Hex checkbox (no auto-detect)
            bool useHex = (chkClientSendHex != null && chkClientSendHex.Checked);
            byte[] data = useHex ? HexStringToBytes(input) : Encoding.UTF8.GetBytes(input);
            try
            {
                await _clientStream.WriteAsync(data, 0, data.Length);
                LogClient($"TX: {(useHex ? BytesToHex(data, data.Length) : input)}");
            }
            catch (Exception ex) { LogClient($"Send error: {ex.Message}"); }
        }

        private void btnClientDisconnect_Click(object sender, EventArgs e) => DisconnectClient();

        private void DisconnectClient()
        {
            try
            {
                _clientCts?.Cancel();
                _clientStream?.Close();
                _client?.Close();
            }
            catch { }
            finally
            {
                _clientCts = null;
                _clientStream = null;
                _client = null;
                btnClientConnect.Enabled = true;
                btnClientDisconnect.Enabled = false;
                LogClient("Disconnected");
            }
        }

        // ===== Devices & Parameters =====
        private void InitializeDevicesSystem()
        {
            try
            {
                _devices = DeviceStore.Load();
            }
            catch { _devices = new List<DeviceDefinition>(); }

            PopulateDeviceList();

            // Open Device Manager on double-clicking a device row in the grid
            if (dgvDevices != null)
            {
                dgvDevices.CellDoubleClick += (s, e) =>
                {
                    try
                    {
                        if (e.RowIndex < 0) return;
                        var row = dgvDevices.Rows[e.RowIndex];
                        var name = row.Cells[1].Value?.ToString();
                        if (!string.IsNullOrEmpty(name)) OpenDeviceManager(name);
                    }
                    catch { }
                };

                // Events used to update the marker column
                dgvDevices.DataBindingComplete += (s, e) => UpdateDeviceMarkerColumn();
                dgvDevices.SelectionChanged += (s, e) => UpdateDeviceMarkerColumn();
            }
        }

        // Put modern angled arrow in the marker column for the selected row, clear others
        private void UpdateDeviceMarkerColumn()
        {
            if (dgvDevices == null || dgvDevices.Rows.Count == 0) return;
            for (int i = 0; i < dgvDevices.Rows.Count; i++)
            {
                var row = dgvDevices.Rows[i];
                if (row.Selected && dgvDevices.Enabled)
                    row.Cells[0].Value = "❯"; // Modern angled arrow
                else
                    row.Cells[0].Value = "";
            }
        }

        private void PopulateDeviceList()
        {
            // populate the hidden list (legacy)
            string? selectedName = null;
            bool isDefault = chkServerDefault != null && chkServerDefault.Checked;
            try
            {
                if (dgvDevices != null && dgvDevices.CurrentRow != null)
                {
                    selectedName = dgvDevices.CurrentRow.Cells.Count > 1 ? dgvDevices.CurrentRow.Cells[1].Value?.ToString() : null;
                }
            }
            catch { }
            if (lstDevices != null)
            {
                lstDevices.BeginUpdate();
                lstDevices.Items.Clear();
                foreach (var d in _devices) lstDevices.Items.Add(d.Name);
                lstDevices.EndUpdate();
            }
            try
            {
                if (dgvDevices != null)
                {
                    var list = _devices.Select((d, i) => new { Index = i + 1, Name = d.Name, Format = d.PayloadFormat.ToString() }).ToList();
                    dgvDevices.DataSource = list;
                    dgvDevices.Enabled = !isDefault;
                    if (isDefault)
                    {
                        dgvDevices.ClearSelection();
                    }
                    else
                    {
                        // When Default is unchecked, selection is required: keep previous selection or pick the first device
                        bool found = false;
                        if (!string.IsNullOrEmpty(selectedName))
                        {
                            foreach (DataGridViewRow row in dgvDevices.Rows)
                            {
                                var name = row.Cells.Count > 1 ? row.Cells[1].Value?.ToString() : null;
                                if (string.Equals(name, selectedName, StringComparison.OrdinalIgnoreCase))
                                {
                                    row.Selected = true;
                                    dgvDevices.CurrentCell = row.Cells[1];
                                    found = true;
                                    break;
                                }
                            }
                        }
                        if (!found && dgvDevices.Rows.Count > 0)
                        {
                            dgvDevices.Rows[0].Selected = true;
                            dgvDevices.CurrentCell = dgvDevices.Rows[0].Cells[1];
                        }
                    }
                }
            }
            catch { }
        }

        private void OnDefaultModeChanged(object? sender, EventArgs e)
        {
            if (chkServerDefault == null) return;
            PopulateDeviceList();
        }

        private void RemoveSelectedDevice()
        {
            // Prefer DataGridView selection; fall back to hidden ListBox
            string? name = null;
            if (dgvDevices != null && dgvDevices.SelectedRows.Count > 0)
            {
                var row = dgvDevices.SelectedRows[0];
                name = row.Cells.Count > 1 ? row.Cells[1].Value?.ToString() : null;
            }
            if (string.IsNullOrEmpty(name) && lstDevices?.SelectedIndex is not null and >= 0)
            {
                name = lstDevices.SelectedItem?.ToString();
            }
            if (string.IsNullOrEmpty(name)) return;
            var def = _devices.FirstOrDefault(d => d.Name == name);
            if (def == null) return;
            if (MessageBox.Show($"Are you sure you want to delete the '{name}' device?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            _devices.Remove(def);
            SaveDevices();
            PopulateDeviceList();
        }

        private void SaveDevices() => DeviceStore.Save(_devices);

        private void LoadSelectedDeviceToUI()
        {
            _currentDevice = null;
            var name = lstDevices?.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(name))
                _currentDevice = _devices.FirstOrDefault(d => d.Name == name);
        }

        public static string BytesToHex(byte[] buffer, int count)
        {
            var sb = new StringBuilder(count * 3);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(buffer[i].ToString("X2"));
            }
            return sb.ToString();
        }

        // Helper to generate a unique device name
        private string CreateUniqueDeviceName(string baseName = "Device")
        {
            int idx = 1;
            string name;
            do { name = $"{baseName} {idx++}"; } while (_devices.Any(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
            return name;
        }

        private static byte[] HexStringToBytes(string hex)
        {
            var chars = hex.Where(c => Uri.IsHexDigit(c)).ToArray();
            if (chars.Length % 2 == 1)
            {
                // If odd length, ignore last nibble
                chars = chars.Take(chars.Length - 1).ToArray();
            }
            var bytes = new byte[chars.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(new string(new[] { chars[i * 2], chars[i * 2 + 1] }), 16);
            }
            return bytes;
        }

        private static bool IsPrintableAscii(byte[] buffer, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var b = buffer[i];
                // allow CR/LF/TAB alongside printable range 0x20-0x7E
                if (b == 0x0D || b == 0x0A || b == 0x09) continue;
                if (b < 0x20 || b > 0x7E) return false;
            }
            return count > 0;
        }

        private void txtClientSend_KeyDown(object? sender, KeyEventArgs e)
        {
            // Enter: Send message
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnClientSend.PerformClick();
                return;
            }
            // Ctrl+H: Toggle Hex mode
            if (e.Control && e.KeyCode == Keys.H)
            {
                chkClientSendHex.Checked = !chkClientSendHex.Checked;
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
    }

    public partial class MainForm
    {
        public void OpenDeviceManager()
        {
            // ensure current selection exists
            if (lstDevices.SelectedIndex < 0 && _devices.Count > 0)
            {
                lstDevices.SelectedIndex = 0;
            }
            using var dlg = new DeviceManagerForm(_devices, defs =>
            {
                DeviceStore.Save(defs);
            }, GetLastReceivedBytes);
            dlg.ShowDialog(this);
            // refresh list after changes
            PopulateDeviceList();
        }

        public void OpenDeviceManager(string selectedDeviceName)
        {
            using var dlg = new DeviceManagerForm(_devices, defs =>
            {
                DeviceStore.Save(defs);
            }, GetLastReceivedBytes, selectedDeviceName);
            dlg.ShowDialog(this);
            PopulateDeviceList();
            if (!string.IsNullOrEmpty(selectedDeviceName))
            {
                lstDevices.SelectedItem = selectedDeviceName;
            }
        }

        public void OpenDeviceManagerForNew()
        {
            // Auto-create a new device with a unique name and open the modal to edit it
            var name = CreateUniqueDeviceName();
            var newDef = new DeviceDefinition { Name = name, PayloadFormat = PayloadFormat.HexDump, Fields = new List<FieldDef>() };
            _devices.Add(newDef);
            using var dlg = new DeviceManagerForm(_devices, defs => { DeviceStore.Save(defs); }, GetLastReceivedBytes, newDef.Name);
            dlg.ShowDialog(this);
            PopulateDeviceList();
            // Keep selection consistent in grid; no need to select hidden list
        }
    }
}

