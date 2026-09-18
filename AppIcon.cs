using System.Drawing;
using System.Drawing.Drawing2D;

namespace LenovoSettingsGui
{
    internal static class AppIcon
    {
        public static Icon Create()
        {
            var bitmap = new Bitmap(32, 32);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var outline = new Pen(Color.FromArgb(37, 99, 235), 3F))
            using (var fill = new SolidBrush(Color.FromArgb(96, 165, 250)))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.White);
                graphics.DrawRoundedRectangle(outline, 5, 5, 21, 22, 4);
                graphics.FillRectangle(fill, 9, 13, 13, 10);
                graphics.FillRectangle(fill, 11, 2, 9, 4);
            }
            Icon temporary = Icon.FromHandle(bitmap.GetHicon());
            Icon result = (Icon)temporary.Clone();
            temporary.Dispose();
            bitmap.Dispose();
            return result;
        }

        private static void DrawRoundedRectangle(this Graphics graphics, Pen pen, int x, int y, int width, int height, int radius)
        {
            using (var path = new GraphicsPath())
            {
                path.AddArc(x, y, radius * 2, radius * 2, 180, 90);
                path.AddArc(x + width - radius * 2, y, radius * 2, radius * 2, 270, 90);
                path.AddArc(x + width - radius * 2, y + height - radius * 2, radius * 2, radius * 2, 0, 90);
                path.AddArc(x, y + height - radius * 2, radius * 2, radius * 2, 90, 90);
                path.CloseFigure();
                graphics.DrawPath(pen, path);
            }
        }
    }
}
