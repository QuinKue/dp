using System;
using System.Data.SQLite;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Vullnerability.db;

namespace Vullnerability
{
    // Стартовое окно. Логин/регистрация. После успешного входа открывается Form1.
    // Пользователи хранятся в той же sqlite-БД (таблица users), пароли — соль + SHA-256.
    public partial class LoginForm : Form
    {
        // Имя залогиненного пользователя — Form1 может вывести его в заголовке.
        public string LoggedInUserName { get; private set; }

        public LoginForm()
        {
            InitializeComponent();
            UiTheme.Apply(this);

            // Enter в полях = клик «Войти»
            this.AcceptButton = btnLogin;

            txtUsername.KeyDown += LoginField_KeyDown;
            txtPassword.KeyDown += LoginField_KeyDown;
        }

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

    // Работа с таблицей users. Никаких EF — обычный System.Data.SQLite,
    // чтобы не дёргать DbContext до подтверждения логина.
    internal static class UserStore
    {
        // SHA-256(salt || password). Соль — 16 случайных байт, хранится в БД base64.
        private const int SaltSize = 16;
        // Дефолтный администратор, чтобы сразу можно было войти на свежей БД.
        public const string DefaultAdminLogin = "admin";
        public const string DefaultAdminPassword = "admin";

        private static string GetConnectionString()
        {
            string dbPath = SqliteBootstrap.GetDbPath();
            return new SQLiteConnectionStringBuilder
            {
                DataSource = dbPath,
                ForeignKeys = true,
                BusyTimeout = 10000,
            }.ConnectionString;
        }

        private static SQLiteConnection OpenConnection()
        {
            var conn = new SQLiteConnection(GetConnectionString());
            conn.Open();
            using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = "PRAGMA busy_timeout=10000;";
                pragma.ExecuteNonQuery();
            }
            return conn;
        }

        public static bool UserExists(string username)
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(1) FROM users WHERE username = @u";
                cmd.Parameters.AddWithValue("@u", username);
                long n = Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
                return n > 0;
            }
        }

        public static void CreateUser(string username, string password)
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = new RNGCryptoServiceProvider())
                rng.GetBytes(salt);
            string saltB64 = Convert.ToBase64String(salt);
            string hashB64 = ComputeHash(salt, password);

            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO users(username, password_hash, password_salt, created_at) " +
                    "VALUES(@u, @h, @s, @c)";
                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@h", hashB64);
                cmd.Parameters.AddWithValue("@s", saltB64);
                cmd.Parameters.AddWithValue("@c", DateTime.UtcNow.ToString("o"));
                cmd.ExecuteNonQuery();
            }
        }

        public static bool VerifyCredentials(string username, string password)
        {
            using (var conn = OpenConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT password_hash, password_salt FROM users WHERE username = @u LIMIT 1";
                cmd.Parameters.AddWithValue("@u", username);

                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read()) return false;

                    string hashB64 = r.GetString(0);
                    string saltB64 = r.GetString(1);
                    byte[] salt = Convert.FromBase64String(saltB64);
                    string computed = ComputeHash(salt, password);

                    return ConstantTimeEquals(computed, hashB64);
                }
            }
        }

        // Гарантирует, что в БД есть хотя бы один пользователь (admin/admin).
        // Вызывается из SqliteBootstrap после применения схемы.
        public static void EnsureDefaultUser()
        {
            try
            {
                if (UserExists(DefaultAdminLogin)) return;
                CreateUser(DefaultAdminLogin, DefaultAdminPassword);
            }
            catch
            {
                // если БД ещё не готова — не валим запуск приложения
            }
        }

        private static string ComputeHash(byte[] salt, string password)
        {
            byte[] pwd = Encoding.UTF8.GetBytes(password);
            byte[] buf = new byte[salt.Length + pwd.Length];
            Buffer.BlockCopy(salt, 0, buf, 0, salt.Length);
            Buffer.BlockCopy(pwd, 0, buf, salt.Length, pwd.Length);
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(buf));
        }

        // Сравнение строк постоянного времени, чтобы не было утечек по таймингу.
        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
