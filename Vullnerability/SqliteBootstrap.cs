using System;
using System.Data.SQLite;
using System.IO;
using System.Reflection;
using System.Text;

namespace Vullnerability.db
{
    // Создаёт файл БД при первом запуске и накатывает на него схему
    // из 01_schema.sqlite.sql. Должно вызываться до того, как EF откроет контекст.
    //
    // Защита от ошибки "database is locked" (актуально для колледжа):
    //   * BusyTimeout=10000 в connection string + PRAGMA busy_timeout=10000;
    //   * fallback-каталоги, если %LOCALAPPDATA% оказался не writable;
    //   * journal_mode=TRUNCATE при ошибке открытия WAL — WAL ломается на
    //     сетевых SMB-шарках и при включённом «контролируемом доступе к папкам»;
    //   * единый writable путь к .sqlite экспортируется через GetDbPath().
    public static class SqliteBootstrap
    {
        private const string DbFileName = "vulndb.sqlite";
        private const string SchemaFileName = "01_schema.sqlite.sql";

        private static string _resolvedDbPath;

        // Возвращает полный путь к .sqlite (вдруг пригодится показать в UI).
        // Также используется UserStore для отдельного подключения к users.
        public static string EnsureDatabase()
        {
            string dbDir = ResolveWritableDbDirectory();
            string dbPath = Path.Combine(dbDir, DbFileName);

            // подставится в |DataDirectory| из App.config
            AppDomain.CurrentDomain.SetData("DataDirectory", dbDir);
            _resolvedDbPath = dbPath;

            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);

                string schemaSql = LoadSchemaScript();
                ExecuteSchemaOnFreshDb(dbPath, schemaSql);
            }
            else
            {
                EnsureSchemaUpToDate(dbPath);
            }

            // На случай старых БД, где ещё не было таблицы users — заводим её и
            // дефолтного админа. Делать это надо после applied schema, иначе
            // CREATE TABLE может упасть из-за активной транзакции.
            EnsureUsersTable(dbPath);

            return dbPath;
        }

        public static string GetDbPath()
        {
            if (string.IsNullOrEmpty(_resolvedDbPath))
                EnsureDatabase();
            return _resolvedDbPath;
        }

        // Перебираем кандидаты, пока не найдём первый writable каталог.
        // На колледжных машинах %LOCALAPPDATA% может быть на сетевом профиле
        // или закрыт политикой ⇒ падает CreateFile / WAL.
        private static string ResolveWritableDbDirectory()
        {
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VulnDb"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "VulnDb"),
                Path.Combine(Path.GetTempPath(), "VulnDb"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"),
            };

            foreach (string dir in candidates)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                if (TryEnsureWritable(dir))
                    return dir;
            }

            // Если все пути не writable — последний шанс, рабочая директория.
            string fallback = Path.Combine(Directory.GetCurrentDirectory(), "VulnDb");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        private static bool TryEnsureWritable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write_probe");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string LoadSchemaScript()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.Combine(baseDir, SchemaFileName);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Не найден файл схемы '{SchemaFileName}' в каталоге '{baseDir}'. " +
                    "Проверь, что у файла стоит 'Copy to Output Directory: Copy if newer'.",
                    path);
            }

            return File.ReadAllText(path, Encoding.UTF8);
        }

        private static void ExecuteSchemaOnFreshDb(string dbPath, string schemaSql)
        {
            using (var conn = new SQLiteConnection(BuildConnectionString(dbPath)))
            {
                conn.Open();
                ApplyPragmas(conn);

                // весь скрипт схемы накатываем одной транзакцией
                using (var tx = conn.BeginTransaction())
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = schemaSql;
                    cmd.ExecuteNonQuery();
                    tx.Commit();
                }
            }
        }

        private static void EnsureSchemaUpToDate(string dbPath)
        {
            using (var conn = new SQLiteConnection(BuildConnectionString(dbPath)))
            {
                conn.Open();
                ApplyPragmas(conn);
            }
        }

        // CREATE TABLE IF NOT EXISTS users — на случай старых баз без таблицы.
        private static void EnsureUsersTable(string dbPath)
        {
            try
            {
                using (var conn = new SQLiteConnection(BuildConnectionString(dbPath)))
                {
                    conn.Open();
                    ApplyPragmas(conn);

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS users (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    username      TEXT    NOT NULL,
    password_hash TEXT    NOT NULL,
    password_salt TEXT    NOT NULL,
    created_at    TEXT    NOT NULL,
    CONSTRAINT UQ_users_username UNIQUE (username)
);";
                        cmd.ExecuteNonQuery();
                    }
                }

                // Поднимаем дефолтного admin/admin, если пользователей нет.
                Vullnerability.UserStore.EnsureDefaultUser();
            }
            catch
            {
                // не падаем — пусть форма логина сама покажет ошибку,
                // если ничего не получится.
            }
        }

        private static void ApplyPragmas(SQLiteConnection conn)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "PRAGMA foreign_keys = ON;" +
                    "PRAGMA busy_timeout = 10000;";
                cmd.ExecuteNonQuery();
            }
        }

        private static string BuildConnectionString(string dbPath)
        {
            return new SQLiteConnectionStringBuilder
            {
                DataSource = dbPath,
                ForeignKeys = true,
                // TRUNCATE вместо WAL: WAL создаёт -wal и -shm файлы и не работает
                // на SMB / при «контролируемом доступе к папкам». TRUNCATE медленнее,
                // но переживает любую файловую систему — это и нужно на колледжной
                // машине, где постоянно ловили "database is locked".
                JournalMode = SQLiteJournalModeEnum.Truncate,
                SyncMode = SynchronizationModes.Normal,
                BusyTimeout = 10000,
            }.ConnectionString;
        }
    }
}
