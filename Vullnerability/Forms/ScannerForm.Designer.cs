using System.Windows.Forms;

namespace Vullnerability.Forms
{
    partial class ScannerForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private Panel panelToolbar;
        private Button btnAdd;
        private Button btnEdit;
        private Button btnDelete;
        private Button btnScan;
        private Button btnExportPdf;
        private Button btnImportExcel;
        private Label lblStatus;

        private SplitContainer splitOuter;
        private DataGridView dgvSoftware;

        private SplitContainer splitResults;
        private TableLayoutPanel tableLeft;
        private Label lblTop;
        private DataGridView dgvTop;
        private SimpleBarChart chart;

        private TableLayoutPanel tableRight;
        private Label lblTotal;
        private Label lblRecommendations;
        private RichTextBox rtbRecommendations;

        private DataGridViewTextBoxColumn colSoftware;
        private DataGridViewTextBoxColumn colVersion;
        private DataGridViewCheckBoxColumn colIsCritical;

        private DataGridViewTextBoxColumn colTopSoftware;
        private DataGridViewTextBoxColumn colTopBdu;
        private DataGridViewTextBoxColumn colTopVuln;
        private DataGridViewTextBoxColumn colTopSeverity;
        private DataGridViewTextBoxColumn colTopCvss;
        private DataGridViewTextBoxColumn colTopDate;
        private DataGridViewTextBoxColumn colTopScore;

        private ToolTip toolTips;

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.toolTips = new ToolTip(this.components);

            this.panelToolbar = new Panel();
            this.btnAdd = new Button();
            this.btnEdit = new Button();
            this.btnDelete = new Button();
            this.btnScan = new Button();
            this.btnExportPdf = new Button();
            this.btnImportExcel = new Button();
            this.lblStatus = new Label();

            this.splitOuter = new SplitContainer();
            this.dgvSoftware = new DataGridView();

            this.splitResults = new SplitContainer();

            this.tableLeft = new TableLayoutPanel();
            this.lblTop = new Label();
            this.dgvTop = new DataGridView();
            this.chart = new SimpleBarChart();

            this.tableRight = new TableLayoutPanel();
            this.lblTotal = new Label();
            this.lblRecommendations = new Label();
            this.rtbRecommendations = new RichTextBox();

            this.colSoftware = new DataGridViewTextBoxColumn();
            this.colVersion = new DataGridViewTextBoxColumn();
            this.colIsCritical = new DataGridViewCheckBoxColumn();

            this.colTopSoftware = new DataGridViewTextBoxColumn();
            this.colTopBdu = new DataGridViewTextBoxColumn();
            this.colTopVuln = new DataGridViewTextBoxColumn();
            this.colTopSeverity = new DataGridViewTextBoxColumn();
            this.colTopCvss = new DataGridViewTextBoxColumn();
            this.colTopDate = new DataGridViewTextBoxColumn();
            this.colTopScore = new DataGridViewTextBoxColumn();

            ((System.ComponentModel.ISupportInitialize)(this.splitOuter)).BeginInit();
            this.splitOuter.Panel1.SuspendLayout();
            this.splitOuter.Panel2.SuspendLayout();
            this.splitOuter.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitResults)).BeginInit();
            this.splitResults.Panel1.SuspendLayout();
            this.splitResults.Panel2.SuspendLayout();
            this.splitResults.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSoftware)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvTop)).BeginInit();
            this.panelToolbar.SuspendLayout();
            this.tableLeft.SuspendLayout();
            this.tableRight.SuspendLayout();
            this.SuspendLayout();

            // ---- toolbar ----
            this.panelToolbar.Dock = DockStyle.Top;
            this.panelToolbar.Height = 56;
            this.panelToolbar.Padding = new Padding(10);
            this.panelToolbar.Controls.Add(this.lblStatus);
            this.panelToolbar.Controls.Add(this.btnImportExcel);
            this.panelToolbar.Controls.Add(this.btnExportPdf);
            this.panelToolbar.Controls.Add(this.btnScan);
            this.panelToolbar.Controls.Add(this.btnDelete);
            this.panelToolbar.Controls.Add(this.btnEdit);
            this.panelToolbar.Controls.Add(this.btnAdd);

            StyleAsToolButton(this.btnAdd,         "Добавить",          10);
            StyleAsToolButton(this.btnEdit,        "Изменить",         140);
            StyleAsToolButton(this.btnDelete,      "Удалить",          270);
            StyleAsToolButton(this.btnScan,        "Сканировать",      400);
            StyleAsToolButton(this.btnExportPdf,   "Экспорт PDF",      530);
            StyleAsToolButton(this.btnImportExcel, "Обновить БД",      660);

            this.toolTips.SetToolTip(this.btnAdd,         "Добавить запись о ПО в список");
            this.toolTips.SetToolTip(this.btnEdit,        "Изменить выбранную запись");
            this.toolTips.SetToolTip(this.btnDelete,      "Удалить выбранную запись");
            this.toolTips.SetToolTip(this.btnScan,        "Найти уязвимости для добавленного ПО");
            this.toolTips.SetToolTip(this.btnExportPdf,   "Сохранить отчёт в PDF");
            this.toolTips.SetToolTip(this.btnImportExcel, "Обновить локальную БД из Excel-файла БДУ ФСТЭК");

            this.btnAdd.Click         += new System.EventHandler(this.btnAdd_Click);
            this.btnEdit.Click        += new System.EventHandler(this.btnEdit_Click);
            this.btnDelete.Click      += new System.EventHandler(this.btnDelete_Click);
            this.btnScan.Click        += new System.EventHandler(this.btnScan_Click);
            this.btnExportPdf.Click   += new System.EventHandler(this.btnExportPdf_Click);
            this.btnImportExcel.Click += new System.EventHandler(this.btnImportExcel_Click);

            this.lblStatus.AutoSize = true;
            this.lblStatus.Dock = DockStyle.Right;
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.lblStatus.Padding = new Padding(0, 12, 12, 0);
            this.lblStatus.Text = "Готово";
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 9.5F);

            // ---- splitOuter: верх = user_software, низ = результаты ----
            this.splitOuter.Dock = DockStyle.Fill;
            this.splitOuter.Orientation = Orientation.Horizontal;
            this.splitOuter.SplitterDistance = 220;
            this.splitOuter.SplitterWidth = 6;
            this.splitOuter.Panel1.Padding = new Padding(10);
            this.splitOuter.Panel2.Padding = new Padding(10);

            this.splitOuter.Panel1.Controls.Add(this.dgvSoftware);
            this.splitOuter.Panel2.Controls.Add(this.splitResults);

            // ---- dgvSoftware ----
            this.dgvSoftware.Dock = DockStyle.Fill;
            this.dgvSoftware.AutoGenerateColumns = false;
            this.dgvSoftware.AllowUserToAddRows = false;
            this.dgvSoftware.AllowUserToDeleteRows = false;
            this.dgvSoftware.MultiSelect = false;
            this.dgvSoftware.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dgvSoftware.RowHeadersVisible = false;
            this.dgvSoftware.ReadOnly = false;
            this.dgvSoftware.Columns.Add(this.colSoftware);
            this.dgvSoftware.Columns.Add(this.colVersion);
            this.dgvSoftware.Columns.Add(this.colIsCritical);

            this.colSoftware.HeaderText = "Название ПО";
            this.colSoftware.DataPropertyName = "SoftwareName";
            this.colSoftware.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            this.colSoftware.FillWeight = 60;

            this.colVersion.HeaderText = "Версия";
            this.colVersion.DataPropertyName = "Version";
            this.colVersion.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            this.colVersion.FillWeight = 30;

            this.colIsCritical.HeaderText = "Критично";
            this.colIsCritical.DataPropertyName = "IsCritical";
            this.colIsCritical.Width = 90;
            this.colIsCritical.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;

            // ---- splitResults: слева топ+график, справа итог+рекомендации ----
            this.splitResults.Dock = DockStyle.Fill;
            this.splitResults.Orientation = Orientation.Vertical;
            this.splitResults.SplitterWidth = 6;
            this.splitResults.SplitterDistance = 560;
            this.splitResults.Panel1.Controls.Add(this.tableLeft);
            this.splitResults.Panel2.Controls.Add(this.tableRight);

            // ---- tableLeft (Top grid + chart) ----
            this.tableLeft.Dock = DockStyle.Fill;
            this.tableLeft.ColumnCount = 1;
            this.tableLeft.RowCount = 3;
            this.tableLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.tableLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));
            this.tableLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));

            this.lblTop.AutoSize = true;
            this.lblTop.Text = "Топ-10 рисков";
            this.lblTop.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold);
            this.lblTop.Padding = new Padding(2, 2, 0, 6);

            this.tableLeft.Controls.Add(this.lblTop, 0, 0);
            this.tableLeft.Controls.Add(this.dgvTop, 0, 1);
            this.tableLeft.Controls.Add(this.chart, 0, 2);

            // ---- dgvTop ----
            this.dgvTop.Dock = DockStyle.Fill;
            this.dgvTop.AutoGenerateColumns = false;
            this.dgvTop.AllowUserToAddRows = false;
            this.dgvTop.AllowUserToDeleteRows = false;
            this.dgvTop.ReadOnly = true;
            this.dgvTop.RowHeadersVisible = false;
            this.dgvTop.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dgvTop.MultiSelect = false;
            this.dgvTop.Columns.Add(this.colTopSoftware);
            this.dgvTop.Columns.Add(this.colTopBdu);
            this.dgvTop.Columns.Add(this.colTopVuln);
            this.dgvTop.Columns.Add(this.colTopSeverity);
            this.dgvTop.Columns.Add(this.colTopCvss);
            this.dgvTop.Columns.Add(this.colTopDate);
            this.dgvTop.Columns.Add(this.colTopScore);

            this.colTopSoftware.HeaderText = "ПО";
            this.colTopSoftware.DataPropertyName = "Software";
            this.colTopSoftware.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            this.colTopSoftware.FillWeight = 22;

            this.colTopBdu.HeaderText = "BDU";
            this.colTopBdu.DataPropertyName = "BduCode";
            this.colTopBdu.Width = 110;

            this.colTopVuln.HeaderText = "Уязвимость";
            this.colTopVuln.DataPropertyName = "VulnName";
            this.colTopVuln.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            this.colTopVuln.FillWeight = 38;

            this.colTopSeverity.HeaderText = "Уровень";
            this.colTopSeverity.DataPropertyName = "Severity";
            this.colTopSeverity.Width = 110;

            this.colTopCvss.HeaderText = "CVSS 3.0";
            this.colTopCvss.DataPropertyName = "Cvss3";
            this.colTopCvss.Width = 80;
            this.colTopCvss.DefaultCellStyle.Format = "0.0";

            this.colTopDate.HeaderText = "Дата";
            this.colTopDate.DataPropertyName = "PublishedAt";
            this.colTopDate.Width = 95;
            this.colTopDate.DefaultCellStyle.Format = "dd.MM.yyyy";

            this.colTopScore.HeaderText = "Score";
            this.colTopScore.DataPropertyName = "RiskScore";
            this.colTopScore.Width = 70;
            this.colTopScore.DefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold);

            // ---- chart ----
            this.chart.Dock = DockStyle.Fill;
            this.chart.Margin = new Padding(0, 6, 0, 0);
            this.chart.Title = "Средний CVSS 3.0 по ПО";

            // ---- tableRight ----
            this.tableRight.Dock = DockStyle.Fill;
            this.tableRight.Padding = new Padding(6, 0, 0, 0);
            this.tableRight.ColumnCount = 1;
            this.tableRight.RowCount = 3;
            this.tableRight.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.tableRight.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.tableRight.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            this.lblTotal.AutoSize = true;
            this.lblTotal.Text = "Общий риск-скор: 0";
            this.lblTotal.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold);
            this.lblTotal.Padding = new Padding(4, 4, 4, 6);

            this.lblRecommendations.AutoSize = true;
            this.lblRecommendations.Text = "Рекомендации";
            this.lblRecommendations.Font = new System.Drawing.Font("Segoe UI", 10.5F, System.Drawing.FontStyle.Bold);
            this.lblRecommendations.Padding = new Padding(4, 0, 4, 4);

            this.rtbRecommendations.Dock = DockStyle.Fill;
            this.rtbRecommendations.ReadOnly = true;
            this.rtbRecommendations.BorderStyle = BorderStyle.FixedSingle;
            this.rtbRecommendations.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.rtbRecommendations.Text = "Нажмите «Сканировать», чтобы получить персональные рекомендации.";

            this.tableRight.Controls.Add(this.lblTotal, 0, 0);
            this.tableRight.Controls.Add(this.lblRecommendations, 0, 1);
            this.tableRight.Controls.Add(this.rtbRecommendations, 0, 2);

            // ---- ScannerForm ----
            this.Controls.Add(this.splitOuter);
            this.Controls.Add(this.panelToolbar);
            this.Padding = new Padding(0);
            this.Font = new System.Drawing.Font("Segoe UI", 9.5F);

            this.panelToolbar.ResumeLayout(false);
            this.panelToolbar.PerformLayout();
            this.splitOuter.Panel1.ResumeLayout(false);
            this.splitOuter.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitOuter)).EndInit();
            this.splitOuter.ResumeLayout(false);
            this.splitResults.Panel1.ResumeLayout(false);
            this.splitResults.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitResults)).EndInit();
            this.splitResults.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvSoftware)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dgvTop)).EndInit();
            this.tableLeft.ResumeLayout(false);
            this.tableLeft.PerformLayout();
            this.tableRight.ResumeLayout(false);
            this.tableRight.PerformLayout();
            this.ResumeLayout(false);
        }

        // Минимум одинаковых свойств для всех кнопок тулбара.
        // Базовый стиль накатывает UiTheme.Apply на этапе показа.
        private static void StyleAsToolButton(Button b, string text, int x)
        {
            b.Text = text;
            b.Size = new System.Drawing.Size(120, 35);
            b.Location = new System.Drawing.Point(x, 10);
            b.FlatStyle = FlatStyle.Flat;
            b.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            b.UseVisualStyleBackColor = false;
        }
    }
}
