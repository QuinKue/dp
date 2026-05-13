using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Vullnerability
{
    // Светлая тема для всего приложения. Цветовая палитра спокойная, блоки
    // отличаются по фону (Surface / Card / Header), чтобы они визуально не сливались.
    // Подход: после InitializeComponent() вызвать UiTheme.Apply(this) — палитра
    // рекурсивно проходит по дереву контролов и подменяет тёмные цвета.
    public static class UiTheme
    {
        // Базовая палитра
        public static readonly Color Background = Color.FromArgb(245, 247, 250); // основной фон формы
        public static readonly Color Surface = Color.White;                      // карточки и панели поверх фона
        public static readonly Color Surface2 = Color.FromArgb(238, 241, 246);   // вспомогательная (вторая) поверхность
        public static readonly Color Header = Color.FromArgb(229, 234, 240);    // шапки таблиц, заголовки блоков
        public static readonly Color Border = Color.FromArgb(206, 212, 222);    // тонкие рамки между блоками
        public static readonly Color TextPrimary = Color.FromArgb(28, 32, 38);  // основной тёмный текст
        public static readonly Color TextMuted = Color.FromArgb(96, 105, 117); // подписи, второстепенный текст
        public static readonly Color Accent = Color.FromArgb(0, 120, 215);      // первичные кнопки
        public static readonly Color AccentText = Color.White;
        public static readonly Color SecondaryButton = Color.FromArgb(231, 236, 242);
        public static readonly Color SecondaryButtonText = Color.FromArgb(50, 55, 65);

        // Цвета строки уязвимости по уровню опасности — оставляем «светофор», но
        // в более мягких тонах, чтобы не выбивалось из светлой темы.
        public static readonly Color SeverityCritical = Color.FromArgb(214, 68, 76);
        public static readonly Color SeverityHigh = Color.FromArgb(232, 142, 64);
        public static readonly Color SeverityMedium = Color.FromArgb(220, 188, 70);
        public static readonly Color SeverityLow = Color.FromArgb(110, 175, 110);
        public static readonly Color SeverityUnknown = Color.FromArgb(170, 178, 188);

        // Имена кнопок, которые трактуем как «первичные» (акцентные).
        // Стиль кнопок задаётся в Apply() по имени, чтобы не плодить хардкод.
        private static readonly HashSet<string> PrimaryButtonNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "btnApplyFilter",
            "btnUpdateFromBdu",
            "btnLogin",
            "btnRegister",
            "btnApply",
        };

        public static void Apply(Control root)
        {
            if (root == null) return;

            // Сама форма / контейнер
            if (root is Form form)
            {
                form.BackColor = Background;
                form.ForeColor = TextPrimary;
            }

            ApplyRecursive(root);
        }

        private static void ApplyRecursive(Control c)
        {
            StyleControl(c);
            foreach (Control child in c.Controls)
                ApplyRecursive(child);
        }

        private static void StyleControl(Control c)
        {
            // Без switch-pattern: Mono mcs 6.8 ломается на CS0589 при таком виде.
            if (c is TabControl)
            {
                c.BackColor = Background;
                c.ForeColor = TextPrimary;
                return;
            }
            if (c is TabPage)
            {
                c.BackColor = Background;
                c.ForeColor = TextPrimary;
                return;
            }

            var grid = c as DataGridView;
            if (grid != null) { StyleDataGrid(grid); return; }

            var lst = c as ListBox;
            if (lst != null)
            {
                lst.BackColor = Surface;
                lst.ForeColor = TextPrimary;
                lst.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            var tb = c as TextBox;
            if (tb != null)
            {
                tb.BackColor = Surface;
                tb.ForeColor = TextPrimary;
                tb.BorderStyle = BorderStyle.FixedSingle;
                return;
            }

            var cb = c as ComboBox;
            if (cb != null)
            {
                cb.BackColor = Surface;
                cb.ForeColor = TextPrimary;
                cb.FlatStyle = FlatStyle.Flat;
                return;
            }

            var dtp = c as DateTimePicker;
            if (dtp != null)
            {
                dtp.CalendarMonthBackground = Surface;
                dtp.CalendarForeColor = TextPrimary;
                return;
            }

            var chk = c as CheckBox;
            if (chk != null)
            {
                chk.BackColor = Color.Transparent;
                chk.ForeColor = TextPrimary;
                chk.FlatStyle = FlatStyle.Standard;
                return;
            }

            var btn = c as Button;
            if (btn != null) { StyleButton(btn); return; }

            var lbl = c as Label;
            if (lbl != null) { StyleLabel(lbl); return; }

            var pnl = c as Panel;
            if (pnl != null) { StylePanel(pnl); return; }
        }

        private static void StylePanel(Panel p)
        {
            // Угадываем роль панели по имени — так не надо менять Designer.
            string name = p.Name ?? string.Empty;
            if (name.Equals("panelFilters", StringComparison.Ordinal))
            {
                p.BackColor = Surface;
            }
            else if (name.Equals("panelRight", StringComparison.Ordinal))
            {
                p.BackColor = Surface;
            }
            else if (name.Equals("panelCenter", StringComparison.Ordinal))
            {
                p.BackColor = Surface2;
            }
            else if (name.Equals("panelTopBar", StringComparison.Ordinal)
                     || name.Equals("panelBottomBar", StringComparison.Ordinal))
            {
                p.BackColor = Header;
            }
            else if (name.Equals("panelTableHeader", StringComparison.Ordinal))
            {
                p.BackColor = Header;
            }
            else if (name.Equals("panelVullsWrap", StringComparison.Ordinal))
            {
                p.BackColor = Surface;
            }
            else
            {
                p.BackColor = Surface;
            }
            p.ForeColor = TextPrimary;
        }

        private static void StyleLabel(Label l)
        {
            // Прозрачный фон, чтобы подложка панели «проступала» — так блоки видно.
            l.BackColor = Color.Transparent;

            string name = l.Name ?? string.Empty;
            // Заголовки/шапки таблицы — более тёмные.
            if (name.Equals("lblFilterTitle", StringComparison.Ordinal)
                || name.Equals("lblLastChanges", StringComparison.Ordinal)
                || name.StartsWith("lblCol", StringComparison.Ordinal))
            {
                l.ForeColor = TextPrimary;
            }
            else
            {
                // Имена полей — приглушённые
                l.ForeColor = TextMuted;
            }
        }

        private static void StyleButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.UseVisualStyleBackColor = false;

            if (PrimaryButtonNames.Contains(b.Name))
            {
                b.BackColor = Accent;
                b.ForeColor = AccentText;
                b.FlatAppearance.BorderColor = Accent;
            }
            else
            {
                b.BackColor = SecondaryButton;
                b.ForeColor = SecondaryButtonText;
                b.FlatAppearance.BorderColor = Border;
            }
        }

        private static void StyleDataGrid(DataGridView g)
        {
            g.BackgroundColor = Surface;
            g.GridColor = Border;
            g.BorderStyle = BorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.RowHeadersVisible = false;

            var headerStyle = new DataGridViewCellStyle
            {
                BackColor = Header,
                ForeColor = TextPrimary,
                SelectionBackColor = Header,
                SelectionForeColor = TextPrimary,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(6, 0, 6, 0),
            };
            g.ColumnHeadersDefaultCellStyle = headerStyle;

            var rowStyle = new DataGridViewCellStyle
            {
                BackColor = Surface,
                ForeColor = TextPrimary,
                SelectionBackColor = Color.FromArgb(212, 226, 244),
                SelectionForeColor = TextPrimary,
                WrapMode = g.DefaultCellStyle.WrapMode,
                Alignment = g.DefaultCellStyle.Alignment,
            };
            g.DefaultCellStyle = rowStyle;

            // чередуем строки, чтобы строки не сливались
            g.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Surface2,
                ForeColor = TextPrimary,
                SelectionBackColor = Color.FromArgb(212, 226, 244),
                SelectionForeColor = TextPrimary,
                WrapMode = rowStyle.WrapMode,
                Alignment = rowStyle.Alignment,
            };
        }

        // Цвет полоски по уровню опасности — используется и в Form1, и в графике.
        public static Color SeverityColor(string severityKeyword)
        {
            if (string.IsNullOrEmpty(severityKeyword)) return SeverityUnknown;
            var t = severityKeyword.ToLowerInvariant();
            if (t.Contains("крит")) return SeverityCritical;
            if (t.Contains("высок")) return SeverityHigh;
            if (t.Contains("сред")) return SeverityMedium;
            if (t.Contains("низк")) return SeverityLow;
            return SeverityUnknown;
        }
    }
}
