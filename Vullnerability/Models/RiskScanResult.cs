using System;
using System.Collections.Generic;

namespace Vullnerability.Models
{
    // Одна строка таблицы «Топ-10 рисков»: сопоставление ПО ↔ уязвимость + RiskScore.
    public class RiskRow
    {
        public string Software { get; set; }
        public string BduCode { get; set; }
        public string VulnName { get; set; }
        public string Severity { get; set; }   // "Критический" / "Высокий" / "Средний" / "Низкий"
        public double Cvss3 { get; set; }       // 0..10
        public bool IsCritical { get; set; }    // признак критичности ПО
        public DateTime? PublishedAt { get; set; }
        public int RiskScore { get; set; }      // см. ScannerService.ComputeRiskScore
        public string Mitigation { get; set; }
    }

    // Агрегат по одному ПО для столбчатого графика.
    public class SoftwareRisk
    {
        public string Software { get; set; }
        public double AverageSeverity { get; set; } // 0..10
        public int Findings { get; set; }
    }

    public class ScanReport
    {
        public List<RiskRow> Top { get; set; } = new List<RiskRow>();
        public List<SoftwareRisk> PerSoftware { get; set; } = new List<SoftwareRisk>();
        public int TotalRiskScore { get; set; }
        public List<string> Recommendations { get; set; } = new List<string>();
        public DateTime ScannedAt { get; set; } = DateTime.Now;
    }
}
