namespace Github2Telegram.Model
{
    public class TelegramChat
    {
        public static readonly string ROLE_ADMIN = "admin";
        public static readonly string ROLE_AUTH = "auth";
        public static readonly string ROLE_NOTIFY = "notify";
        public static readonly string ROLE_GROUP = "group";

        public long Id { get; set; }
        public string? Name { get; set; }
        public string? Role { get; set; }
        public DateTime CreatedAt { get; set; }

        public bool IsAuthUser => Role == ROLE_AUTH;
        public bool IsGroup => Role == ROLE_NOTIFY;
    }
}
