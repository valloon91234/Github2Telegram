using Github2Telegram.Model;
using Octokit;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Github2Telegram
{
    internal class TelegramClient
    {
        public static TelegramBotClient? Client;
        public static Telegram.Bot.Types.User? Me { get; set; }

        public static void Init()
        {
            Client = new TelegramBotClient(DotNetEnv.Env.GetString("TELEGRAM_TOKEN"));
            using var cts = new CancellationTokenSource();
            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types
            };
            Client.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );
            Me = Client.GetMeAsync().Result;
            Logger.WriteLine($"Telegram connected: username = {Me.Username}");
        }

        static readonly Dictionary<string, string> LastCommand = new();

        static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            long chatId;
            int messageId;
            string chatUsername;
            string receivedMessageText;
            // Only process Message updates: https://core.telegram.org/bots/api#message
            if (update.Type == UpdateType.Message)
            {
                // Only process text messages
                if (update.Message!.Type != MessageType.Text) return;
                if (update.Message!.Chat.Type != ChatType.Private) return;

                chatId = update.Message.Chat.Id;
                messageId = update.Message.MessageId;
                chatUsername = update.Message.Chat.Username!;
                receivedMessageText = update.Message.Text!;
                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  \"{receivedMessageText}\" from {chatUsername}. chatId = {chatId}, messageId = {messageId}", ConsoleColor.DarkGray);
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                chatId = update.CallbackQuery!.Message!.Chat.Id;
                chatUsername = update.CallbackQuery.From.Username!;
                receivedMessageText = update.CallbackQuery.Data!;
                await botClient.AnswerCallbackQueryAsync(callbackQueryId: update.CallbackQuery!.Id, cancellationToken: cancellationToken);
            }
            else
                return;
            string[] superAdminList = DotNetEnv.Env.GetString("TELEGRAM_SUPER_ADMIN").Split(new char[] { ';', ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            bool isSuperAdmin = superAdminList.Contains(chatUsername!);
            bool isAuthUser = isSuperAdmin;
            if (!isSuperAdmin)
            {
                var user = Database.TelegramAccount!.SelectByName(chatUsername!);
                if (user != null && user.IsAuthUser) isAuthUser = true;
            }

            if (receivedMessageText[0] == '/')
            {
                var command = receivedMessageText;
                switch (command)
                {
                    case "/list_all_repo":
                        if (isSuperAdmin)
                        {
                            var githubAccountList = Database.GithubAccount!.SelectAll();
                            var repoListAll = new List<GithubRepo>();
                            foreach (var githubAccount in githubAccountList)
                            {
                                string token = githubAccount.Token!;
                                Credentials tokenAuth = new(token);
                                GitHubClient githubClient = new(new ProductHeaderValue(token.GetHashCode().ToString("X")))
                                {
                                    Credentials = tokenAuth
                                };
                                var githubUsername = githubClient.User.Current().Result.Login;
                                if (githubUsername != githubAccount.Name)
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <WARN>  github username does not match: original = {githubAccount.Name}, response = {githubUsername}", ConsoleColor.DarkYellow);
                                var repoList = githubClient.Repository.GetAllForCurrent().Result;
                                foreach (var repo in repoList)
                                {
                                    repoListAll.Add(new GithubRepo
                                    {
                                        Account = repo.Owner.Login,
                                        Name = repo.Name,
                                        CreatedAt = repo.CreatedAt.UtcDateTime
                                    });
                                }
                            }
                            if (repoListAll.Count == 0)
                            {
                                string replyMessageText = $"No repository available.";
                                //await botClient.SendTextMessageAsync(chatId: chatId, replyToMessageId: messageId, text: replyMessageText, cancellationToken: cancellationToken);
                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            }
                            //else if (githubAccountList.Count == 1)
                            //{
                            //    string replyMessageText = $"{repoListAll.Count} repositories:";
                            //    foreach (var repo in repoListAll)
                            //    {
                            //        replyMessageText += "\n\n" + repo.Name;
                            //    }
                            //    await botClient.SendTextMessageAsync(chatId: chatId,text: replyMessageText, cancellationToken: cancellationToken);
                            //}
                            else
                            {
                                string replyMessageText = $"{repoListAll.Count} repositories in {githubAccountList.Count} users:\n";
                                foreach (var repo in repoListAll)
                                {
                                    replyMessageText += $"\n{repo.Account}/{repo.Name}";
                                }
                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            }
                        }
                        else
                        {
                            string replyMessageText = $"Permission denied.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                        }
                        LastCommand.Remove(chatUsername!);
                        break;
                    case "/list_added_repo":
                        if (isSuperAdmin)
                        {
                            var githubRepoList = Database.GithubRepo!.SelectAll();
                            string replyMessageText = $"{githubRepoList.Count} repositories added:\n";
                            foreach (var repo in githubRepoList)
                            {
                                replyMessageText += $"\n{repo.Account}/{repo.Name}";
                            }
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                        }
                        else
                        {
                            string replyMessageText = $"Permission denied.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                        }
                        LastCommand.Remove(chatUsername!);
                        break;
                    case "/add_repo":
                        if (isSuperAdmin)
                        {
                            string replyMessageText = "Type [Github Username]/[Repository Name] to add or type 'exit' to cancel.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            LastCommand[chatUsername!] = command;
                        }
                        else
                        {
                            string replyMessageText = $"Permission denied.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            LastCommand.Remove(chatUsername!);
                        }
                        break;
                    case "/remove_repo":
                        if (isSuperAdmin)
                        {
                            string replyMessageText = "Type [Github Username]/[Repository Name] to remove or type 'exit' to cancel.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            LastCommand[chatUsername!] = command;
                        }
                        else
                        {
                            string replyMessageText = $"Permission denied.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            LastCommand.Remove(chatUsername!);
                        }
                        break;
                    case "/list_auth_user":
                        if (isSuperAdmin)
                        {
                            var accountList = Database.TelegramAccount!.SelectByRole(TelegramAccount.ROLE_AUTH);
                            string replyMessageText = $"{accountList.Count} users:\n";
                            foreach (var a in accountList)
                            {
                                replyMessageText += $"\n{a.Name}";
                            }
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                        }
                        else
                        {
                            string replyMessageText = $"Permission denied.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                        }
                        LastCommand.Remove(chatUsername!);
                        break;
                    case "/add_auth_user":
                        if (isSuperAdmin)
                        {
                            string replyMessageText = "Type Telegram Username to add or type 'exit' to cancel.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            LastCommand[chatUsername!] = command;
                        }
                        else
                        {
                            string replyMessageText = $"Permission denied.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            LastCommand.Remove(chatUsername!);
                        }
                        break;
                    case "/remove_auth_user":
                        if (isSuperAdmin)
                        {
                            string replyMessageText = "Type Telegram Username to remove or type 'exit' to cancel.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            LastCommand[chatUsername!] = command;
                        }
                        else
                        {
                            string replyMessageText = $"Permission denied.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            LastCommand.Remove(chatUsername!);
                        }
                        break;
                    case "/list_notify_user":
                        if (isSuperAdmin)
                        {
                            var accountList = Database.TelegramAccount!.SelectByRole(TelegramAccount.ROLE_NOTIFY);
                            string replyMessageText = $"{accountList.Count} users:\n";
                            foreach (var a in accountList)
                            {
                                replyMessageText += $"\n{a.Name}";
                            }
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                        }
                        else
                        {
                            string replyMessageText = $"Permission denied.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                        }
                        LastCommand.Remove(chatUsername!);
                        break;
                    case "/add_notify_user":
                        if (isSuperAdmin)
                        {
                            string replyMessageText = "Type Telegram Username to add or type 'exit' to cancel.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            LastCommand[chatUsername!] = command;
                        }
                        else
                        {
                            string replyMessageText = $"Permission denied.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            LastCommand.Remove(chatUsername!);
                        }
                        break;
                    case "/remove_notify_user":
                        if (isSuperAdmin)
                        {
                            string replyMessageText = "Type Telegram Username to remove or type 'exit' to cancel.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            LastCommand[chatUsername!] = command;
                        }
                        else
                        {
                            string replyMessageText = $"Permission denied.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            LastCommand.Remove(chatUsername!);
                        }
                        break;
                    case string x when x.StartsWith("/view_commits_by_repo"):
                        if (isAuthUser)
                        {
                            string repoUsername, repoName;
                            int offset = 0, limit = 5;
                            string[] array = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (array.Length >= 2)
                            {
                                string[] paramArray = array[1].Split('/');
                                if (paramArray.Length == 2)
                                {
                                    repoUsername = paramArray[0].Trim();
                                    repoName = paramArray[1].Trim();
                                    try
                                    {
                                        if (array.Length >= 3)
                                        {
                                            offset = Convert.ToInt32(array[1].Trim());
                                            if (array.Length >= 4)
                                            {
                                                limit = Convert.ToInt32(array[2].Trim());
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.WriteLine(ex.ToString(), ConsoleColor.Red);
                                    }
                                    var commitList = Database.GithubCommit!.SelectLast(repoUsername, repoName, offset, limit);
                                    if (commitList.Count >= 5)
                                    {
                                        string replyMessageText = $"Recent commits on {repoUsername}/{repoName}\n";
                                        foreach (var a in commitList)
                                        {
                                            replyMessageText += $"\n{a.CommittedAt:yyyy-MM-dd HH:mm}    {a.Committer}    {a.Sha![..6]}    <a href=\"{a.Url}\">Go</a>";
                                        }
                                        InlineKeyboardMarkup replyMarkup = new(new[]
                                        {
                                            new []
                                            {
                                                InlineKeyboardButton.WithCallbackData(text: "More...", callbackData: $"/view_commits_by_repo {repoUsername}/{repoName} {offset+limit}"),
                                            },
                                        });
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, parseMode: ParseMode.Html, disableNotification: true, disableWebPagePreview: true, replyMarkup: replyMarkup, cancellationToken: cancellationToken);
                                        //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    }
                                    else if (commitList.Count > 0)
                                    {
                                        string replyMessageText = $"";
                                        foreach (var a in commitList)
                                        {
                                            replyMessageText += $"\n{a.CommittedAt:yyyy-MM-dd HH:mm}    {a.Committer}    {a.Sha![..6]}    <a href=\"{a.Url}\">Go</a>";
                                        }
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, parseMode: ParseMode.Html, disableNotification: true, disableWebPagePreview: true, cancellationToken: cancellationToken);
                                        //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    }
                                    else
                                    {
                                        string replyMessageText = offset > 0 ? "No more commit." : "No commit.";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                        //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    }
                                }
                                else
                                {
                                    string replyMessageText = "Invalid value. Try again or type 'exit' to cancel.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                }
                                LastCommand.Remove(chatUsername!);
                            }
                            else
                            {
                                string replyMessageText = "Type [Github Username]/[Repository Name] to list most commits or type 'exit' to cancel.";
                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                LastCommand[chatUsername!] = command;
                            }
                        }
                        else
                        {
                            string replyMessageText = $"Permission denied.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            LastCommand.Remove(chatUsername!);
                        }
                        break;
                    case string x when x.StartsWith("/view_commits"):
                        if (isAuthUser)
                        {
                            int offset = 0, limit = 5;
                            string[] array = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            try
                            {
                                if (array.Length >= 2)
                                {
                                    offset = Convert.ToInt32(array[1].Trim());
                                    if (array.Length >= 3)
                                    {
                                        limit = Convert.ToInt32(array[2].Trim());
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.WriteLine(ex.ToString(), ConsoleColor.Red);
                            }
                            var commitList = Database.GithubCommit!.SelectLast(offset, limit);
                            if (commitList.Count >= 5)
                            {
                                string replyMessageText = $"Recent commits:\n";
                                foreach (var a in commitList)
                                {
                                    replyMessageText += $"\n{a.CommittedAt:yyyy-MM-dd HH:mm}    {a.Account}/{a.Repo}    {a.Committer}    <a href=\"{a.Url}\">Go</a>";
                                }
                                InlineKeyboardMarkup replyMarkup = new(new[]
                                {
                                    new []
                                    {
                                        InlineKeyboardButton.WithCallbackData(text: "More...", callbackData: $"/view_commits {offset+limit}"),
                                    },
                                });
                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, parseMode: ParseMode.Html, disableNotification: true, disableWebPagePreview: true, replyMarkup: replyMarkup, cancellationToken: cancellationToken);
                                //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            }
                            else if (commitList.Count > 0)
                            {
                                string replyMessageText = $"";
                                foreach (var a in commitList)
                                {
                                    replyMessageText += $"\n{a.CommittedAt:yyyy-MM-dd HH:mm}    {a.Account}/{a.Repo}    {a.Committer}    <a href=\"{a.Url}\">Go</a>";
                                }
                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, parseMode: ParseMode.Html, disableNotification: true, disableWebPagePreview: true, cancellationToken: cancellationToken);
                                //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            }
                            else
                            {
                                string replyMessageText = offset > 0 ? "No more commit." : "No commit.";
                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            }
                        }
                        else
                        {
                            string replyMessageText = $"Permission denied.";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                        }
                        LastCommand.Remove(chatUsername!);
                        break;
                    default:
                        {
                            string replyMessageText = $"Unknown command: {command}";
                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                        }
                        LastCommand.Remove(chatUsername!);
                        break;
                }
            }
            else if (LastCommand.ContainsKey(chatUsername!))
            {
                if (receivedMessageText == "exit" || receivedMessageText == "/exit")
                    LastCommand.Remove(chatUsername!);
                else
                    switch (LastCommand[chatUsername!])
                    {
                        case "/add_repo":
                            {
                                string[] paramArray = receivedMessageText.Split('/');
                                if (paramArray.Length == 2)
                                {
                                    string repoUsername = paramArray[0].Trim();
                                    string repoName = paramArray[1].Trim();
                                    var account = Database.GithubAccount!.SelectByName(repoUsername);
                                    if (account == null)
                                    {
                                        string replyMessageText = $"Github token for repository '{repoUsername}/{repoName}' does not registered. Try again or type 'exit' to cancel.";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    }
                                    else
                                    {
                                        string token = account.Token!;
                                        Credentials tokenAuth = new(token);
                                        GitHubClient githubClient = new(new ProductHeaderValue(token.GetHashCode().ToString("X")))
                                        {
                                            Credentials = tokenAuth
                                        };
                                        Repository repo;
                                        try
                                        {
                                            repo = githubClient.Repository.Get(repoUsername, repoName).Result;
                                            Database.GithubRepo!.Insert(new GithubRepo
                                            {
                                                Account = repoUsername,
                                                Name = repoName,
                                                //AddedBy=
                                            });
                                            string replyMessageText = $"Successfully added.";
                                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                            LastCommand.Remove(chatUsername!);
                                        }
                                        catch (Exception ex)
                                        {
                                            if (ex is MySql.Data.MySqlClient.MySqlException mySqlException && mySqlException.Number == 1062)
                                            {
                                                string replyMessageText = $"Repository '{repoUsername}/{repoName}' already added.";
                                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                                LastCommand.Remove(chatUsername!);
                                            }
                                            else if (ex.InnerException != null && ex.InnerException is NotFoundException)
                                            {
                                                string replyMessageText = $"Repository '{repoUsername}/{repoName}' does not exist. Try again or type 'exit' to cancel.";
                                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <WARN>  repository not found: {repoUsername}/{repoName}", ConsoleColor.DarkYellow);
                                            }
                                            else
                                            {
                                                string replyMessageText = $"Failed to add repository '{repoUsername}/{repoName}'\n{(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}";
                                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <ERROR>  failed to get repository: {repoUsername}/{repoName}\n\t{(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red);
                                                LastCommand.Remove(chatUsername!);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    string replyMessageText = "Invalid value. Try again or type 'exit' to cancel.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                }
                            }
                            break;
                        case "/remove_repo":
                            {
                                string[] paramArray = receivedMessageText.Split('/');
                                if (paramArray.Length == 2)
                                {
                                    string repoUsername = paramArray[0].Trim();
                                    string repoName = paramArray[1].Trim();
                                    int result = Database.GithubRepo!.Delete(repoUsername, repoName);
                                    if (result == 0)
                                    {
                                        string replyMessageText = $"Repository '{repoUsername}/{repoName}' does not exist.";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    }
                                    else
                                    {
                                        string replyMessageText = $"Successfully removed.";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    }
                                    LastCommand.Remove(chatUsername!);
                                }
                                else
                                {
                                    string replyMessageText = "Invalid value. Try again or type 'exit' to cancel.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                }
                            }
                            break;
                        case "/add_auth_user":
                            {
                                string value = receivedMessageText.Trim();
                                try
                                {
                                    int result = Database.TelegramAccount!.Insert(value, TelegramAccount.ROLE_AUTH);
                                    string replyMessageText = $"Successfully added.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                }
                                catch (Exception ex)
                                {
                                    if (ex is MySql.Data.MySqlClient.MySqlException mySqlException && mySqlException.Number == 1062)
                                    {
                                        string replyMessageText = $"Telegram User '{value}' already added.";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    }
                                    else
                                    {
                                        string replyMessageText = $"Failed to add Telegram User '{value}'\n{(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <ERROR>  failed to add Telegram User '{value}'\n\t{(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red);
                                    }
                                }
                            }
                            LastCommand.Remove(chatUsername!);
                            break;
                        case "/remove_auth_user":
                            {
                                string value = receivedMessageText.Trim();
                                var user = Database.TelegramAccount!.SelectByName(value);
                                if (user == null)
                                {
                                    string replyMessageText = $"Telegram User '{value}' does not exist.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                }
                                else if (user.Role != TelegramAccount.ROLE_AUTH)
                                {
                                    string replyMessageText = $"'{value}' is not auth user.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                }
                                else
                                {
                                    int result = Database.TelegramAccount!.Delete(value);
                                    if (result == 0)
                                    {
                                        string replyMessageText = $"Telegram User '{value}' does not exist.";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    }
                                    else
                                    {
                                        string replyMessageText = $"Successfully deleted.";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    }
                                }
                            }
                            LastCommand.Remove(chatUsername!);
                            break;
                        case "/add_notify_user":
                            {
                                string value = receivedMessageText.Trim();
                                try
                                {
                                    int result = Database.TelegramAccount!.Insert(value, TelegramAccount.ROLE_NOTIFY);
                                    string replyMessageText = $"Successfully added.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                }
                                catch (Exception ex)
                                {
                                    if (ex is MySql.Data.MySqlClient.MySqlException mySqlException && mySqlException.Number == 1062)
                                    {
                                        string replyMessageText = $"Telegram User '{value}' already added.";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    }
                                    else
                                    {
                                        string replyMessageText = $"Failed to add Telegram User '{value}'\n{(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <ERROR>  failed to add Telegram User '{value}'\n\t{(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red);
                                    }
                                }
                            }
                            LastCommand.Remove(chatUsername!);
                            break;
                        case "/remove_notify_user":
                            {
                                string value = receivedMessageText.Trim();
                                var user = Database.TelegramAccount!.SelectByName(value);
                                if (user == null)
                                {
                                    string replyMessageText = $"Telegram User '{value}' does not exist.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                }
                                else if (user.Role != TelegramAccount.ROLE_NOTIFY)
                                {
                                    string replyMessageText = $"'{value}' is not notify user.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                }
                                else
                                {
                                    int result = Database.TelegramAccount!.Delete(value);
                                    if (result == 0)
                                    {
                                        string replyMessageText = $"Telegram User '{value}' does not exist.";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    }
                                    else
                                    {
                                        string replyMessageText = $"Successfully deleted.";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    }
                                }
                                LastCommand.Remove(chatUsername!);
                            }
                            break;
                        case "/view_commits_by_repo":
                            {
                                string[] paramArray = receivedMessageText.Split('/');
                                if (paramArray.Length == 2)
                                {
                                    string repoUsername = paramArray[0].Trim();
                                    string repoName = paramArray[1].Trim();
                                    var commitList = Database.GithubCommit!.SelectLast(repoUsername, repoName, 0, 5);
                                    if (commitList.Count >= 5)
                                    {
                                        string replyMessageText = $"Recent commits on {repoUsername}/{repoName}\n";
                                        foreach (var a in commitList)
                                        {
                                            replyMessageText += $"\n{a.CommittedAt:yyyy-MM-dd HH:mm}    {a.Committer}    {a.Sha![..6]}    <a href=\"{a.Url}\">Go</a>";
                                        }
                                        InlineKeyboardMarkup replyMarkup = new(new[]
                                        {
                                            new []
                                            {
                                                InlineKeyboardButton.WithCallbackData(text: "More...", callbackData: "more"),
                                            },
                                        });
                                        await botClient.SendTextMessageAsync(chatId: chatId, replyToMessageId: update.Message!.MessageId, text: replyMessageText, parseMode: ParseMode.Html, disableNotification: true, disableWebPagePreview: true, replyMarkup: replyMarkup, cancellationToken: cancellationToken);
                                    }
                                    else if (commitList.Count > 0)
                                    {
                                        string replyMessageText = $"";
                                        foreach (var a in commitList)
                                        {
                                            replyMessageText += $"\n{a.CommittedAt:yyyy-MM-dd HH:mm}    {a.Committer}    {a.Sha![..6]}    <a href=\"{a.Url}\">Go</a>";
                                        }
                                        await botClient.SendTextMessageAsync(chatId: chatId, replyToMessageId: update.Message!.MessageId, text: replyMessageText, parseMode: ParseMode.Html, disableNotification: true, disableWebPagePreview: true, cancellationToken: cancellationToken);
                                    }
                                    else
                                    {
                                        string replyMessageText = "No commit.";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    }
                                    LastCommand.Remove(chatUsername!);
                                }
                                else
                                {
                                    string replyMessageText = "Invalid value. Try again or type 'exit' to cancel.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                }
                            }
                            break;
                        default:
                            {
                                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <ERROR>  Unknown error", ConsoleColor.Red);
                            }
                            break;
                    }
            }

            //Logger.WriteLine($"Received a '{receivedMessageText}' message in chat {chatId}.");

            // Echo received message text
            //Message sentMessage = await botClient.SendTextMessageAsync(
            //    chatId: chatId,
            //    text: "You said:\n" + receivedMessageText,
            //    cancellationToken: cancellationToken);
        }

        static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException
                    => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Logger.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public static void SendMessageToGroup(string text, ParseMode? parseMode = default)
        {
            void sendMessage(string target)
            {
                if (!target.StartsWith("@")) target = "@" + target;
                Client!.SendTextMessageAsync(chatId: target, text: text, disableWebPagePreview: true, parseMode: parseMode);
                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  Message sent to {target}: {text}", ConsoleColor.DarkGray);
            }
            string[] superAdminList = DotNetEnv.Env.GetString("TELEGRAM_SUPER_ADMIN").Split(new char[] { ';', ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var name in superAdminList)
            {
                sendMessage(name);
            }
            var groupList = Database.TelegramAccount!.SelectAll();
            foreach (var group in groupList)
            {
                string name = group.Name!;
                sendMessage(name);
            }
        }

    }
}
