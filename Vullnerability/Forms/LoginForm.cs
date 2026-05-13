using System;
using System.Windows.Forms;
using Vullnerability.Services;
using Vullnerability.Utils;

namespace Vullnerability.Forms
{
    // Стартовое окно. Логин/регистрация. После успешного входа открывается MainForm.
    // Пользователи хранятся в той же sqlite-БД (таблица users), пароли — соль + SHA-256.
    public partial class LoginForm : Form
    {
        // Имя залогиненного пользователя — MainForm может вывести его в заголовке.
        public string LoggedInUserName { get; private set; }

        public LoginForm()
        {
            InitializeComponent();
            UiTheme.Apply(this);
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
    }
}
