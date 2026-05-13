using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Vullnerability.Data;
using Vullnerability.Models;
using Vullnerability.Services;
using Vullnerability.Utils;

namespace Vullnerability.Forms
{
    // Вкладка «Персональный сканер рисков».
    // Сверху — список ПО пользователя (CRUD), снизу — результаты последнего скана:
    // топ-10 уязвимостей, столбчатый график и блок рекомендаций.
    public partial class ScannerForm : UserControl
    {
        private readonly BindingSource _softwareSource = new BindingSource();
        private readonly BindingSource _topSource = new BindingSource();
        private ScanReport _lastReport;

        public ScannerForm()
        {
            InitializeComponent();

            this.dgvSoftware.DataSource = _softwareSource;
            this.dgvTop.DataSource = _topSource;

            // Под формирование RiskScore-ячейки: красим её цветом уровня.
            this.dgvTop.CellFormatting += DgvTop_CellFormatting;

            this.Load += ScannerForm_Load;
        }

        private async void ScannerForm_Load(object sender, EventArgs e)
        {
            await ReloadSoftwareAsync();
        }

        private async Task ReloadSoftwareAsync()
        {
            try
            {
                var list = await ScannerService.GetUserSoftwareAsync();
                _softwareSource.DataSource = new BindingList<UserSoftware>(list);
                SetStatus($"ПО в списке: {list.Count}");
            }
            catch (Exception ex)
            {
                ShowError("Не удалось загрузить список ПО", ex);
            }
        }

        // ---------- CRUD ----------

        private async void btnAdd_Click(object sender, EventArgs e)
        {
            var item = new UserSoftware();
            using (var dlg = new SoftwareEditorDialog(item))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    await ScannerService.AddOrUpdateAsync(item);
                    await ReloadSoftwareAsync();
                }
                catch (Exception ex) { ShowError("Не удалось сохранить запись", ex); }
            }
        }

        private async void btnEdit_Click(object sender, EventArgs e)
        {
            var current = GetSelectedSoftware();
            if (current == null) { ShowError("Выберите запись для редактирования"); return; }

            using (var dlg = new SoftwareEditorDialog(current))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    await ScannerService.AddOrUpdateAsync(current);
                    await ReloadSoftwareAsync();
                }
                catch (Exception ex) { ShowError("Не удалось обновить запись", ex); }
            }
        }

        private async void btnDelete_Click(object sender, EventArgs e)
        {
            var current = GetSelectedSoftware();
            if (current == null) { ShowError("Выберите запись для удаления"); return; }

            var confirm = MessageBox.Show(
                $"Удалить «{current.SoftwareName}»?",
                "Подтверждение",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;

            try
            {
                await ScannerService.DeleteAsync(current.Id);
                await ReloadSoftwareAsync();
            }
            catch (Exception ex) { ShowError("Не удалось удалить запись", ex); }
        }

        private UserSoftware GetSelectedSoftware()
        {
            if (this.dgvSoftware.CurrentRow == null) return null;
            return this.dgvSoftware.CurrentRow.DataBoundItem as UserSoftware;
        }

        // ---------- scan / export / import ----------

        private async void btnScan_Click(object sender, EventArgs e)
        {
            this.btnScan.Enabled = false;
            try
            {
                SetStatus("Сканирование…");
                var report = await ScannerService.ScanAsync();
                _lastReport = report;

                _topSource.DataSource = new BindingList<RiskRow>(report.Top);
                this.chart.SetData(report.PerSoftware);
                this.lblTotal.Text = $"Общий риск-скор: {report.TotalRiskScore}";

                if (report.Recommendations.Count == 0)
                {
                    this.rtbRecommendations.Text = report.Top.Count == 0
                        ? "Уязвимости за последние 6 месяцев не найдены — список ПО либо пустой, либо ничего не совпало с БДУ."
                        : "Подробных рекомендаций нет в базе по найденным уязвимостям.";
                }
                else
                {
                    this.rtbRecommendations.Text = string.Join(Environment.NewLine + Environment.NewLine,
                        report.Recommendations);
                }

                SetStatus($"Найдено уязвимостей: {report.Top.Count}, скор: {report.TotalRiskScore}");
            }
            catch (Exception ex)
            {
                ShowError("Сканирование не выполнено", ex);
            }
            finally
            {
                this.btnScan.Enabled = true;
            }
        }

        private void btnExportPdf_Click(object sender, EventArgs e)
        {
            if (_lastReport == null)
            {
                ShowError("Сначала запустите сканирование");
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "PDF (*.pdf)|*.pdf";
                sfd.FileName = $"risk-report-{DateTime.Now:yyyy-MM-dd-HHmm}.pdf";
                if (sfd.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    using (var bmp = this.chart.RenderToBitmap())
                    {
                        PdfExportService.ExportRiskReport(sfd.FileName, _lastReport, bmp);
                    }
                    SetStatus("PDF сохранён");
                }
                catch (Exception ex) { ShowError("Не удалось сохранить PDF", ex); }
            }
        }

        private async void btnImportExcel_Click(object sender, EventArgs e)
        {
            string xlsxPath;
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Excel БДУ (*.xlsx;*.xls)|*.xlsx;*.xls";
                ofd.Title = "Выберите xlsx с уязвимостями (vullist.xlsx)";
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                xlsxPath = ofd.FileName;
            }

            this.btnImportExcel.Enabled = false;
            try
            {
                SetStatus("Импорт из Excel…");
                var dbPath = SqliteBootstrap.GetDbPath();
                var lastBefore = TryReadMaxPublicationDate(dbPath);

                // ExcelImporter синхронный, поэтому уносим в фоновый таск.
                string connStr = new System.Data.SQLite.SQLiteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    ForeignKeys = true,
                    BusyTimeout = 10000,
                }.ConnectionString;
                var stats = await Task.Run(() =>
                {
                    var importer = new ExcelImporter(connStr);
                    return importer.ImportFromExcel(xlsxPath);
                });

                var lastAfter = TryReadMaxPublicationDate(dbPath);
                int newSince = 0;
                if (lastBefore.HasValue && lastAfter.HasValue && lastAfter.Value > lastBefore.Value)
                {
                    newSince = CountVulnsSince(dbPath, lastBefore.Value);
                }

                SetStatus($"Импорт ок: добавлено {stats.AddedVulns}, пропущено {stats.SkippedVulns}.");
                MessageBox.Show(
                    $"Импорт завершён.\n" +
                    $"Добавлено уязвимостей: {stats.AddedVulns}\n" +
                    $"Пропущено: {stats.SkippedVulns}\n" +
                    $"Новых после последней даты ({lastBefore?.ToString("dd.MM.yyyy") ?? "—"}): {newSince}",
                    "Обновление БД",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex) { ShowError("Импорт не выполнен", ex); }
            finally { this.btnImportExcel.Enabled = true; }
        }

        // ---------- helpers ----------

        private void DgvTop_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var col = this.dgvTop.Columns[e.ColumnIndex];
            if (col == this.colTopSeverity && e.Value is string s)
            {
                e.CellStyle.ForeColor = UiTheme.SeverityColor(s);
                e.CellStyle.Font = new Font(this.dgvTop.Font, FontStyle.Bold);
            }
            if (col == this.colTopScore && e.Value is int score)
            {
                e.CellStyle.ForeColor = score >= 100
                    ? UiTheme.SeverityCritical
                    : (score >= 60 ? UiTheme.SeverityHigh : UiTheme.TextPrimary);
            }
        }

        private void ShowError(string title, Exception ex = null)
        {
            string text = ex == null ? title : title + ":\n" + ex.Message;
            MessageBox.Show(text, "Сканер рисков", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetStatus(title);
        }

        private void SetStatus(string text)
        {
            this.lblStatus.Text = text;
        }

        private static DateTime? TryReadMaxPublicationDate(string dbPath)
        {
            try
            {
                using (var conn = new System.Data.SQLite.SQLiteConnection(
                    new System.Data.SQLite.SQLiteConnectionStringBuilder
                    {
                        DataSource = dbPath,
                        ForeignKeys = true,
                        BusyTimeout = 10000,
                    }.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT MAX(publication_date) FROM vulnerabilities";
                        var raw = cmd.ExecuteScalar();
                        if (raw == null || raw == DBNull.Value) return null;
                        DateTime dt;
                        return DateTime.TryParse(raw.ToString(), out dt) ? dt : (DateTime?)null;
                    }
                }
            }
            catch { return null; }
        }

        private static int CountVulnsSince(string dbPath, DateTime since)
        {
            try
            {
                using (var conn = new System.Data.SQLite.SQLiteConnection(
                    new System.Data.SQLite.SQLiteConnectionStringBuilder
                    {
                        DataSource = dbPath,
                        ForeignKeys = true,
                        BusyTimeout = 10000,
                    }.ConnectionString))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(1) FROM vulnerabilities WHERE publication_date > @d";
                        cmd.Parameters.AddWithValue("@d", since.ToString("o"));
                        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
                    }
                }
            }
            catch { return 0; }
        }
    }

    // Маленькое модальное окошко для Add/Edit записи в user_software.
    // Сделано в коде специально: это диалог из трёх контролов, под Designer не вижу смысла.
    internal sealed class SoftwareEditorDialog : Form
    {
        private readonly UserSoftware _item;
        private readonly TextBox _txtName;
        private readonly TextBox _txtVersion;
        private readonly CheckBox _chkCritical;

        public SoftwareEditorDialog(UserSoftware item)
        {
            _item = item;
            Text = item.Id == 0 ? "Добавить ПО" : "Изменить ПО";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 200);
            Font = new Font("Segoe UI", 9.5F);

            var lblName = new Label { Text = "Название ПО:", Left = 16, Top = 18, Width = 120, AutoSize = true };
            _txtName = new TextBox { Left = 140, Top = 14, Width = 260, Text = item.SoftwareName ?? string.Empty };

            var lblVer = new Label { Text = "Версия:", Left = 16, Top = 56, Width = 120, AutoSize = true };
            _txtVersion = new TextBox { Left = 140, Top = 52, Width = 260, Text = item.Version ?? string.Empty };

            _chkCritical = new CheckBox
            {
                Left = 140, Top = 88, Width = 260,
                Text = "Критично для меня",
                Checked = item.IsCritical,
                AutoSize = true,
            };

            var ok = new Button { Text = "OK", Left = 220, Top = 140, Width = 80, Height = 30, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Отмена", Left = 310, Top = 140, Width = 90, Height = 30, DialogResult = DialogResult.Cancel };
            ok.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtName.Text))
                {
                    MessageBox.Show(this, "Введите название ПО", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                _item.SoftwareName = _txtName.Text.Trim();
                _item.Version = string.IsNullOrWhiteSpace(_txtVersion.Text) ? null : _txtVersion.Text.Trim();
                _item.IsCritical = _chkCritical.Checked;
            };

            Controls.AddRange(new Control[] { lblName, _txtName, lblVer, _txtVersion, _chkCritical, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;

            UiTheme.Apply(this);
        }
    }
}
