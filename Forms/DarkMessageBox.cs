using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EasyDNS.UI;

namespace EasyDNS.Forms
{
    public class DarkMessageBox : Form
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

        private readonly MessageBoxIcon _iconType;

        private DarkMessageBox(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            _iconType = icon;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            BackColor = Theme.CardBackground;
            ForeColor = Theme.TextPrimary;
            Font = Theme.BodyFont;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);

            // 1. Calculate required text dimensions
            int textMaxWidth = 360;
            Size textSize = TextRenderer.MeasureText(
                message, 
                Theme.BodyFont, 
                new Size(textMaxWidth, int.MaxValue), 
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

            int contentWidth = Math.Max(420, textSize.Width + 100);
            int contentHeight = Math.Max(50, textSize.Height + 10);
            int formWidth = contentWidth + 40;
            int formHeight = 40 + 20 + contentHeight + 20 + 34 + 18; // title + pad + text + pad + btn + pad

            Size = new Size(formWidth, formHeight);

            // 2. Custom Title Bar
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
                DialogResult = (buttons == MessageBoxButtons.YesNo) ? DialogResult.No : DialogResult.Cancel;
                Close(); 
            };

            titleBar.Controls.Add(lblTitle);
            titleBar.Controls.Add(btnClose);
            Controls.Add(titleBar);

            // 3. Icon Badge Control
            var picIcon = new IconBadgeControl(icon)
            {
                Location = new Point(22, 54),
                Size = new Size(42, 42)
            };
            Controls.Add(picIcon);

            // 4. Message Text Label
            var lblMessage = new Label
            {
                Text = message,
                Font = Theme.BodyFont,
                ForeColor = Theme.TextPrimary,
                BackColor = Theme.CardBackground,
                Location = new Point(78, 54),
                Size = new Size(formWidth - 100, contentHeight),
                AutoEllipsis = false
            };
            Controls.Add(lblMessage);

            // 5. Buttons Row
            int btnY = formHeight - 48;
            int btnH = 32;
            int btnW = 84;

            bool isDestructive = title.IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 message.IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0;

            if (buttons == MessageBoxButtons.YesNo)
            {
                var btnYes = new ModernButton
                {
                    Text = "Yes",
                    Font = Theme.BodyBoldFont,
                    NormalColor = isDestructive ? Theme.AccentDanger : Theme.AccentPrimary,
                    HoverColor = isDestructive ? Theme.AccentDangerHover : Theme.AccentPrimaryHover,
                    BorderRadius = 6,
                    Size = new Size(btnW, btnH),
                    Location = new Point(formWidth - (btnW * 2) - 26, btnY),
                    DialogResult = DialogResult.Yes
                };

                var btnNo = new ModernButton
                {
                    Text = "No",
                    Font = Theme.BodyFont,
                    NormalColor = Theme.InputBackground,
                    HoverColor = Theme.CardHover,
                    CustomBorderColor = Theme.BorderColor,
                    BorderThickness = 1,
                    BorderRadius = 6,
                    Size = new Size(btnW, btnH),
                    Location = new Point(formWidth - btnW - 18, btnY),
                    DialogResult = DialogResult.No
                };

                AcceptButton = btnYes;
                CancelButton = btnNo;

                Controls.Add(btnYes);
                Controls.Add(btnNo);
            }
            else if (buttons == MessageBoxButtons.OKCancel)
            {
                var btnOk = new ModernButton
                {
                    Text = "OK",
                    Font = Theme.BodyBoldFont,
                    NormalColor = Theme.AccentPrimary,
                    HoverColor = Theme.AccentPrimaryHover,
                    BorderRadius = 6,
                    Size = new Size(btnW, btnH),
                    Location = new Point(formWidth - (btnW * 2) - 26, btnY),
                    DialogResult = DialogResult.OK
                };

                var btnCancel = new ModernButton
                {
                    Text = "Cancel",
                    Font = Theme.BodyFont,
                    NormalColor = Theme.InputBackground,
                    HoverColor = Theme.CardHover,
                    CustomBorderColor = Theme.BorderColor,
                    BorderThickness = 1,
                    BorderRadius = 6,
                    Size = new Size(btnW, btnH),
                    Location = new Point(formWidth - btnW - 18, btnY),
                    DialogResult = DialogResult.Cancel
                };

                AcceptButton = btnOk;
                CancelButton = btnCancel;

                Controls.Add(btnOk);
                Controls.Add(btnCancel);
            }
            else
            {
                // Default: OK Button
                Color okColor = (icon == MessageBoxIcon.Information) ? Theme.AccentSuccess : Theme.AccentPrimary;
                Color okHover = (icon == MessageBoxIcon.Information) ? Theme.AccentSuccessHover : Theme.AccentPrimaryHover;

                var btnOk = new ModernButton
                {
                    Text = "OK",
                    Font = Theme.BodyBoldFont,
                    NormalColor = okColor,
                    HoverColor = okHover,
                    BorderRadius = 6,
                    Size = new Size(btnW, btnH),
                    Location = new Point(formWidth - btnW - 18, btnY),
                    DialogResult = DialogResult.OK
                };

                AcceptButton = btnOk;
                CancelButton = btnOk;

                Controls.Add(btnOk);
            }
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

        #region Static Show Methods
        public static DialogResult Show(string message)
        {
            return Show(null, message, "EasyDNS", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static DialogResult Show(string message, string title)
        {
            return Show(null, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static DialogResult Show(string message, string title, MessageBoxButtons buttons)
        {
            return Show(null, message, title, buttons, MessageBoxIcon.Information);
        }

        public static DialogResult Show(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return Show(null, message, title, buttons, icon);
        }

        public static DialogResult Show(IWin32Window owner, string message)
        {
            return Show(owner, message, "EasyDNS", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static DialogResult Show(IWin32Window owner, string message, string title)
        {
            return Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static DialogResult Show(IWin32Window owner, string message, string title, MessageBoxButtons buttons)
        {
            return Show(owner, message, title, buttons, MessageBoxIcon.Information);
        }

        public static DialogResult Show(IWin32Window owner, string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            using (var dlg = new DarkMessageBox(message, title, buttons, icon))
            {
                if (owner != null)
                {
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    return dlg.ShowDialog(owner);
                }
                else
                {
                    dlg.StartPosition = FormStartPosition.CenterScreen;
                    return dlg.ShowDialog();
                }
            }
        }
        #endregion

        #region Vector Icon Badge Component
        private class IconBadgeControl : Control
        {
            private readonly MessageBoxIcon _icon;

            public IconBadgeControl(MessageBoxIcon icon)
            {
                _icon = icon;
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

                Color badgeColor;
                if (_icon == MessageBoxIcon.Error || _icon == MessageBoxIcon.Hand || _icon == MessageBoxIcon.Stop)
                {
                    badgeColor = Theme.AccentDanger;
                }
                else if (_icon == MessageBoxIcon.Warning || _icon == MessageBoxIcon.Exclamation)
                {
                    badgeColor = Theme.AccentWarning;
                }
                else if (_icon == MessageBoxIcon.Question)
                {
                    badgeColor = Theme.AccentPrimary;
                }
                else
                {
                    badgeColor = Theme.AccentSuccess;
                }

                // Background box
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

                // Vector Icon Glyph
                if (_icon == MessageBoxIcon.Error || _icon == MessageBoxIcon.Hand || _icon == MessageBoxIcon.Stop)
                {
                    using (var pen = new Pen(badgeColor, 2.4f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, 14, 14, 28, 28);
                        g.DrawLine(pen, 28, 14, 14, 28);
                    }
                }
                else if (_icon == MessageBoxIcon.Warning || _icon == MessageBoxIcon.Exclamation)
                {
                    using (var pen = new Pen(badgeColor, 2.2f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        pen.LineJoin = LineJoin.Round;
                        PointF[] triPts = { new PointF(21, 9), new PointF(33, 30), new PointF(9, 30), new PointF(21, 9) };
                        g.DrawLines(pen, triPts);
                        g.DrawLine(pen, 21, 15, 21, 22);
                    }
                    using (var b = new SolidBrush(badgeColor))
                    {
                        g.FillEllipse(b, 19.5f, 25.5f, 3f, 3f);
                    }
                }
                else if (_icon == MessageBoxIcon.Question)
                {
                    using (var font = new Font("Segoe UI", 16f, FontStyle.Bold))
                    {
                        TextRenderer.DrawText(g, "?", font, rect, badgeColor,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                }
                else
                {
                    // Information / Success Checkmark
                    using (var pen = new Pen(badgeColor, 2.4f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        pen.LineJoin = LineJoin.Round;
                        PointF[] checkPts = { new PointF(13, 21), new PointF(18, 26), new PointF(29, 14) };
                        g.DrawLines(pen, checkPts);
                    }
                }
            }
        }
        #endregion
    }
}
