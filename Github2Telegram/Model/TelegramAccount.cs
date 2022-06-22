using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Github2Telegram.Model
{
    public class TelegramAccount
    {
        public static readonly string ROLE_AUTH = "auth";
        public static readonly string ROLE_NOTIFY = "notify";

        public string? Name { get; set; }
        public string? Role { get; set; }
        public DateTime CreatedAt { get; set; }

        public bool IsAuthUser => Role == ROLE_AUTH;
        public bool IsGroup => Role == ROLE_NOTIFY;
    }
}
