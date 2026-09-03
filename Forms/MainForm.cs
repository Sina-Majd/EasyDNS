using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EasyDNS.Models;
using EasyDNS.Services;
using EasyDNS.UI;

namespace EasyDNS.Forms
{
    public partial class MainForm : Form
    {
        #region Win32 API for Window Drag, Resize & Modern Corners
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int ResizeBorderWidth = 7;
        #endregion

        private readonly DnsManagerService _dnsManager;
        private readonly LatencyBenchmarkService _benchmarkService;
        private readonly PresetRepository _presetRepo;

        private List<NetworkAdapterInfo> _adapters;
        private NetworkAdapterInfo _selectedAdapter;
        private string _selectedCategory = "All";
        private string _searchQuery = "";
        private bool _isBenchmarkingAll = false;
        private CancellationTokenSource _benchmarkCts;
        private DnsPreset _lastAppliedPreset;
        private Image _appLogoImage;

        // Custom Window Title Bar
        private Panel _titleBar;
        private PictureBox _picLogo;
        private Label _lblAppTitle;
        private Panel _panelAdapterDropdown;
        private Label _lblAdapterTitle;
        private ModernComboBox _cmbAdapters;
        private ModernRefreshButton _btnRefreshAdapters;
        private WindowControlButton _btnMinimize;
        private WindowControlButton _btnMaximize;
        private WindowControlButton _btnClose;

        // Main Layout
        private TableLayoutPanel _mainLayout;
        private Panel _panelLeft;
        private Panel _panelRight;

        // Left Panel (Presets)
        private ModernInputField _txtSearch;
        private FlowLayoutPanel _panelCategories;
        private DarkScrollPanel _presetsScrollPanel;
        private ModernButton _btnBenchmarkAll;
        private ModernButton _btnFastestDns;

        // Right Panel (Controls & Logs)
        private ModernCard _cardCurrentConfig;
        private Label _lblCurrentPrimaryDns;
        private Label _lblCurrentSecondaryDns;
        private Label _lblCurrentLatency;
        private ModernButton _btnTestCurrentLatency;

        private ModernCard _cardCustomDns;
        private ModernInputField _txtPrimaryDns;
        private ModernInputField _txtSecondaryDns;
        private ModernButton _btnSwapDns;
        private ModernButton _btnClearDns;
        private ModernButton _btnSaveCustomPreset;

        private ModernCard _cardActions;
        private ModernButton _btnApplyDns;
        private ModernButton _btnResetDhcp;
        private ModernButton _btnFlushDns;

        private ModernCard _cardLogs;
        private DarkLogConsole _darkLogConsole;
        private ModernButton _btnClearLogs;

        public MainForm()
        {
            InitializeComponent();

            _dnsManager = new DnsManagerService();
            _benchmarkService = new LatencyBenchmarkService();
            _presetRepo = new PresetRepository();

            LoadAppResources();
            ConfigureFormWindow();
            BuildCustomUi();
            LoadAdapters();
            LogMessage("EasyDNS initialized successfully.");
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshPresetsList();
            AdjustPresetCardsWidth();
            if (_presetsScrollPanel != null)
            {
                _presetsScrollPanel.RecalculateContentHeight();
            }
        }

        private void LoadAppResources()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();

                // 1. Load Window Icon from embedded resource stream (or application executable)
                try
                {
                    using (var icoStream = asm.GetManifestResourceStream("EasyDNS.app.ico"))
                    {
                        if (icoStream != null)
                        {
                            Icon = new Icon(icoStream);
                        }
                    }
                }
                catch { }

                if (Icon == null)
                {
                    try
                    {
                        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    }
                    catch { }
                }

                // 2. Load Logo Image directly from embedded resource stream
                try
                {
                    using (var logoStream = asm.GetManifestResourceStream("EasyDNS.Resources.app_logo.jpg"))
                    {
                        if (logoStream != null)
                        {
                            _appLogoImage = Image.FromStream(logoStream);
                        }
                    }
                }
                catch { }

                // Fallback to disk if running in development environment
                if (_appLogoImage == null)
                {
                    string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app_logo.jpg");
                    if (File.Exists(logoPath))
                    {
                        _appLogoImage = Image.FromFile(logoPath);
                    }
                }
            }
            catch { }
        }

        private void ConfigureFormWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.BackgroundDark;
            ForeColor = Theme.TextPrimary;
            Font = Theme.BodyFont;
            Size = new Size(1160, 750);
            MinimumSize = new Size(1000, 640);
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyModernWindowCorners();
        }

        private void ApplyModernWindowCorners()
        {
            try
            {
                int preference = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            }
            catch { }

            ApplyRegionCorners();
        }

        private void ApplyRegionCorners()
        {
            if (WindowState == FormWindowState.Maximized)
            {
                Region = null;
            }
            else
            {
                try
                {
                    IntPtr hRgn = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 14, 14);
                    Region = Region.FromHrgn(hRgn);
                    DeleteObject(hRgn);
                }
                catch { }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRegionCorners();
            Invalidate();
        }

        #region Resizable Window Border & Drag Support
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_NCHITTEST && WindowState != FormWindowState.Maximized)
            {
                var cursor = PointToClient(Cursor.Position);
                int w = ClientSize.Width;
                int h = ClientSize.Height;

                if (cursor.X <= ResizeBorderWidth && cursor.Y <= ResizeBorderWidth)
                    m.Result = (IntPtr)HTTOPLEFT;
                else if (cursor.X >= w - ResizeBorderWidth && cursor.Y <= ResizeBorderWidth)
                    m.Result = (IntPtr)HTTOPRIGHT;
                else if (cursor.X <= ResizeBorderWidth && cursor.Y >= h - ResizeBorderWidth)
                    m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (cursor.X >= w - ResizeBorderWidth && cursor.Y >= h - ResizeBorderWidth)
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (cursor.X <= ResizeBorderWidth)
                    m.Result = (IntPtr)HTLEFT;
                else if (cursor.X >= w - ResizeBorderWidth)
                    m.Result = (IntPtr)HTRIGHT;
                else if (cursor.Y <= ResizeBorderWidth)
                    m.Result = (IntPtr)HTTOP;
                else if (cursor.Y >= h - ResizeBorderWidth)
                    m.Result = (IntPtr)HTBOTTOM;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (WindowState != FormWindowState.Maximized)
            {
                using (var path = Theme.CreateRoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 7))
                {
                    using (var pen = new Pen(Theme.BorderHighlight, 1))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
            else
            {
                using (var pen = new Pen(Theme.BorderColor, 1))
                {
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            }
        }

        private void DragWindow(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
        #endregion

        private void BuildCustomUi()
        {
            SuspendLayout();

            // 1. Unified Modern Header Bar
            BuildUnifiedHeader();

            // 2. Main Split View Layout
            _mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(12, 8, 12, 10),
                BackColor = Theme.BackgroundDark
            };
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            _mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));

            BuildLeftPanel();
            BuildRightPanel();

            _mainLayout.Controls.Add(_panelLeft, 0, 0);
            _mainLayout.Controls.Add(_panelRight, 1, 0);

            Controls.Add(_mainLayout);
            _mainLayout.BringToFront();

            ResumeLayout(false);
        }

        private void BuildUnifiedHeader()
        {
            _titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Theme.TitleBarBackground,
                Padding = new Padding(12, 0, 0, 0)
            };
            _titleBar.MouseDown += DragWindow;
            _titleBar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(Theme.BorderColor, 1))
                {
                    e.Graphics.DrawLine(pen, 0, _titleBar.Height - 1, _titleBar.Width, _titleBar.Height - 1);
                }
            };

            // App Logo
            _picLogo = new PictureBox
            {
                Size = new Size(26, 26),
                Location = new Point(12, 12),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Image = _appLogoImage
            };
            _picLogo.MouseDown += DragWindow;

            // App Title
            _lblAppTitle = new Label
            {
                Text = "EasyDNS",
                Font = Theme.TitleBarFont,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(44, 15),
                BackColor = Color.Transparent
            };
            _lblAppTitle.MouseDown += DragWindow;

            // Adapter Selector Container (Centered in Title Bar)
            _panelAdapterDropdown = new Panel
            {
                Height = 36,
                Width = 414,
                BackColor = Color.Transparent
            };

            Action centerAdapterDropdown = delegate
            {
                _panelAdapterDropdown.Location = new Point(Math.Max(140, (_titleBar.ClientSize.Width - _panelAdapterDropdown.Width) / 2), 7);
            };
            _titleBar.SizeChanged += delegate(object s, EventArgs e) { centerAdapterDropdown(); };

            _lblAdapterTitle = new Label
            {
                Text = "Adapter:",
                Font = Theme.SmallFont,
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(0, 10)
            };

            _cmbAdapters = new ModernComboBox
            {
                Width = 320,
                Location = new Point(54, 4)
            };
            _cmbAdapters.SelectedIndexChanged += delegate(object s, EventArgs e) { OnAdapterSelectionChanged(); };

            _btnRefreshAdapters = new ModernRefreshButton
            {
                Location = new Point(380, 4),
                Size = new Size(28, 28)
            };
            _btnRefreshAdapters.Click += delegate(object s, EventArgs e) { LoadAdapters(); };

            _panelAdapterDropdown.Controls.Add(_lblAdapterTitle);
            _panelAdapterDropdown.Controls.Add(_cmbAdapters);
            _panelAdapterDropdown.Controls.Add(_btnRefreshAdapters);
            centerAdapterDropdown();

            // Window Control Buttons (Min, Max, Close)
            _btnClose = new WindowControlButton(WindowControlButton.ButtonType.Close)
            {
                Dock = DockStyle.Right,
                Width = 46
            };
            _btnClose.Click += delegate(object s, EventArgs e) { Close(); };

            _btnMaximize = new WindowControlButton(WindowControlButton.ButtonType.Maximize)
            {
                Dock = DockStyle.Right,
                Width = 46
            };
            _btnMaximize.Click += delegate(object s, EventArgs e)
            {
                if (WindowState == FormWindowState.Maximized)
                {
                    WindowState = FormWindowState.Normal;
                    _btnMaximize.IsMaximized = false;
                }
                else
                {
                    MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
                    WindowState = FormWindowState.Maximized;
                    _btnMaximize.IsMaximized = true;
                }
                ApplyRegionCorners();
                _btnMaximize.Invalidate();
            };

            _btnMinimize = new WindowControlButton(WindowControlButton.ButtonType.Minimize)
            {
                Dock = DockStyle.Right,
                Width = 46
            };
            _btnMinimize.Click += delegate(object s, EventArgs e) { WindowState = FormWindowState.Minimized; };

            _titleBar.Controls.Add(_picLogo);
            _titleBar.Controls.Add(_lblAppTitle);
            _titleBar.Controls.Add(_panelAdapterDropdown);
            _titleBar.Controls.Add(_btnMinimize);
            _titleBar.Controls.Add(_btnMaximize);
            _titleBar.Controls.Add(_btnClose);

            Controls.Add(_titleBar);
        }

        private void BuildLeftPanel()
        {
            _panelLeft = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 0, 6, 0)
            };

            // Top Search & Benchmark Bar
            var topBar = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent };

            _txtSearch = new ModernInputField
            {
                PlaceholderText = "Search presets...",
                Size = new Size(358, 26),
                Location = new Point(0, 2)
            };
            _txtSearch.InnerTextBox.TextChanged += delegate(object s, EventArgs e)
            {
                _searchQuery = _txtSearch.Text.Trim();
                RefreshPresetsList();
            };

            _btnBenchmarkAll = new ModernButton
            {
                Text = "Ping All",
                NormalColor = Theme.AccentPrimary,
                HoverColor = Theme.AccentPrimaryHover,
                Font = Theme.SmallFont,
                BorderRadius = 5,
                Size = new Size(76, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(topBar.Width - 168, 2)
            };
            _btnBenchmarkAll.Click += async delegate(object s, EventArgs e) { await BenchmarkAllPresetsAsync(); };

            _btnFastestDns = new ModernButton
            {
                Text = "Best DNS",
                NormalColor = Theme.AccentSuccess,
                HoverColor = Theme.AccentSuccessHover,
                Font = Theme.SmallFont,
                BorderRadius = 5,
                Size = new Size(84, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(topBar.Width - 88, 2)
            };
            _btnFastestDns.Click += async delegate(object s, EventArgs e) { await SelectFastestDnsAsync(); };

            topBar.Controls.Add(_txtSearch);
            topBar.Controls.Add(_btnBenchmarkAll);
            topBar.Controls.Add(_btnFastestDns);

            // Category Filter Pills Bar
            _panelCategories = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.Transparent,
                WrapContents = false,
                AutoScroll = false,
                Padding = new Padding(0, 2, 0, 2)
            };
            BuildCategoryButtons();

            // Custom Dark Scroll Panel (Zero White Scrollbars!)
            _presetsScrollPanel = new DarkScrollPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.BackgroundDark
            };
            _presetsScrollPanel.SizeChanged += delegate(object s, EventArgs e)
            {
                AdjustPresetCardsWidth();
            };

            _panelLeft.Controls.Add(_presetsScrollPanel);
            _panelLeft.Controls.Add(_panelCategories);
            _panelLeft.Controls.Add(topBar);
            _presetsScrollPanel.BringToFront();
        }

        private void BuildCategoryButtons()
        {
            _panelCategories.Controls.Clear();
            var categories = _presetRepo.GetCategories();

            int[] widths = new int[] { 100, 135, 115 };
            int i = 0;

            foreach (var cat in categories)
            {
                string categoryName = cat;
                bool isSelected = (categoryName == _selectedCategory);
                int btnW = (i < widths.Length) ? widths[i] : 100;

                var btn = new ModernButton
                {
                    Text = categoryName,
                    Font = Theme.SmallFont,
                    NormalColor = isSelected ? Theme.AccentPrimary : Theme.CardBackground,
                    HoverColor = isSelected ? Theme.AccentPrimaryHover : Theme.CardHover,
                    CustomBorderColor = isSelected ? Theme.BorderHighlight : Theme.BorderColor,
                    BorderThickness = 1,
                    BorderRadius = 5,
                    Width = btnW,
                    Margin = new Padding(0, 0, 4, 0),
                    Height = 24
                };

                btn.Click += delegate(object s, EventArgs e)
                {
                    _selectedCategory = categoryName;
                    BuildCategoryButtons();
                    RefreshPresetsList();
                };

                _panelCategories.Controls.Add(btn);
                i++;
            }
        }

        private void BuildRightPanel()
        {
            _panelRight = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                AutoScroll = false, // Prevents unwanted focus scrolling/jumping!
                Padding = new Padding(6, 0, 0, 0)
            };

            // 1. Current Active DNS Card
            _cardCurrentConfig = new ModernCard
            {
                Location = new Point(0, 0),
                Size = new Size(_panelRight.Width - 6, 100),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                FillColor = Theme.CardBackground
            };

            var lblActiveTitle = new Label
            {
                Text = "Current Active DNS",
                Font = Theme.SubHeaderFont,
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(14, 8)
            };

            _lblCurrentPrimaryDns = new Label
            {
                Text = "Primary: --",
                Font = Theme.BodyBoldFont,
                ForeColor = Theme.AccentCyan,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(15, 34)
            };

            _lblCurrentSecondaryDns = new Label
            {
                Text = "Secondary: --",
                Font = Theme.BodyFont,
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(15, 60)
            };

            _lblCurrentLatency = new Label
            {
                Text = "● Latency: -- ms",
                Font = Theme.SmallFont,
                ForeColor = Theme.TextMuted,
                BackColor = Color.Transparent,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(_cardCurrentConfig.Width - 140, 36)
            };

            _btnTestCurrentLatency = new ModernButton
            {
                Text = "Ping Now",
                Font = Theme.SmallFont,
                NormalColor = Theme.InputBackground,
                HoverColor = Theme.CardHover,
                CustomBorderColor = Theme.BorderColor,
                BorderThickness = 1,
                BorderRadius = 5,
                Size = new Size(78, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(_cardCurrentConfig.Width - 92, 60)
            };
            _btnTestCurrentLatency.Click += async delegate(object s, EventArgs e) { await TestCurrentDnsLatencyAsync(); };

            _cardCurrentConfig.Controls.Add(lblActiveTitle);
            _cardCurrentConfig.Controls.Add(_lblCurrentPrimaryDns);
            _cardCurrentConfig.Controls.Add(_lblCurrentSecondaryDns);
            _cardCurrentConfig.Controls.Add(_lblCurrentLatency);
            _cardCurrentConfig.Controls.Add(_btnTestCurrentLatency);

            // 2. Custom DNS Input Card
            _cardCustomDns = new ModernCard
            {
                Location = new Point(0, 110),
                Size = new Size(_panelRight.Width - 6, 138),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                FillColor = Theme.CardBackground
            };

            var lblCustomTitle = new Label
            {
                Text = "Custom DNS Configuration",
                Font = Theme.SubHeaderFont,
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(14, 8)
            };

            var lblPrimaryPrompt = new Label
            {
                Text = "Primary IPv4:",
                Font = Theme.SmallFont,
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(15, 32)
            };

            _txtPrimaryDns = new ModernInputField
            {
                Text = "1.1.1.1",
                PlaceholderText = "e.g. 1.1.1.1",
                Location = new Point(15, 50),
                Size = new Size(160, 26)
            };

            var lblSecondaryPrompt = new Label
            {
                Text = "Secondary IPv4:",
                Font = Theme.SmallFont,
                ForeColor = Theme.TextSecondary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(190, 32)
            };

            _txtSecondaryDns = new ModernInputField
            {
                Text = "1.0.0.1",
                PlaceholderText = "e.g. 1.0.0.1",
                Location = new Point(190, 50),
                Size = new Size(160, 26)
            };

            _btnSwapDns = new ModernButton
            {
                Text = "Swap",
                Font = Theme.SmallFont,
                NormalColor = Theme.InputBackground,
                HoverColor = Theme.CardHover,
                CustomBorderColor = Theme.BorderColor,
                BorderThickness = 1,
                BorderRadius = 5,
                Size = new Size(62, 25),
                Location = new Point(15, 92)
            };
            _btnSwapDns.Click += delegate(object s, EventArgs e)
            {
                string temp = _txtPrimaryDns.Text;
                _txtPrimaryDns.Text = _txtSecondaryDns.Text;
                _txtSecondaryDns.Text = temp;
            };

            _btnClearDns = new ModernButton
            {
                Text = "Clear",
                Font = Theme.SmallFont,
                NormalColor = Theme.InputBackground,
                HoverColor = Theme.CardHover,
                CustomBorderColor = Theme.BorderColor,
                BorderThickness = 1,
                BorderRadius = 5,
                Size = new Size(62, 25),
                Location = new Point(83, 92)
            };
            _btnClearDns.Click += delegate(object s, EventArgs e)
            {
                _txtPrimaryDns.Text = "";
                _txtSecondaryDns.Text = "";
            };

            _btnSaveCustomPreset = new ModernButton
            {
                Text = "Save Preset",
                Font = Theme.SmallFont,
                NormalColor = Theme.InputBackground,
                HoverColor = Theme.CardHover,
                CustomBorderColor = Theme.BorderColor,
                BorderThickness = 1,
                BorderRadius = 5,
                Size = new Size(95, 25),
                Location = new Point(151, 92)
            };
            _btnSaveCustomPreset.Click += delegate(object s, EventArgs e) { SaveCustomPreset(); };

            _cardCustomDns.Controls.Add(lblCustomTitle);
            _cardCustomDns.Controls.Add(lblPrimaryPrompt);
            _cardCustomDns.Controls.Add(_txtPrimaryDns);
            _cardCustomDns.Controls.Add(lblSecondaryPrompt);
            _cardCustomDns.Controls.Add(_txtSecondaryDns);
            _cardCustomDns.Controls.Add(_btnSwapDns);
            _cardCustomDns.Controls.Add(_btnClearDns);
            _cardCustomDns.Controls.Add(_btnSaveCustomPreset);

            // 3. Action Buttons Card (Responsive Auto-Layout)
            _cardActions = new ModernCard
            {
                Location = new Point(0, 258),
                Size = new Size(_panelRight.Width - 6, 115),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                FillColor = Theme.CardBackground
            };

            _btnApplyDns = new ModernButton
            {
                Text = "Apply DNS to Adapter",
                Font = Theme.BodyBoldFont,
                NormalColor = Theme.AccentPrimary,
                HoverColor = Theme.AccentPrimaryHover,
                BorderRadius = 6,
                Height = 36,
                Location = new Point(14, 12)
            };
            _btnApplyDns.Click += delegate(object s, EventArgs e) { ApplyDnsFromInputs(); };

            _btnResetDhcp = new ModernButton
            {
                Text = "Reset to Default",
                Font = Theme.BodyBoldFont,
                NormalColor = Color.FromArgb(217, 119, 6),
                HoverColor = Color.FromArgb(245, 158, 11),
                BorderRadius = 6,
                Height = 34
            };
            _btnResetDhcp.Click += delegate(object s, EventArgs e) { ResetToDhcp(); };

            _btnFlushDns = new ModernButton
            {
                Text = "Flush DNS Cache",
                Font = Theme.BodyBoldFont,
                NormalColor = Color.FromArgb(5, 150, 105),
                HoverColor = Theme.AccentSuccess,
                BorderRadius = 6,
                Height = 34
            };
            _btnFlushDns.Click += delegate(object s, EventArgs e) { FlushDnsCache(); };

            Action layoutActionButtons = delegate
            {
                int totalW = Math.Max(200, _cardActions.ClientSize.Width - 28);
                int halfW = (totalW - 10) / 2;

                _btnApplyDns.Location = new Point(14, 12);
                _btnApplyDns.Width = totalW;

                _btnResetDhcp.Location = new Point(14, 56);
                _btnResetDhcp.Width = halfW;

                _btnFlushDns.Location = new Point(14 + halfW + 10, 56);
                _btnFlushDns.Width = halfW;
            };

            _cardActions.SizeChanged += delegate(object s, EventArgs e) { layoutActionButtons(); };

            _cardActions.Controls.Add(_btnApplyDns);
            _cardActions.Controls.Add(_btnResetDhcp);
            _cardActions.Controls.Add(_btnFlushDns);
            layoutActionButtons();

            // 4. Logs Console Card (Clean dynamic height, zero native white scrollbars!)
            _cardLogs = new ModernCard
            {
                Location = new Point(0, 383),
                Size = new Size(_panelRight.Width - 6, Math.Max(150, _panelRight.Height - 390)),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                FillColor = Theme.CardBackground
            };

            var lblLogsTitle = new Label
            {
                Text = "Activity Log",
                Font = Theme.SubHeaderFont,
                ForeColor = Theme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(14, 8)
            };

            _btnClearLogs = new ModernButton
            {
                Text = "Clear",
                Font = Theme.SmallFont,
                NormalColor = Theme.InputBackground,
                HoverColor = Theme.CardHover,
                BorderRadius = 4,
                Size = new Size(46, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(_cardLogs.Width - 60, 8)
            };
            _btnClearLogs.Click += delegate(object s, EventArgs e) { _darkLogConsole.ClearLogs(); };

            _darkLogConsole = new DarkLogConsole
            {
                Location = new Point(12, 32),
                Size = new Size(_cardLogs.Width - 24, _cardLogs.Height - 42),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            _cardLogs.Controls.Add(lblLogsTitle);
            _cardLogs.Controls.Add(_btnClearLogs);
            _cardLogs.Controls.Add(_darkLogConsole);

            _panelRight.Controls.Add(_cardCurrentConfig);
            _panelRight.Controls.Add(_cardCustomDns);
            _panelRight.Controls.Add(_cardActions);
            _panelRight.Controls.Add(_cardLogs);

            _panelRight.SizeChanged += delegate(object s, EventArgs e)
            {
                _cardLogs.Height = Math.Max(150, _panelRight.ClientSize.Height - 390);
            };
        }

        private void AdjustPresetCardsWidth()
        {
            if (_presetsScrollPanel == null || _presetsScrollPanel.ContentContainer == null) return;
            int targetWidth = _presetsScrollPanel.ContentContainer.Width - 4;
            if (targetWidth <= 50) return;

            foreach (Control ctrl in _presetsScrollPanel.ContentContainer.Controls)
            {
                var card = ctrl as DnsPresetCardControl;
                if (card != null)
                {
                    card.Width = targetWidth;
                }
            }
        }

        private void LoadAdapters()
        {
            _adapters = _dnsManager.GetNetworkAdapters();
            _cmbAdapters.Items.Clear();

            if (_adapters == null || _adapters.Count == 0)
            {
                _cmbAdapters.Items.Add("No active adapters found");
                _cmbAdapters.SelectedIndex = 0;
                _selectedAdapter = null;
                UpdateCurrentConfigView();
                return;
            }

            foreach (var adapter in _adapters)
            {
                _cmbAdapters.Items.Add(adapter);
            }

            int defaultIndex = 0;
            for (int i = 0; i < _adapters.Count; i++)
            {
                if (_adapters[i].IsOperational)
                {
                    defaultIndex = i;
                    break;
                }
            }

            _cmbAdapters.SelectedIndex = defaultIndex;
            _selectedAdapter = _adapters[defaultIndex];
            UpdateCurrentConfigView();
            LogMessage("Loaded " + _adapters.Count + " network adapter(s).");
        }

        private void OnAdapterSelectionChanged()
        {
            var adapter = _cmbAdapters.SelectedItem as NetworkAdapterInfo;
            if (adapter != null)
            {
                _selectedAdapter = adapter;
                UpdateCurrentConfigView();
                LogMessage("Selected adapter: " + adapter.Name);
            }
        }

        private void UpdateCurrentConfigView()
        {
            if (_selectedAdapter == null)
            {
                _lblCurrentPrimaryDns.Text = "Primary: None";
                _lblCurrentSecondaryDns.Text = "Secondary: None";
                UpdatePresetsActiveState();
                return;
            }

            if (_selectedAdapter.DnsServers.Count > 0)
            {
                _lblCurrentPrimaryDns.Text = "Primary: " + _selectedAdapter.DnsServers[0];
                _lblCurrentSecondaryDns.Text = "Secondary: " + (_selectedAdapter.DnsServers.Count > 1 ? _selectedAdapter.DnsServers[1] : "None");
            }
            else
            {
                _lblCurrentPrimaryDns.Text = "Primary: Automatic (Router)";
                _lblCurrentSecondaryDns.Text = "Secondary: None";
            }

            UpdatePresetsActiveState();
        }

        private void UpdatePresetsActiveState()
        {
            string currentPrimary = (_selectedAdapter != null && _selectedAdapter.DnsServers.Count > 0)
                ? _selectedAdapter.DnsServers[0]
                : null;

            var all = _presetRepo.GetAllPresets();
            foreach (var preset in all)
            {
                preset.IsActive = (!string.IsNullOrEmpty(currentPrimary) &&
                                   string.Equals(preset.PrimaryDns, currentPrimary, StringComparison.OrdinalIgnoreCase));
            }

            if (_presetsScrollPanel != null && _presetsScrollPanel.ContentContainer != null)
            {
                foreach (Control ctrl in _presetsScrollPanel.ContentContainer.Controls)
                {
                    var card = ctrl as DnsPresetCardControl;
                    if (card != null)
                    {
                        card.Invalidate();
                    }
                }
            }
        }

        private void RefreshPresetsList()
        {
            UpdatePresetsActiveState();

            _presetsScrollPanel.ContentContainer.SuspendLayout();
            _presetsScrollPanel.ContentContainer.Controls.Clear();
            _presetsScrollPanel.ResetScroll();

            var all = _presetRepo.GetAllPresets();
            var filtered = all.Where(delegate(DnsPreset p)
            {
                if (_selectedCategory != "All")
                {
                    if (_selectedCategory == "Universal" && p.Category != "Universal" && !p.IsCustom) return false;
                    if (_selectedCategory == "Persian" && p.Category != "Persian") return false;
                    if (_selectedCategory != "Universal" && _selectedCategory != "Persian" && p.Category != _selectedCategory) return false;
                }

                if (!string.IsNullOrEmpty(_searchQuery))
                {
                    string q = _searchQuery.ToLowerInvariant();
                    bool nameMatch = (p.Name != null && p.Name.ToLowerInvariant().Contains(q));
                    bool ipMatch = (p.PrimaryDns != null && p.PrimaryDns.Contains(q)) || (p.SecondaryDns != null && p.SecondaryDns.Contains(q));
                    bool descMatch = (p.Description != null && p.Description.ToLowerInvariant().Contains(q));
                    return nameMatch || ipMatch || descMatch;
                }

                return true;
            }).ToList();

            int cardWidth = Math.Max(260, _presetsScrollPanel.ContentContainer.Width - 4);
            int yPos = 2;

            foreach (var preset in filtered)
            {
                var card = new DnsPresetCardControl(preset)
                {
                    Location = new Point(0, yPos),
                    Width = cardWidth,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left
                };

                card.OnApplyClicked += delegate(DnsPreset target)
                {
                    ApplyPreset(target);
                };

                card.OnTestClicked += async delegate(DnsPreset target)
                {
                    card.UpdateLatencyView();
                    await _benchmarkService.MeasurePresetLatencyAsync(target);
                    card.UpdateLatencyView();
                };

                card.OnCardSelected += delegate(DnsPreset target)
                {
                    _txtPrimaryDns.Text = target.PrimaryDns;
                    _txtSecondaryDns.Text = target.SecondaryDns ?? "";
                    _lastAppliedPreset = target;
                    LogMessage("Loaded " + target.Name + " into inputs.");
                };

                card.OnDeleteClicked += delegate(DnsPreset target)
                {
                    if (MessageBox.Show("Delete custom preset '" + target.Name + "'?", "EasyDNS", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        _presetRepo.DeleteCustomPreset(target.Id);
                        RefreshPresetsList();
                        LogMessage("Deleted custom preset: " + target.Name);
                    }
                };

                card.OnIpCopied += delegate(string ip)
                {
                    LogMessage("Copied DNS IP to clipboard: " + ip);
                };

                _presetsScrollPanel.ContentContainer.Controls.Add(card);
                yPos += card.Height + 6;
            }

            _presetsScrollPanel.ContentContainer.ResumeLayout(true);
            _presetsScrollPanel.RecalculateContentHeight();
            AdjustPresetCardsWidth();
            _presetsScrollPanel.Invalidate();
        }

        private async Task BenchmarkAllPresetsAsync()
        {
            if (_isBenchmarkingAll)
            {
                if (_benchmarkCts != null)
                {
                    _benchmarkCts.Cancel();
                    LogMessage("Cancelling latency benchmark...");
                }
                return;
            }

            _isBenchmarkingAll = true;
            _benchmarkCts = new CancellationTokenSource();
            var ct = _benchmarkCts.Token;

            _btnBenchmarkAll.Text = "Cancel";
            _btnBenchmarkAll.NormalColor = Theme.AccentDanger;
            _btnBenchmarkAll.HoverColor = Theme.AccentDangerHover;
            _btnBenchmarkAll.Invalidate();

            LogMessage("Starting latency benchmark (UDP DNS query with ICMP fallback, concurrency: 5)...");

            var presets = _presetRepo.GetAllPresets();
            foreach (Control ctrl in _presetsScrollPanel.ContentContainer.Controls)
            {
                var card = ctrl as DnsPresetCardControl;
                if (card != null)
                {
                    card.Preset.IsCheckingLatency = true;
                    card.UpdateLatencyView();
                }
            }

            try
            {
                await _benchmarkService.BenchmarkAllAsync(presets, delegate(DnsPreset p, LatencyResult res)
                {
                    foreach (Control ctrl in _presetsScrollPanel.ContentContainer.Controls)
                    {
                        var card = ctrl as DnsPresetCardControl;
                        if (card != null && card.Preset.Id == p.Id)
                        {
                            card.UpdateLatencyView();
                            break;
                        }
                    }
                }, ct);
            }
            catch (OperationCanceledException) { }
            finally
            {
                foreach (Control ctrl in _presetsScrollPanel.ContentContainer.Controls)
                {
                    var card = ctrl as DnsPresetCardControl;
                    if (card != null)
                    {
                        card.Preset.IsCheckingLatency = false;
                        card.UpdateLatencyView();
                    }
                }

                _isBenchmarkingAll = false;
                if (_benchmarkCts != null)
                {
                    _benchmarkCts.Dispose();
                    _benchmarkCts = null;
                }

                _btnBenchmarkAll.Text = "Ping All";
                _btnBenchmarkAll.NormalColor = Theme.AccentPrimary;
                _btnBenchmarkAll.HoverColor = Theme.AccentPrimaryHover;
                _btnBenchmarkAll.Invalidate();

                if (ct.IsCancellationRequested)
                {
                    LogMessage("Benchmark cancelled by user.");
                }
                else
                {
                    LogMessage("Benchmark completed.");
                }
            }
        }

        private async Task SelectFastestDnsAsync()
        {
            LogMessage("Finding fastest DNS server...");
            await BenchmarkAllPresetsAsync();

            var fastest = _presetRepo.GetAllPresets()
                .Where(delegate(DnsPreset p) { return p.LatencyMs.HasValue && p.LatencyMs.Value > 0; })
                .OrderBy(delegate(DnsPreset p) { return p.LatencyMs.Value; })
                .FirstOrDefault();

            if (fastest != null)
            {
                _txtPrimaryDns.Text = fastest.PrimaryDns;
                _txtSecondaryDns.Text = fastest.SecondaryDns ?? "";
                _lastAppliedPreset = fastest;

                string msg = string.Format("Fastest DNS: {0} ({1} ms)\n\nWould you like to apply it now to '{2}'?",
                    fastest.Name, fastest.LatencyMs.Value, _selectedAdapter != null ? _selectedAdapter.Name : "Selected Adapter");

                if (MessageBox.Show(msg, "Fastest DNS Found", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    ApplyPreset(fastest);
                }
            }
            else
            {
                MessageBox.Show("No responsive DNS servers were detected in the benchmark.", "EasyDNS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task TestCurrentDnsLatencyAsync()
        {
            if (_selectedAdapter == null || _selectedAdapter.DnsServers.Count == 0)
            {
                _lblCurrentLatency.Text = "● Latency: No DNS set";
                return;
            }

            _lblCurrentLatency.Text = "● Latency: Pinging...";
            _btnTestCurrentLatency.Enabled = false;

            string target = _selectedAdapter.DnsServers[0];
            var result = await _benchmarkService.MeasureLatencyAsync(target);

            if (result.Success)
            {
                _lblCurrentLatency.Text = "● Latency: " + result.RoundtripTimeMs + " ms";
                _lblCurrentLatency.ForeColor = Theme.GetLatencyColor(result.RoundtripTimeMs);
                LogMessage("Current DNS (" + target + ") latency: " + result.RoundtripTimeMs + " ms [" + result.Protocol + "]");
            }
            else
            {
                _lblCurrentLatency.Text = "● Latency: Timeout";
                _lblCurrentLatency.ForeColor = Theme.AccentDanger;
                LogMessage("Current DNS (" + target + ") ping failed.");
            }

            _btnTestCurrentLatency.Enabled = true;
        }

        private void ApplyPreset(DnsPreset preset)
        {
            if (preset == null) return;
            _lastAppliedPreset = preset;
            _txtPrimaryDns.Text = preset.PrimaryDns;
            _txtSecondaryDns.Text = preset.SecondaryDns ?? "";
            ApplyDnsFromInputs();
        }

        private void ApplyDnsFromInputs()
        {
            if (_selectedAdapter == null)
            {
                MessageBox.Show("Please select a valid network adapter first.", "EasyDNS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string primary = _txtPrimaryDns.Text.Trim();
            string secondary = _txtSecondaryDns.Text.Trim();

            if (!DnsManagerService.IsValidIpv4(primary))
            {
                MessageBox.Show("Please enter a valid Primary IPv4 DNS address (e.g. 1.1.1.1 or 8.8.8.8).", "Invalid IP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtPrimaryDns.InnerTextBox.Focus();
                return;
            }

            if (!string.IsNullOrEmpty(secondary) && !DnsManagerService.IsValidIpv4(secondary))
            {
                MessageBox.Show("Please enter a valid Secondary IPv4 DNS address or leave it blank.", "Invalid IP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtSecondaryDns.InnerTextBox.Focus();
                return;
            }

            LogMessage("Applying DNS (" + primary + (string.IsNullOrEmpty(secondary) ? "" : ", " + secondary) + ") to " + _selectedAdapter.Name + "...");

            string resultMsg;
            bool success = _dnsManager.SetDns(_selectedAdapter, primary, secondary, out resultMsg);

            if (success)
            {
                LogMessage("OK: " + resultMsg);

                // If on Windows 11 and a known preset with DoH was applied, configure native DoH encryption
                if (_lastAppliedPreset != null &&
                    string.Equals(_lastAppliedPreset.PrimaryDns, primary, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(_lastAppliedPreset.DohTemplate) &&
                    DnsManagerService.IsWindows11OrGreater())
                {
                    string dohMsg;
                    if (_dnsManager.ConfigureDoH(_selectedAdapter, primary, _lastAppliedPreset.DohTemplate, out dohMsg))
                    {
                        LogMessage("DoH: " + dohMsg);
                    }
                }

                LoadAdapters();
            }
            else
            {
                LogMessage("FAIL: " + resultMsg);
                MessageBox.Show(resultMsg, "EasyDNS Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetToDhcp()
        {
            if (_selectedAdapter == null)
            {
                MessageBox.Show("Please select a valid network adapter first.", "EasyDNS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LogMessage("Resetting DNS to default on " + _selectedAdapter.Name + "...");

            string resultMsg;
            bool success = _dnsManager.ResetToDhcp(_selectedAdapter, out resultMsg);

            if (success)
            {
                LogMessage("OK: " + resultMsg);
                _txtPrimaryDns.Text = "";
                _txtSecondaryDns.Text = "";
                LoadAdapters();
            }
            else
            {
                LogMessage("FAIL: " + resultMsg);
                MessageBox.Show(resultMsg, "EasyDNS Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FlushDnsCache()
        {
            LogMessage("Flushing Windows DNS Resolver Cache...");
            string msg;
            bool success = _dnsManager.FlushDnsCache(out msg);

            if (success)
            {
                LogMessage("OK: " + msg);
                MessageBox.Show("Windows DNS Resolver Cache successfully flushed.", "Flush DNS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                LogMessage("FAIL: " + msg);
            }
        }

        private void SaveCustomPreset()
        {
            string primary = _txtPrimaryDns.Text.Trim();
            string secondary = _txtSecondaryDns.Text.Trim();

            if (!DnsManagerService.IsValidIpv4(primary))
            {
                MessageBox.Show("Please enter a valid Primary IPv4 DNS to save preset.", "Invalid IP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string presetName = PromptDialog.Show(this, "Save Custom Preset", "Enter a name for this custom DNS preset:", "My Custom DNS");
            if (string.IsNullOrWhiteSpace(presetName)) return;

            _presetRepo.AddCustomPreset(presetName.Trim(), primary, secondary, "Custom DNS preset saved by user.");
            RefreshPresetsList();
            LogMessage("Saved custom preset: " + presetName);
            MessageBox.Show("Preset '" + presetName + "' saved successfully!", "EasyDNS", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LogMessage(string text)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(LogMessage), text);
                return;
            }

            if (_darkLogConsole != null)
            {
                _darkLogConsole.AppendLog(text);
            }
        }
    }
}
