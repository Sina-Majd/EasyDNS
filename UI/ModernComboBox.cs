using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using EasyDNS.Models;

namespace EasyDNS.UI
{
    public class ModernComboBox : Control
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUND = 2;

        private readonly List<object> _items = new List<object>();
        private int _selectedIndex = -1;
        private bool _isHovered;
        private bool _isDroppedDown;
        private ContextMenuStrip _dropdownMenu;

        public event EventHandler SelectedIndexChanged;

        public IList<object> Items
        {
            get { return _items; }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                int clamped = value;
                if (clamped < -1 || clamped >= _items.Count) clamped = -1;
                if (_selectedIndex != clamped)
                {
                    _selectedIndex = clamped;
                    Invalidate();
                    OnSelectedIndexChanged(EventArgs.Empty);
                }
            }
        }

        public object SelectedItem
        {
            get
            {
                if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
                    return _items[_selectedIndex];
                return null;
            }
            set
            {
                int idx = _items.IndexOf(value);
                SelectedIndex = idx;
            }
        }

        public ModernComboBox()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(320, 28);
            Cursor = Cursors.Hand;
            Font = Theme.BodyFont;
            BackColor = Theme.TitleBarBackground;
        }

        protected virtual void OnSelectedIndexChanged(EventArgs e)
        {
            var handler = SelectedIndexChanged;
            if (handler != null) handler(this, e);
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
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            ShowDropdownMenu();
        }

        private void ShowDropdownMenu()
        {
            if (_items.Count == 0) return;

            if (_dropdownMenu != null)
            {
                _dropdownMenu.Dispose();
            }

            _dropdownMenu = new ContextMenuStrip();
            _dropdownMenu.BackColor = Theme.CardBackground;
            _dropdownMenu.ForeColor = Theme.TextPrimary;
            _dropdownMenu.Renderer = new AdapterMenuRenderer();
            _dropdownMenu.ShowImageMargin = false;
            _dropdownMenu.ShowCheckMargin = false;
            _dropdownMenu.AutoSize = false;
            _dropdownMenu.Width = Width;
            _dropdownMenu.Padding = new Padding(2, 4, 2, 4);

            int itemHeight = 30;
            int totalHeight = (_items.Count * itemHeight) + 8;
            _dropdownMenu.Height = totalHeight;

            for (int i = 0; i < _items.Count; i++)
            {
                int itemIndex = i;
                object item = _items[i];
                string text = item != null ? item.ToString() : "";
                var adapter = item as NetworkAdapterInfo;
                bool isOp = adapter != null && adapter.IsOperational;
                bool isSel = (itemIndex == _selectedIndex);

                var menuItem = new AdapterMenuItem(text, isOp, isSel, delegate
                {
                    SelectedIndex = itemIndex;
                })
                {
                    Width = Width - 4
                };

                _dropdownMenu.Items.Add(menuItem);
            }

            _isDroppedDown = true;
            Invalidate();

            Action applyCorners = delegate
            {
                try
                {
                    int preference = DWMWCP_ROUND;
                    DwmSetWindowAttribute(_dropdownMenu.Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
                }
                catch { }

                try
                {
                    using (var path = Theme.CreateRoundedRectangle(new Rectangle(0, 0, _dropdownMenu.Width, _dropdownMenu.Height), 8))
                    {
                        _dropdownMenu.Region = new Region(path);
                    }
                }
                catch { }
            };

            _dropdownMenu.HandleCreated += delegate { applyCorners(); };
            _dropdownMenu.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var path = Theme.CreateRoundedRectangle(new Rectangle(0, 0, _dropdownMenu.Width - 1, _dropdownMenu.Height - 1), 8))
                using (var pen = new Pen(Theme.BorderHighlight, 1.5f))
                {
                    pen.Alignment = PenAlignment.Inset;
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawPath(pen, path);
                }
            };

            _dropdownMenu.Closed += delegate
            {
                _isDroppedDown = false;
                Invalidate();
            };

            _dropdownMenu.Show(this, new Point(0, Height + 2));
            applyCorners();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.Default;

            Color parentBg = Theme.TitleBarBackground;
            if (Parent != null && Parent.BackColor != Color.Transparent)
                parentBg = Parent.BackColor;

            using (var clearBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBrush, ClientRectangle);
            }

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            Color border = (_isDroppedDown || _isHovered) ? Theme.BorderHighlight : Theme.BorderColor;
            Color fill = _isHovered ? Theme.CardHover : Theme.InputBackground;

            int radius = 8;
            using (var path = Theme.CreateRoundedRectangle(bounds, radius))
            {
                using (var bgBrush = new SolidBrush(fill))
                {
                    g.FillPath(bgBrush, path);
                }

                using (var pen = new Pen(border, (_isDroppedDown || _isHovered) ? 1.5f : 1f))
                {
                    pen.Alignment = PenAlignment.Inset;
                    g.DrawPath(pen, path);
                }
            }

            // Draw Selected Item Content
            int textX = 12;
            var currentAdapter = SelectedItem as NetworkAdapterInfo;
            if (currentAdapter != null)
            {
                Color dotColor = currentAdapter.IsOperational ? Theme.AccentSuccess : Theme.TextMuted;
                using (var dotBrush = new SolidBrush(dotColor))
                {
                    g.FillEllipse(dotBrush, 12, (Height - 8) / 2, 8, 8);
                }
                textX = 26;
            }

            string displayText = SelectedItem != null ? SelectedItem.ToString() : "Select network adapter...";
            var textRect = new Rectangle(textX, 0, Width - textX - 26, Height);
            TextRenderer.DrawText(g, displayText, Font, textRect, Theme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordEllipsis);

            // Draw Chevron Arrow (v / ^)
            int arrowX = Width - 14;
            int arrowY = Height / 2 - 1;
            Color chevronColor = (_isDroppedDown || _isHovered) ? Theme.TextPrimary : Theme.TextSecondary;
            using (var arrowPen = new Pen(chevronColor, 1.5f))
            {
                arrowPen.StartCap = LineCap.Round;
                arrowPen.EndCap = LineCap.Round;
                if (_isDroppedDown)
                {
                    g.DrawLine(arrowPen, arrowX - 4, arrowY + 2, arrowX, arrowY - 2);
                    g.DrawLine(arrowPen, arrowX, arrowY - 2, arrowX + 4, arrowY + 2);
                }
                else
                {
                    g.DrawLine(arrowPen, arrowX - 4, arrowY - 2, arrowX, arrowY + 2);
                    g.DrawLine(arrowPen, arrowX, arrowY + 2, arrowX + 4, arrowY - 2);
                }
            }
        }

        private class AdapterMenuItem : ToolStripMenuItem
        {
            private readonly bool _isOperational;
            private readonly bool _isSelected;

            public AdapterMenuItem(string text, bool isOperational, bool isSelected, EventHandler onClick)
                : base(text, null, onClick)
            {
                _isOperational = isOperational;
                _isSelected = isSelected;
                AutoSize = false;
                Height = 30;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.Default;

                // 1. Background
                Color bg = Selected ? Theme.CardHover : Theme.CardBackground;
                using (var brush = new SolidBrush(bg))
                {
                    g.FillRectangle(brush, 0, 0, Width, Height);
                }

                // 2. Status Dot (Anti-aliased circle with NO box)
                Color dotColor = _isOperational ? Theme.AccentSuccess : Theme.TextMuted;
                using (var dotBrush = new SolidBrush(dotColor))
                {
                    g.FillEllipse(dotBrush, 12, (Height - 8) / 2, 8, 8);
                }

                // 3. Adapter Text
                Color textColor = _isSelected ? Theme.TextAccent : (Selected ? Color.White : Theme.TextPrimary);
                Font font = _isSelected ? Theme.BodyBoldFont : Theme.BodyFont;
                int textWidth = Width - 36;
                var textRect = new Rectangle(28, 0, Math.Max(10, textWidth), Height);
                TextRenderer.DrawText(g, Text, font, textRect, textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordEllipsis);
            }
        }

        private class AdapterMenuRenderer : ToolStripProfessionalRenderer
        {
            public AdapterMenuRenderer() : base(new DarkColorTable()) { }

            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (var path = Theme.CreateRoundedRectangle(new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 8))
                using (var pen = new Pen(Theme.BorderHighlight, 1.5f))
                {
                    pen.Alignment = PenAlignment.Inset;
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }
    }
}
