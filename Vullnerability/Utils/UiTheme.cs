using System.Drawing;

namespace Vullnerability.Utils
{
    // Палитра светлой темы. Используется только как источник цветовых констант
    // для код-биуилда (Stats3dControl, VulnerabilityDetailsForm) и для
    // отрисовки в OwnerDraw-компонентах. Сам UI задаётся в Designer.cs —
    // тут НЕТ хелперов, патчящих контролы во время рантайма.
    public static class UiTheme
    {
        public static readonly Color Background = Color.FromArgb(245, 247, 250);
        public static readonly Color Surface = Color.White;
        public static readonly Color Surface2 = Color.FromArgb(238, 241, 246);
        public static readonly Color Header = Color.FromArgb(229, 234, 240);
        public static readonly Color Border = Color.FromArgb(206, 212, 222);
        public static readonly Color TextPrimary = Color.FromArgb(28, 32, 38);
        public static readonly Color TextMuted = Color.FromArgb(96, 105, 117);
        public static readonly Color Accent = Color.FromArgb(0, 120, 215);
        public static readonly Color AccentText = Color.White;
        public static readonly Color SecondaryButton = Color.FromArgb(231, 236, 242);
        public static readonly Color SecondaryButtonText = Color.FromArgb(50, 55, 65);

        public static readonly Color SeverityCritical = Color.FromArgb(214, 68, 76);
        public static readonly Color SeverityHigh = Color.FromArgb(232, 142, 64);
        public static readonly Color SeverityMedium = Color.FromArgb(220, 188, 70);
        public static readonly Color SeverityLow = Color.FromArgb(110, 175, 110);
        public static readonly Color SeverityUnknown = Color.FromArgb(170, 178, 188);

        // Цвет полоски слева в DataGridView по ключевому слову уровня опасности.
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
