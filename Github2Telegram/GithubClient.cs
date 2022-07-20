using DotNetEnv;
using Github2Telegram.Dao;
using Github2Telegram.Model;
using MySql.Data.MySqlClient;
using Octokit;

namespace Github2Telegram
{
    internal class GithubClient
    {
        public static void Run()
        {
            Thread.Sleep(5000);
            using GithubAccountDao githubAccountDao = new();
            using GithubRepoDao githubRepoDao = new();
            using GithubCommitDao githubCommitDao = new();
            while (true)
            {
                //TelegramClient.SendMessageToGroup($"test message", Telegram.Bot.Types.Enums.ParseMode.Html);
                //Thread.Sleep(5000);
                //continue;

                try
                {
                    var githubAccountList = githubAccountDao.SelectAll();
                    var githubRepoList = githubRepoDao.SelectAll();
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
                            var lastCommit = githubCommitDao.SelectLast(githubRepo.Account, githubRepo.Name!);
                            List<GithubCommit> commits = new();
                            List<string> branchList = new();
                            try
                            {
                                var defaultBranch = repo.DefaultBranch;
                                var branchs = githubClient.Repository.Branch.GetAll(username, githubRepo.Name).Result;
                                foreach (var branch in branchs) branchList.Add(branch.Name);
                                branchList.Remove(defaultBranch);
                                branchList.Insert(0, defaultBranch);
                                branchList.Reverse();
                                foreach (var branchName in branchList)
                                {
                                    CommitRequest request = new();
                                    if (lastCommit != null && lastCommit.CommittedAt != null) request.Since = new DateTimeOffset(lastCommit.CommittedAt.Value.AddSeconds(1), TimeSpan.Zero);
                                    request.Sha = branchName;
                                    var result = githubClient.Repository.Commit.GetAll(username, githubRepo.Name, request).Result;
                                    foreach (var item in result)
                                    {
                                        commits.Add(new()
                                        {
                                            Account = githubRepo.Account,
                                            Repo = githubRepo.Name,
                                            Sha = item.Sha,
                                            Committer = item.Author == null ? githubRepo.Account : item.Author.Login,
                                            Branch = branchName,
                                            Message = item.Commit.Message,
                                            Url = item.HtmlUrl,
                                            CommittedAt = item.Commit.Committer.Date.UtcDateTime
                                        });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                if (ex.InnerException != null && ex.InnerException is NotFoundException)
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <WARN>  repository not found: {githubRepo.Account}/{githubRepo.Name}", ConsoleColor.DarkYellow);
                                else
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red);
                                continue;
                            }
                            var commitsCount = commits.Count;
                            if (commitsCount > 0)
                            {
                                commits.Reverse();
                                int insertedCount = 0;
                                for (int i = 0; i < commitsCount; i++)
                                {
                                    var commit = commits[i];
                                    try
                                    {
                                        insertedCount += githubCommitDao.Insert(commit);
                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <INFO>  {insertedCount} commits saved ({i} / {commitsCount}): {githubRepo.Account}/{githubRepo.Name}/{commit.Sha![..6]} ({commit.Branch} branch)", ConsoleColor.DarkGray);
                                        if (lastCommit != null && lastCommit.CommittedAt != null)
                                        {
                                            string message = $"*************************\n{commit.CommittedAt:M'/'d'/'yyyy dddd hh:mm:ss tt}\n<a href=\"https://github.com/{commit.Committer}/\">{commit.Committer}</a> pushed to <a href=\"https://github.com/{githubRepo.Account}/{githubRepo.Name}/\">{githubRepo.Account}/{githubRepo.Name}</a>\n{commit.Branch} branch  *{commit.Title}*    Click: <a href=\"{commit.Url}\">{commit.Sha![..6]}</a>";
                                            if (!string.IsNullOrWhiteSpace(commit.Description)) message += $"\n\n\"{commit.Description}\"";
                                            message += "\n*************************";
                                            TelegramClient.SendMessageToGroup(message, Telegram.Bot.Types.Enums.ParseMode.Html);
                                        }
                                    }
                                    catch (MySqlException ex)
                                    {
                                        if (ex.Number == 1062)
                                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <WARN>  commit already exists ({i} / {commitsCount}): {githubRepo.Account}/{githubRepo.Name}/{commit.Sha![..6]} ({commit.Branch} branch)", ConsoleColor.DarkYellow);
                                        else
                                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <ERROR>  failed to insert into database ({i} / {commitsCount}): {githubRepo.Account}/{githubRepo.Name}/{commit.Sha![..6]}\n\t{(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red);
                                        continue;
                                    }
                                }
                                if (insertedCount > 0)
                                {
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <INFO>  {insertedCount} new commits on {githubRepo.Account}/{githubRepo.Name} ({branchList.Count} branchs)", ConsoleColor.Green);
                                    insertedCountAll += insertedCount;
                                    if (lastCommit == null || lastCommit.CommittedAt == null)
                                    {
                                        TelegramClient.SendMessageToGroup($"Repository initialized:  <a href=\"https://github.com/{githubRepo.Account}/{githubRepo.Name}/\">{githubRepo.Account}/{githubRepo.Name}</a>  ({insertedCount} commits in {branchList.Count} branchs)", Telegram.Bot.Types.Enums.ParseMode.Html);
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
                }
                catch (Exception ex)
                {
                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {ex}", ConsoleColor.Red);
                }
                int timeout = Env.GetInt("GITHUB_INTERVAL", 30);
                //Logger.WriteWait($"Waiting for {timeout} seconds", timeout);
                Thread.Sleep(timeout * 1000);
            }
        }

    }
}
