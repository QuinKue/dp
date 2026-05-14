using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Vullnerability.Utils;

namespace Vullnerability.Forms
{
    // GDI+ канвас «3D-столбики». Сам ничего не загружает: данные подаёт
    // MainForm через SetData(Bar[]). Тулбар (срез/«Загрузить БД из файла»/счётчик)
    // полностью переехал в MainForm.Designer.cs.
    public sealed class Stats3dControl : UserControl
    {
        // Один столбец диаграммы.
        public struct Bar
        {
            public string Label;
            public int Count;
            public Color Color;
        }

        private Bar[] _data = new Bar[0];

        public Stats3dControl()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = UiTheme.Surface;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint
                          | ControlStyles.OptimizedDoubleBuffer
                          | ControlStyles.UserPaint
                          | ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true;
        }

        public void SetData(Bar[] data)
        {
            _data = data ?? new Bar[0];
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var clientArea = this.ClientRectangle;
            g.Clear(this.BackColor);

            if (_data.Length == 0)
            {
                using (var brush = new SolidBrush(UiTheme.TextMuted))
                using (var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString("Нет данных для отображения", this.Font, brush, clientArea, fmt);
                return;
            }

            // Поля: слева под подписи оси Y, снизу под подписи столбцов,
            // сверху небольшой запас под значения, справа поле под глубину 3D.
            int padLeft = 64, padRight = 80, padTop = 24, padBottom = 64;
            int width = Math.Max(0, clientArea.Width - padLeft - padRight);
            int height = Math.Max(0, clientArea.Height - padTop - padBottom);
            if (width < 20 || height < 20) return;

            int max = Math.Max(1, _data.Max(b => b.Count));
            int n = _data.Length;
            float depth = Math.Min(40f, Math.Max(20f, width / (n * 6f)));
            float gap = Math.Max(8f, width / (n * 8f));
            float barWidth = Math.Max(20f, (width - gap * (n + 1) - depth) / n);

            using (var axisPen = new Pen(UiTheme.Border, 1f))
            {
                g.DrawLine(axisPen, padLeft, padTop, padLeft, padTop + height);
                g.DrawLine(axisPen, padLeft, padTop + height, padLeft + width, padTop + height);
                g.DrawLine(axisPen, padLeft, padTop, padLeft + depth, padTop - depth);
                g.DrawLine(axisPen, padLeft + depth, padTop - depth, padLeft + width + depth, padTop - depth);
                g.DrawLine(axisPen, padLeft + width, padTop + height, padLeft + width + depth, padTop + height - depth);
                g.DrawLine(axisPen, padLeft + width + depth, padTop - depth, padLeft + width + depth, padTop + height - depth);
            }

            int steps = 5;
            using (var gridPen = new Pen(Color.FromArgb(40, UiTheme.Border)))
            using (var labelBrush = new SolidBrush(UiTheme.TextMuted))
            using (var fmtRight = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                for (int i = 0; i <= steps; i++)
                {
                    float y = padTop + height - i * (float)height / steps;
                    g.DrawLine(gridPen, padLeft, y, padLeft + width, y);
                    int value = (int)Math.Round(i * (double)max / steps);
                    var rect = new RectangleF(0, y - 8, padLeft - 6, 16);
                    g.DrawString(value.ToString("N0").Replace(',', ' '), this.Font, labelBrush, rect, fmtRight);
                }
            }

            using (var labelBrush = new SolidBrush(UiTheme.TextPrimary))
            using (var labelMuted = new SolidBrush(UiTheme.TextMuted))
            using (var fmtCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter })
            {
                float x = padLeft + gap;
                for (int i = 0; i < n; i++)
                {
                    var bar = _data[i];
                    float h = bar.Count <= 0 ? 0f : (float)bar.Count / max * height;
                    float top = padTop + height - h;

                    DrawBar3D(g, x, top, barWidth, h, depth, bar.Color);

                    if (h > 0)
                    {
                        string valueText = bar.Count.ToString("N0").Replace(',', ' ');
                        var valueSize = g.MeasureString(valueText, this.Font);
                        var valueRect = new RectangleF(x, Math.Max(padTop - 4, top - valueSize.Height - 4), barWidth + depth, valueSize.Height);
                        g.DrawString(valueText, this.Font, labelBrush, valueRect, fmtCenter);
                    }

                    var labelRect = new RectangleF(x - gap / 2f, padTop + height + 6, barWidth + depth + gap, 36);
                    DrawWrappedLabel(g, bar.Label, this.Font, labelMuted, labelRect);

                    x += barWidth + gap;
                }
            }
        }

        // Имитация 3D: лицо + верх (parallelogram) + правый бок.
        private static void DrawBar3D(Graphics g, float x, float y, float w, float h, float depth, Color color)
        {
            if (h <= 0) h = 0.5f;

            Color top = ControlPaint.Light(color, 0.5f);
            Color side = ControlPaint.Dark(color, 0.15f);

            using (var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                g.FillRectangle(shadow, x + 3, y + h, w + depth, 3);

            var faceRect = new RectangleF(x, y, w, h);
            using (var faceBrush = new LinearGradientBrush(faceRect,
                ControlPaint.Light(color, 0.1f), color, LinearGradientMode.Vertical))
                g.FillRectangle(faceBrush, faceRect);

            PointF[] sideQuad =
            {
                new PointF(x + w, y),
                new PointF(x + w + depth, y - depth),
                new PointF(x + w + depth, y + h - depth),
                new PointF(x + w, y + h),
            };
            using (var sideBrush = new SolidBrush(side))
                g.FillPolygon(sideBrush, sideQuad);

            PointF[] topQuad =
            {
                new PointF(x, y),
                new PointF(x + depth, y - depth),
                new PointF(x + w + depth, y - depth),
                new PointF(x + w, y),
            };
            using (var topBrush = new SolidBrush(top))
                g.FillPolygon(topBrush, topQuad);

            using (var pen = new Pen(ControlPaint.Dark(color, 0.4f), 1f))
            {
                g.DrawRectangle(pen, x, y, w, h);
                g.DrawPolygon(pen, sideQuad);
                g.DrawPolygon(pen, topQuad);
            }
        }

        private static void DrawWrappedLabel(Graphics g, string text, Font font, Brush brush, RectangleF rect)
        {
            if (string.IsNullOrEmpty(text)) return;
            using (var fmt = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Near,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.LineLimit,
            })
                g.DrawString(text, font, brush, rect, fmt);
        }
    }
}
