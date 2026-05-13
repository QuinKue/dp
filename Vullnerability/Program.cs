using System;
using System.Threading;
using System.Windows.Forms;
using Vullnerability.Data;
using Vullnerability.Forms;

namespace Vullnerability
{
    internal static class Program
    {
        // Имя должно быть уникальным на машине, поэтому добавил суффикс — иначе на
        // колледжной машине другой проект с похожим Mutex'ом может с нами столкнуться.
        private const string SingleInstanceMutexName = "Global\\Vullnerability.SingleInstance.1B7A3";

        [STAThread]
        static void Main()
        {
            // Защита от второго экземпляра: два процесса на одной БД были одной из
            // причин "database is locked".
            bool createdNew;
            using (var mutex = new Mutex(true, SingleInstanceMutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "Программа уже запущена.\nЗакрой предыдущее окно и попробуй ещё раз.",
                        "Vullnerability",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // создаём БД при первом запуске, чтобы EF потом не упал
                try
                {
                    SqliteBootstrap.EnsureDatabase();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Не удалось подготовить базу данных:\n" + ex.Message,
                        "Vullnerability",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                // 1) Окно авторизации
                using (var login = new LoginForm())
                {
                    var result = login.ShowDialog();
                    if (result != DialogResult.OK) return;

                    // 2) Основное окно — запускается только после успешного входа
                    Application.Run(new MainForm(login.LoggedInUserName));
                }
            }
        }
    }
}
