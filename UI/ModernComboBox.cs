using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using EasyDNS.Models;

namespace EasyDNS.UI
{
    public class ModernComboBox : ComboBox
    {
        public ModernComboBox()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            BackColor = Theme.InputBackground;
            ForeColor = Theme.TextPrimary;
            Font = Theme.BodyFont;
            ItemHeight = 22;

            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color bg = isSelected ? Theme.CardHover : Theme.InputBackground;
            Color fg = isSelected ? Theme.TextAccent : Theme.TextPrimary;

            using (var brush = new SolidBrush(bg))
            {
                g.FillRectangle(brush, e.Bounds);
            }

            string text = Items[e.Index].ToString();
            var adapter = Items[e.Index] as NetworkAdapterInfo;

            int textX = e.Bounds.Left + 8;
            if (adapter != null)
            {
                // Draw small network icon / indicator dot
                Color dotColor = adapter.IsOperational ? Theme.AccentSuccess : Theme.TextMuted;
                var dotRect = new Rectangle(e.Bounds.Left + 6, e.Bounds.Top + (e.Bounds.Height - 6) / 2, 6, 6);
                using (var dotBrush = new SolidBrush(dotColor))
                {
                    g.FillEllipse(dotBrush, dotRect);
                }
                textX = e.Bounds.Left + 18;
            }

            var textRect = new Rectangle(textX, e.Bounds.Top, e.Bounds.Width - textX - 4, e.Bounds.Height);
            TextRenderer.DrawText(g, text, Font, textRect, fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordEllipsis);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);

            // Draw rounded background
            using (var path = Theme.CreateRoundedRectangle(bounds, 5))
            {
                using (var bgBrush = new SolidBrush(Theme.InputBackground))
                {
                    g.FillPath(bgBrush, path);
                }

                using (var pen = new Pen(Theme.BorderColor, 1))
                {
                    g.DrawPath(pen, path);
                }
            }

            // Draw Selected Text
            string selectedText = SelectedItem != null ? SelectedItem.ToString() : Text;
            if (!string.IsNullOrEmpty(selectedText))
            {
                int textX = 8;
                var adapter = SelectedItem as NetworkAdapterInfo;
                if (adapter != null)
                {
                    Color dotColor = adapter.IsOperational ? Theme.AccentSuccess : Theme.TextMuted;
                    var dotRect = new Rectangle(8, (Height - 6) / 2, 6, 6);
                    using (var dotBrush = new SolidBrush(dotColor))
                    {
                        g.FillEllipse(dotBrush, dotRect);
                    }
                    textX = 18;
                }

                var textRect = new Rectangle(textX, 0, Width - textX - 22, Height);
                TextRenderer.DrawText(g, selectedText, Font, textRect, Theme.TextPrimary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordEllipsis);
            }

            // Draw Dropdown Arrow Chevron (▼)
            int arrowX = Width - 16;
            int arrowY = Height / 2 - 2;
            using (var arrowPen = new Pen(Theme.TextSecondary, 1.4f))
            {
                g.DrawLine(arrowPen, arrowX - 4, arrowY, arrowX, arrowY + 4);
                g.DrawLine(arrowPen, arrowX, arrowY + 4, arrowX + 4, arrowY);
            }
        }
    }
}
