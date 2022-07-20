using Github2Telegram.Model;
using MySql.Data.MySqlClient;

namespace Github2Telegram.Dao
{
    public class TelegramChatDao : IDisposable
    {
        private MySqlConnection Connection { get; set; }

        public TelegramChatDao()
        {
            MySqlConnection connection = new(Database.ConnectionString);
            connection.OpenAsync().GetAwaiter().GetResult();
            this.Connection = connection;
            using var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `tbl_telegram_chat`  (
  `id` bigint NOT NULL DEFAULT 0,
  `name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `role` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`name`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic", Connection);
            cmd.ExecuteNonQuery();
        }

        public List<TelegramChat> SelectAll()
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_telegram_chat ORDER BY created_at", Connection);
            using var reader = cmd.ExecuteReader();
            List<TelegramChat> list = new();
            while (reader.Read())
            {
                list.Add(new TelegramChat
                {
                    Id = (long)reader["id"],
                    Name = (string)reader["name"],
                    Role = reader["role"] is DBNull ? null : (string)reader["role"],
                    CreatedAt = (DateTime)reader["created_at"]
                });
            }
            return list;
        }

        public TelegramChat? SelectById(long id)
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_telegram_chat WHERE id=@id", Connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Prepare();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new TelegramChat
                {
                    Id = (long)reader["id"],
                    Name = (string)reader["name"],
                    Role = (string)reader["role"],
                    CreatedAt = (DateTime)reader["created_at"]
                };
            }
            return null;
        }

        public TelegramChat? SelectByName(string name)
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_telegram_chat WHERE name=@name", Connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Prepare();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new TelegramChat
                {
                    Id = (long)reader["id"],
                    Name = (string)reader["name"],
                    Role = (string)reader["role"],
                    CreatedAt = (DateTime)reader["created_at"]
                };
            }
            return null;
        }

        public List<TelegramChat> SelectByRole(string role)
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_telegram_chat WHERE role=@role ORDER BY created_at", Connection);
            cmd.Parameters.AddWithValue("@role", role);
            cmd.Prepare();
            using var reader = cmd.ExecuteReader();
            List<TelegramChat> list = new();
            while (reader.Read())
            {
                list.Add(new TelegramChat
                {
                    Id = (long)reader["id"],
                    Name = (string)reader["name"],
                    Role = reader["role"] is DBNull ? null : (string)reader["role"],
                    CreatedAt = (DateTime)reader["created_at"]
                });
            }
            return list;
        }

        public int Insert(TelegramChat chat)
        {
            using var cmd = new MySqlCommand($"INSERT INTO tbl_telegram_chat(id, name, role) VALUES(@id, @name, @role)", Connection);
            cmd.Parameters.AddWithValue("@id", chat.Id);
            cmd.Parameters.AddWithValue("@name", chat.Name);
            cmd.Parameters.AddWithValue("@role", chat.Role);
            cmd.Prepare();
            return cmd.ExecuteNonQuery();
        }

        public int UpdateId(string name, long id)
        {
            using var cmd = new MySqlCommand($"UPDATE tbl_telegram_chat SET id=@id WHERE name=@name", Connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Prepare();
            return cmd.ExecuteNonQuery();
        }

        public int UpdateName(long id, string name)
        {
            using var cmd = new MySqlCommand($"UPDATE tbl_telegram_chat SET name=@name WHERE id=@id", Connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Prepare();
            return cmd.ExecuteNonQuery();
        }

        public int DeleteById(long id)
        {
            using var cmd = new MySqlCommand($"DELETE FROM tbl_telegram_chat WHERE id=@id", Connection);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Prepare();
            return cmd.ExecuteNonQuery();
        }

        public int DeleteByName(string name)
        {
            using var cmd = new MySqlCommand($"DELETE FROM tbl_telegram_chat WHERE name=@name", Connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Prepare();
            return cmd.ExecuteNonQuery();
        }

        public void Dispose()
        {
            Connection?.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
