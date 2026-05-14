using System;
using System.Drawing;
using System.Windows.Forms;
using Vullnerability.Services;
using Vullnerability.Utils;

namespace Vullnerability.Forms
{
    // Стартовое окно. Логин/регистрация. После успешного входа открывается MainForm.
    // Пользователи хранятся в той же sqlite-БД (таблица users), пароли — соль + SHA-256.
    public partial class LoginForm : Form
    {
        // Гейт «не дать зарегистрироваться кому попало». Это НЕ безопасность —
        // строка лежит в exe в plain-tex; задача — отсекать случайных пользователей.
        private const string RegistrationMasterPassword = "Qwerty123";
        // Имя залогиненного пользователя — MainForm может вывести его в заголовке.
        public string LoggedInUserName { get; private set; }

        public LoginForm()
        {
            InitializeComponent();
            this.AcceptButton = btnLogin;
            txtUsername.KeyDown += LoginField_KeyDown;
            txtPassword.KeyDown += LoginField_KeyDown;
        }

        // Enter в любом поле = «Войти». Дефолтная кнопка AcceptButton тоже работает,
        // но людям удобнее когда после ввода пароля сразу логинит.
        private void LoginField_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                btnLogin_Click(btnLogin, EventArgs.Empty);
            }
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            var username = (txtUsername.Text ?? string.Empty).Trim();
            var password = txtPassword.Text ?? string.Empty;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Введите имя пользователя и пароль");
                return;
            }

            try
            {
                if (UserStore.VerifyCredentials(username, password))
                {
                    LoggedInUserName = username;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    ShowError("Неверное имя пользователя или пароль");
                    txtPassword.SelectAll();
                    txtPassword.Focus();
                }
            }
            catch (Exception ex)
            {
                ShowError("Не удалось проверить пользователя:\n" + ex.Message);
            }
        }

        private void btnRegister_Click(object sender, EventArgs e)
        {
            var username = (txtUsername.Text ?? string.Empty).Trim();
            var password = txtPassword.Text ?? string.Empty;

            if (string.IsNullOrEmpty(username) || password.Length < 4)
            {
                ShowError("Введите имя пользователя и пароль (минимум 4 символа)");
                return;
            }

            // Мастер-пароль на регистрацию — спрашиваем ДО любых обращений в БД.
            string master = PromptMasterPassword();
            if (master == null) return;                       // отмена — молча
            if (master != RegistrationMasterPassword)
            {
                ShowError("Неверный мастер-пароль для регистрации");
                return;
            }

            try
            {
                if (UserStore.UserExists(username))
                {
                    ShowError("Пользователь с таким именем уже существует");
                    return;
                }

                UserStore.CreateUser(username, password);
                lblStatus.ForeColor = UiTheme.TextPrimary;
                lblStatus.Text = "Пользователь создан, можно войти";
            }
            catch (Exception ex)
            {
                ShowError("Не удалось создать пользователя:\n" + ex.Message);
            }
        }

        private void ShowError(string text)
        {
            lblStatus.ForeColor = UiTheme.SeverityCritical;
            lblStatus.Text = text;
        }

        // Маленький модальный диалог: одно поле для мастер-пароля + ОК/Отмена.
        // Возвращает введённую строку или null, если отмена/закрытие.
        private string PromptMasterPassword()
        {
            using (var dlg = new Form())
            {
                dlg.Text = "Регистрация";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MinimizeBox = false;
                dlg.MaximizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.ClientSize = new Size(360, 150);
                dlg.BackColor = UiTheme.Background;
                dlg.Font = new Font("Segoe UI", 11F);

                var lbl = new Label
                {
                    Text = "Введите мастер-пароль для регистрации:",
                    AutoSize = true,
                    Location = new Point(16, 16),
                    ForeColor = UiTheme.TextPrimary
                };
                dlg.Controls.Add(lbl);

                var txt = new TextBox
                {
                    UseSystemPasswordChar = true,
                    Location = new Point(16, 48),
                    Size = new Size(328, 28),
                    BackColor = Color.FromArgb(230, 243, 255),
                    ForeColor = UiTheme.TextPrimary,
                    BorderStyle = BorderStyle.FixedSingle
                };
                dlg.Controls.Add(txt);

                var btnOk = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(168, 96),
                    Size = new Size(82, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = UiTheme.Accent,
                    ForeColor = Color.White
                };
                btnOk.FlatAppearance.BorderSize = 0;
                dlg.Controls.Add(btnOk);

                var btnCancel = new Button
                {
                    Text = "Отмена",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(262, 96),
                    Size = new Size(82, 32),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = UiTheme.Surface2,
                    ForeColor = UiTheme.TextPrimary
                };
                btnCancel.FlatAppearance.BorderColor = UiTheme.Border;
                dlg.Controls.Add(btnCancel);

                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;
                txt.Focus();

                return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text : null;
            }
        }
    }
}
