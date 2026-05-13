using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Vullnerability.Data;
using Vullnerability.Models;

namespace Vullnerability.Services
{
    // Логика «Персонального сканера рисков»:
    //   * CRUD по таблице user_software (System.Data.SQLite, без EF)
    //   * Скан: для каждого ПО ищем уязвимости в БДУ ФСТЭК через JOIN на products / vulnerabilities,
    //     фильтр LIKE по имени, ограничение по дате публикации (по умолчанию — 6 месяцев),
    //     агрегаты по ПО и сводный RiskScore.
    //
    // Формулы:
    //   RowRiskScore = floor(cvss3 * 10) * (isCritical ? 2 : 1) + (published > now - 30d ? 5 : 0)
    //   TotalRiskScore = sum(RowRiskScore топ-10)
    public static class ScannerService
    {
        private const int RecentDaysWindow = 180; // 6 месяцев — окно сканирования
        private const int FreshBonusDays = 30;    // бонус +5 к скору за «свежую» уязвимость
        private const int TopRowsCount = 10;

        // -------- user_software CRUD --------

        public static Task<List<UserSoftware>> GetUserSoftwareAsync()
        {
            return Task.Run(() =>
            {
                var list = new List<UserSoftware>();
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT id, software_name, version, is_critical, created_at " +
                        "FROM user_software ORDER BY software_name COLLATE NOCASE";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new UserSoftware
                            {
                                Id = r.GetInt32(0),
                                SoftwareName = r.GetString(1),
                                Version = r.IsDBNull(2) ? null : r.GetString(2),
                                IsCritical = r.GetInt64(3) != 0,
                                CreatedAt = ParseIsoDate(r.IsDBNull(4) ? null : r.GetString(4)),
                            });
                        }
                    }
                }
                return list;
            });
        }

        public static Task AddOrUpdateAsync(UserSoftware item)
        {
            return Task.Run(() =>
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    if (item.Id <= 0)
                    {
                        cmd.CommandText =
                            "INSERT INTO user_software(software_name, version, is_critical, created_at) " +
                            "VALUES(@n, @v, @c, @t)";
                        cmd.Parameters.AddWithValue("@n", (object)item.SoftwareName ?? string.Empty);
                        cmd.Parameters.AddWithValue("@v", (object)item.Version ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@c", item.IsCritical ? 1 : 0);
                        cmd.Parameters.AddWithValue("@t", DateTime.UtcNow.ToString("o"));
                    }
                    else
                    {
                        cmd.CommandText =
                            "UPDATE user_software SET software_name=@n, version=@v, is_critical=@c " +
                            "WHERE id=@id";
                        cmd.Parameters.AddWithValue("@n", (object)item.SoftwareName ?? string.Empty);
                        cmd.Parameters.AddWithValue("@v", (object)item.Version ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@c", item.IsCritical ? 1 : 0);
                        cmd.Parameters.AddWithValue("@id", item.Id);
                    }
                    cmd.ExecuteNonQuery();
                }
            });
        }

        public static Task DeleteAsync(int id)
        {
            return Task.Run(() =>
            {
                using (var conn = OpenConnection())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM user_software WHERE id=@id";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            });
        }

        // -------- скан рисков --------

        // Главный метод. Бежит по списку user_software, для каждого ищет уязвимости
        // по LIKE с фильтром по дате публикации, считает RiskScore и агрегирует.
        public static Task<ScanReport> ScanAsync()
        {
            return Task.Run(() =>
            {
                var report = new ScanReport();
                var softwareList = GetUserSoftwareAsync().GetAwaiter().GetResult();
                if (softwareList.Count == 0)
                    return report;

                using (var conn = OpenConnection())
                {
                    var allRows = new List<RiskRow>();

                    foreach (var sw in softwareList)
                    {
                        if (string.IsNullOrWhiteSpace(sw.SoftwareName)) continue;

                        using (var cmd = conn.CreateCommand())
                        {
                            // JOIN products → vulnerability_products → vulnerabilities → severity_levels
                            // Фильтр LIKE по имени продукта + окно 6 месяцев по publication_date.
                            cmd.CommandText = @"
SELECT
    v.id,
    v.bdu_code,
    v.name        AS vuln_name,
    sl.name       AS severity_name,
    v.cvss_3_0_score,
    v.publication_date,
    v.fix_info,
    p.name        AS product_name
FROM products p
JOIN vulnerability_products vp ON vp.product_id      = p.id
JOIN vulnerabilities        v  ON v.id               = vp.vulnerability_id
LEFT JOIN severity_levels   sl ON sl.id              = v.severity_level_id
WHERE p.name LIKE @pat
  AND v.publication_date IS NOT NULL
  AND v.publication_date >= date('now', '-' || @days || ' day');";
                            cmd.Parameters.AddWithValue("@pat", "%" + sw.SoftwareName + "%");
                            cmd.Parameters.AddWithValue("@days", RecentDaysWindow);

                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    double cvss = r.IsDBNull(4) ? 0.0 : Convert.ToDouble(r.GetValue(4), CultureInfo.InvariantCulture);
                                    DateTime? published = ParseDateOrNull(r.IsDBNull(5) ? null : r.GetValue(5).ToString());
                                    string mitigation = r.IsDBNull(6) ? null : r.GetString(6);
                                    string severity = r.IsDBNull(3) ? "—" : r.GetString(3);

                                    var row = new RiskRow
                                    {
                                        Software = sw.SoftwareName,
                                        BduCode = r.IsDBNull(1) ? null : r.GetString(1),
                                        VulnName = r.IsDBNull(2) ? null : r.GetString(2),
                                        Severity = severity,
                                        Cvss3 = cvss,
                                        IsCritical = sw.IsCritical,
                                        PublishedAt = published,
                                        Mitigation = mitigation,
                                    };
                                    row.RiskScore = ComputeRiskScore(cvss, sw.IsCritical, published);
                                    allRows.Add(row);
                                }
                            }
                        }
                    }

                    // дедуп: одна уязвимость могла прицепиться к одному ПО несколько раз
                    // (разные версии в БДУ). Берём максимум по (Software, BduCode).
                    var deduped = allRows
                        .GroupBy(x => new { x.Software, x.BduCode })
                        .Select(g => g.OrderByDescending(z => z.RiskScore).First())
                        .ToList();

                    report.Top = deduped
                        .OrderByDescending(x => x.RiskScore)
                        .Take(TopRowsCount)
                        .ToList();

                    report.PerSoftware = deduped
                        .GroupBy(x => x.Software, StringComparer.OrdinalIgnoreCase)
                        .Select(g => new SoftwareRisk
                        {
                            Software = g.Key,
                            AverageSeverity = g.Any() ? g.Average(z => z.Cvss3) : 0.0,
                            Findings = g.Count(),
                        })
                        .OrderByDescending(s => s.AverageSeverity)
                        .ToList();

                    report.TotalRiskScore = report.Top.Sum(r => r.RiskScore);

                    report.Recommendations = report.Top
                        .Where(r => !string.IsNullOrWhiteSpace(r.Mitigation))
                        .GroupBy(r => r.Software, StringComparer.OrdinalIgnoreCase)
                        .Select(g =>
                        {
                            var bestRow = g.OrderByDescending(z => z.RiskScore).First();
                            string mitigation = (bestRow.Mitigation ?? string.Empty).Trim();
                            if (mitigation.Length > 300) mitigation = mitigation.Substring(0, 300) + "…";
                            return $"Обновите «{g.Key}»: {mitigation}";
                        })
                        .ToList();
                }

                return report;
            });
        }

        public static int ComputeRiskScore(double cvss3, bool isCritical, DateTime? publishedAt)
        {
            int baseScore = (int)Math.Floor(cvss3 * 10.0); // 0..100
            int multiplied = baseScore * (isCritical ? 2 : 1);
            int freshBonus = (publishedAt.HasValue && publishedAt.Value > DateTime.UtcNow.AddDays(-FreshBonusDays)) ? 5 : 0;
            return multiplied + freshBonus;
        }

        // -------- helpers --------

        private static SQLiteConnection OpenConnection()
        {
            string dbPath = SqliteBootstrap.GetDbPath();
            var conn = new SQLiteConnection(new SQLiteConnectionStringBuilder
            {
                DataSource = dbPath,
                ForeignKeys = true,
                BusyTimeout = 10000,
            }.ConnectionString);
            conn.Open();
            using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = "PRAGMA busy_timeout=10000;";
                pragma.ExecuteNonQuery();
            }
            return conn;
        }

        private static DateTime ParseIsoDate(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return DateTime.MinValue;
            DateTime dt;
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt))
                return dt;
            return DateTime.MinValue;
        }

        private static DateTime? ParseDateOrNull(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            DateTime dt;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
                return dt;
            return null;
        }
    }
}
