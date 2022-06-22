using Github2Telegram.Model;
using MySql.Data.MySqlClient;

namespace Github2Telegram.Dao
{
    public class TelegramAccountDao
    {
        private MySqlConnection Connection { get; set; }

        public TelegramAccountDao(MySqlConnection connection)
        {
            this.Connection = connection;
            using var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `tbl_telegram_account`  (
  `name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `role` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`name`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic", Connection);
            cmd.ExecuteNonQuery();
        }

        public List<TelegramAccount> SelectAll()
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_telegram_account ORDER BY created_at", Connection);
            using var reader = cmd.ExecuteReader();
            List<TelegramAccount> list = new();
            while (reader.Read())
            {
                list.Add(new TelegramAccount
                {
                    Name = (string)reader["name"],
                    Role = reader["role"] is DBNull ? null : (string)reader["role"],
                    CreatedAt = (DateTime)reader["created_at"]
                });
            }
            return list;
        }

        public TelegramAccount? SelectByName(string name)
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_telegram_account WHERE name=@name", Connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Prepare();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new TelegramAccount
                {
                    Name = (string)reader["name"],
                    Role = (string)reader["role"],
                    CreatedAt = (DateTime)reader["created_at"]
                };
            }
            return null;
        }

        public List<TelegramAccount> SelectByRole(string role)
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_telegram_account WHERE role=@role ORDER BY created_at", Connection);
            cmd.Parameters.AddWithValue("@role", role);
            cmd.Prepare();
            using var reader = cmd.ExecuteReader();
            List<TelegramAccount> list = new();
            while (reader.Read())
            {
                list.Add(new TelegramAccount
                {
                    Name = (string)reader["name"],
                    Role = reader["role"] is DBNull ? null : (string)reader["role"],
                    CreatedAt = (DateTime)reader["created_at"]
                });
            }
            return list;
        }

        public int Insert(string name, string role)
        {
            using var cmd = new MySqlCommand($"INSERT INTO tbl_telegram_account(name, role) VALUES(@name, @role)", Connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@role", role);
            cmd.Prepare();
            return cmd.ExecuteNonQuery();
        }

        public int Delete(string name)
        {
            using var cmd = new MySqlCommand($"DELETE FROM tbl_telegram_account WHERE name=@name", Connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Prepare();
            return cmd.ExecuteNonQuery();
        }
    }
}
