using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EasyDNS.UI;

namespace EasyDNS.Forms
{
    public class PromptDialog : Form
    {
        private const int CS_DROPSHADOW = 0x00020000;
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        private readonly ModernInputField _txtInput;
        private readonly ModernButton _btnOk;
        private readonly ModernButton _btnCancel;

        public string InputText
        {
            get { return _txtInput.Text; }
        }

        public PromptDialog(string title, string promptText, string defaultValue = "")
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.CardBackground;
            ForeColor = Theme.TextPrimary;
            Font = Theme.BodyFont;
            Size = new Size(450, 215);
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);

            // 1. Custom Title Bar
            var titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = Theme.TitleBarBackground,
                Padding = new Padding(14, 0, 0, 0)
            };
            titleBar.MouseDown += DragWindow;
            titleBar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(Theme.BorderColor, 1))
                {
                    e.Graphics.DrawLine(pen, 0, titleBar.Height - 1, titleBar.Width, titleBar.Height - 1);
                }
            };

            var lblTitle = new Label
            {
                Text = string.IsNullOrEmpty(title) ? "EasyDNS" : title,
                Font = Theme.TitleBarFont,
                ForeColor = Theme.TextPrimary,
                AutoSize = true,
                Location = new Point(14, 10),
                BackColor = Theme.TitleBarBackground
            };
            lblTitle.MouseDown += DragWindow;

            var btnClose = new WindowControlButton(WindowControlButton.ButtonType.Close)
            {
                Dock = DockStyle.Right,
                Width = 42
            };
            btnClose.Click += delegate 
            { 
                DialogResult = DialogResult.Cancel;
                Close(); 
            };

            titleBar.Controls.Add(lblTitle);
            titleBar.Controls.Add(btnClose);
            Controls.Add(titleBar);

            // 2. Icon Badge Control (Edit / Tag Icon)
            var iconBadge = new PromptBadgeControl
            {
                Location = new Point(22, 54),
                Size = new Size(42, 42)
            };
            Controls.Add(iconBadge);

            // 3. Prompt Label
            var lblPrompt = new Label
            {
                Text = promptText,
                Font = Theme.BodyBoldFont,
                ForeColor = Theme.TextPrimary,
                Location = new Point(78, 54),
                Size = new Size(350, 20),
                BackColor = Theme.CardBackground
            };
            Controls.Add(lblPrompt);

            // 4. Input Field
            _txtInput = new ModernInputField
            {
                Text = defaultValue,
                Location = new Point(78, 80),
                Size = new Size(346, 32)
            };
            Controls.Add(_txtInput);

            // 5. Buttons
            int btnY = Height - 48;
            int btnW = 84;
            int btnH = 32;

            _btnOk = new ModernButton
            {
                Text = "Save",
                Font = Theme.BodyBoldFont,
                NormalColor = Theme.AccentPrimary,
                HoverColor = Theme.AccentPrimaryHover,
                BorderRadius = 6,
                Size = new Size(btnW, btnH),
                Location = new Point(Width - (btnW * 2) - 26, btnY),
                DialogResult = DialogResult.OK
            };

            _btnCancel = new ModernButton
            {
                Text = "Cancel",
                Font = Theme.BodyFont,
                NormalColor = Theme.InputBackground,
                HoverColor = Theme.CardHover,
                CustomBorderColor = Theme.BorderColor,
                BorderThickness = 1,
                BorderRadius = 6,
                Size = new Size(btnW, btnH),
                Location = new Point(Width - btnW - 18, btnY),
                DialogResult = DialogResult.Cancel
            };

            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            Controls.Add(_btnOk);
            Controls.Add(_btnCancel);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _txtInput.Focus();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyModernCorners();
        }

        private void ApplyModernCorners()
        {
            try
            {
                int preference = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
            }
            catch { }

            try
            {
                IntPtr hRgn = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 10, 10);
                Region = Region.FromHrgn(hRgn);
                DeleteObject(hRgn);
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var path = Theme.CreateRoundedRectangle(new Rectangle(0, 0, Width - 1, Height - 1), 6))
            {
                using (var pen = new Pen(Theme.BorderHighlight, 1))
                {
                    e.Graphics.DrawPath(pen, path);
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

        public static string Show(IWin32Window owner, string title, string prompt, string defaultValue = "")
        {
            using (var dlg = new PromptDialog(title, prompt, defaultValue))
            {
                DialogResult res = (owner != null) ? dlg.ShowDialog(owner) : dlg.ShowDialog();
                if (res == DialogResult.OK)
                {
                    return dlg.InputText;
                }
            }
            return null;
        }

        private class PromptBadgeControl : Control
        {
            public PromptBadgeControl()
            {
                Size = new Size(42, 42);
                SetStyle(ControlStyles.UserPaint |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.SupportsTransparentBackColor, true);
                BackColor = Theme.CardBackground;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var bgBrush = new SolidBrush(Theme.CardBackground))
                {
                    g.FillRectangle(bgBrush, ClientRectangle);
                }

                Color badgeColor = Theme.AccentPrimary;

                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                using (var path = Theme.CreateRoundedRectangle(rect, 8))
                {
                    using (var fillBrush = new SolidBrush(Color.FromArgb(28, badgeColor)))
                    {
                        g.FillPath(fillBrush, path);
                    }
                    using (var borderPen = new Pen(Color.FromArgb(90, badgeColor), 1))
                    {
                        borderPen.Alignment = PenAlignment.Inset;
                        g.DrawPath(borderPen, path);
                    }
                }

                // Edit Pencil Glyph
                using (var pen = new Pen(badgeColor, 2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, 13, 29, 27, 15);
                    g.DrawLine(pen, 27, 15, 30, 18);
                    g.DrawLine(pen, 30, 18, 16, 32);
                    g.DrawLine(pen, 16, 32, 11, 32);
                    g.DrawLine(pen, 11, 32, 13, 27);
                }
            }
        }
    }
}
