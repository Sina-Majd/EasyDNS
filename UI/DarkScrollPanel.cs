using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyDNS.UI
{
    internal sealed class DarkScrollContentPanel : Panel
    {
        public DarkScrollContentPanel()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            UpdateStyles();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x02000000; // WS_CLIPCHILDREN
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Do not paint background separately to prevent flicker
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(brush, e.ClipRectangle);
            }
            base.OnPaint(e);
        }
    }

    public class DarkScrollPanel : Panel, IMessageFilter
    {
        private int _scrollValue = 0;
        private int _maxScroll = 0;
        private readonly DarkScrollContentPanel _contentContainer;

        private const int WM_MOUSEWHEEL = 0x020A;
        private const int ScrollBarWidth = 8;
        private bool _isDraggingThumb = false;
        private int _dragStartY = 0;
        private int _dragStartScroll = 0;
        private bool _isThumbHovered = false;

        public Panel ContentContainer
        {
            get { return _contentContainer; }
        }

        public int MaxScroll
        {
            get { return _maxScroll; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x02000000; // WS_CLIPCHILDREN
                cp.ExStyle |= 0x02000000; // WS_EX_COMPOSITED
                return cp;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Do not paint background separately to prevent flicker
        }

        public DarkScrollPanel()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Theme.BackgroundDark;
            Padding = new Padding(0);

            _contentContainer = new DarkScrollContentPanel
            {
                Location = new Point(0, 0),
                BackColor = Theme.BackgroundDark,
                Size = new Size(Math.Max(100, Width - ScrollBarWidth - 10), Height)
            };

            _contentContainer.MouseWheel += delegate(object s, MouseEventArgs e)
            {
                OnMouseWheel(e);
            };

            _contentContainer.SizeChanged += delegate(object s, EventArgs e)
            {
                int targetW = _contentContainer.Width - 4;
                if (targetW > 50)
                {
                    foreach (Control ctrl in _contentContainer.Controls)
                    {
                        var card = ctrl as DnsPresetCardControl;
                        if (card != null && card.Width != targetW)
                        {
                            card.Width = targetW;
                        }
                    }
                }
            };

            Controls.Add(_contentContainer);

            try
            {
                Application.AddMessageFilter(this);
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    Application.RemoveMessageFilter(this);
                }
                catch { }
            }
            base.Dispose(disposing);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_MOUSEWHEEL && IsHandleCreated && Visible)
            {
                Point mousePos = Cursor.Position;
                Rectangle screenRect = RectangleToScreen(ClientRectangle);

                if (screenRect.Contains(mousePos))
                {
                    if (_maxScroll > 0)
                    {
                        int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                        int scrollStep = -(delta / 120) * 45;
                        SetScrollPosition(_scrollValue + scrollStep);
                    }
                    return true;
                }
            }
            return false;
        }

        public void ResetScroll()
        {
            _scrollValue = 0;
            _contentContainer.Location = new Point(0, 0);
            Invalidate();
        }

        public void RecalculateContentHeight()
        {
            int totalHeight = 0;
            foreach (Control ctrl in _contentContainer.Controls)
            {
                if (ctrl.Visible)
                {
                    int bottom = ctrl.Top + ctrl.Height;
                    if (bottom > totalHeight) totalHeight = bottom;
                }
            }

            totalHeight += 20;
            _contentContainer.Height = Math.Max(Height, totalHeight);

            int viewHeight = Height;
            _maxScroll = Math.Max(0, totalHeight - viewHeight);

            if (_scrollValue > _maxScroll)
            {
                _scrollValue = _maxScroll;
            }

            _contentContainer.Location = new Point(0, -_scrollValue);
            _contentContainer.Width = Math.Max(100, Width - (_maxScroll > 0 ? (ScrollBarWidth + 10) : 4));
            Invalidate();
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            RecalculateContentHeight();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_maxScroll <= 0) return;

            int delta = -(e.Delta / 120) * 45;
            SetScrollPosition(_scrollValue + delta);
        }

        private void InvalidateScrollBar()
        {
            int trackX = Width - ScrollBarWidth - 8;
            Invalidate(new Rectangle(Math.Max(0, trackX), 0, ScrollBarWidth + 8, Height));
        }

        public void SetScrollPosition(int newPos)
        {
            if (newPos < 0) newPos = 0;
            if (newPos > _maxScroll) newPos = _maxScroll;

            if (_scrollValue != newPos)
            {
                _scrollValue = newPos;
                _contentContainer.Location = new Point(0, -_scrollValue);
                Invalidate();
                Update();
                _contentContainer.Update();
            }
        }

        private Rectangle GetThumbRect()
        {
            if (_maxScroll <= 0 || Height <= 0) return Rectangle.Empty;

            int trackH = Height - 8;
            int viewH = Height;
            int contentH = _contentContainer.Height;

            int thumbH = Math.Max(32, (viewH * trackH) / Math.Max(1, contentH));
            int thumbY = 4 + (_scrollValue * (trackH - thumbH)) / Math.Max(1, _maxScroll);
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
                _dragStartScroll = _scrollValue;
                Capture = true;
            }
            else if (e.X >= Width - ScrollBarWidth - 8)
            {
                int trackH = Height - 8;
                int thumbH = Math.Max(32, (Height * trackH) / Math.Max(1, _contentContainer.Height));
                int targetY = e.Y - 4 - (thumbH / 2);
                int newScroll = (targetY * _maxScroll) / Math.Max(1, trackH - thumbH);
                SetScrollPosition(newScroll);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var thumb = GetThumbRect();
            bool isOver = thumb.Contains(e.Location) || (e.X >= Width - ScrollBarWidth - 8 && _maxScroll > 0);
            if (isOver != _isThumbHovered)
            {
                _isThumbHovered = isOver;
                InvalidateScrollBar();
            }

            if (_isDraggingThumb && _maxScroll > 0)
            {
                int trackH = Height - 8;
                int thumbH = Math.Max(32, (Height * trackH) / Math.Max(1, _contentContainer.Height));
                int availableTrack = trackH - thumbH;

                if (availableTrack > 0)
                {
                    int deltaY = e.Y - _dragStartY;
                    int deltaScroll = (deltaY * _maxScroll) / availableTrack;
                    SetScrollPosition(_dragStartScroll + deltaScroll);
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
                InvalidateScrollBar();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            using (var bgBrush = new SolidBrush(Theme.BackgroundDark))
            {
                g.FillRectangle(bgBrush, e.ClipRectangle);
            }

            if (_maxScroll <= 0) return;

            // Draw Subtle Scroll Track
            int trackX = Width - ScrollBarWidth - 3;
            var trackRect = new Rectangle(trackX, 4, ScrollBarWidth, Height - 8);
            using (var trackPath = Theme.CreateRoundedRectangle(trackRect, ScrollBarWidth / 2))
            {
                using (var trackBrush = new SolidBrush(Color.FromArgb(16, 22, 34)))
                {
                    g.FillPath(trackBrush, trackPath);
                }
            }

            // Draw Scroll Thumb
            var thumb = GetThumbRect();
            if (thumb.IsEmpty) return;

            Color thumbColor = _isDraggingThumb ? Color.FromArgb(99, 102, 241) : (_isThumbHovered ? Color.FromArgb(79, 88, 125) : Color.FromArgb(48, 54, 80));

            using (var path = Theme.CreateRoundedRectangle(thumb, ScrollBarWidth / 2))
            {
                using (var brush = new SolidBrush(thumbColor))
                {
                    g.FillPath(brush, path);
                }
            }
        }
    }
}
