using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;
using EasyDNS.Models;

namespace EasyDNS.UI
{
    public class ModernButton : Control, IButtonControl
    {
        public Color NormalColor { get; set; }
        public Color HoverColor { get; set; }
        public Color PressedColor { get; set; }
        public Color CustomBorderColor { get; set; }
        public int BorderRadius { get; set; }
        public int BorderThickness { get; set; }
        public DialogResult DialogResult { get; set; }

        private bool _isHovered;
        private bool _isPressed;
        private bool _isDefault;

        public void NotifyDefault(bool value)
        {
            _isDefault = value;
            Invalidate();
        }

        public void PerformClick()
        {
            if (CanSelect)
            {
                OnClick(EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (DialogResult != DialogResult.None)
            {
                var parentForm = FindForm();
                if (parentForm != null)
                {
                    parentForm.DialogResult = DialogResult;
                }
            }
        }

        public ModernButton()
        {
            NormalColor = Theme.AccentPrimary;
            HoverColor = Theme.AccentPrimaryHover;
            PressedColor = Color.FromArgb(55, 48, 163);
            CustomBorderColor = Color.Transparent;
            BorderRadius = 5;
            BorderThickness = 0;
            DialogResult = DialogResult.None;

            ForeColor = Theme.TextPrimary;
            Font = Theme.BodyBoldFont;
            Cursor = Cursors.Hand;
            Size = new Size(130, 36);

            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBackground;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = false;
                Invalidate();
            }
        }

        private Color GetEffectiveParentBackground()
        {
            Control p = Parent;
            while (p != null)
            {
                var card = p as ModernCard;
                if (card != null) return card.FillColor;

                if (p.BackColor != Color.Transparent && p.BackColor.A == 255)
                    return p.BackColor;

                p = p.Parent;
            }
            return Theme.CardBackground;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // 1. Fill exact parent background color
            Color parentBg = GetEffectiveParentBackground();
            using (var bgBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            Color fillColor = !Enabled ? Theme.BorderColor : (_isPressed ? PressedColor : (_isHovered ? HoverColor : NormalColor));

            using (var path = Theme.CreateRoundedRectangle(bounds, BorderRadius))
            {
                using (var brush = new SolidBrush(fillColor))
                {
                    g.FillPath(brush, path);
                }

                if (BorderThickness > 0 && CustomBorderColor != Color.Transparent)
                {
                    using (var pen = new Pen(CustomBorderColor, BorderThickness))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            Color textColor = Enabled ? ForeColor : Theme.TextMuted;
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordEllipsis);
        }
    }

    public class ModernRefreshButton : Control
    {
        private bool _isHovered;
        private bool _isPressed;
        private float _rotationAngle = 0f;
        private Timer _spinTimer;

        public ModernRefreshButton()
        {
            Size = new Size(28, 28);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.TitleBarBackground;

            _spinTimer = new Timer();
            _spinTimer.Interval = 20;
            _spinTimer.Tick += delegate
            {
                _rotationAngle += 24f;
                if (_rotationAngle >= 360f)
                {
                    _rotationAngle = 0f;
                    _spinTimer.Stop();
                }
                Invalidate();
            };
        }

        public void Spin()
        {
            _rotationAngle = 0f;
            _spinTimer.Start();
        }

        protected override void OnClick(EventArgs e)
        {
            Spin();
            base.OnClick(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _spinTimer != null)
            {
                _spinTimer.Dispose();
                _spinTimer = null;
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = false;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color parentBg = Theme.TitleBarBackground;
            if (Parent != null && Parent.BackColor != Color.Transparent) parentBg = Parent.BackColor;

            using (var clearBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBrush, ClientRectangle);
            }

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = _isPressed ? Color.FromArgb(40, 45, 65) : (_isHovered ? Theme.CardHover : Theme.InputBackground);
            Color border = _isHovered ? Theme.BorderHighlight : Theme.BorderColor;

            using (var path = Theme.CreateRoundedRectangle(bounds, 5))
            {
                using (var brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }
                using (var pen = new Pen(border, 1))
                {
                    g.DrawPath(pen, path);
                }
            }

            float cx = Width / 2f;
            float cy = Height / 2f;
            float r = 5.2f;
            Color iconColor = _isHovered ? Theme.TextPrimary : Theme.TextSecondary;

            var state = g.Save();
            g.TranslateTransform(cx, cy);
            if (_rotationAngle != 0f)
            {
                g.RotateTransform(_rotationAngle);
            }

            using (var pen = new Pen(iconColor, 1.45f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                // Dual arc circular sync icon
                g.DrawArc(pen, -r, -r, r * 2, r * 2, 210, 125);
                g.DrawArc(pen, -r, -r, r * 2, r * 2, 30, 125);

                // Arrow 1 at 335 deg (top right)
                float ax1 = (float)(r * Math.Cos(335 * Math.PI / 180));
                float ay1 = (float)(r * Math.Sin(335 * Math.PI / 180));
                g.DrawLine(pen, ax1, ay1, ax1 + 2.8f, ay1 - 1.2f);
                g.DrawLine(pen, ax1, ay1, ax1 - 1.2f, ay1 - 2.8f);

                // Arrow 2 at 155 deg (bottom left)
                float ax2 = (float)(r * Math.Cos(155 * Math.PI / 180));
                float ay2 = (float)(r * Math.Sin(155 * Math.PI / 180));
                g.DrawLine(pen, ax2, ay2, ax2 - 2.8f, ay2 + 1.2f);
                g.DrawLine(pen, ax2, ay2, ax2 + 1.2f, ay2 + 2.8f);
            }

            g.Restore(state);
        }
    }

    public class WindowControlButton : Control
    {
        public enum ButtonType
        {
            Minimize,
            Maximize,
            Close
        }

        public ButtonType Type { get; set; }
        public bool IsMaximized { get; set; }

        private bool _isHovered;
        private bool _isPressed;

        public WindowControlButton(ButtonType type)
        {
            Type = type;
            ForeColor = Color.FromArgb(200, 205, 220);
            Size = new Size(46, 38);
            Cursor = Cursors.Default;

            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.TitleBarBackground;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                _isPressed = false;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            var bounds = ClientRectangle;

            Color bg = Theme.TitleBarBackground;
            if (_isPressed)
            {
                bg = (Type == ButtonType.Close) ? Color.FromArgb(190, 15, 30) : Color.FromArgb(48, 54, 76);
            }
            else if (_isHovered)
            {
                bg = (Type == ButtonType.Close) ? Theme.CloseBtnHover : Theme.WindowBtnHover;
            }

            using (var brush = new SolidBrush(bg))
            {
                g.FillRectangle(brush, bounds);
            }

            Color iconColor = (_isHovered && Type == ButtonType.Close) ? Color.White : Color.FromArgb(215, 220, 235);
            int cx = Width / 2;
            int cy = Height / 2;

            if (Type == ButtonType.Minimize)
            {
                using (var pen = new Pen(iconColor, 1.5f))
                {
                    g.DrawLine(pen, cx - 5, cy, cx + 5, cy);
                }
            }
            else if (Type == ButtonType.Maximize)
            {
                using (var pen = new Pen(iconColor, 1.3f))
                {
                    if (IsMaximized)
                    {
                        g.DrawRectangle(pen, cx - 4, cy - 2, 7, 7);
                        g.DrawLine(pen, cx - 2, cy - 2, cx - 2, cy - 5);
                        g.DrawLine(pen, cx - 2, cy - 5, cx + 5, cy - 5);
                        g.DrawLine(pen, cx + 5, cy - 5, cx + 5, cy + 2);
                        g.DrawLine(pen, cx + 5, cy + 2, cx + 3, cy + 2);
                    }
                    else
                    {
                        g.DrawRectangle(pen, cx - 5, cy - 5, 10, 10);
                    }
                }
            }
            else if (Type == ButtonType.Close)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(iconColor, 1.4f))
                {
                    g.DrawLine(pen, cx - 4, cy - 4, cx + 4, cy + 4);
                    g.DrawLine(pen, cx + 4, cy - 4, cx - 4, cy + 4);
                }
            }
        }
    }

    public class ModernInputField : UserControl
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, string lp);
        private const int EM_SETCUEBANNER = 0x1501;

        private readonly TextBox _innerBox;
        private bool _isFocused;
        private string _placeholderText = "";

        public TextBox InnerTextBox
        {
            get { return _innerBox; }
        }

        public string PlaceholderText
        {
            get { return _placeholderText; }
            set
            {
                _placeholderText = value ?? "";
                UpdateCueBanner();
            }
        }

        public override string Text
        {
            get { return _innerBox.Text; }
            set { _innerBox.Text = value ?? ""; }
        }

        public new event EventHandler TextChanged
        {
            add { _innerBox.TextChanged += value; }
            remove { _innerBox.TextChanged -= value; }
        }

        public ModernInputField()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBackground;
            Size = new Size(180, 28);
            Padding = new Padding(8, 4, 8, 4);

            _innerBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.InputBackground,
                ForeColor = Theme.TextPrimary,
                Font = Theme.BodyFont,
                Location = new Point(8, 5),
                Width = Width - 16,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            _innerBox.HandleCreated += delegate(object s, EventArgs e)
            {
                UpdateCueBanner();
            };

            _innerBox.GotFocus += delegate(object s, EventArgs e)
            {
                _isFocused = true;
                Invalidate();
            };

            _innerBox.LostFocus += delegate(object s, EventArgs e)
            {
                _isFocused = false;
                Invalidate();
            };

            Controls.Add(_innerBox);
        }

        private void UpdateCueBanner()
        {
            if (_innerBox.IsHandleCreated && !string.IsNullOrEmpty(_placeholderText))
            {
                SendMessage(_innerBox.Handle, EM_SETCUEBANNER, (IntPtr)0, _placeholderText);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_innerBox != null)
            {
                _innerBox.Location = new Point(8, (Height - _innerBox.Height) / 2);
                _innerBox.Width = Width - 16;
            }
        }

        private Color GetEffectiveParentBackground()
        {
            Control p = Parent;
            while (p != null)
            {
                var card = p as ModernCard;
                if (card != null) return card.FillColor;

                if (p.BackColor != Color.Transparent && p.BackColor.A == 255)
                    return p.BackColor;

                p = p.Parent;
            }
            return Theme.CardBackground;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color parentBg = GetEffectiveParentBackground();
            using (var clearBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBrush, ClientRectangle);
            }

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            Color borderColor = _isFocused ? Theme.BorderHighlight : Theme.BorderColor;

            using (var path = Theme.CreateRoundedRectangle(bounds, 5))
            {
                using (var brush = new SolidBrush(Theme.InputBackground))
                {
                    g.FillPath(brush, path);
                }

                using (var pen = new Pen(borderColor, _isFocused ? 1.5f : 1f))
                {
                    g.DrawPath(pen, path);
                }
            }
        }
    }

    public class ModernCard : Panel
    {
        public int BorderRadius { get; set; }
        public Color CustomBorderColor { get; set; }
        public int CustomBorderWidth { get; set; }
        public Color FillColor { get; set; }

        public ModernCard()
        {
            BorderRadius = 10;
            CustomBorderColor = Theme.BorderColor;
            CustomBorderWidth = 1;
            FillColor = Theme.CardBackground;

            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBackground;
            Padding = new Padding(12);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Color parentBg = (Parent != null && Parent.BackColor != Color.Transparent) ? Parent.BackColor : Theme.BackgroundDark;
            using (var clearBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBrush, ClientRectangle);
            }

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            using (var path = Theme.CreateRoundedRectangle(bounds, BorderRadius))
            {
                using (var brush = new SolidBrush(FillColor))
                {
                    g.FillPath(brush, path);
                }

                if (CustomBorderWidth > 0)
                {
                    using (var pen = new Pen(CustomBorderColor, CustomBorderWidth))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            base.OnPaint(e);
        }
    }

    public class DnsPresetCardControl : Control
    {
        private readonly DnsPreset _preset;
        private bool _isCardHovered;
        private bool _isSelected;
        private bool _isApplyHovered;
        private bool _isApplyPressed;
        private bool _isTestHovered;
        private bool _isTestPressed;
        private bool _isDeleteHovered;

        public event Action<DnsPreset> OnApplyClicked;
        public event Action<DnsPreset> OnCardSelected;
        public event Action<DnsPreset> OnTestClicked;
        public event Action<DnsPreset> OnDeleteClicked;

        private Rectangle _applyBtnRect;
        private Rectangle _testBtnRect;
        private Rectangle _deleteBtnRect;

        public DnsPreset Preset
        {
            get { return _preset; }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                Invalidate();
            }
        }

        public DnsPresetCardControl(DnsPreset preset)
        {
            _preset = preset;
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.BackgroundDark;
            Height = 88;
            Cursor = Cursors.Hand;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _applyBtnRect = new Rectangle(Width - 68, Height - 32, 58, 25);
            _testBtnRect = new Rectangle(Width - 116, Height - 32, 44, 25);
            _deleteBtnRect = new Rectangle(Width - 24, 6, 18, 18);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool applyHover = _applyBtnRect.Contains(e.Location);
            bool testHover = _testBtnRect.Contains(e.Location);
            bool deleteHover = _preset.IsCustom && _deleteBtnRect.Contains(e.Location);

            if (applyHover != _isApplyHovered || testHover != _isTestHovered || deleteHover != _isDeleteHovered)
            {
                _isApplyHovered = applyHover;
                _isTestHovered = testHover;
                _isDeleteHovered = deleteHover;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isCardHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isCardHovered = false;
            _isApplyHovered = false;
            _isApplyPressed = false;
            _isTestHovered = false;
            _isTestPressed = false;
            _isDeleteHovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_applyBtnRect.Contains(e.Location))
                {
                    _isApplyPressed = true;
                    Invalidate();
                }
                else if (_testBtnRect.Contains(e.Location))
                {
                    _isTestPressed = true;
                    Invalidate();
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_isApplyPressed && _applyBtnRect.Contains(e.Location))
                {
                    _isApplyPressed = false;
                    Invalidate();
                    if (OnApplyClicked != null) OnApplyClicked(_preset);
                    return;
                }
                _isApplyPressed = false;

                if (_isTestPressed && _testBtnRect.Contains(e.Location))
                {
                    _isTestPressed = false;
                    Invalidate();
                    if (OnTestClicked != null) OnTestClicked(_preset);
                    return;
                }
                _isTestPressed = false;

                if (_preset.IsCustom && _deleteBtnRect.Contains(e.Location))
                {
                    if (OnDeleteClicked != null) OnDeleteClicked(_preset);
                    return;
                }

                if (OnCardSelected != null) OnCardSelected(_preset);
            }
        }

        public void UpdateLatencyView()
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(UpdateLatencyView));
                return;
            }
            Invalidate();
        }

        private static readonly Dictionary<string, Image> _logoCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _logoLock = new object();

        private static string GetProviderKey(DnsPreset preset)
        {
            if (preset == null) return "custom";
            if (preset.IsCustom) return "custom";
            if (string.IsNullOrEmpty(preset.Name)) return "custom";

            string name = preset.Name.ToUpperInvariant();
            if (name.StartsWith("CLOUDFLARE")) return "cloudflare";
            if (name.StartsWith("GOOGLE")) return "google";
            if (name.StartsWith("QUAD9")) return "quad9";
            if (name.StartsWith("ADGUARD")) return "adguard";
            if (name.StartsWith("OPENDNS")) return "opendns";
            if (name.StartsWith("CONTROL D")) return "controld";
            if (name.StartsWith("CLEANBROWSING")) return "cleanbrowsing";
            if (name.StartsWith("NEXTDNS")) return "nextdns";
            if (name.StartsWith("SHECAN")) return "shecan";
            if (name.StartsWith("ELECTRO")) return "electro";
            if (name.StartsWith("403")) return "custom";
            if (name.StartsWith("RADAR")) return "custom";
            if (name.StartsWith("LEVEL3")) return "level3";
            if (name.StartsWith("COMODO")) return "comodo";
            if (name.StartsWith("DNS.WATCH")) return "custom";
            return "custom";
        }

        private static Image GetProviderLogo(DnsPreset preset)
        {
            string key = GetProviderKey(preset);
            if (string.IsNullOrEmpty(key)) key = "custom";

            lock (_logoLock)
            {
                if (_logoCache.ContainsKey(key))
                {
                    return _logoCache[key];
                }

                Image img = null;
                try
                {
                    var asm = System.Reflection.Assembly.GetExecutingAssembly();
                    string resName = "EasyDNS.Resources.Logos." + key + ".png";
                    using (var stream = asm.GetManifestResourceStream(resName))
                    {
                        if (stream != null)
                        {
                            img = Image.FromStream(stream);
                        }
                    }
                }
                catch { }

                if (img == null)
                {
                    try
                    {
                        string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Logos", key + ".png");
                        if (File.Exists(filePath))
                        {
                            img = Image.FromFile(filePath);
                        }
                    }
                    catch { }
                }

                _logoCache[key] = img;
                return img;
            }
        }

        private string GetProviderInitials()
        {
            if (string.IsNullOrEmpty(_preset.Name)) return "DNS";
            string name = _preset.Name.ToUpperInvariant();
            if (name.StartsWith("CLOUDFLARE")) return "CF";
            if (name.StartsWith("GOOGLE")) return "GG";
            if (name.StartsWith("QUAD9")) return "Q9";
            if (name.StartsWith("ADGUARD")) return "AG";
            if (name.StartsWith("OPENDNS")) return "OD";
            if (name.StartsWith("CONTROL D")) return "CD";
            if (name.StartsWith("CLEANBROWSING")) return "CB";
            if (name.StartsWith("NEXTDNS")) return "NX";
            if (name.StartsWith("SHECAN")) return "SH";
            if (name.StartsWith("ELECTRO")) return "EL";
            if (name.StartsWith("403")) return "403";
            if (name.StartsWith("RADAR")) return "RG";
            if (name.StartsWith("LEVEL3")) return "L3";
            if (name.StartsWith("COMODO")) return "CM";
            if (_preset.IsCustom) return "★";

            string[] parts = _preset.Name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpperInvariant();
            return _preset.Name.Substring(0, Math.Min(2, _preset.Name.Length)).ToUpperInvariant();
        }

        private Color GetProviderColor()
        {
            string key = GetProviderKey(_preset);
            if (key == "cloudflare") return Color.FromArgb(246, 130, 31);
            if (key == "google") return Color.FromArgb(66, 133, 244);
            if (key == "quad9") return Color.FromArgb(139, 92, 246);
            if (key == "adguard") return Color.FromArgb(16, 185, 129);
            if (key == "opendns") return Color.FromArgb(6, 182, 212);
            if (key == "controld") return Color.FromArgb(236, 72, 153);
            if (key == "cleanbrowsing") return Color.FromArgb(59, 130, 246);
            if (key == "nextdns") return Color.FromArgb(99, 102, 241);
            if (key == "shecan") return Color.FromArgb(245, 158, 11);
            if (key == "electro") return Color.FromArgb(168, 85, 247);
            if (key == "level3") return Color.FromArgb(14, 165, 233);
            if (key == "comodo") return Color.FromArgb(239, 68, 68);
            if (key == "custom") return Color.FromArgb(168, 85, 247);
            return Color.FromArgb(99, 102, 241);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // 1. Clear parent background
            Color parentBg = (Parent != null && Parent.BackColor != Color.Transparent) ? Parent.BackColor : Theme.BackgroundDark;
            using (var clearBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBrush, ClientRectangle);
            }

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            Color cardBg = _isSelected ? Theme.CardSelected : (_isCardHovered ? Theme.CardHover : Theme.CardBackground);
            Color borderColor = _isSelected ? Theme.BorderHighlight : (_isCardHovered ? Color.FromArgb(79, 70, 229) : Theme.BorderColor);
            int borderWidth = _isSelected ? 2 : 1;

            using (var path = Theme.CreateRoundedRectangle(bounds, 8))
            {
                using (var brush = new SolidBrush(cardBg))
                {
                    g.FillPath(brush, path);
                }

                using (var pen = new Pen(borderColor, borderWidth))
                {
                    g.DrawPath(pen, path);
                }
            }

            // 2. Draw Provider Logo Avatar Box
            var avatarRect = new Rectangle(10, 10, 36, 36);
            Color brandColor = GetProviderColor();
            using (var avatarPath = Theme.CreateRoundedRectangle(avatarRect, 7))
            {
                using (var avBgBrush = new SolidBrush(Color.FromArgb(28, brandColor)))
                {
                    g.FillPath(avBgBrush, avatarPath);
                }
                using (var avPen = new Pen(Color.FromArgb(90, brandColor), 1))
                {
                    g.DrawPath(avPen, avatarPath);
                }
            }

            Image logo = GetProviderLogo(_preset);
            if (logo != null)
            {
                int logoSize = 24;
                int logoX = avatarRect.X + (avatarRect.Width - logoSize) / 2;
                int logoY = avatarRect.Y + (avatarRect.Height - logoSize) / 2;
                var oldMode = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(logo, new Rectangle(logoX, logoY, logoSize, logoSize));
                g.InterpolationMode = oldMode;
            }
            else
            {
                TextRenderer.DrawText(g, GetProviderInitials(), Theme.BodyBoldFont, avatarRect, brandColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // 3. Draw Preset Name (Tag badges removed)
            int textX = 54;
            using (var titleBrush = new SolidBrush(Theme.TextPrimary))
            {
                g.DrawString(_preset.Name, Theme.SubHeaderFont, titleBrush, new PointF(textX, 8));
            }

            // 4. Draw Description
            using (var descBrush = new SolidBrush(Theme.TextSecondary))
            {
                var descRect = new RectangleF(textX, 29, Width - textX - 120, 15);
                g.DrawString(_preset.Description, Theme.SmallFont, descBrush, descRect);
            }

            // 5. Draw IP Address Badge
            string ipText = _preset.PrimaryDns + (string.IsNullOrEmpty(_preset.SecondaryDns) ? "" : "  •  " + _preset.SecondaryDns);
            int maxIpW = Width - textX - 125;
            if (maxIpW > 50)
            {
                var ipRect = new Rectangle(textX, Height - 30, maxIpW, 20);
                using (var ipPath = Theme.CreateRoundedRectangle(ipRect, 4))
                {
                    using (var ipBgBrush = new SolidBrush(Theme.InputBackground))
                    {
                        g.FillPath(ipBgBrush, ipPath);
                    }
                    using (var ipPen = new Pen(Theme.BorderColor, 1))
                    {
                        g.DrawPath(ipPen, ipPath);
                    }
                }
                TextRenderer.DrawText(g, ipText, Theme.MonospaceFont, new Rectangle(textX + 4, Height - 29, maxIpW - 8, 18), Theme.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordEllipsis);
            }

            // 6. Draw Latency Badge
            DrawLatencyBadge(g);

            // 7. Directly Paint "Apply" Button (Zero Child Control Fringes!)
            Color applyFill = _isApplyPressed ? Color.FromArgb(55, 48, 163) : (_isApplyHovered ? Theme.AccentPrimaryHover : Theme.AccentPrimary);
            using (var applyPath = Theme.CreateRoundedRectangle(_applyBtnRect, 5))
            {
                using (var applyBrush = new SolidBrush(applyFill))
                {
                    g.FillPath(applyBrush, applyPath);
                }
            }
            TextRenderer.DrawText(g, "Apply", Theme.SmallFont, _applyBtnRect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 8. Directly Paint "Ping" Button
            Color testFill = _isTestPressed ? Color.FromArgb(30, 35, 50) : (_isTestHovered ? Theme.CardHover : Theme.InputBackground);
            using (var testPath = Theme.CreateRoundedRectangle(_testBtnRect, 5))
            {
                using (var testBrush = new SolidBrush(testFill))
                {
                    g.FillPath(testBrush, testPath);
                }
                using (var testPen = new Pen(Theme.BorderColor, 1))
                {
                    g.DrawPath(testPen, testPath);
                }
            }
            TextRenderer.DrawText(g, "Ping", Theme.SmallFont, _testBtnRect, Theme.TextPrimary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 9. Draw Custom Delete "X" if custom preset
            if (_preset.IsCustom)
            {
                Color delColor = _isDeleteHovered ? Theme.AccentDanger : Theme.TextMuted;
                using (var delPen = new Pen(delColor, 1.5f))
                {
                    g.DrawLine(delPen, _deleteBtnRect.X + 4, _deleteBtnRect.Y + 4, _deleteBtnRect.Right - 4, _deleteBtnRect.Bottom - 4);
                    g.DrawLine(delPen, _deleteBtnRect.Right - 4, _deleteBtnRect.Y + 4, _deleteBtnRect.X + 4, _deleteBtnRect.Bottom - 4);
                }
            }
        }

        private void DrawLatencyBadge(Graphics g)
        {
            string latencyText;
            Color badgeColor;

            if (_preset.IsCheckingLatency)
            {
                latencyText = "● Ping...";
                badgeColor = Theme.AccentWarning;
            }
            else if (_preset.LatencyMs.HasValue)
            {
                if (_preset.LatencyMs.Value < 0)
                {
                    latencyText = "● Timeout";
                    badgeColor = Theme.AccentDanger;
                }
                else
                {
                    latencyText = "● " + _preset.LatencyMs.Value + " ms";
                    badgeColor = Theme.GetLatencyColor(_preset.LatencyMs);
                }
            }
            else
            {
                latencyText = "● -- ms";
                badgeColor = Theme.TextMuted;
            }

            int badgeWidth = TextRenderer.MeasureText(latencyText, Theme.SmallFont).Width + 10;
            int badgeX = Width - badgeWidth - (_preset.IsCustom ? 26 : 10);
            var badgeRect = new Rectangle(badgeX, 9, badgeWidth, 18);

            using (var badgePath = Theme.CreateRoundedRectangle(badgeRect, 4))
            {
                using (var bgBrush = new SolidBrush(Color.FromArgb(24, badgeColor)))
                {
                    g.FillPath(bgBrush, badgePath);
                }
                using (var borderPen = new Pen(Color.FromArgb(80, badgeColor), 1))
                {
                    g.DrawPath(borderPen, badgePath);
                }
            }

            TextRenderer.DrawText(g, latencyText, Theme.SmallFont, badgeRect, badgeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    public class StatusBadgeControl : Control
    {
        private string _statusText = "DHCP (Auto)";
        private Color _statusColor = Theme.AccentSuccess;

        public StatusBadgeControl()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = Theme.SmallFont;
            Size = new Size(110, 28);
        }

        public void SetStatus(string text, Color color)
        {
            _statusText = text;
            _statusColor = color;
            using (var g = CreateGraphics())
            {
                int textW = TextRenderer.MeasureText(g, "● " + _statusText, Font).Width + 20;
                Width = Math.Max(100, textW);
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = Theme.CreateRoundedRectangle(rect, 6))
            {
                using (var bgBrush = new SolidBrush(Color.FromArgb(28, _statusColor)))
                {
                    g.FillPath(bgBrush, path);
                }
                using (var pen = new Pen(Color.FromArgb(80, _statusColor), 1))
                {
                    g.DrawPath(pen, path);
                }
            }

            TextRenderer.DrawText(g, "● " + _statusText, Font, rect, _statusColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
