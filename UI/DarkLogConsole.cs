using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace EasyDNS.UI
{
    public class DarkLogConsole : Control
    {
        public class LogEntry
        {
            public string Timestamp { get; set; }
            public string Message { get; set; }
            public Color TextColor { get; set; }

            public LogEntry(string message, Color color)
            {
                Timestamp = DateTime.Now.ToString("HH:mm:ss");
                Message = message;
                TextColor = color;
            }
        }

        private readonly List<LogEntry> _logs = new List<LogEntry>();
        private int _scrollOffset = 0;
        private int _maxScroll = 0;
        private const int LineHeight = 20;
        private const int ScrollBarWidth = 6;
        private bool _isDraggingThumb = false;
        private int _dragStartY = 0;
        private int _dragStartScroll = 0;
        private bool _isThumbHovered = false;

        public DarkLogConsole()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CardBackground;
            Font = Theme.MonospaceFont;
            Cursor = Cursors.Default;
        }

        public void AppendLog(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), message);
                return;
            }

            Color color = Theme.TextSecondary;
            if (message.StartsWith("OK:") || message.StartsWith("✓") || message.Contains("success"))
            {
                color = Theme.AccentSuccess;
            }
            else if (message.StartsWith("FAIL:") || message.StartsWith("✕") || message.Contains("failed") || message.Contains("Error"))
            {
                color = Theme.AccentDanger;
            }
            else if (message.Contains("latency:") || message.Contains("Fastest"))
            {
                color = Theme.AccentCyan;
            }

            _logs.Add(new LogEntry(message, color));
            if (_logs.Count > 300)
            {
                _logs.RemoveAt(0);
            }

            RecalculateScroll(true);
            Invalidate();
        }

        public void ClearLogs()
        {
            _logs.Clear();
            _scrollOffset = 0;
            _maxScroll = 0;
            Invalidate();
        }

        private void RecalculateScroll(bool scrollToBottom = false)
        {
            int visibleLines = Math.Max(1, (Height - 12) / LineHeight);
            int totalLines = _logs.Count;
            _maxScroll = Math.Max(0, totalLines - visibleLines);

            if (scrollToBottom || _scrollOffset > _maxScroll)
            {
                _scrollOffset = _maxScroll;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            RecalculateScroll(false);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_maxScroll <= 0) return;

            int lines = -(e.Delta / 120) * 3;
            SetScroll(_scrollOffset + lines);
        }

        private void SetScroll(int val)
        {
            if (val < 0) val = 0;
            if (val > _maxScroll) val = _maxScroll;

            if (_scrollOffset != val)
            {
                _scrollOffset = val;
                Invalidate();
            }
        }

        private Rectangle GetThumbRect()
        {
            if (_maxScroll <= 0 || Height <= 0) return Rectangle.Empty;

            int trackH = Height - 8;
            int visibleLines = Math.Max(1, (Height - 12) / LineHeight);
            int thumbH = Math.Max(20, (visibleLines * trackH) / Math.Max(1, _logs.Count));
            int thumbY = 4 + (_scrollOffset * (trackH - thumbH)) / Math.Max(1, _maxScroll);
            int thumbX = Width - ScrollBarWidth - 3;

            return new Rectangle(thumbX, thumbY, ScrollBarWidth, thumbH);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_maxScroll <= 0) return;

            var thumb = GetThumbRect();
            if (thumb.Contains(e.Location))
            {
                _isDraggingThumb = true;
                _dragStartY = e.Y;
                _dragStartScroll = _scrollOffset;
                Capture = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var thumb = GetThumbRect();
            bool isOver = thumb.Contains(e.Location);
            if (isOver != _isThumbHovered)
            {
                _isThumbHovered = isOver;
                Invalidate();
            }

            if (_isDraggingThumb && _maxScroll > 0)
            {
                int trackH = Height - 8;
                int visibleLines = Math.Max(1, (Height - 12) / LineHeight);
                int thumbH = Math.Max(20, (visibleLines * trackH) / Math.Max(1, _logs.Count));
                int avail = trackH - thumbH;
                if (avail > 0)
                {
                    int dy = e.Y - _dragStartY;
                    int dScroll = (dy * _maxScroll) / avail;
                    SetScroll(_dragStartScroll + dScroll);
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDraggingThumb)
            {
                _isDraggingThumb = false;
                Capture = false;
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

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // 1. Clear with exact parent card background color (Zero corner fringes!)
            Color parentBg = GetEffectiveParentBackground();
            using (var clearBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(clearBrush, ClientRectangle);
            }

            // 2. Draw rounded container background
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            using (var path = Theme.CreateRoundedRectangle(bounds, 5))
            {
                using (var bgBrush = new SolidBrush(Theme.InputBackground))
                {
                    g.FillPath(bgBrush, path);
                }
                using (var borderPen = new Pen(Theme.BorderColor, 1))
                {
                    g.DrawPath(borderPen, path);
                }
            }

            // 3. Draw Log Lines
            int yPos = 6;
            int maxDrawX = Width - (_maxScroll > 0 ? (ScrollBarWidth + 10) : 10);

            for (int i = _scrollOffset; i < _logs.Count; i++)
            {
                if (yPos + LineHeight > Height - 4) break;

                var entry = _logs[i];
                string timeStr = "[" + entry.Timestamp + "] ";

                TextRenderer.DrawText(g, timeStr, Font, new Point(8, yPos), Theme.TextMuted);
                int timeWidth = TextRenderer.MeasureText(timeStr, Font).Width;

                var msgRect = new Rectangle(8 + timeWidth, yPos, maxDrawX - 8 - timeWidth, LineHeight);
                TextRenderer.DrawText(g, entry.Message, Font, msgRect, entry.TextColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordEllipsis);

                yPos += LineHeight;
            }

            // 4. Draw Dark Scrollbar Thumb if scrollable
            if (_maxScroll > 0)
            {
                var thumb = GetThumbRect();
                if (!thumb.IsEmpty)
                {
                    Color thumbColor = _isDraggingThumb ? Color.FromArgb(99, 102, 241) : (_isThumbHovered ? Color.FromArgb(71, 85, 120) : Color.FromArgb(42, 47, 69));
                    using (var thumbPath = Theme.CreateRoundedRectangle(thumb, ScrollBarWidth / 2))
                    {
                        using (var thumbBrush = new SolidBrush(thumbColor))
                        {
                            g.FillPath(thumbBrush, thumbPath);
                        }
                    }
                }
            }
        }
    }
}
