using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Github2Telegram.Model
{
    public class GithubCommit
    {
        public int Id { get; set; }
        public string? Account { get; set; }
        public string? Repo { get; set; }
        public string? Sha { get; set; }
        public string? Committer { get; set; }
        public string? Url { get; set; }
        public DateTime? CommittedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
