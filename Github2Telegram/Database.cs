using DotNetEnv;
using Github2Telegram.Dao;
using MySql.Data.MySqlClient;

namespace Github2Telegram
{
    internal class Database
    {
        public static MySqlConnection? Connection { get; set; }
        public static GithubAccountDao? GithubAccount { get; set; }
        public static GithubRepoDao? GithubRepo { get; set; }
        public static GithubCommitDao? GithubCommit { get; set; }
        public static TelegramAccountDao? TelegramAccount { get; set; }

        public static void Init()
        {
            Connection = new($"Server={Env.GetString("MYSQL_HOST")};port={Env.GetInt("MYSQL_PORT")};uid={Env.GetString("MYSQL_USERNAME")};pwd={Env.GetString("MYSQL_PASSWORD")};database={Env.GetString("MYSQL_DATABASE")};charSet=utf8;");
            Connection.Open();
            GithubAccount = new(Connection);
            GithubRepo = new(Connection);
            GithubCommit = new(Connection);
            TelegramAccount = new(Connection);
        }

        public static void Dispose()
        {
            if (Connection == null) return;
            Connection.Dispose();
        }
    }
}
