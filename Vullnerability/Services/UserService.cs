using System;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Text;
using Vullnerability.Data;

namespace Vullnerability.Services
{
    // Работа с таблицей users. Никаких EF — обычный System.Data.SQLite,
    // чтобы не дёргать DbContext до подтверждения логина.
    public static class UserStore
    {
        // SHA-256(salt || password). Соль — 16 случайных байт, в БД хранится в base64.
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
