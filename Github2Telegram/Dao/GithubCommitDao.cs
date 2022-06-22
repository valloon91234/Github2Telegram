using Github2Telegram.Model;
using MySql.Data.MySqlClient;

namespace Github2Telegram.Dao
{
    public class GithubCommitDao
    {
        private MySqlConnection Connection { get; set; }

        public GithubCommitDao(MySqlConnection connection)
        {
            this.Connection = connection;
            using var cmd = new MySqlCommand(@"CREATE TABLE IF NOT EXISTS `tbl_github_commit`  (
  `id` int NOT NULL AUTO_INCREMENT,
  `account` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `repo` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `sha` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `committer` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `url` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `committed_at` datetime NOT NULL,
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `idx_github_repo`(`account` ASC, `repo` ASC, `sha` ASC) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 407 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic", Connection);
            cmd.ExecuteNonQuery();
        }

        public GithubCommit? SelectLast(string account, string repo)
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_github_commit WHERE account=@account AND repo=@repo ORDER BY committed_at DESC LIMIT 1", Connection);
            cmd.Parameters.AddWithValue("@account", account);
            cmd.Parameters.AddWithValue("@repo", repo);
            cmd.Prepare();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new GithubCommit
                {
                    Id = (int)reader["id"],
                    Account = (string)reader["account"],
                    Repo = (string)reader["repo"],
                    Sha = (string)reader["sha"],
                    Committer = (string)reader["committer"],
                    Url = (string)reader["url"],
                    CommittedAt = (DateTime)reader["committed_at"],
                    CreatedAt = (DateTime)reader["created_at"]
                };
            }
            return null;
        }

        public List<GithubCommit> SelectLast(int offset, int limit)
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_github_commit ORDER BY committed_at DESC LIMIT @offset, @limit", Connection);
            cmd.Parameters.AddWithValue("@offset", offset);
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Prepare();
            using var reader = cmd.ExecuteReader();
            List<GithubCommit> list = new();
            while (reader.Read())
            {
                list.Add(new GithubCommit
                {
                    Id = (int)reader["id"],
                    Account = (string)reader["account"],
                    Repo = (string)reader["repo"],
                    Sha = (string)reader["sha"],
                    Committer = (string)reader["committer"],
                    Url = (string)reader["url"],
                    CommittedAt = (DateTime)reader["committed_at"],
                    CreatedAt = (DateTime)reader["created_at"]
                });
            }
            return list;
        }

        public List<GithubCommit> SelectLast(string account, string repo, int offset, int limit)
        {
            using var cmd = new MySqlCommand($"SELECT * FROM tbl_github_commit WHERE account=@account AND repo=@repo ORDER BY committed_at DESC LIMIT @offset, @limit", Connection);
            cmd.Parameters.AddWithValue("@account", account);
            cmd.Parameters.AddWithValue("@repo", repo);
            cmd.Parameters.AddWithValue("@offset", offset);
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Prepare();
            using var reader = cmd.ExecuteReader();
            List<GithubCommit> list = new();
            while (reader.Read())
            {
                list.Add(new GithubCommit
                {
                    Id = (int)reader["id"],
                    Account = (string)reader["account"],
                    Repo = (string)reader["repo"],
                    Sha = (string)reader["sha"],
                    Committer = (string)reader["committer"],
                    Url = (string)reader["url"],
                    CommittedAt = (DateTime)reader["committed_at"],
                    CreatedAt = (DateTime)reader["created_at"]
                });
            }
            return list;
        }

        public int Insert(GithubCommit m)
        {
            using var cmd = new MySqlCommand($"INSERT INTO tbl_github_commit(account, repo, sha, committer, url, committed_at) VALUES(@account, @repo, @sha, @committer, @url, @committed_at)", Connection);
            cmd.Parameters.AddWithValue("@account", m.Account);
            cmd.Parameters.AddWithValue("@repo", m.Repo);
            cmd.Parameters.AddWithValue("@sha", m.Sha);
            cmd.Parameters.AddWithValue("@committer", m.Committer);
            cmd.Parameters.AddWithValue("@url", m.Url);
            cmd.Parameters.AddWithValue("@committed_at", m.CommittedAt);
            cmd.Prepare();
            return cmd.ExecuteNonQuery();
        }
    }
}
