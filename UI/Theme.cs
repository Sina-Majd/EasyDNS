using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyDNS.UI
{
    public static class Theme
    {
        // 2026 Dark Modern Palette (Matte Slate / Luxury Dark)
        public static readonly Color BackgroundDark = Color.FromArgb(10, 11, 16);       // #0A0B10
        public static readonly Color TitleBarBackground = Color.FromArgb(15, 17, 25);   // #0F1119
        public static readonly Color PanelDark = Color.FromArgb(17, 20, 30);            // #11141E
        public static readonly Color CardBackground = Color.FromArgb(20, 23, 35);        // #141723
        public static readonly Color CardHover = Color.FromArgb(27, 31, 47);             // #1B1F2F
        public static readonly Color CardSelected = Color.FromArgb(28, 34, 58);          // #1C223A
        public static readonly Color InputBackground = Color.FromArgb(13, 15, 23);       // #0D0F17

        // Accents & Borders
        public static readonly Color BorderColor = Color.FromArgb(34, 38, 56);           // #222638
        public static readonly Color BorderHighlight = Color.FromArgb(99, 102, 241);     // #6366F1
        public static readonly Color AccentPrimary = Color.FromArgb(79, 70, 229);        // Indigo #4F46E5
        public static readonly Color AccentPrimaryHover = Color.FromArgb(99, 102, 241);   // Light Indigo #6366F1
        public static readonly Color AccentSuccess = Color.FromArgb(16, 185, 129);       // Emerald Green #10B981
        public static readonly Color AccentSuccessHover = Color.FromArgb(52, 211, 153);
        public static readonly Color AccentWarning = Color.FromArgb(245, 158, 11);       // Amber #F59E0B
        public static readonly Color AccentDanger = Color.FromArgb(244, 63, 94);         // Rose Red #F43F5E
        public static readonly Color AccentDangerHover = Color.FromArgb(251, 113, 133);
        public static readonly Color AccentCyan = Color.FromArgb(6, 182, 212);           // Cyan #06B6D4

        // Window Control Colors
        public static readonly Color WindowBtnHover = Color.FromArgb(32, 37, 54);
        public static readonly Color CloseBtnHover = Color.FromArgb(225, 29, 72);        // Crimson Red #E11D48

        // Text Colors
        public static readonly Color TextPrimary = Color.FromArgb(248, 250, 252);        // #F8FAFC
        public static readonly Color TextSecondary = Color.FromArgb(148, 163, 184);      // #94A3B8
        public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);          // #64748B
        public static readonly Color TextAccent = Color.FromArgb(129, 140, 248);         // #818CF8

        // Fonts
        public static readonly Font HeaderFont = new Font("Segoe UI", 12f, FontStyle.Bold);
        public static readonly Font TitleBarFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        public static readonly Font SubHeaderFont = new Font("Segoe UI", 9.75f, FontStyle.Bold);
        public static readonly Font BodyFont = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font BodyBoldFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        public static readonly Font SmallFont = new Font("Segoe UI", 8.25f, FontStyle.Regular);
        public static readonly Font MonospaceFont = new Font("Consolas", 9f, FontStyle.Regular);

        public static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            if (bounds.Width <= 0 || bounds.Height <= 0) return path;

            if (diameter > bounds.Width) diameter = bounds.Width;
            if (diameter > bounds.Height) diameter = bounds.Height;

            var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color GetLatencyColor(long? latencyMs)
        {
            if (!latencyMs.HasValue) return TextMuted;
            if (latencyMs.Value < 0) return AccentDanger;
            if (latencyMs.Value <= 45) return AccentSuccess;
            if (latencyMs.Value <= 100) return AccentWarning;
            return AccentDanger;
        }
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.CardBackground; } }
        public override Color MenuBorder { get { return Theme.BorderColor; } }
        public override Color MenuItemBorder { get { return Theme.BorderHighlight; } }
        public override Color MenuItemSelected { get { return Theme.CardHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.CardHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.CardHover; } }
        public override Color MenuStripGradientBegin { get { return Theme.CardBackground; } }
        public override Color MenuStripGradientEnd { get { return Theme.CardBackground; } }
        public override Color ImageMarginGradientBegin { get { return Theme.CardBackground; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.CardBackground; } }
        public override Color ImageMarginGradientEnd { get { return Theme.CardBackground; } }
        public override Color SeparatorDark { get { return Theme.BorderColor; } }
        public override Color SeparatorLight { get { return Color.Transparent; } }
    }

    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = (e.Item != null && e.Item.Selected) ? Color.White : Theme.TextPrimary;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.TextSecondary;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var pen = new Pen(Theme.BorderColor, 1))
            {
                int y = e.Item.Height / 2;
                e.Graphics.DrawLine(pen, 28, y, e.Item.Width - 6, y);
            }
        }
    }
}
