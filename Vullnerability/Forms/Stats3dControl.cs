using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vullnerability.Data;
using Vullnerability.Utils;

namespace Vullnerability.Forms
{
    // Вкладка «Статистика». Рисует 3D-вид столбчатой диаграммы (изометрия + тень)
    // на основе агрегатов по таблице vulnerabilities. Срез выбирается в комбобоксе:
    //   - по уровню опасности;
    //   - по году публикации;
    //   - по статусу.
    public sealed class Stats3dControl : UserControl
    {
        private enum Slice { Severity, Year, Status }

        private struct Bar
        {
            public string Label;
            public int Count;
            public Color Color;
        }

        private readonly Panel _toolbar;
        private readonly Label _lblSlice;
        private readonly ComboBox _cmbSlice;
        private readonly Button _btnRefresh;
        private readonly Label _lblTotal;
        private readonly ChartArea _chart;

        private Bar[] _bars = new Bar[0];
        private int _total = 0;

        public Stats3dControl()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = UiTheme.Surface2;

            _toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = UiTheme.Header,
                Padding = new Padding(10, 6, 10, 6),
            };
            _lblSlice = new Label
            {
                Text = "Срез:",
                AutoSize = false,
                Location = new Point(10, 12),
                Size = new Size(48, 20),
                ForeColor = UiTheme.TextPrimary,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _cmbSlice = new ComboBox
            {
                Location = new Point(58, 9),
                Size = new Size(260, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = UiTheme.Surface,
                ForeColor = UiTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
            };
            _cmbSlice.Items.AddRange(new object[]
            {
                "По уровню опасности",
                "По году публикации",
                "По статусу",
            });
            _cmbSlice.SelectedIndex = 0;
            _cmbSlice.SelectedIndexChanged += (s, e) => ReloadAsync();

            _btnRefresh = new Button
            {
                Text = "Обновить",
                Location = new Point(330, 8),
                Size = new Size(110, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = UiTheme.SecondaryButton,
                ForeColor = UiTheme.SecondaryButtonText,
                UseVisualStyleBackColor = false,
            };
            _btnRefresh.FlatAppearance.BorderColor = UiTheme.Border;
            _btnRefresh.Click += (s, e) => ReloadAsync();

            _lblTotal = new Label
            {
                AutoSize = false,
                Location = new Point(460, 12),
                Size = new Size(400, 20),
                BackColor = Color.Transparent,
                ForeColor = UiTheme.TextMuted,
                Text = "Всего уязвимостей: —",
            };

            _toolbar.Controls.Add(_lblSlice);
            _toolbar.Controls.Add(_cmbSlice);
            _toolbar.Controls.Add(_btnRefresh);
            _toolbar.Controls.Add(_lblTotal);

            _chart = new ChartArea
            {
                Dock = DockStyle.Fill,
                BackColor = UiTheme.Surface,
            };

            this.Controls.Add(_chart);
            this.Controls.Add(_toolbar);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!this.IsInDesignMode())
                ReloadAsync();
        }

        private bool IsInDesignMode()
        {
            return LicenseManager.UsageMode == LicenseUsageMode.Designtime
                   || this.Site != null && this.Site.DesignMode;
        }

        private async void ReloadAsync()
        {
            try
            {
                _btnRefresh.Enabled = false;
                _cmbSlice.Enabled = false;
                _lblTotal.Text = "Загрузка...";

                Slice slice = (Slice)_cmbSlice.SelectedIndex;
                var data = await Task.Run(() => LoadSlice(slice));

                _bars = data.Bars;
                _total = data.Total;
                _lblTotal.Text = $"Всего уязвимостей: {_total:N0}".Replace(',', ' ');
                _chart.SetData(_bars);
            }
            catch (Exception ex)
            {
                _lblTotal.Text = "Ошибка: " + ex.Message;
            }
            finally
            {
                _btnRefresh.Enabled = true;
                _cmbSlice.Enabled = true;
            }
        }

        private struct SliceData
        {
            public Bar[] Bars;
            public int Total;
        }

        // вся выборка делается в фоновом потоке через отдельный контекст
        private static SliceData LoadSlice(Slice slice)
        {
            using (var ctx = new VulnDbContext())
            {
                int total = ctx.Vulnerabilities.AsNoTracking().Count();
                Bar[] bars;
                switch (slice)
                {
                    case Slice.Severity:
                        bars = LoadBySeverity(ctx);
                        break;
                    case Slice.Year:
                        bars = LoadByYear(ctx);
                        break;
                    case Slice.Status:
                    default:
                        bars = LoadByStatus(ctx);
                        break;
                }
                return new SliceData { Bars = bars, Total = total };
            }
        }

        private static Bar[] LoadBySeverity(VulnDbContext ctx)
        {
            var raw = ctx.Vulnerabilities.AsNoTracking()
                .GroupBy(v => v.SeverityLevel != null ? v.SeverityLevel.Name : null)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToList();

            // Сортируем в нужном порядке Критический → Низкий → Прочее
            string[] order = { "Критический", "Высокий", "Средний", "Низкий" };
            var ordered = order
                .Select(name => new Bar
                {
                    Label = name,
                    Count = raw.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))?.Count ?? 0,
                    Color = UiTheme.SeverityColor(name),
                })
                .ToList();

            int other = raw
                .Where(r => !order.Any(o => string.Equals(o, r.Name, StringComparison.OrdinalIgnoreCase)))
                .Sum(r => r.Count);
            if (other > 0)
                ordered.Add(new Bar { Label = "Не указан", Count = other, Color = UiTheme.SeverityUnknown });

            return ordered.Where(b => b.Count > 0).ToArray();
        }

        private static Bar[] LoadByYear(VulnDbContext ctx)
        {
            var raw = ctx.Vulnerabilities.AsNoTracking()
                .Where(v => v.PublicationDate != null)
                .Select(v => v.PublicationDate.Value.Year)
                .GroupBy(y => y)
                .Select(g => new { Year = g.Key, Count = g.Count() })
                .OrderBy(x => x.Year)
                .ToList();

            return raw
                .Select(x => new Bar
                {
                    Label = x.Year.ToString(),
                    Count = x.Count,
                    Color = UiTheme.Accent,
                })
                .ToArray();
        }

        private static Bar[] LoadByStatus(VulnDbContext ctx)
        {
            var raw = ctx.Vulnerabilities.AsNoTracking()
                .GroupBy(v => v.Status != null ? v.Status.Name : null)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToList();

            Color[] palette =
            {
                Color.FromArgb(50, 132, 200),
                Color.FromArgb(170, 110, 200),
                Color.FromArgb(230, 150, 70),
                Color.FromArgb(80, 170, 130),
            };

            int i = 0;
            return raw
                .OrderByDescending(r => r.Count)
                .Select(r => new Bar
                {
                    Label = r.Name ?? "Не указан",
                    Count = r.Count,
                    Color = palette[i++ % palette.Length],
                })
                .ToArray();
        }

        // Сама диаграмма как отдельный контрол, чтобы инвалидация была локальной.
        private sealed class ChartArea : Panel
        {
            private Bar[] _data = new Bar[0];

            public ChartArea()
            {
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

                // оси
                using (var axisPen = new Pen(UiTheme.Border, 1f))
                {
                    // Y-ось
                    g.DrawLine(axisPen, padLeft, padTop, padLeft, padTop + height);
                    // X-ось (нижняя)
                    g.DrawLine(axisPen, padLeft, padTop + height, padLeft + width, padTop + height);
                    // «глубина» — задняя плоскость
                    g.DrawLine(axisPen, padLeft, padTop, padLeft + depth, padTop - depth);
                    g.DrawLine(axisPen, padLeft + depth, padTop - depth, padLeft + width + depth, padTop - depth);
                    g.DrawLine(axisPen, padLeft + width, padTop + height, padLeft + width + depth, padTop + height - depth);
                    g.DrawLine(axisPen, padLeft + width + depth, padTop - depth, padLeft + width + depth, padTop + height - depth);
                }

                // горизонтальные деления Y
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

                // сами столбцы
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

                        // значение над столбцом
                        if (h > 0)
                        {
                            string valueText = bar.Count.ToString("N0").Replace(',', ' ');
                            var valueSize = g.MeasureString(valueText, this.Font);
                            var valueRect = new RectangleF(x, Math.Max(padTop - 4, top - valueSize.Height - 4), barWidth + depth, valueSize.Height);
                            g.DrawString(valueText, this.Font, labelBrush, valueRect, fmtCenter);
                        }

                        // подпись под столбцом
                        var labelRect = new RectangleF(x - gap / 2f, padTop + height + 6, barWidth + depth + gap, 36);
                        DrawWrappedLabel(g, bar.Label, this.Font, labelMuted, labelRect);

                        x += barWidth + gap;
                    }
                }
            }

            // Имитация 3D: лицо + верх (parallelogram) + правый бок.
            private static void DrawBar3D(Graphics g, float x, float y, float w, float h, float depth, Color color)
            {
                if (h <= 0) h = 0.5f; // чтобы был хоть какой-то намёк на нулевой столбец

                Color top = ControlPaint.Light(color, 0.5f);
                Color side = ControlPaint.Dark(color, 0.15f);

                // тень под лицом
                using (var shadow = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                    g.FillRectangle(shadow, x + 3, y + h, w + depth, 3);

                // лицевая грань с градиентом
                var faceRect = new RectangleF(x, y, w, h);
                using (var faceBrush = new LinearGradientBrush(faceRect,
                    ControlPaint.Light(color, 0.1f), color, LinearGradientMode.Vertical))
                    g.FillRectangle(faceBrush, faceRect);

                // правая боковая грань
                PointF[] sideQuad =
                {
                    new PointF(x + w, y),
                    new PointF(x + w + depth, y - depth),
                    new PointF(x + w + depth, y + h - depth),
                    new PointF(x + w, y + h),
                };
                using (var sideBrush = new SolidBrush(side))
                    g.FillPolygon(sideBrush, sideQuad);

                // верхняя грань (параллелограмм)
                PointF[] topQuad =
                {
                    new PointF(x, y),
                    new PointF(x + depth, y - depth),
                    new PointF(x + w + depth, y - depth),
                    new PointF(x + w, y),
                };
                using (var topBrush = new SolidBrush(top))
                    g.FillPolygon(topBrush, topQuad);

                // контур
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
}
