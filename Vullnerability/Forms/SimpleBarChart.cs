using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Vullnerability.Models;
using Vullnerability.Utils;

namespace Vullnerability.Forms
{
    // Лёгкая столбчатая диаграмма для модуля «Сканер рисков».
    // Не таскает System.Windows.Forms.DataVisualization — рисуем GDI+.
    // X = ПО, Y = средний CVSS3. Цвет столбика: <4 зелёный, 4..7 жёлтый, >7 красный.
    public sealed class SimpleBarChart : Control
    {
        private List<SoftwareRisk> _data = new List<SoftwareRisk>();
        public string Title { get; set; } = "Средний CVSS 3.0 по ПО";

        public SimpleBarChart()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Surface;
            ForeColor = UiTheme.TextPrimary;
            Font = new Font("Segoe UI", 9f);
        }

        [Browsable(false)]
        public IReadOnlyList<SoftwareRisk> Data
        {
            get { return _data; }
        }

        public void SetData(IEnumerable<SoftwareRisk> data)
        {
            _data = (data ?? Enumerable.Empty<SoftwareRisk>())
                .OrderByDescending(d => d.AverageSeverity)
                .Take(12)
                .ToList();
            Invalidate();
        }

        // Скрин самого контрола под текущий размер — нужен для PDF-экспорта.
        public Bitmap RenderToBitmap()
        {
            var bmp = new Bitmap(Math.Max(400, Width), Math.Max(180, Height));
            DrawTo(Graphics.FromImage(bmp), new Rectangle(0, 0, bmp.Width, bmp.Height));
            return bmp;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            DrawTo(e.Graphics, ClientRectangle);
        }

        private void DrawTo(Graphics g, Rectangle rc)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (var bg = new SolidBrush(UiTheme.Surface))
                g.FillRectangle(bg, rc);

            using (var titleBrush = new SolidBrush(UiTheme.TextPrimary))
            using (var titleFont = new Font(Font, FontStyle.Bold))
                g.DrawString(Title, titleFont, titleBrush, rc.Left + 12, rc.Top + 8);

            // Поле для осей. Y-ось 0..10 (CVSS 3.0).
            var plot = new Rectangle(rc.Left + 56, rc.Top + 36, rc.Width - 70, rc.Height - 70);

            using (var border = new Pen(UiTheme.Border))
                g.DrawRectangle(border, plot);

            // Горизонтальные линии и подписи.
            using (var grid = new Pen(UiTheme.Border) { DashStyle = DashStyle.Dot })
            using (var labelBrush = new SolidBrush(UiTheme.TextMuted))
            {
                for (int i = 0; i <= 10; i += 2)
                {
                    int y = plot.Bottom - (int)(plot.Height * (i / 10.0));
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    g.DrawString(i.ToString(), Font, labelBrush, plot.Left - 28, y - 7);
                }
            }

            if (_data.Count == 0)
            {
                using (var brush = new SolidBrush(UiTheme.TextMuted))
                    g.DrawString("Нет данных — добавьте ПО и нажмите «Сканировать»",
                        Font, brush, plot.Left + 8, plot.Top + plot.Height / 2 - 8);
                return;
            }

            int barCount = _data.Count;
            double slot = plot.Width / (double)barCount;
            double barWidth = Math.Max(8, slot * 0.65);

            using (var muted = new SolidBrush(UiTheme.TextMuted))
            using (var primary = new SolidBrush(UiTheme.TextPrimary))
            using (var valueFont = new Font(Font, FontStyle.Bold))
            {
                for (int i = 0; i < barCount; i++)
                {
                    var item = _data[i];
                    double v = Math.Max(0, Math.Min(10, item.AverageSeverity));
                    int x = (int)(plot.Left + slot * i + (slot - barWidth) / 2);
                    int h = (int)(plot.Height * (v / 10.0));
                    int y = plot.Bottom - h;
                    int w = (int)barWidth;

                    Color top = SeverityToColor(v);
                    Color bottom = ControlPaint.Dark(top, 0.10f);
                    using (var lin = new LinearGradientBrush(
                        new Rectangle(x, y, w, Math.Max(1, h)),
                        top, bottom, LinearGradientMode.Vertical))
                    {
                        g.FillRectangle(lin, x, y, w, h);
                    }
                    using (var pen = new Pen(ControlPaint.Dark(top, 0.20f)))
                        g.DrawRectangle(pen, x, y, w, h);

                    // подпись «значение» над столбиком
                    g.DrawString(v.ToString("0.0"), valueFont, primary,
                        x + w / 2f - 12, y - 16);

                    // подпись «ПО» под осью X — поворачиваем, если длинная
                    var name = TruncateForAxis(item.Software, 22);
                    if (slot < 60)
                    {
                        var state = g.Save();
                        g.TranslateTransform(x + w / 2f, plot.Bottom + 8);
                        g.RotateTransform(-30);
                        g.DrawString(name, Font, muted, 0, 0);
                        g.Restore(state);
                    }
                    else
                    {
                        var sz = g.MeasureString(name, Font);
                        g.DrawString(name, Font, muted, x + w / 2f - sz.Width / 2, plot.Bottom + 6);
                    }
                }
            }
        }

        private static Color SeverityToColor(double v)
        {
            if (v < 4) return UiTheme.SeverityLow;
            if (v < 7) return UiTheme.SeverityMedium;
            return UiTheme.SeverityHigh;
        }

        private static string TruncateForAxis(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length > n ? s.Substring(0, n - 1) + "…" : s;
        }
    }
}
