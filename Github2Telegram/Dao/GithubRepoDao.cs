using Github2Telegram.Model;
using MySql.Data.MySqlClient;

namespace Github2Telegram.Dao
{
    public class GithubRepoDao : IDisposable
    {
        private MySqlConnection Connection { get; set; }

        public GithubRepoDao()
        {
            MySqlConnection connection = new(Database.ConnectionString);
            connection.OpenAsync().GetAwaiter().GetResult();
            this.Connection = connection;
            using var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `tbl_github_repo`  (
  `account` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `added_by` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`account`, `name`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic", Connection);
            cmd.ExecuteNonQuery();
        }

        public List<GithubRepo> SelectAll()
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_github_repo ORDER BY account,name", Connection);
            using var reader = cmd.ExecuteReader();
            List<GithubRepo> list = new();
            while (reader.Read())
            {
                list.Add(new GithubRepo
                {
                    Account = (string)reader["account"],
                    Name = (string)reader["name"],
                    AddedBy = reader["added_by"] is DBNull ? null : (string)reader["added_by"],
                    CreatedAt = (DateTime)reader["created_at"]
                });
            }
            return list;
        }

        public GithubRepo? Select(string account, string name)
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_github_repo WHERE account=@account AND name=@name", Connection);
            cmd.Parameters.AddWithValue("@account", account);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Prepare();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new GithubRepo
                {
                    Account = (string)reader["account"],
                    Name = (string)reader["name"],
                    AddedBy = reader["added_by"] is System.DBNull ? null : (string)reader["added_by"],
                    CreatedAt = (DateTime)reader["created_at"]
                };
            }
            return null;
        }

        public int Insert(GithubRepo m)
        {
            using var cmd = new MySqlCommand($"INSERT INTO tbl_github_repo(account, name, added_by) VALUES(@account, @name, @added_by)", Connection);
            cmd.Parameters.AddWithValue("@account", m.Account);
            cmd.Parameters.AddWithValue("@name", m.Name);
            cmd.Parameters.AddWithValue("@added_by", m.AddedBy);
            cmd.Prepare();
            return cmd.ExecuteNonQuery();
        }

        public int Delete(string account, string name)
        {
            using var cmd = new MySqlCommand($"DELETE FROM tbl_github_repo WHERE account=@account AND name=@name", Connection);
            cmd.Parameters.AddWithValue("@account", account);
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
