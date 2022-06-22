using DotNetEnv;
using Github2Telegram.Model;
using MySql.Data.MySqlClient;
using Octokit;

namespace Github2Telegram
{
    internal class GithubClient
    {
        public static void Run()
        {
            while (true)
            {
                var githubAccountList = Database.GithubAccount!.SelectAll();
                var githubRepoList = Database.GithubRepo!.SelectAll();
                int repoCountAll = 0;
                int insertedCountAll = 0;
                foreach (var githubAccount in githubAccountList)
                {
                    string token = githubAccount.Token!;
                    Credentials tokenAuth = new(token);
                    GitHubClient githubClient = new(new ProductHeaderValue(token.GetHashCode().ToString("X")))
                    {
                        Credentials = tokenAuth
                    };
                    var username = githubClient.User.Current().Result.Login;
                    if (username != githubAccount.Name)
                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <WARN>  github username does not match: original = {githubAccount.Name}, response = {username}", ConsoleColor.DarkYellow);
                    foreach (var githubRepo in githubRepoList)
                    {
                        if (githubRepo.Account != username) continue;
                        Repository repo;
                        try
                        {
                            repo = githubClient.Repository.Get(githubRepo.Account, githubRepo.Name).Result;
                        }
                        catch (Exception ex)
                        {
                            if (ex.InnerException != null && ex.InnerException is NotFoundException)
                                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <WARN>  repository not found: {githubRepo.Account}/{githubRepo.Name}", ConsoleColor.DarkYellow);
                            else
                                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <ERROR>  failed to get repository: {githubRepo.Account}/{githubRepo.Name}\n\t{(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red);
                            continue;
                        }
                        repoCountAll++;
                        var lastCommit = Database.GithubCommit!.SelectLast(githubRepo.Account, githubRepo.Name!);
                        IReadOnlyList<GitHubCommit> commits;
                        try
                        {
                            CommitRequest request = new();
                            if (lastCommit != null && lastCommit.CommittedAt != null) request.Since = new DateTimeOffset(lastCommit.CommittedAt.Value.AddSeconds(1), TimeSpan.Zero);
                            commits = githubClient.Repository.Commit.GetAll(username, githubRepo.Name, request).Result;
                        }
                        catch (Exception ex)
                        {
                            if (ex.InnerException != null && ex.InnerException is NotFoundException)
                                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <WARN>  repository not found: {githubRepo.Account}/{githubRepo.Name}", ConsoleColor.DarkYellow);
                            else
                                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red);
                            continue;
                        }
                        if (commits.Count > 0)
                        {
                            var commitsReversed = commits.Reverse();
                            int insertedCount = 0;
                            foreach (var commit in commitsReversed)
                            {
                                GithubCommit gitHubCommit = new()
                                {
                                    Account = githubRepo.Account,
                                    Repo = githubRepo.Name,
                                    Sha = commit.Sha,
                                    Committer = commit.Committer == null ? commit.Commit.Author.Name : commit.Committer.Login,
                                    Url = commit.HtmlUrl,
                                    CommittedAt = commit.Commit.Committer.Date.UtcDateTime
                                };
                                try
                                {
                                    insertedCount += Database.GithubCommit.Insert(gitHubCommit);
                                    if (lastCommit != null && lastCommit.CommittedAt != null)
                                        TelegramClient.SendMessageToGroup($"{gitHubCommit.CommittedAt}\n<a href=\"https://github.com/{gitHubCommit.Committer}/\">{gitHubCommit.Committer}</a>  committed on  <a href=\"https://github.com/{githubRepo.Account}/{githubRepo.Name}/\">{githubRepo.Account}/{githubRepo.Name}</a>\n<a href=\"{gitHubCommit.Url}\">{gitHubCommit.Sha![..6]}</a>  \"{commit.Commit.Message}\"", Telegram.Bot.Types.Enums.ParseMode.Html);
                                }
                                catch (MySqlException ex)
                                {
                                    if (ex.Number == 1062)
                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <WARN>  commit already exists: {githubRepo.Account}/{githubRepo.Name}/{commit.Sha}", ConsoleColor.DarkYellow);
                                    else
                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <ERROR>  failed to insert into database: {githubRepo.Account}/{githubRepo.Name}/{commit.Sha}\n\t{(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red);
                                    continue;
                                }
                            }
                            if (insertedCount > 0)
                            {
                                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <INFO>  {insertedCount} new commits on {githubRepo.Account}/{githubRepo.Name}", ConsoleColor.Green);
                                insertedCountAll += insertedCount;
                                if (lastCommit == null || lastCommit.CommittedAt == null)
                                {
                                    TelegramClient.SendMessageToGroup($"Repository initialized:  <a href=\"https://github.com/{githubRepo.Account}/{githubRepo.Name}/\">{githubRepo.Account}/{githubRepo.Name}</a>  ({insertedCount} commits)", Telegram.Bot.Types.Enums.ParseMode.Html);
                                }
                            }
                        }
                    }
                }
                Console.Title = $"{repoCountAll} repositories | {TelegramClient.Me!.Username}";
                if (insertedCountAll > 0)
                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <INFO>  {insertedCountAll} new commits in {repoCountAll} repositories.", ConsoleColor.Green);
                else
                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <INFO>  No new commit in {repoCountAll} repositories.", ConsoleColor.DarkGray);

                int timeout = Env.GetInt("GITHUB_INTERVAL", 30);
                //Logger.WriteWait($"Waiting for {timeout} seconds", timeout);
                Thread.Sleep(timeout * 1000);
            }
        }

    }
}
