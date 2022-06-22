using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Github2Telegram.Model
{
    public class GithubAccount
    {
        public string? Name { get; set; }
        public string? Token { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
