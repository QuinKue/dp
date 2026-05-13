using System;

namespace Vullnerability.Models
{
    // Запись из таблицы user_software. Это «ПО пользователя», которое
    // сканер сопоставляет с уязвимостями из БДУ.
    public class UserSoftware
    {
        public int Id { get; set; }
        public string SoftwareName { get; set; }
        public string Version { get; set; }
        public bool IsCritical { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
