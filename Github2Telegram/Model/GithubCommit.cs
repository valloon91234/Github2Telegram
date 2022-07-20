namespace Github2Telegram.Model
{
    public class GithubCommit
    {
        public int Id { get; set; }
        public string? Account { get; set; }
        public string? Repo { get; set; }
        public string? Sha { get; set; }
        public string? Committer { get; set; }
        public string? Branch { get; set; }
        public string? Message { get; set; }
        public string? Url { get; set; }
        public DateTime? CommittedAt { get; set; }
        public DateTime? CreatedAt { get; set; }

        public string? Title
        {
            get
            {
                if (Message == null) return null;
                return Message.Split("\n\n")[0];
            }
        }

        public string? Description
        {
            get
            {
                if (Message == null) return null;
                var array = Message.Split("\n\n");
                if (array.Length < 2) return null;
                return array[1];
            }
        }

    }
}
