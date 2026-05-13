using System;
using System.Drawing.Imaging;
using System.IO;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Vullnerability.Models;

namespace Vullnerability.Services
{
    // Простая выгрузка отчёта сканера в PDF (PdfSharp).
    // Содержимое: заголовок, общий риск-скор, скрин графика, таблица топ-10,
    // блок рекомендаций.
    public static class PdfExportService
    {
        public static void ExportRiskReport(string path, ScanReport report, System.Drawing.Image chartImage)
        {
            if (report == null) throw new ArgumentNullException("report");

            using (var document = new PdfDocument())
            {
                document.Info.Title = "Отчёт о персональных рисках";
                document.Info.Creator = "Vullnerability";

                var fontTitle = new XFont("Arial", 16, XFontStyle.Bold);
                var fontHeader = new XFont("Arial", 11, XFontStyle.Bold);
                var fontBody = new XFont("Arial", 9, XFontStyle.Regular);
                var fontSmall = new XFont("Arial", 8, XFontStyle.Regular);

                var page = document.AddPage();
                page.Size = PdfSharp.PageSize.A4;
                var gfx = XGraphics.FromPdfPage(page);
                double y = 30;

                try
                {
                    gfx.DrawString("Персональный отчёт о рисках", fontTitle, XBrushes.Black, new XPoint(30, y));
                    y += 22;
                    gfx.DrawString($"Сформирован: {report.ScannedAt:dd.MM.yyyy HH:mm}", fontBody, XBrushes.DarkGray, new XPoint(30, y));
                    y += 14;
                    gfx.DrawString($"Общий риск-скор: {report.TotalRiskScore}", fontHeader, XBrushes.Firebrick, new XPoint(30, y));
                    y += 22;

                    if (chartImage != null)
                    {
                        using (var ms = new MemoryStream())
                        {
                            chartImage.Save(ms, ImageFormat.Png);
                            ms.Position = 0;
                            using (var img = XImage.FromStream(ms))
                            {
                                double w = 535;
                                double h = w * img.PixelHeight / Math.Max(1, img.PixelWidth);
                                if (h > 220) { h = 220; w = h * img.PixelWidth / Math.Max(1, img.PixelHeight); }
                                gfx.DrawImage(img, 30, y, w, h);
                                y += h + 12;
                            }
                        }
                    }

                    gfx.DrawString("Топ-10 уязвимостей", fontHeader, XBrushes.Black, new XPoint(30, y));
                    y += 16;

                    double[] colX = { 30, 130, 230, 330, 380, 430, 480 };
                    string[] headers = { "ПО", "BDU", "Уровень", "CVSS3", "Крит.", "Дата", "Score" };
                    for (int i = 0; i < headers.Length; i++)
                        gfx.DrawString(headers[i], fontHeader, XBrushes.Black, new XPoint(colX[i], y));
                    y += 14;
                    gfx.DrawLine(XPens.LightGray, 30, y - 4, 540, y - 4);

                    foreach (var row in report.Top)
                    {
                        if (y > page.Height - 80)
                        {
                            gfx.Dispose();
                            page = document.AddPage();
                            page.Size = PdfSharp.PageSize.A4;
                            gfx = XGraphics.FromPdfPage(page);
                            y = 30;
                        }

                        gfx.DrawString(Truncate(row.Software, 18), fontBody, XBrushes.Black, new XPoint(colX[0], y));
                        gfx.DrawString(row.BduCode ?? "—", fontBody, XBrushes.Black, new XPoint(colX[1], y));
                        gfx.DrawString(Truncate(row.Severity, 14), fontBody, XBrushes.Black, new XPoint(colX[2], y));
                        gfx.DrawString(row.Cvss3.ToString("0.0"), fontBody, XBrushes.Black, new XPoint(colX[3], y));
                        gfx.DrawString(row.IsCritical ? "да" : "нет", fontBody, XBrushes.Black, new XPoint(colX[4], y));
                        gfx.DrawString(row.PublishedAt.HasValue ? row.PublishedAt.Value.ToString("dd.MM.yy") : "—",
                            fontBody, XBrushes.Black, new XPoint(colX[5], y));
                        gfx.DrawString(row.RiskScore.ToString(), fontBody, XBrushes.Firebrick, new XPoint(colX[6], y));
                        y += 13;
                    }

                    if (report.Recommendations != null && report.Recommendations.Count > 0)
                    {
                        y += 12;
                        if (y > page.Height - 60)
                        {
                            gfx.Dispose();
                            page = document.AddPage();
                            page.Size = PdfSharp.PageSize.A4;
                            gfx = XGraphics.FromPdfPage(page);
                            y = 30;
                        }
                        gfx.DrawString("Рекомендации", fontHeader, XBrushes.Black, new XPoint(30, y));
                        y += 14;
                        foreach (var r in report.Recommendations)
                        {
                            if (y > page.Height - 30) break;
                            gfx.DrawString("• " + Truncate(r, 130), fontSmall, XBrushes.Black, new XPoint(30, y));
                            y += 12;
                        }
                    }
                }
                finally
                {
                    gfx.Dispose();
                }

                document.Save(path);
            }
        }

        private static string Truncate(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length > n ? s.Substring(0, n - 1) + "…" : s;
        }
    }
}
