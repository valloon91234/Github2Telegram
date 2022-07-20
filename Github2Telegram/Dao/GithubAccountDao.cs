using Github2Telegram.Model;
using MySql.Data.MySqlClient;

namespace Github2Telegram.Dao
{
    public class GithubAccountDao : IDisposable
    {
        private MySqlConnection Connection { get; set; }

        public GithubAccountDao()
        {
            MySqlConnection connection = new(Database.ConnectionString);
            connection.OpenAsync().GetAwaiter().GetResult();
            this.Connection = connection;
            using var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `tbl_github_account`  (
  `name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `token` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`name`) USING BTREE,
  UNIQUE INDEX `idx_github_account_token`(`token` ASC) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic", Connection);
            cmd.ExecuteNonQuery();
        }

        public List<GithubAccount> SelectAll()
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_github_account ORDER BY created_at", Connection);
            using var reader = cmd.ExecuteReader();
            List<GithubAccount> list = new();
            while (reader.Read())
            {
                list.Add(new GithubAccount
                {
                    Name = (string)reader["name"],
                    Token = (string)reader["token"],
                    CreatedAt = (DateTime)reader["created_at"]
                });
            }
            return list;
        }

        public GithubAccount? SelectByName(string name)
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_github_account WHERE name=@name", Connection);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Prepare();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new GithubAccount
                {
                    Name = (string)reader["name"],
                    Token = (string)reader["token"],
                    CreatedAt = (DateTime)reader["created_at"]
                };
            }
            return null;
        }

        public GithubAccount? SelectByToken(string token)
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_github_account WHERE token=@token", Connection);
            cmd.Parameters.AddWithValue("@token", token);
            cmd.Prepare();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new GithubAccount
                {
                    Name = (string)reader["name"],
                    Token = (string)reader["token"],
                    CreatedAt = (DateTime)reader["created_at"]
                };
            }
            return null;
        }

        public int Insert(GithubAccount m)
        {
            using var cmd = new MySqlCommand($"INSERT INTO tbl_github_account(name, token) VALUES(@name, @token)", Connection);
            cmd.Parameters.AddWithValue("@name", m.Name);
            cmd.Parameters.AddWithValue("@token", m.Token);
            cmd.Prepare();
            return cmd.ExecuteNonQuery();
        }

        public int Delete(string name)
        {
            using var cmd = new MySqlCommand($"DELETE FROM tbl_github_account WHERE name=@name", Connection);
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
