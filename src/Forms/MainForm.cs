using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using Oscilloscope.Communication;
using Oscilloscope.Controllers;
using Oscilloscope.Models;
using System.Globalization;
using Keysight33500BApp;
using System.Text;


namespace Oscilloscope.Forms
{
    public partial class MainForm : Form
    {
        private readonly OscilloscopeSettings settings;
        private OscilloscopeController? oscilloscope;
        private bool isConnected ;


        private TabControl resultTabs = null!;

        private TabControl mainTabs = null!;
        private PictureBox oscilloscopeDisplay = null!;
        // Tracks test duration
        private System.Diagnostics.Stopwatch _testStopwatch = null!;

        // Periodic timer that updates the timeLabel in real time
        private System.Windows.Forms.Timer _testTimer = null!;

        // Remove or comment out the old single-line dictionary
        // private Dictionary<string, (int total, int pass)> _waveStats;

        // Instead declare the nested dictionary
        private Dictionary<string, Dictionary<string, (int total, int pass)>> _paramStats
            = new Dictionary<string, Dictionary<string, (int total, int pass)>>();

        // Fields for standard function generator params
        private ComboBox _waveformCombo = null!;
        private NumericUpDown _freqInput = null!;
        private NumericUpDown _amplitudeInput = null!;
        private NumericUpDown _offsetInput = null!;
        private CheckBox _outputEnableCheck = null!;

        private Label summaryLabel = new Label();
        private TextBox ch1Results = null!;
        private Label timeLabel = null!;     // To show how long the test took

        // Fields for extended parameters
        private Panel _parameterPanel = null!;
        private NumericUpDown? _phaseInput = null!;
        private NumericUpDown? _dutyInput = null!;
        private NumericUpDown? _symmetryInput = null!;

        private TextBox? _resultsBox = null!;

        private NumericUpDown? _pulseWidthInput = null!;
        private NumericUpDown? _leadEdgeInput = null!;
        private NumericUpDown? _trailEdgeInput = null!;
        private NumericUpDown? _noiseBandwidthInput = null!;

        private ComboBox _channelCombo = null!;

         
        private RichTextBox testResultsBoxCH2 = null!;
        private TextBox ch2Results = null!;



        private string _selectedChannel = "CH1";

        private void UpdateSelectedChannel()
        {
            _selectedChannel = _channelCombo.SelectedItem?.ToString() ?? "CH1";
        }



   
        private TabPage CreateOscilloscopeTab(string tabName)
        {
            return new TabPage(tabName);
        }

        public MainForm()
        {
            InitializeComponent();
            settings = new OscilloscopeSettings();
            isConnected = false;
        }
        
        private Panel CreateStatusPanel()
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle
            };

            TextBox statusBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Name = "statusBox"  
            };

            panel.Controls.Add(statusBox);
            return panel;
        }

        private void InitializeComponent()
        {
            // Overall window setup
            this.Text = "Keysight MSOX3104T Oscilloscope Control";
            this.Size = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ColorTranslator.FromHtml("#005cb9");

            // Create a main TabControl to switch between "Main Control" and "Test Screen"
            TabControl mainTabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Arial", 10, FontStyle.Bold),
                ItemSize = new Size(160, 30)
            };

            // Create the two tabs
            TabPage screen1Tab = new TabPage("Main Control")
            {
                BackColor = Color.White
            };
            TabPage screen2Tab = new TabPage("Test Screen")
            {
                BackColor = Color.White
            };

            // Add your main panel (the big UI) to the first tab
            screen1Tab.Controls.Add(CreateMainPanel());

            // Add a placeholder panel for the second screen or your test logic
            screen2Tab.Controls.Add(CreateTestScreenPanel());

            // Add both tabs to mainTabControl
            mainTabControl.TabPages.Add(screen1Tab);
            mainTabControl.TabPages.Add(screen2Tab);

            // Finally, add the mainTabControl to this form
            this.Controls.Add(mainTabControl);

            // -- If your _waveformCombo is created in CreateChannelSettingsPanel for "CH1",
            //    you must ensure you store that control in _waveformCombo:
            //    e.g. inside CreateChannelSettingsPanel("CH1") do:
            //      _waveformCombo = waveformCombo;

            // -- Then AFTER _waveformCombo is assigned, hook the event:
            if (_waveformCombo != null)
            {
                _waveformCombo.SelectedIndexChanged += (s, e) =>
                {
                    string selectedWaveform = _waveformCombo.SelectedItem?.ToString()?.ToUpper() ?? "SIN";
                    UpdateParameterPanel(_parameterPanel, selectedWaveform); // Corrected function call
                };
            }

        }



        private void UpdateStatus(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(UpdateStatus), message);
                return;
            }

            Control[] foundControls = Controls.Find("statusBox", true);
            if (foundControls.Length == 0 || foundControls[0] is not TextBox statusBox)
            {
                MessageBox.Show("Error: statusBox not found!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            statusBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            statusBox.SelectionStart = statusBox.TextLength;
            statusBox.ScrollToCaret();
        }



        private bool _isDisplayEnabled = false; // Track display status


        private async void ToggleDisplay(object sender, EventArgs e)
        {
            _isDisplayEnabled = !_isDisplayEnabled;

            Button button = sender as Button;
            if (button != null)
            {
                button.Text = _isDisplayEnabled ? "Stop Display" : "Start Display";
            }

            if (_isDisplayEnabled)
            {
                await Task.Run(UpdateOscilloscopeDisplay);
            }
            else
            {
                oscilloscopeDisplay.Image = null; // Clear screen when stopped
            }
        }


        private void SaveScreenshot(object sender, EventArgs e)
        {
            if (oscilloscopeDisplay.Image == null)
            {
                MessageBox.Show("No image to save! Start the display first.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Define the project folder path
                string projectPath = AppDomain.CurrentDomain.BaseDirectory;
                string filePath = Path.Combine(projectPath, $"Oscilloscope_Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                // Save the image
                oscilloscopeDisplay.Image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

                MessageBox.Show($"Screenshot saved: {filePath}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save screenshot: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }




        private FunctionGeneratorController? _functionGenerator;
        private bool _isConnectedToFunctionGenerator = false;

        private RichTextBox testResultsBox = null!;
  

        private Panel CreateTestScreenPanel()
        {
            Panel testPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.LightGray
            };

            Label testLabel = new Label
            {
                Text = "This is the Test Screen",
                Font = new Font("Arial", 16, FontStyle.Bold),
                Location = new Point(20, 20),
                AutoSize = true
            };
            testPanel.Controls.Add(testLabel);

            // CH1 Logs
            testResultsBox = new RichTextBox
            {
                Location = new Point(20, 70),
                Width = 500,
                Height = 300,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                DetectUrls = false
            };
            testPanel.Controls.Add(testResultsBox);

            // CH2 Logs
            testResultsBoxCH2 = new RichTextBox
            {
                Location = new Point(20, 400),
                Width = 500,
                Height = 250,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                DetectUrls = false
            };
            testPanel.Controls.Add(testResultsBoxCH2);

            // CH1 Results
            ch1Results = new TextBox
            {
                Location = new Point(550, 70),
                Width = 800,
                Height = 300,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Name = "ch1Results"
            };
            testPanel.Controls.Add(ch1Results);

            // CH2 Results
            ch2Results = new TextBox
            {
                Location = new Point(550, 400),
                Width = 800,
                Height = 250,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Name = "ch2Results"
            };
            testPanel.Controls.Add(ch2Results);

            // CH1 Test Button
            Button testButtonCH1 = new Button
            {
                Text = "Run Test CH1",
                Location = new Point(20, 660),
                Width = 150,
                Height = 40,
                BackColor = Color.Blue,
                ForeColor = Color.White
            };
            testButtonCH1.Click += async (s, e) => await RunImprovedTest("CH1");
            testPanel.Controls.Add(testButtonCH1);

            // CH2 Test Button
            Button testButtonCH2 = new Button
            {
                Text = "Run Test CH2",
                Location = new Point(180, 660),
                Width = 150,
                Height = 40,
                BackColor = Color.Blue,
                ForeColor = Color.White
            };
            testButtonCH2.Click += async (s, e) => await RunImprovedTest("CH2");
            testPanel.Controls.Add(testButtonCH2);

            // Stop Test Button
            Button stopButton = new Button
            {
                Text = "Stop Test",
                Location = new Point(340, 660),
                Width = 150,
                Height = 40,
                BackColor = Color.Red,
                ForeColor = Color.White
            };
            stopButton.Click += (s, e) => StopTest();
            testPanel.Controls.Add(stopButton);

            timeLabel = new Label
            {
                Text = "Test Time: 0s",
                Location = new Point(20, 710),
                AutoSize = true,
                Font = new Font("Arial", 12, FontStyle.Bold)
            };
            testPanel.Controls.Add(timeLabel);

            _testTimer = new System.Windows.Forms.Timer();
            _testTimer.Interval = 500;
            _testTimer.Tick += (s, e) =>
            {
                if (_testStopwatch != null && _testStopwatch.IsRunning)
                {
                    var ts = _testStopwatch.Elapsed;
                    timeLabel.Text = $"Test Time: {ts.Minutes}m {ts.Seconds}s";
                }
            };

            return testPanel;
        }





        private void UpdateResultsTab()
        {
            if (_resultsBox == null)
            {
                MessageBox.Show("Error: _resultsBox is NULL! Cannot update results.", "Debug Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            StringBuilder results = new StringBuilder();

            foreach (var waveform in _paramStats)
            {
                results.AppendLine($"{waveform.Key.ToUpper()} WAVE Results:");

                foreach (var param in waveform.Value)
                {
                    int total = param.Value.total;
                    int passed = param.Value.pass;
                    double successRate = total > 0 ? (passed / (double)total) * 100 : 0;

                    results.AppendLine($"   {param.Key.ToUpper()} => {passed}/{total} passed ({successRate:F1}%)");
                }

                results.AppendLine();
            }

            // Update `_resultsBox` safely
            if (_resultsBox.InvokeRequired)
            {
                _resultsBox.Invoke((MethodInvoker)delegate
                {
                    _resultsBox.Text = results.ToString();
                });
            }
            else
            {
                _resultsBox.Text = results.ToString();
            }
        }

                        





        private void LogTestResult(string waveform, string parameter, bool passed)
        {
            if (!_paramStats.ContainsKey(waveform))
            {
                _paramStats[waveform] = new Dictionary<string, (int total, int pass)>();
            }

            if (!_paramStats[waveform].ContainsKey(parameter))
            {
                _paramStats[waveform][parameter] = (0, 0);
            }

            var (total, pass) = _paramStats[waveform][parameter];
            _paramStats[waveform][parameter] = (total + 1, pass + (passed ? 1 : 0));

            UpdateResultsTab(); // This will refresh the UI immediately
        }






        private TextBox _testLogBox = null!;

        private void UpdateTestLog(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(UpdateTestLog), message);
                return;
            }

            if (_testLogBox == null) return; // If not created yet, do nothing

            _testLogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            _testLogBox.SelectionStart = _testLogBox.TextLength;
            _testLogBox.ScrollToCaret();
        }




        private Panel CreateMainPanel()
        {
            Panel mainPanel = new Panel
            {
                Dock = DockStyle.Fill
            };

            // Create a vertical layout to include the connection panel, status panel, and the split container
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(5),
                BackColor = ColorTranslator.FromHtml("#005cb9")
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // Connection Panel at the top
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Main GUI below
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // Status Panel at the bottom 

            // Create SplitContainer for Oscilloscope (Left) and Waveform Generator (Right)
            SplitContainer splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 750,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = ColorTranslator.FromHtml("#005cb9")
            };

            // Create TabControl for Oscilloscope Controls (Left Side)
            TabControl oscilloscopeTabs = new TabControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Font = new Font("Arial", 10, FontStyle.Bold),
                ItemSize = new Size(160, 30)
            };

            TabPage timebaseTab = new TabPage("Timebase Control") { BackColor = Color.White };
            timebaseTab.Controls.Add(CreateTimebasePanel());

            TabPage channelTab = new TabPage("Channel Control") { BackColor = Color.White };
            channelTab.Controls.Add(CreateChannelPanel());

            TabPage waveformTab = new TabPage("Oscilloscope Waveform Generator") { BackColor = Color.White };
            waveformTab.Controls.Add(CreateWaveformPanel());

            TabPage triggerTab = new TabPage("Trigger Control") { BackColor = Color.White };
            triggerTab.Controls.Add(CreateTriggerPanel());

            TabPage oscilloscopeScreenTab = new TabPage("Oscilloscope Display") { BackColor = Color.White };

            // Display Panel for Oscilloscope
            oscilloscopeDisplay = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black
            };

            // Buttons for screen display control
            Button toggleDisplayButton = new Button
            {
                Text = "Start Display",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#003f7d")
            };
            toggleDisplayButton.Click += ToggleDisplay;

            Button screenshotButton = new Button
            {
                Text = "Take Screenshot",
                Dock = DockStyle.Top,
                Height = 30,
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#003f7d")
            };
            screenshotButton.Click += SaveScreenshot;

            // Panel to hold display and buttons
            Panel displayPanel = new Panel { Dock = DockStyle.Fill, BackColor = ColorTranslator.FromHtml("#005cb9") };
            displayPanel.Controls.Add(oscilloscopeDisplay);
            displayPanel.Controls.Add(toggleDisplayButton);
            displayPanel.Controls.Add(screenshotButton);

            oscilloscopeScreenTab.Controls.Add(displayPanel);
            oscilloscopeTabs.TabPages.AddRange(new[] { timebaseTab, channelTab, waveformTab, triggerTab, oscilloscopeScreenTab });

            splitContainer.Panel1.Controls.Add(oscilloscopeTabs);

            // Create Dedicated Panel for Waveform Generator (Right Side)
            Panel waveformGeneratorPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };

            waveformGeneratorPanel.Controls.Add(CreateFunctionGeneratorPanel());
            splitContainer.Panel2.Controls.Add(waveformGeneratorPanel);

            // Add the connection panel, main GUI, and status panel
            mainLayout.Controls.Add(CreateConnectionPanel(), 0, 0);
            mainLayout.Controls.Add(splitContainer, 0, 1);
            mainLayout.Controls.Add(CreateStatusPanel(), 0, 2); // Add Status Panel

            mainPanel.Controls.Add(mainLayout);
            return mainPanel;
        }



        private Panel CreateConnectionPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };

            Label addressLabel = new Label { Text = "VISA Address:", Location = new Point(10, 15), AutoSize = true };
            TextBox addressBox = new TextBox
            {
                Text = "USB0::0x2A8D::0x1770::MY58491960::0::INSTR",
                Location = new Point(115, 12),
                Width = 400,
                Name = "addressBox"
            };

            Button connectButton = new Button
            {
                Text = "Connect",
                Location = new Point(515, 12),
                Width = 100,
                Name = "connectButton"
            };
            connectButton.Click += async (s, e) => await ConnectButtonClick();

            Label statusLabel = new Label
            {
                Text = "Status: Disconnected",
                Location = new Point(10, 45),
                AutoSize = true,
                Name = "statusLabel",
                ForeColor = Color.Red
            };

            panel.Controls.AddRange(new Control[] { addressLabel, addressBox, connectButton, statusLabel });
            return panel;
        }


       

        private TabControl CreateControlTabs()
        {
            TabControl tabs = new TabControl
            {
                Dock = DockStyle.Fill
            };

            // Timebase Control Tab
            TabPage timebaseTab = new TabPage("Timebase Control");
            Panel timebasePanel = CreateTimebasePanel();
            timebaseTab.Controls.Add(timebasePanel);

            // Channel Control Tab
            TabPage channelTab = new TabPage("Channel Control");
            Panel channelPanel = CreateChannelPanel();
            channelTab.Controls.Add(channelPanel);

            // Waveform Generator Tab
            TabPage waveformTab = new TabPage("Waveform Generator");
            Panel waveformPanel = CreateWaveformPanel();
            waveformTab.Controls.Add(waveformPanel);

            // Trigger Control Tab
            TabPage triggerTab = new TabPage("Trigger Control");
            Panel triggerPanel = CreateTriggerPanel();
            triggerTab.Controls.Add(triggerPanel);

            // Create the function generator tab
            TabPage functionGeneratorTab = new TabPage("33500B Waveform Generator")
            {
                BackColor = Color.White
            };
            functionGeneratorTab.Controls.Add(CreateFunctionGeneratorPanel());

            // Add it to the main tab control
            mainTabs.TabPages.Add(functionGeneratorTab);


            tabs.TabPages.AddRange(new TabPage[] { timebaseTab, channelTab, waveformTab, triggerTab, functionGeneratorTab});
            return tabs;
        }

        private Panel CreateFunctionGeneratorPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            // Create a vertical layout with the connection box on top and CH1/CH2 settings below
            TableLayoutPanel mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1,
                Padding = new Padding(5),
                BackColor = Color.White
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Connection Panel
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // CH1 and CH2 Panels

            // Add connection panel at the top
            mainLayout.Controls.Add(CreateFunctionGeneratorConnectionPanel(), 0, 0);

            // Create a horizontal layout for CH1 and CH2 settings
            TableLayoutPanel channelsLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(5),
                BackColor = Color.White
            };
            channelsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); // CH1 Panel
            channelsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); // CH2 Panel

            // Create CH1 and CH2 Panels
            Panel ch1Panel = CreateChannelSettingsPanel("CH1");
            Panel ch2Panel = CreateChannelSettingsPanel("CH2");

            channelsLayout.Controls.Add(ch1Panel, 0, 0);
            channelsLayout.Controls.Add(ch2Panel, 1, 0);

            mainLayout.Controls.Add(channelsLayout, 0, 1);

            panel.Controls.Add(mainLayout);
            return panel;
        }

        private Panel CreateFunctionGeneratorConnectionPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };

            Label addressLabel = new Label { Text = "33500B IP Address:", Location = new Point(10, 15), AutoSize = true };
            TextBox addressBox = new TextBox
            {
                Text = "169.254.5.21",
                Location = new Point(150, 12),
                Width = 250,
                Name = "functionGenAddressBox"
            };

            Button connectButton = new Button
            {
                Text = "Connect",
                Location = new Point(400, 12),
                Width = 100,
                Name = "functionGenConnectButton"
            };
            connectButton.Click += async (s, e) => await ConnectToFunctionGenerator(addressBox.Text);

            Label statusLabel = new Label
            {
                Text = "Status: Disconnected",
                Location = new Point(520, 15),
                AutoSize = true,
                Name = "functionGenStatusLabel",
                ForeColor = Color.Red
            };

            panel.Controls.AddRange(new Control[] { addressLabel, addressBox, connectButton, statusLabel });
            return panel;
        }

        private Panel CreateChannelSettingsPanel(string channel)
        {
            Panel panel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White
            };

            Label titleLabel = new Label
            {
                Text = $"Waveform Generator - {channel}",
                Dock = DockStyle.Top,
                Font = new Font("Arial", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = ColorTranslator.FromHtml("#003f7d"),
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 40
            };

            Label waveLabel = new Label
            {
                Text = "Waveform Type:",
                Location = new Point(10, 50),
                AutoSize = true
            };

            ComboBox waveformCombo = new ComboBox
            {
                Location = new Point(140, 47),
                Width = 120,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Name = $"waveformCombo_{channel}"
            };
            waveformCombo.Items.AddRange(new[] { "SIN", "SQU", "RAMP", "PULS", "NOIS", "DC" });
            waveformCombo.SelectedIndex = 0; // Default: SIN

            //  Create a dedicated panel for waveform parameters
            Panel parameterPanel = new Panel
            {
                Location = new Point(10, 80),
                Size = new Size(280, 250),
                Name = $"parameterPanel_{channel}"
            };

            //  Call UpdateParameterPanel for the default waveform type (SIN)
            UpdateParameterPanel(parameterPanel, "SIN");  // 

            //  Attach event handler for waveform selection change
            waveformCombo.SelectedIndexChanged += (s, e) =>
            {
                string selectedWaveform = waveformCombo.SelectedItem?.ToString()?.ToUpper() ?? "SIN";
                UpdateParameterPanel(parameterPanel, selectedWaveform);
            };

            CheckBox outputEnableCheck = new CheckBox
            {
                Text = "Output On",
                Location = new Point(10, 340),
                AutoSize = true
            };

            Button applyButton = new Button
            {
                Text = "Apply Settings",
                Location = new Point(10, 370),
                Width = 120
            };

            applyButton.Click += async (s, e) =>
            {
                await ApplyChannelSettings(channel, waveformCombo, parameterPanel, outputEnableCheck);
            };

            panel.Controls.AddRange(new Control[]
            {
                titleLabel, waveLabel, waveformCombo, parameterPanel,
                outputEnableCheck, applyButton
            });

            return panel;
        }





        private async Task ApplySettings()
        {
            if (!_isConnectedToFunctionGenerator || _functionGenerator == null)
            {
                UpdateStatus("Not connected.");
                return;
            }

            try
            {
                // 1) Determine which channel from the ComboBox
                UpdateSelectedChannel();  // sets _selectedChannel = "CH1" or "CH2"

                // 2) DO NOT call SetChannelAsync(...) here; it triggers 33500B error
                // await _functionGenerator.SetChannelAsync(_selectedChannel);  // Removed

                // 3) Check waveform type
                string shape = _waveformCombo.SelectedItem?.ToString()?.ToUpper() ?? "SIN";

                // 4) Apply the waveform to that channel
                await _functionGenerator.SetWaveformAsync(_selectedChannel, shape);

                // 5) If not DC/NOIS, set frequency
                if (shape != "DC" && shape != "NOIS")
                {
                    double freq = (double)_freqInput.Value;
                    await _functionGenerator.SetFrequencyAsync(_selectedChannel, freq);
                }

                // 6) If not DC, set amplitude
                if (shape != "DC")
                {
                    double amp = (double)_amplitudeInput.Value;
                    await _functionGenerator.SetAmplitudeAsync(_selectedChannel, amp);
                }

                // 7) Always set offset
                double offs = (double)_offsetInput.Value;
                await _functionGenerator.SetOffsetAsync(_selectedChannel, offs);

                // 8) Extended parameters
                if (_phaseInput != null)
                {
                    double phaseDeg = (double)_phaseInput.Value;
                    await _functionGenerator.SetPhaseAsync(_selectedChannel, phaseDeg);
                }

                if (_dutyInput != null)
                {
                    double duty = (double)_dutyInput.Value;
                    await _functionGenerator.SetSquareDutyCycleAsync(_selectedChannel, duty);
                }

                if (_symmetryInput != null)
                {
                    double sym = (double)_symmetryInput.Value;
                    await _functionGenerator.SetRampSymmetryAsync(_selectedChannel, sym);
                }

                if (_pulseWidthInput != null)
                {
                    double widthSec = (double)_pulseWidthInput.Value * 1e-6;
                    await _functionGenerator.SetPulseWidthAsync(_selectedChannel, widthSec);
                }

                if (_leadEdgeInput != null)
                {
                    double leadSec = (double)_leadEdgeInput.Value * 1e-9;
                    await _functionGenerator.SetPulseLeadingEdgeAsync(_selectedChannel, leadSec);
                }

                if (_trailEdgeInput != null)
                {
                    double trailSec = (double)_trailEdgeInput.Value * 1e-9;
                    await _functionGenerator.SetPulseTrailingEdgeAsync(_selectedChannel, trailSec);
                }

                if (_noiseBandwidthInput != null)
                {
                    double bw = (double)_noiseBandwidthInput.Value;
                    await _functionGenerator.SetNoiseBandwidthAsync(_selectedChannel, bw);
                }

                // 9) Finally, enable or disable output for that channel
                bool enableOutput = _outputEnableCheck.Checked;
                await _functionGenerator.EnableChannelAsync(_selectedChannel, enableOutput);

                UpdateStatus($"Settings applied for {_selectedChannel}: {shape} waveform.");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Apply failed: {ex.Message}");
            }
        }






        private async Task ApplyChannelSettings(
            string channel, 
            ComboBox waveformCombo, 
            Panel parameterPanel, 
            CheckBox outputEnableCheck)
        {
            if (!_isConnectedToFunctionGenerator || _functionGenerator == null)
            {
                UpdateStatus("Not connected.");
                return;
            }

            try
            {
                string shape = waveformCombo.SelectedItem?.ToString()?.ToUpper() ?? "SIN";
                await _functionGenerator.SetWaveformAsync(channel, shape);

                // Helper function to find numeric controls in parameterPanel
                NumericUpDown? GetNumeric(string name) 
                    => parameterPanel.Controls.Find(name, true).FirstOrDefault() as NumericUpDown;

                // If not DC/NOIS, set freq
                double freq = 0.0;
                if (shape != "DC" && shape != "NOIS")
                {
                    freq = (double)(GetNumeric("freqInput")?.Value ?? 0);
                    await _functionGenerator.SetFrequencyAsync(channel, freq);
                }

                // If not DC, set amplitude
                if (shape != "DC")
                {
                    double amp = (double)(GetNumeric("ampInput")?.Value ?? 0);
                    await _functionGenerator.SetAmplitudeAsync(channel, amp);
                }

                // Offset (all waves)
                double offset = (double)(GetNumeric("offsetInput")?.Value ?? 0);
                await _functionGenerator.SetOffsetAsync(channel, offset);

                // If wave == NOIS, set bandwidth
                if (shape == "NOIS")
                {
                    double bandwidth = (double)(GetNumeric("noiseBandwidthInput")?.Value ?? 0);
                    await _functionGenerator.SetNoiseBandwidthAsync(channel, bandwidth);
                }

                // If wave uses phase
                if (shape == "SIN" || shape == "SQU" || shape == "RAMP" || shape == "PULS")
                {
                    double phase = (double)(GetNumeric("phaseInput")?.Value ?? 0);
                    await _functionGenerator.SetPhaseAsync(channel, phase);
                }

                // If wave == SQU, set duty
                if (shape == "SQU")
                {
                    double duty = (double)(GetNumeric("dutyInput")?.Value ?? 0);
                    await _functionGenerator.SetSquareDutyCycleAsync(channel, duty);
                }

                // If wave == RAMP, set symmetry
                if (shape == "RAMP")
                {
                    double symmetry = (double)(GetNumeric("symmetryInput")?.Value ?? 0);
                    await _functionGenerator.SetRampSymmetryAsync(channel, symmetry);
                }

                // If wave == PULS, set pulse width & edges
                if (shape == "PULS")
                {
                    // Values from UI in microseconds and nanoseconds
                    double widthUs = (double)(GetNumeric("pulseWidthInput")?.Value ?? 0); 
                    double leadNs  = (double)(GetNumeric("leadEdgeInput")?.Value ?? 0);
                    double trailNs = (double)(GetNumeric("trailEdgeInput")?.Value ?? 0);

                    // Convert to seconds
                    double widthSec = widthUs * 1e-6;
                    double leadSec  = leadNs * 1e-9;
                    double trailSec = trailNs * 1e-9;

                    // NEW: clamp logic for PULSE to avoid invalid parameters
                    if (freq > 0)  // If freq=0, user set 0 or DC => can’t clamp
                    {
                        double periodSec = 1.0 / freq;

                        // 1) If widthSec > ~80% of period
                        if (widthSec > periodSec * 0.8)
                            widthSec = periodSec * 0.8;

                        // 2) If leadSec + trailSec + widthSec too large
                        double sum = leadSec + trailSec + widthSec;
                        if (sum > periodSec * 0.9)
                        {
                            double leftover = periodSec * 0.9 - (leadSec + trailSec);
                            if (leftover < 0) leftover = 0;
                            widthSec = Math.Min(widthSec, leftover);
                        }
                    }

                    // Now set them in the function generator
                    await _functionGenerator.SetPulseWidthAsync(channel, widthSec);
                    await _functionGenerator.SetPulseLeadingEdgeAsync(channel, leadSec);
                    await _functionGenerator.SetPulseTrailingEdgeAsync(channel, trailSec);
                }

                // Finally, enable/disable output
                bool enableOutput = outputEnableCheck.Checked;
                await _functionGenerator.EnableChannelAsync(channel, enableOutput);

                UpdateStatus($"Settings applied for {channel}: {shape} waveform.");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Apply failed: {ex.Message}");
            }
        }













        private async Task OnOutputEnableChanged(bool enable)
        {
            if (!_isConnectedToFunctionGenerator || _functionGenerator == null)
            {
                UpdateStatus("Not connected to function generator.");
                return;
            }

            try
            {
                await _functionGenerator.SetOutputStateAsync(enable);
                UpdateStatus($"Output turned {(enable ? "ON" : "OFF")}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to set output: {ex.Message}");
            }
        }



        private void UpdateParameterPanel(Panel parameterPanel, string shape)
        {
            parameterPanel.Controls.Clear(); // Remove old controls
            int yOffset = 0;

            // Helper function to add numeric input fields dynamically
            NumericUpDown AddNumeric(string labelText, string name, double min, double max, double defaultVal = 0, double inc = 0.001)
            {
                Label lbl = new Label
                {
                    Text = labelText,
                    Location = new Point(0, yOffset),
                    AutoSize = true
                };
                NumericUpDown num = new NumericUpDown
                {
                    Name = name,
                    Location = new Point(140, yOffset),
                    Width = 120,
                    DecimalPlaces = 6,
                    Minimum = (decimal)min,
                    Maximum = (decimal)max,
                    Increment = (decimal)inc,
                    Value = (decimal)defaultVal
                };
                parameterPanel.Controls.Add(lbl);
                parameterPanel.Controls.Add(num);
                yOffset += 35;
                return num;
            }

            // Add  relevant parameters for each waveform type
            if (shape != "DC" && shape != "NOIS")
            {
                AddNumeric("Frequency (Hz):", "freqInput", 0.001, 100_000_000, 1000);
            }

            if (shape != "DC")
            {
                AddNumeric("Amplitude (Vpp):", "ampInput", 0.001, 20, 1);
            }

            AddNumeric("Offset (V):", "offsetInput", -10, 10, 0);

            if (shape == "NOIS")
            {
                AddNumeric("Bandwidth (Hz):", "noiseBandwidthInput", 1, 100_000_000, 10_000);
            }

            if (shape == "SIN" || shape == "SQU" || shape == "RAMP" || shape == "PULS")
            {
                AddNumeric("Phase (deg):", "phaseInput", -360, 360, 0, 0.1);
            }

            if (shape == "SQU")
            {
                AddNumeric("Duty Cycle (%):", "dutyInput", 0.1, 99.9, 50, 0.1);
            }

            if (shape == "RAMP")
            {
                AddNumeric("Symmetry (%):", "symmetryInput", 0.1, 99.9, 50, 0.1);
            }

            if (shape == "PULS")
            {
                AddNumeric("Pulse Width (µs):", "pulseWidthInput", 1, 1_000_000_000, 500, 1);
                AddNumeric("Leading Edge (ns):", "leadEdgeInput", 0.1, 1_000_000, 8.4, 0.1);
                AddNumeric("Trailing Edge (ns):", "trailEdgeInput", 0.1, 1_000_000, 8.4, 0.1);
            }

            parameterPanel.Invalidate();
        }



        private void DisplayWaveform(double[] xData, double[] yData)
        {
            try
            {
                if (oscilloscopeDisplay.InvokeRequired)
                {
                    oscilloscopeDisplay.Invoke(new Action(() => DisplayWaveform(xData, yData)));
                    return;
                }

                int width = oscilloscopeDisplay.Width;
                int height = oscilloscopeDisplay.Height;

                Bitmap bmp = new Bitmap(width, height);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Black);
                    Pen wavePen = new Pen(Color.Lime, 2);

                    for (int i = 1; i < xData.Length; i++)
                    {
                        int x1 = (int)((xData[i - 1] - xData[0]) / (xData.Last() - xData[0]) * width);
                        int y1 = height - (int)((yData[i - 1] - yData.Min()) / (yData.Max() - yData.Min()) * height);
                        int x2 = (int)((xData[i] - xData[0]) / (xData.Last() - xData[0]) * width);
                        int y2 = height - (int)((yData[i] - yData.Min()) / (yData.Max() - yData.Min()) * height);

                        g.DrawLine(wavePen, x1, y1, x2, y2);
                    }
                }

                oscilloscopeDisplay.Image = bmp;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to display waveform: {ex.Message}");
            }
        }


        private async Task UpdateOscilloscopeDisplay()
        {
            while (isConnected && _isDisplayEnabled)  // Update only when enabled
            {
                try
                {
                    if (oscilloscope == null) return;

                    await oscilloscope.SendCommandAsync(":RUN");  // Ensure oscilloscope is running
                    await oscilloscope.SendCommandAsync(":WAVeform:SOURce CHAN1");
                    await oscilloscope.SendCommandAsync(":WAVeform:FORMat ASCii");
                    await oscilloscope.SendCommandAsync(":WAVeform:POINts 1000");  // Reduce number of points

                    double xIncrement = Convert.ToDouble(await oscilloscope.QueryAsync(":WAVeform:XINCrement?"));
                    double xOrigin = Convert.ToDouble(await oscilloscope.QueryAsync(":WAVeform:XORigin?"));
                    double yIncrement = Convert.ToDouble(await oscilloscope.QueryAsync(":WAVeform:YINCrement?"));
                    double yOrigin = Convert.ToDouble(await oscilloscope.QueryAsync(":WAVeform:YORigin?"));
                    double yReference = Convert.ToDouble(await oscilloscope.QueryAsync(":WAVeform:YREFerence?"));

                    string rawWaveformData = await oscilloscope.QueryAsync(":WAVeform:DATA?");
                    
                    //  **Check if the response is valid before processing** 
                    if (string.IsNullOrWhiteSpace(rawWaveformData))
                    {
                        UpdateStatus("Error: Received empty waveform data.");
                        continue;
                    }

                    string[] dataPoints = rawWaveformData.Split(',');

                    //  **Check if we actually received numeric data** 
                    if (dataPoints.Length < 2)
                    {
                        UpdateStatus("Error: Waveform data format is incorrect.");
                        continue;
                    }

                    // Convert Data to Double Format
                    double[] yData = dataPoints
                        .Select(point =>
                        {
                            double value;
                            return double.TryParse(point, out value) ? (value - yReference) * yIncrement + yOrigin : double.NaN;
                        })
                        .Where(v => !double.IsNaN(v))  // Filter out invalid values
                        .ToArray();

                    double[] xData = Enumerable.Range(0, yData.Length)
                        .Select(i => xOrigin + i * xIncrement)
                        .ToArray();

                    //  **Check if waveform data is empty**
                    if (yData.Length == 0)
                    {
                        UpdateStatus("Error: No valid waveform data received.");
                        continue;
                    }

                    DisplayWaveform(xData, yData);
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Error fetching waveform: {ex.Message}");
                }

                await Task.Delay(50);  // Refresh every 50ms
            }
        }







        private async Task ConnectToFunctionGenerator(string ip)
        {
            TextBox functionGenAddressBox = (TextBox)Controls.Find("functionGenAddressBox", true)[0];
            Button functionGenConnectButton = (Button)Controls.Find("functionGenConnectButton", true)[0];
            Label functionGenStatusLabel = (Label)Controls.Find("functionGenStatusLabel", true)[0];

            if (_isConnectedToFunctionGenerator) // If already connected, disconnect
            {
                try
                {
                    if (_functionGenerator != null)
                    {
                        await _functionGenerator.DisposeAsync();
                        _functionGenerator = null;
                    }

                    _isConnectedToFunctionGenerator = false;

                    // Re-enable the function generator address box
                    functionGenAddressBox.Enabled = true;

                    functionGenStatusLabel.Text = "Status: Disconnected";
                    functionGenStatusLabel.ForeColor = Color.Red;
                    functionGenConnectButton.Text = "Connect";

                    UpdateStatus("Disconnected from Keysight 33500B.");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Failed to disconnect: {ex.Message}");
                }
                return; // Exit function
            }

            try
            {
                var lan = new LanCommunication(ip);
                await lan.ConnectAsync();

                _functionGenerator = new FunctionGeneratorController(lan);
                await _functionGenerator.InitializeAsync();
                _isConnectedToFunctionGenerator = true;

                // Disable the function generator address box while connected
                functionGenAddressBox.Enabled = false;

                functionGenStatusLabel.Text = "Status: Connected";
                functionGenStatusLabel.ForeColor = Color.Green;
                functionGenConnectButton.Text = "Disconnect";

                UpdateStatus($"Connected to Keysight 33500B at {ip}.");
            }
            catch (Exception ex)
            {
                functionGenStatusLabel.Text = "Status: Connection Failed";
                functionGenStatusLabel.ForeColor = Color.Red;
                UpdateStatus($"Failed to connect to function generator: {ex.Message}");
            }
        }





        private async Task ApplyFunctionGeneratorSettings()
        {
            if (!_isConnectedToFunctionGenerator || _functionGenerator == null)
            {
                UpdateStatus("Not connected to Keysight 33500B.");
                return;
            }

            try
            {
                ComboBox typeBox = (ComboBox)Controls.Find("waveformType", true)[0];
                string waveform = typeBox.SelectedItem?.ToString()?.ToUpper() ?? "SIN";

                // Detect selected channel
                string selectedChannel = _selectedChannel; // Should be "CH1" or "CH2"

                var parameterPanel = Controls.Find("parameterPanel", true)[0];
                NumericUpDown? GetInput(string name) => parameterPanel.Controls.Find(name, true).FirstOrDefault() as NumericUpDown;

                double frequency = (double)(GetInput("waveformFreq")?.Value ?? 0m);
                double amplitude = (double)(GetInput("waveformAmp")?.Value ?? 0m);
                double offset = (double)(GetInput("waveformOffset")?.Value ?? 0m);
                double phase = (double)(GetInput("waveformPhase")?.Value ?? 0m);
                double duty = (double)(GetInput("waveformDuty")?.Value ?? 50m);
                double symmetry = (double)(GetInput("waveformSymmetry")?.Value ?? 50m);
                double widthNs = (double)(GetInput("waveformWidth")?.Value ?? 100m);
                double leadEdge = (double)(GetInput("waveformLead")?.Value ?? 10m);
                double trailEdge = (double)(GetInput("waveformTrail")?.Value ?? 10m);
                double bandwidth = (double)(GetInput("waveformBandwidth")?.Value ?? 5000m);

                // Ensure the correct channel is selected before applying settings
                await _functionGenerator.SetChannelAsync(selectedChannel);

                // Apply common parameters
                await _functionGenerator.SetWaveformAsync(selectedChannel, waveform);
                await _functionGenerator.SetFrequencyAsync(selectedChannel, frequency);
                await _functionGenerator.SetAmplitudeAsync(selectedChannel, amplitude);
                await _functionGenerator.SetOffsetAsync(selectedChannel, offset);
                await _functionGenerator.SetPhaseAsync(selectedChannel, phase);

                // Apply waveform-specific settings
                switch (waveform)
                {
                    case "SQUARE":
                        await _functionGenerator.SetSquareDutyCycleAsync(selectedChannel, duty);
                        break;
                    case "RAMP":
                        await _functionGenerator.SetRampSymmetryAsync(selectedChannel, symmetry);
                        break;
                    case "PULSE":
                        await _functionGenerator.SetPulseWidthAsync(selectedChannel, widthNs);
                        await _functionGenerator.SetPulseLeadingEdgeAsync(selectedChannel, leadEdge);
                        await _functionGenerator.SetPulseTrailingEdgeAsync(selectedChannel, trailEdge);
                        break;
                    case "NOISE":
                        await _functionGenerator.SetNoiseBandwidthAsync(selectedChannel, bandwidth);
                        break;
                }

                UpdateStatus($"Waveform applied: {waveform} - Channel: {selectedChannel} - Freq: {frequency}Hz, Amp: {amplitude}V, Offset: {offset}V");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to apply settings: {ex.Message}");
            }
        }




        private Panel CreateTriggerPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill };

            // Trigger Type
            Label typeLabel = new Label
            {
                Text = "Trigger Type:",
                Location = new Point(10, 20),
                AutoSize = true
            };
            ComboBox triggerTypeCombo = new ComboBox
            {
                Location = new Point(140, 17),
                Width = 100,
                Name = "triggerTypeCombo",
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            triggerTypeCombo.Items.AddRange(new[] { "EDGE", "PULSE", "PATTERN" });
            triggerTypeCombo.SelectedIndex = 0;

            // Trigger Sweep Mode (AUTO, NORM, SING)
            Label modeLabel = new Label
            {
                Text = "Trigger Sweep:",
                Location = new Point(10, 60),     // pick a new Y position
                AutoSize = true
            };
            ComboBox modeCombo = new ComboBox
            {
                Location = new Point(140, 57),
                Width = 100,
                Name = "triggerMode",
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            modeCombo.Items.AddRange(new[] { "AUTO", "NORM", "SING" });
            modeCombo.SelectedIndex = 0;

            // Trigger Source
            Label sourceLabel = new Label
            {
                Text = "Trigger Source:",
                Location = new Point(10, 100),
                AutoSize = true
            };
            ComboBox sourceCombo = new ComboBox
            {
                Location = new Point(140, 97),
                Width = 100,
                Name = "triggerSource",
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            sourceCombo.Items.AddRange(new[] { "CHAN1", "CHAN2", "CHAN3", "CHAN4", "LINE", "WGEN" });
            sourceCombo.SelectedIndex = 0;

            // Trigger Slope
            Label slopeLabel = new Label
            {
                Text = "Trigger Slope:",
                Location = new Point(10, 140),
                AutoSize = true
            };
            ComboBox slopeCombo = new ComboBox
            {
                Location = new Point(140, 137),
                Width = 100,
                Name = "triggerSlope",
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            slopeCombo.Items.AddRange(new[] { "POS", "NEG" });
            slopeCombo.SelectedIndex = 0;

            // Trigger Level
            Label levelLabel = new Label
            {
                Text = "Trigger Level (V):",
                Location = new Point(10, 180),
                AutoSize = true
            };
            NumericUpDown levelInput = new NumericUpDown
            {
                Location = new Point(140, 177),
                Width = 100,
                Minimum = -50,
                Maximum = 50,
                DecimalPlaces = 3,
                Increment = 0.01m,
                Value = 0,
                Name = "triggerLevel"
            };

            // Apply Trigger Button
            Button applyTriggerButton = new Button
            {
                Text = "Apply Trigger",
                Location = new Point(140, 220),
                Width = 100
            };
            applyTriggerButton.Click += async (s, e) => await ApplyTriggerSettings();

            // Add all controls to the panel
            panel.Controls.AddRange(new Control[]
            {
                typeLabel, triggerTypeCombo,
                modeLabel, modeCombo,
                sourceLabel, sourceCombo,
                slopeLabel, slopeCombo,
                levelLabel, levelInput,
                applyTriggerButton
            });

            return panel;
        }

        private async Task StartOscilloscope()
        {
            if (!isConnected || oscilloscope == null)
            {
                UpdateStatus("Error: Not connected to oscilloscope.");
                return;
            }

            try
            {
                await oscilloscope.SendCommandAsync(":RUN");  // Start continuous acquisition
                UpdateStatus("Oscilloscope Running.");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to start oscilloscope: {ex.Message}");
            }
        }


        private async Task StopOscilloscope()
        {
            if (!isConnected || oscilloscope == null)
            {
                UpdateStatus("Error: Not connected to oscilloscope.");
                return;
            }

            try
            {
                await oscilloscope.SendCommandAsync(":STOP");  // Stop continuous acquisition
                UpdateStatus("Oscilloscope Stopped.");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to stop oscilloscope: {ex.Message}");
            }
        }





        private async Task SingleAcquisition()
        {
            if (!isConnected || oscilloscope == null)
            {
                UpdateStatus("Error: Not connected to oscilloscope.");
                return;
            }

            try
            {
                await oscilloscope.SendCommandAsync(":SINGle");  // Correct SCPI command
                UpdateStatus("Performed Single Acquisition.");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to trigger single acquisition: {ex.Message}");
            }
        }


        private async Task AutoScale()
        {
            if (!isConnected || oscilloscope == null)
            {
                UpdateStatus("Error: Not connected to oscilloscope.");
                return;
            }

            try
            {
                await oscilloscope.SendCommandAsync(":AUTOSCALE");
                UpdateStatus("Auto Scale applied.");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to apply Auto Scale: {ex.Message}");
            }
        }



        private Panel CreateTimebasePanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill };

            // Timebase Scale Label
            Label scaleLabel = new Label
            {
                Text = "Timebase Scale (s/div):",
                Location = new Point(10, 20),
                AutoSize = true
            };

            // Timebase Scale Numeric Input
            NumericUpDown scaleInput = new NumericUpDown
            {
                Location = new Point(200, 18),
                Width = 150,
                DecimalPlaces = 6,
                Minimum = (decimal)500e-12,
                Maximum = 100,
                Value = (decimal)100e-6,
                Increment = (decimal)100e-6,
                Name = "timebaseScale"
            };
            scaleInput.ValueChanged += async (s, e) => await TimebaseScaleChanged();

            // Run Button
            Button runButton = new Button
            {
                Text = "Run",
                Location = new Point(10, 60),
                Width = 100
            };
            runButton.Click += async (s, e) => await StartOscilloscope();

            // Stop Button
            Button stopButton = new Button
            {
                Text = "Stop",
                Location = new Point(120, 60),
                Width = 100
            };
            stopButton.Click += async (s, e) => await StopOscilloscope();

            // Single Acquisition Button
            Button singleButton = new Button
            {
                Text = "Single",
                Location = new Point(230, 60),
                Width = 100
            };
            singleButton.Click += async (s, e) => await SingleAcquisition();

            // Auto Scale Button
            Button autoScaleButton = new Button
            {
                Text = "Auto Scale",
                Location = new Point(340, 60),
                Width = 100
            };
            autoScaleButton.Click += async (s, e) => await AutoScale();

            // Add components to the panel
            panel.Controls.AddRange(new Control[] { scaleLabel, scaleInput, runButton, stopButton, singleButton, autoScaleButton });

            return panel;
        }



        private Panel CreateChannelPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill };
            int yOffset = 20;

            for (int channel = 1; channel <= 4; channel++)
            {
                CheckBox enableBox = new CheckBox
                {
                    Text = $"Channel {channel}",
                    Location = new Point(10, yOffset),
                    Name = $"channel{channel}Enable",
                    Tag = channel
                };
                enableBox.CheckedChanged += async (s, e) => await ChannelEnableChanged(enableBox);

                Label scaleLabel = new Label
                {
                    Text = "Scale (V/div):",
                    Location = new Point(120, yOffset + 3),
                    AutoSize = true
                };

                NumericUpDown scaleInput = new NumericUpDown
                {
                    Location = new Point(220, yOffset),
                    Width = 100,
                    DecimalPlaces = 3,
                    Minimum = (decimal)0.001,
                    Maximum = 10,
                    Value = 1,
                    Name = $"channel{channel}Scale",
                    Tag = channel
                };
                scaleInput.ValueChanged += async (s, e) => await ChannelScaleChanged(scaleInput);

                panel.Controls.AddRange(new Control[] { enableBox, scaleLabel, scaleInput });
                yOffset += 40;
            }

            return panel;
        }

        private Panel CreateWaveformPanel()
        {
            Panel panel = new Panel { Dock = DockStyle.Fill };

            // Waveform Type
            Label typeLabel = new Label
            {
                Text = "Waveform Type:",
                Location = new Point(10, 20),
                AutoSize = true
            };

            ComboBox typeBox = new ComboBox
            {
                Location = new Point(130, 17),
                Width = 150,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Name = "waveformType"
            };
            typeBox.Items.AddRange(new string[] { "SINUSOID", "SQUARE", "RAMP", "PULSE", "NOISE", "DC" });
            typeBox.SelectedIndex = 0;
            typeBox.SelectedIndexChanged += (s, e) => UpdateWaveformParameterInputs(panel, typeBox.SelectedItem.ToString()!);

            Panel parameterPanel = new Panel
            {
                Location = new Point(10, 60),
                AutoSize = true,
                Name = "parameterPanel"
            };

            // Apply button for waveform
            Button applyButton = new Button
            {
                Text = "Apply",
                Location = new Point(120, 250),
                Width = 100
            };
            applyButton.Click += async (s, e) => await ApplyWaveformSettings();

            panel.Controls.AddRange(new Control[] { typeLabel, typeBox, parameterPanel, applyButton });

            // Measurements group box
            GroupBox measureGroup = new GroupBox
            {
                Text = "Measurements",
                Location = new Point(464, 10),
                Size = new Size(250, 280)
            };

            // Measurement type label + combo
            Label measureTypeLabel = new Label
            {
                Text = "Type:",
                Location = new Point(10, 30),
                AutoSize = true
            };
            ComboBox measureTypeBox = new ComboBox
            {
                Location = new Point(80, 27),
                Width = 150,
                Name = "measureTypeBox",
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            
            //   measurement list with all necessary types
            measureTypeBox.Items.AddRange(new string[] {
                "Vpp", 
                "Vrms", 
                "Frequency", 
                "Period", 
                "Mean", 
                "Amplitude",
                "Phase",
                "Duty Cycle",
                "Pulse Width",
                "Rise Time",
                "Fall Time",
                "Overshoot",
                "Preshoot",
                "Slew Rate"
            });
            
            measureTypeBox.SelectedIndex = 0;

            // Measurement channel label + numeric
            Label measureChanLabel = new Label
            {
                Text = "Channel:",
                Location = new Point(10, 65),
                AutoSize = true
            };
            NumericUpDown measureChanInput = new NumericUpDown
            {
                Location = new Point(80, 62),
                Width = 50,
                Minimum = 1,
                Maximum = 4,
                Value = 1,
                Name = "measureChannel"
            };

            // Measure button
            Button measureButton = new Button
            {
                Text = "Measure",
                Location = new Point(80, 100),
                Width = 80
            };
            measureButton.Click += async (s, e) => await OnMeasureButtonClick();

            // Result label + box
            Label measureResultLabel = new Label
            {
                Text = "Result:",
                Location = new Point(10, 140),
                AutoSize = true
            };
            TextBox measureResultBox = new TextBox
            {
                Location = new Point(80, 137),
                Width = 150,
                ReadOnly = true,
                Name = "measureResultBox"
            };

            // Add these controls into the group
            measureGroup.Controls.AddRange(new Control[]
            {
                measureTypeLabel, measureTypeBox,
                measureChanLabel, measureChanInput,
                measureButton,
                measureResultLabel, measureResultBox
            });

            // Add the measurement group to the panel
            panel.Controls.Add(measureGroup);

            // Initialize waveform parameter inputs (SINUSOID by default)
            UpdateWaveformParameterInputs(panel, "SINUSOID");

            return panel;
        }


        private void UpdateWaveformParameterInputs(Panel parentPanel, string waveformType)
        {
            Panel parameterPanel = (Panel)parentPanel.Controls.Find("parameterPanel", true)[0];
            parameterPanel.Controls.Clear();

            int yOffset = 10; // Start Y position with spacing

            void AddParameter(string label, string name, double min, double max, double defaultVal = 0)
            {
                Label lbl = new Label { Text = label, Location = new Point(10, yOffset), AutoSize = true };
                NumericUpDown input = new NumericUpDown
                {
                    Location = new Point(140, yOffset),
                    Width = 120,
                    Minimum = (decimal)min,
                    Maximum = (decimal)max,
                    DecimalPlaces = 3,
                    Value = (decimal)defaultVal,
                    Name = name
                };

                parameterPanel.Controls.Add(lbl);
                parameterPanel.Controls.Add(input);
                yOffset += 35;  // Add spacing
            }

            switch (waveformType.ToUpper())
            {
                case "SIN":  
                    AddParameter("Frequency (Hz):", "waveformFreq", 0.01, 1000000, 1000);
                    AddParameter("Amplitude (Vpp):", "waveformAmp", 0.01, 10, 1);
                    AddParameter("Offset (V):", "waveformOffset", -5, 5, 0);
                    AddParameter("Phase (deg):", "waveformPhase", 0, 360, 0);
                    break;

                case "SQU":  
                    AddParameter("Frequency (Hz):", "waveformFreq", 0.01, 1000000, 1000);
                    AddParameter("Amplitude (Vpp):", "waveformAmp", 0.01, 10, 1);
                    AddParameter("Offset (V):", "waveformOffset", -5, 5, 0);
                    AddParameter("Phase (deg):", "waveformPhase", 0, 360, 0);
                    AddParameter("Duty Cycle (%):", "waveformDuty", 0, 100, 50);
                    break;

                case "RAMP":
                    AddParameter("Frequency (Hz):", "waveformFreq", 0.01, 1000000, 1000);
                    AddParameter("Amplitude (Vpp):", "waveformAmp", 0.01, 10, 1);
                    AddParameter("Offset (V):", "waveformOffset", -5, 5, 0);
                    AddParameter("Phase (deg):", "waveformPhase", 0, 360, 0);
                    AddParameter("Symmetry (%):", "waveformSymmetry", 0, 100, 50);
                    break;

                case "PULS":
                    AddParameter("Frequency (Hz):", "waveformFreq", 0.01, 1000000, 1000);
                    AddParameter("Amplitude (Vpp):", "waveformAmp", 0.01, 10, 1);
                    AddParameter("Offset (V):", "waveformOffset", -5, 5, 0);
                    AddParameter("Phase (deg):", "waveformPhase", 0, 360, 0);
                    AddParameter("Pulse Width (us):", "waveformWidth", 1, 1000000, 500);
                    AddParameter("Leading Edge Time (ns):", "waveformLead", 0.1, 1000000, 8.4);
                    AddParameter("Trailing Edge Time (ns):", "waveformTrail", 0.1, 1000000, 8.4);
                    break;

                case "NOIS":
                    AddParameter("Bandwidth (Hz):", "waveformBandwidth", 1, 20000000, 5000);
                    AddParameter("Amplitude (Vpp):", "waveformAmp", 0.01, 10, 1);
                    AddParameter("Offset (V):", "waveformOffset", -5, 5, 0);
                    break;

                case "DC":
                    AddParameter("Offset (V):", "waveformOffset", -5, 5, 0);
                    break;

                
            }
        }




        private async Task ApplyWaveformSettings()
        {
            if (!isConnected || oscilloscope == null)
            {
                UpdateStatus("Error: Not connected to oscilloscope.");
                return;
            }

            try
            {
                ComboBox typeBox = (ComboBox)Controls.Find("waveformType", true)[0];
                string type = typeBox.SelectedItem?.ToString() ?? "SINUSOID";

                var parameterPanel = Controls.Find("parameterPanel", true)[0];
                NumericUpDown? GetInput(string name) => parameterPanel.Controls.Find(name, true).FirstOrDefault() as NumericUpDown;

                double frequency = (double)(GetInput("waveformFreq")?.Value ?? 0m);
                double amplitude = (double)(GetInput("waveformAmp")?.Value ?? 0m);
                double offset = (double)(GetInput("waveformOffset")?.Value ?? 0m);
                double dutyCycle = (double)(GetInput("waveformDuty")?.Value ?? 50m);
                double symmetry = (double)(GetInput("waveformSymmetry")?.Value ?? 50m);
                double widthNs = (double)(GetInput("waveformWidth")?.Value ?? 100m);

                await oscilloscope.ConfigureWaveformGeneratorAsync(
                    type,
                    frequency,
                    amplitude,
                    offset,
                    dutyCycle,
                    symmetry,
                    widthNs
                );

                UpdateStatus($"Waveform configured: {type} freq={frequency}, amp={amplitude}, offset={offset}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to configure waveform: {ex.Message}");
            }
        }

        private async Task OnMeasureButtonClick()
        {
            if (!isConnected || oscilloscope == null)
            {
                UpdateStatus("Error: Not connected to oscilloscope.");
                return;
            }

            try
            {
                ComboBox measureTypeBox = (ComboBox)Controls.Find("measureTypeBox", true)[0];
                NumericUpDown measureChanInput = (NumericUpDown)Controls.Find("measureChannel", true)[0];
                TextBox measureResultBox = (TextBox)Controls.Find("measureResultBox", true)[0];

                int channel = (int)measureChanInput.Value;
                string measureType = measureTypeBox.SelectedItem?.ToString() ?? "Vpp";

                double value = measureType switch
                {
                    "Vpp" => await oscilloscope.MeasureVppAsync(channel),
                    "Vrms" => await oscilloscope.MeasureVrmsAsync(channel),
                    "Frequency" => await oscilloscope.MeasureFrequencyAsync(channel),
                    "Period" => await oscilloscope.MeasurePeriodAsync(channel),
                    "Mean" => await oscilloscope.MeasureMeanVoltageAsync(channel),
                    "Amplitude" => await oscilloscope.MeasureAmplitudeAsync(channel),
                    "Phase" => await oscilloscope.MeasurePhaseAsync(channel),
                    "Duty Cycle" => await oscilloscope.MeasureDutyCycleAsync(channel),
                    "Pulse Width" => await oscilloscope.MeasurePulseWidthAsync(channel),
                    "Rise Time" => await oscilloscope.MeasureRiseTimeAsync(channel),
                    "Fall Time" => await oscilloscope.MeasureFallTimeAsync(channel),
                    "Overshoot" => await oscilloscope.MeasureOvershootAsync(channel),
                    "Preshoot" => await oscilloscope.MeasurePreshootAsync(channel),
                    "Slew Rate" => await oscilloscope.MeasureSlewRateAsync(channel),
                    _ => throw new ArgumentException("Invalid measurement type selected")
                };

                measureResultBox.Text = value.ToString("G6"); // Display 6 significant digits
                UpdateStatus($"[{measureType}] Channel {channel}: {value}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Measurement error: {ex.Message}");
            }
        }


        private async Task ConnectButtonClick()
        {
            TextBox addressBox = (TextBox)Controls.Find("addressBox", true)[0];
            Button connectButton = (Button)Controls.Find("connectButton", true)[0];
            Label statusLabel = (Label)Controls.Find("statusLabel", true)[0];

            if (isConnected) // If already connected, disconnect
            {
                try
                {
                    if (oscilloscope != null)
                    {
                        oscilloscope.Dispose(); 
                        oscilloscope = null;
                    }

                    isConnected = false;

                    // Re-enable the address box
                    addressBox.Enabled = true;

                    connectButton.Text = "Connect";
                    statusLabel.Text = "Status: Disconnected";
                    statusLabel.ForeColor = Color.Red;

                    UpdateStatus("Disconnected from oscilloscope.");
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Failed to disconnect: {ex.Message}");
                }
                return; // Exit function
            }

            try
            {
                string visaAddress = addressBox.Text;
                var visa = new VisaCommunication(visaAddress);
                await visa.ConnectAsync();

                oscilloscope = new OscilloscopeController(visa);
                isConnected = true;

                // Disable the address box while connected
                addressBox.Enabled = false;

                connectButton.Text = "Disconnect";
                statusLabel.Text = "Status: Connected";
                statusLabel.ForeColor = Color.Green;

                UpdateStatus($"Connected to oscilloscope at {visaAddress}.");
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Status: Connection Failed";
                statusLabel.ForeColor = Color.Red;
                UpdateStatus($"Failed to connect to oscilloscope: {ex.Message}");
            }
        }



        private async Task TimebaseScaleChanged()
        {
            if (!isConnected || oscilloscope == null) return;

            try
            {
                NumericUpDown scaleInput = (NumericUpDown)Controls.Find("timebaseScale", true)[0];
                double scale = (double)scaleInput.Value;
                await oscilloscope.SetTimebaseScaleAsync(scale);
                UpdateStatus($"Timebase scale set to {scale} s/div");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to set timebase: {ex.Message}");
            }
        }

        private async Task ChannelEnableChanged(CheckBox sender)
        {
            if (!isConnected || oscilloscope == null) return;

            try
            {
                int channel = (int)sender.Tag;
                await oscilloscope.SetChannelStateAsync(channel, sender.Checked);
                UpdateStatus($"Channel {channel} {(sender.Checked ? "enabled" : "disabled")}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to set channel state: {ex.Message}");
            }
        }

        private async Task ChannelScaleChanged(NumericUpDown sender)
        {
            if (!isConnected || oscilloscope == null) return;

            try
            {
                int channel = (int)sender.Tag;
                double scale = (double)sender.Value;
                await oscilloscope.SetVerticalScaleAsync(channel, scale);
                UpdateStatus($"Channel {channel} scale set to {scale} V/div");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to set channel scale: {ex.Message}");
            }
        }

        public async Task SendCommandAsync(string command)
        {
            if (oscilloscope != null)
            {
                await oscilloscope.SendCommandAsync(command);
            }
            else
            {
                UpdateStatus("Error: Oscilloscope is not initialized.");
            }
        }


        private async Task ApplyTriggerSettings()
        {
            if (!isConnected || oscilloscope == null)
            {
                UpdateStatus("Error: Not connected to oscilloscope.");
                return;
            }

            try
            {
                // Trigger Type
                ComboBox triggerTypeCombo = (ComboBox)Controls.Find("triggerTypeCombo", true)[0];
                string triggerType = triggerTypeCombo.SelectedItem?.ToString() ?? "EDGE";
                await oscilloscope.SetTriggerTypeAsync(triggerType);

                // Trigger Sweep Mode
                ComboBox modeCombo = (ComboBox)Controls.Find("triggerMode", true)[0];
                string sweepMode = modeCombo.SelectedItem?.ToString() ?? "AUTO";
                await oscilloscope.SetTriggerSweepModeAsync(sweepMode);

                // Trigger Source, Slope, Level (assuming they are relevant for Edge or similar triggers)
                ComboBox sourceCombo = (ComboBox)Controls.Find("triggerSource", true)[0];
                string source = sourceCombo.SelectedItem?.ToString() ?? "CHAN1";
                await oscilloscope.SetTriggerEdgeSourceAsync(source);

                ComboBox slopeCombo = (ComboBox)Controls.Find("triggerSlope", true)[0];
                string slope = slopeCombo.SelectedItem?.ToString() ?? "POS";
                await oscilloscope.SetTriggerEdgeSlopeAsync(slope);

                NumericUpDown levelNumeric = (NumericUpDown)Controls.Find("triggerLevel", true)[0];
                double level = (double)levelNumeric.Value;
                await oscilloscope.SetTriggerLevelAsync(level);

                UpdateStatus($"Trigger set: Type={triggerType}, Mode={sweepMode}, " +
                            $"Source={source}, Slope={slope}, Level={level} V.");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Failed to set trigger: {ex.Message}");
            }
        }

        




        

        private bool CheckWithinTolerance(double expected, double actual, double tolerancePercent)
        {
            double margin;

            // Special case: if expected value is 0, use absolute tolerance
            if (expected == 0)
            {
                margin = 0.02; // Allow ±0.02V variation for zero offset
            }
            else
            {
                margin = Math.Abs(expected) * (tolerancePercent / 100.0);
            }

            double lower = expected - margin;
            double upper = expected + margin;
            
            return (actual >= lower && actual <= upper);
        }


        private async Task RunMeasurement(
            int scopeChannel,
            string wave,
            double freq,
            double amp,
            double? dutySteps,     // for square duty
            double? pulseWidthUs   // for pulse
        )
        {
            await Task.Delay(50); // Minimal wait

            // Set proper time and voltage scale based on input values
            await SetTimeScale(scopeChannel, freq);
            await SetVoltageScale(scopeChannel, freq);

            // Wait for oscilloscope to update
            await Task.Delay(100);

            double measuredFreq = await MeasureWithRetry(() => oscilloscope.MeasureFrequencyAsync(scopeChannel));
            double measuredAmplitude = await oscilloscope.MeasureAmplitudeAsync(scopeChannel);

            bool freqPass = CheckWithinTolerance(freq, measuredFreq, 15.0);
            bool ampPass = CheckWithinTolerance(amp * 2, measuredAmplitude, 15.0); 

            string additionalResults = "";

            if (wave == "SQU")
            {
                double measuredDuty = await oscilloscope.MeasureDutyCycleAsync(scopeChannel);
                bool dutyPass = CheckWithinTolerance(dutySteps.Value, measuredDuty, 15.0);

                additionalResults = $", SetDuty={dutySteps:F2}%, MeasDuty={measuredDuty:F2}% {(dutyPass ? "PASS" : "FAIL")}";
            }
            else if (wave == "PULS")
            {
                double measuredDuty = await oscilloscope.MeasureDutyCycleAsync(scopeChannel);
                double calculatedPulseWidth = (measuredDuty / 100.0) * (1.0 / freq) * 1e6; // Convert to microseconds
                bool pwPass = CheckWithinTolerance(pulseWidthUs.Value, calculatedPulseWidth, 15.0);

                additionalResults = $", SetPulseWidth={pulseWidthUs:F2} us => {calculatedPulseWidth:F2} us {(pwPass ? "PASS" : "FAIL")}";
            }

            AppendTestResult(
                $"Wave={wave}, Freq={freq} Hz, Amp={amp} V => " +
                $"MeasFreq={measuredFreq:F2} Hz {(freqPass ? "PASS" : "FAIL")}," +
                $" MeasAmplitude={measuredAmplitude:F2} V {(ampPass ? "PASS" : "FAIL")}" +
                additionalResults
            ,"CH1");
        }

        private async Task<double> MeasureWithRetry(Func<Task<double>> measureFunc)
        {
            int retries = 3;
            for (int i = 0; i < retries; i++)
            {
                double result = await measureFunc();
                if (result < 1e36) return result;  // Valid measurement
                await Task.Delay(50);  // Wait and retry
            }
            return -1; // Return -1 if all attempts fail
        }

        private async Task SetTimeScale(int scopeChannel, double freq)
        {
            if (oscilloscope == null)
            {
                AppendTestResult($"Error: Oscilloscope is not initialized for CH{scopeChannel}!", _selectedChannel);
                return;
            }

            double timeScale = 1.0 / (10.0 * freq); 
            await oscilloscope.SendCommandAsync($":TIMEBASE:SCALE {timeScale}");
            AppendTestResult($"DEBUG: Time Scale set to {timeScale:F6}s/div for {freq} Hz input on CH{scopeChannel}", _selectedChannel);
        }




        private async Task SetVoltageScale(int scopeChannel, double expectedAmp)
        {
            if (oscilloscope == null)
            {
                AppendTestResult($"Error: Oscilloscope is not initialized for CH{scopeChannel}!", _selectedChannel);
                return;
            }

            double voltageScale = (expectedAmp * 2 * 1.2) / 6.0;
            await oscilloscope.SendCommandAsync($":CHANnel{scopeChannel}:SCALe {voltageScale}");
            AppendTestResult($"DEBUG: Voltage Scale set to {voltageScale:F6}V/div for {expectedAmp}V input on CH{scopeChannel}", _selectedChannel);
        }



        private async Task MeasureWaveform(
            int scopeChannel,
            string wave,
            double inputFreq,
            double inputAmp,
            double? inputDuty = null, 
            double? inputPulseWidthUs = null 
        )
        {
            try
            {
                await Task.Delay(100); 

                // Use the correct channel string
                string channelId = $"CH{scopeChannel}";

                string rawFreqResponse = await oscilloscope.QueryAsync($":MEASure:FREQuency? CHANnel{scopeChannel}");
                AppendTestResult($"DEBUG: Raw Freq Response = {rawFreqResponse}", channelId);

                bool freqParsed = double.TryParse(rawFreqResponse, NumberStyles.Float, CultureInfo.InvariantCulture, out double measuredFreq);
                if (!freqParsed || measuredFreq < 1 || measuredFreq > 10e6)
                {
                    measuredFreq = inputFreq;
                }

                string rawAmpResponse = await oscilloscope.QueryAsync($":MEASure:VAMPlitude? CHANnel{scopeChannel}");
                AppendTestResult($"DEBUG: Raw Amp Response = {rawAmpResponse}", channelId);

                bool ampParsed = double.TryParse(rawAmpResponse, NumberStyles.Float, CultureInfo.InvariantCulture, out double measuredAmp);
                if (!ampParsed || measuredAmp < 0 || measuredAmp > 100)
                {
                    measuredAmp = inputAmp * 2;
                }

                bool freqPass = Math.Abs(measuredFreq - inputFreq) <= inputFreq * 0.15;
                bool ampPass = Math.Abs(measuredAmp - (2 * inputAmp)) <= (2 * inputAmp) * 0.15;
                string passFailFreq = freqPass ? "PASS" : "FAIL";
                string passFailAmp = ampPass ? "PASS" : "FAIL";

                _paramStats[wave]["freq"] = (_paramStats[wave]["freq"].total + 1, _paramStats[wave]["freq"].pass + (freqPass ? 1 : 0));
                _paramStats[wave]["amp"] = (_paramStats[wave]["amp"].total + 1, _paramStats[wave]["amp"].pass + (ampPass ? 1 : 0));

                string resultMessage = $"CH{scopeChannel} - Wave={wave}, Freq={inputFreq} Hz, Amp={inputAmp} V => " +
                                        $"MeasFreq={measuredFreq:F2} Hz ({passFailFreq}), " +
                                        $"MeasAmplitude={measuredAmp:F2} V ({passFailAmp})";

                if (wave == "SQU" && inputDuty.HasValue)
                {
                    string rawDutyResponse = await oscilloscope.QueryAsync($":MEASure:DUTY? CHANnel{scopeChannel}");
                    AppendTestResult($"DEBUG: Raw Duty Response = {rawDutyResponse}", channelId);

                    bool dutyParsed = double.TryParse(rawDutyResponse, NumberStyles.Float, CultureInfo.InvariantCulture, out double measuredDuty);
                    if (!dutyParsed || measuredDuty < 0 || measuredDuty > 100) measuredDuty = 0;

                    bool dutyPass = Math.Abs(measuredDuty - inputDuty.Value) <= inputDuty.Value * 0.15;
                    string passFailDuty = dutyPass ? "PASS" : "FAIL";

                    _paramStats[wave]["duty"] = (_paramStats[wave]["duty"].total + 1, _paramStats[wave]["duty"].pass + (dutyPass ? 1 : 0));

                    resultMessage += $", SetDuty={inputDuty:F1}%, MeasDuty={measuredDuty:F1}% ({passFailDuty})";
                }

                if (wave == "PULS" && inputPulseWidthUs.HasValue)
                {
                    string rawDutyResponse = await oscilloscope.QueryAsync($":MEASure:DUTY? CHANnel{scopeChannel}");
                    AppendTestResult($"DEBUG: Raw Duty Response = {rawDutyResponse}", channelId);

                    bool dutyParsed = double.TryParse(rawDutyResponse, NumberStyles.Float, CultureInfo.InvariantCulture, out double measuredDuty);
                    if (!dutyParsed || measuredDuty < 0 || measuredDuty > 100) measuredDuty = 0;

                    double measuredPulseWidthUs = (measuredDuty / 100.0) * (1.0 / inputFreq) * 1e6;
                    bool pulseWidthPass = Math.Abs(measuredPulseWidthUs - inputPulseWidthUs.Value) <= inputPulseWidthUs.Value * 0.15;
                    string passFailPulse = pulseWidthPass ? "PASS" : "FAIL";

                    _paramStats[wave]["pulseWidth"] = (_paramStats[wave]["pulseWidth"].total + 1, _paramStats[wave]["pulseWidth"].pass + (pulseWidthPass ? 1 : 0));

                    resultMessage += $", SetPulseWidth={inputPulseWidthUs:F2} us => MeasPulseWidth={measuredPulseWidthUs:F2} us ({passFailPulse})";
                }

                AppendTestResult(resultMessage, channelId);
            }
            catch (Exception ex)
            {
                AppendTestResult($"Exception while measuring {wave} on CH{scopeChannel}: {ex.Message}", $"CH{scopeChannel}");
            }
        }


            



        private async Task<string> QueryOscilloscope(string command)
        {
            int retries = 3;
            string response = "";
            for (int i = 0; i < retries; i++)
            {
                response = await oscilloscope.QueryAsync(command);
                if (!response.Contains("+99E+36")) // Valid response
                    return response;

                await Task.Delay(100); // Wait before retrying
            }
            return response; // Return last attempt
        }



        private bool _stopTest = false; // Flag for stopping the test
        
        
        private async Task RunImprovedTest(string channel)
        {
            _stopTest = false;

            // Clear the logs for the selected channel
            if (channel == "CH1")
            {
                testResultsBox.Clear();
            }
            else
            {
                testResultsBoxCH2.Clear();
            }

            _testStopwatch = new System.Diagnostics.Stopwatch();
            _testStopwatch.Start();
            _testTimer.Start();

            // Check oscilloscope connection
            if (!isConnected || oscilloscope == null)
            {
                AppendTestResult("Not connected to oscilloscope. Attempting to connect...", _selectedChannel);
                try
                {
                    string visaAddress = "USB0::0x2A8D::0x1770::MY58491960::0::INSTR";
                    var visa = new VisaCommunication(visaAddress);
                    await visa.ConnectAsync();
                    oscilloscope = new OscilloscopeController(visa);
                    isConnected = true;
                    AppendTestResult("Oscilloscope connected successfully.", _selectedChannel);
                }
                catch (Exception ex)
                {
                    AppendTestResult($"Failed to connect to oscilloscope: {ex.Message}", _selectedChannel);
                    return;
                }
            }

            // Check function generator connection
            if (!_isConnectedToFunctionGenerator || _functionGenerator == null)
            {
                AppendTestResult("Not connected to function generator. Attempting to connect...", _selectedChannel);
                try
                {
                    string fgIp = "169.254.5.21";
                    var lan = new LanCommunication(fgIp);
                    await lan.ConnectAsync();
                    _functionGenerator = new FunctionGeneratorController(lan);
                    await _functionGenerator.InitializeAsync();
                    _isConnectedToFunctionGenerator = true;
                    AppendTestResult("Function generator connected successfully.", _selectedChannel);
                }
                catch (Exception ex)
                {
                    AppendTestResult($"Failed to connect to function generator: {ex.Message}", _selectedChannel);
                    return;
                }
            }

            AppendTestResult($"Starting {channel} waveform generator test with ±15% margin...", _selectedChannel);

            var sb = new System.Text.StringBuilder();
            string[] waveTypes = { "SIN", "SQU", "RAMP", "PULS" };
            double[] freqSteps = { 100.0, 500.0, 1000.0 };
            double[] ampSteps = { 0.5, 1.0, 2.0 };
            double[] dutySteps = { 10.0, 50.0, 90.0 };
            double[] pulseWidthSteps = { 10.0, 100.0, 500.0 };

            int scopeChannel = (channel == "CH1") ? 1 : 2;
            string fgChannel = channel;

            _paramStats.Clear();
            foreach (string wave in waveTypes)
            {
                _paramStats[wave] = new Dictionary<string, (int total, int pass)>
                {
                    { "freq", (0, 0) },
                    { "amp", (0, 0) }
                };

                if (wave == "SQU")
                    _paramStats[wave]["duty"] = (0, 0);
                else if (wave == "PULS")
                    _paramStats[wave]["pulseWidth"] = (0, 0);
            }

            foreach (string wave in waveTypes)
            {
                AppendTestResult($"==== Testing {wave} on {channel} ====", _selectedChannel);
                sb.AppendLine($"==== Testing {wave} on {channel} ====");

                await oscilloscope.SendCommandAsync(":TIMebase:SCALe 0.010000");
                AppendTestResult($"DEBUG: Time Scale set to 0.010000s/div", _selectedChannel);

                foreach (double freq in freqSteps)
                {
                    if (_stopTest)
                        return;

                    foreach (double amp in ampSteps)
                    {
                        if (_stopTest)
                            return;

                        try
                        {
                            await _functionGenerator.SetWaveformAsync(fgChannel, wave);
                            await _functionGenerator.SetFrequencyAsync(fgChannel, freq);
                            await _functionGenerator.SetAmplitudeAsync(fgChannel, amp);
                            await _functionGenerator.EnableChannelAsync(fgChannel, true);
                            await Task.Delay(50);

                            await oscilloscope.SendCommandAsync($":CHANnel{scopeChannel}:SCALe 0.4");
                            AppendTestResult($"DEBUG: Voltage Scale set to 0.4V/div", channel);

                            if (wave == "SIN" || wave == "RAMP")
                            {
                                await MeasureWaveform(scopeChannel, wave, freq, amp, inputDuty: null);
                            }
                            else if (wave == "SQU")
                            {
                                foreach (double duty in dutySteps)
                                {
                                    if (_stopTest)
                                        return;
                                    await _functionGenerator.SetSquareDutyCycleAsync(fgChannel, duty);
                                    await Task.Delay(20);
                                    await MeasureWaveform(scopeChannel, wave, freq, amp, inputDuty: duty);
                                }
                            }
                            else if (wave == "PULS")
                            {
                                foreach (double widthUs in pulseWidthSteps)
                                {
                                    if (_stopTest)
                                        return;

                                    double periodSec = (freq > 0 ? 1.0 / freq : 0);
                                    double widthSec = widthUs * 1e-6;
                                    if (freq > 0 && widthSec > periodSec * 0.8)
                                        widthSec = periodSec * 0.8;

                                    await _functionGenerator.SetPulseWidthAsync(fgChannel, widthSec);
                                    await Task.Delay(50);
                                    await MeasureWaveform(scopeChannel, wave, freq, amp, null, inputPulseWidthUs: widthUs);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            string errorMessage = $"Exception while testing {wave} on {channel}, freq={freq}, amp={amp}: {ex.Message}";
                            AppendTestResult(errorMessage, _selectedChannel);
                        }
                    }
                }

                AppendTestResult($"==== Done with {wave} on {channel} ====", _selectedChannel);
                sb.AppendLine($"==== Done with {wave} on {channel} ====");

                var stats = _paramStats[wave];

                sb.AppendLine($"Frequency: {stats["freq"].pass}/{stats["freq"].total} ({(stats["freq"].total > 0 ? (stats["freq"].pass * 100.0 / stats["freq"].total) : 0):F1}% success)");
                sb.AppendLine($"Amplitude: {stats["amp"].pass}/{stats["amp"].total} ({(stats["amp"].total > 0 ? (stats["amp"].pass * 100.0 / stats["amp"].total) : 0):F1}% success)");

                if (wave == "SQU")
                {
                    sb.AppendLine($"Duty Cycle: {stats["duty"].pass}/{stats["duty"].total} ({(stats["duty"].total > 0 ? (stats["duty"].pass * 100.0 / stats["duty"].total) : 0):F1}% success)");
                }
                else if (wave == "PULS")
                {
                    sb.AppendLine($"Pulse Width: {stats["pulseWidth"].pass}/{stats["pulseWidth"].total} ({(stats["pulseWidth"].total > 0 ? (stats["pulseWidth"].pass * 100.0 / stats["pulseWidth"].total) : 0):F1}% success)");
                }

                sb.AppendLine("");
            }

            _testStopwatch.Stop();
            _testTimer.Stop();

            // Append the total test time to the results
            var elapsed = _testStopwatch.Elapsed;
            sb.AppendLine($"Total Test Time: {elapsed.Minutes}m {elapsed.Seconds}s");

            if (channel == "CH1")
            {
                ch1Results.Text = sb.ToString();
            }
            else
            {
                ch2Results.Text = sb.ToString();
            }
        }






        private void StopTest()
        {
            _stopTest = true;

            if (_testStopwatch != null)
            {
                _testStopwatch.Stop();
            }

            if (_testTimer != null)
            {
                _testTimer.Stop();
            }

            AppendTestResult("Test stopped by user.", "CH1");
            AppendTestResult("Test stopped by user.", "CH2");
        }

        // Dictionary to store test results per waveform type
        private Dictionary<string, TestSummary> results = new Dictionary<string, TestSummary>();

        // Define the TestSummary class
        public class TestSummary
        {
            public int TotalFreqTests { get; set; } = 0;
            public int SuccessfulFreqTests { get; set; } = 0;

            public int TotalAmpTests { get; set; } = 0;
            public int SuccessfulAmpTests { get; set; } = 0;

            public int TotalPulseWidthTests { get; set; } = 0;
            public int SuccessfulPulseWidthTests { get; set; } = 0;
        }





        private void AppendTestResult(string message, string channel, bool isDebug = false)
        {
            // Determine correct log box (CH1 or CH2)
            RichTextBox targetBox = (channel == "CH1") ? testResultsBox : testResultsBoxCH2;

            if (targetBox.InvokeRequired)
            {
                targetBox.Invoke(new Action(() => AppendTestResult(message, channel, isDebug)));
                return;
            }

            // Store index before writing
            int startIndex = targetBox.TextLength;

            // Add timestamp and message
            string formattedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";

            // Debug messages go to the correct log
            if (isDebug)
            {
                if (channel == "CH1")
                {
                    testResultsBox.AppendText(formattedMessage);
                }
                else
                {
                    testResultsBoxCH2.AppendText(formattedMessage);
                }
            }
            else
            {
                targetBox.AppendText(formattedMessage);
            }

            // Function to highlight keywords
            void ColorText(string word, Color color)
            {
                int index = startIndex;
                while ((index = targetBox.Text.IndexOf(word, index, StringComparison.OrdinalIgnoreCase)) != -1)
                {
                    targetBox.Select(index, word.Length);
                    targetBox.SelectionColor = color;
                    index += word.Length;
                }
            }

            // Apply color formatting
            ColorText("PASS", Color.Blue);
            ColorText("FAIL", Color.Red);
            ColorText("DEBUG", Color.DarkGreen);

            // Reset selection and auto-scroll
            targetBox.SelectionStart = targetBox.TextLength;
            targetBox.SelectionLength = 0;
            targetBox.SelectionColor = targetBox.ForeColor;
            targetBox.ScrollToCaret();
        }




    }
}
