using DotNetEnv;
using Github2Telegram.Dao;
using MySql.Data.MySqlClient;

namespace Github2Telegram
{
    internal class Database
    {
        public static string ConnectionString = $"Server={Env.GetString("MYSQL_HOST")};port={Env.GetInt("MYSQL_PORT")};uid={Env.GetString("MYSQL_USERNAME")};pwd={Env.GetString("MYSQL_PASSWORD")};database={Env.GetString("MYSQL_DATABASE")};charSet=utf8;SSLMODE=None;";
    }
}
