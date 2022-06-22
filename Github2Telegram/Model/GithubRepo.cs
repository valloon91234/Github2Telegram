using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Github2Telegram.Model
{
    public class GithubRepo
    {
        public string? Account { get; set; }
        public string? Name { get; set; }
        public string? AddedBy { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
