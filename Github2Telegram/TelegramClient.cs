using Github2Telegram.Dao;
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
            try
            {
                using TelegramChatDao telegramChatDao = new();
                using GithubAccountDao githubAccountDao = new();
                using GithubRepoDao githubRepoDao = new();
                using GithubCommitDao githubCommitDao = new();
                string[] superAdminList = DotNetEnv.Env.GetString("TELEGRAM_SUPER_ADMIN").Split(new char[] { ';', ',' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                long chatId;
                int messageId;
                string chatUsername;
                string senderUsername;
                string receivedMessageText;
                // Only process Message updates: https://core.telegram.org/bots/api#message
                if (update.Type == UpdateType.Message && update.Message!.Type == MessageType.Text && update.Message!.Chat.Type == ChatType.Private)
                {
                    // Only process text messages
                    chatId = update.Message.Chat.Id;
                    messageId = update.Message.MessageId;
                    chatUsername = update.Message.Chat.Username!;
                    senderUsername = update.Message.From!.Username!;
                    receivedMessageText = update.Message.Text!;
                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  \"{receivedMessageText}\" from {senderUsername}. chatId = {chatId}, messageId = {messageId}", ConsoleColor.DarkGray);
                }
                else if (update.Type == UpdateType.Message && update.Message!.Type == MessageType.Text && (update.Message!.Chat.Type == ChatType.Group || update.Message!.Chat.Type == ChatType.Supergroup))
                {
                    chatId = update.Message.Chat.Id;
                    messageId = update.Message.MessageId;
                    chatUsername = update.Message.Chat.Username!;
                    senderUsername = update.Message.From!.Username!;
                    receivedMessageText = update.Message.Text!;
                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  \"{receivedMessageText}\" from {senderUsername}. chatId = {chatId}, messageId = {messageId}", ConsoleColor.DarkGray);
                    if (receivedMessageText[0] == '/' && receivedMessageText.EndsWith($"@{Me!.Username}"))
                    {
                        var command = receivedMessageText[..^$"@{Me!.Username}".Length];
                        bool isSuperAdmin = superAdminList.Contains(senderUsername!);
                        switch (command)
                        {
                            case $"/start":
                                if (isSuperAdmin)
                                {
                                    var senderChat = telegramChatDao.SelectById(chatId);
                                    string chatName = chatUsername ?? chatId.ToString();
                                    if (senderChat == null)
                                        telegramChatDao.Insert(new TelegramChat
                                        {
                                            Id = chatId,
                                            Name = chatName,
                                            Role = TelegramChat.ROLE_GROUP
                                        });
                                    else if (senderChat.Name != chatName)
                                        telegramChatDao.UpdateName(chatId, chatName);
                                    string replyMessageText = $"Welcome!";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                }
                                break;
                            case $"/stop":
                                if (isSuperAdmin)
                                {
                                    telegramChatDao.DeleteById(chatId);
                                    string replyMessageText = $"Bye!";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                }
                                break;
                        }
                    }

                    return;
                }
                else if (update.Type == UpdateType.CallbackQuery)
                {
                    chatId = update.CallbackQuery!.Message!.Chat.Id;
                    senderUsername = update.CallbackQuery.From.Username!;
                    receivedMessageText = update.CallbackQuery.Data!;
                    await botClient.AnswerCallbackQueryAsync(callbackQueryId: update.CallbackQuery!.Id, cancellationToken: cancellationToken);
                }
                else
                    return;
                {
                    var senderChat = telegramChatDao.SelectByName(senderUsername!);
                    bool isSuperAdmin = superAdminList.Contains(senderUsername!);

                    bool isAuthUser = isSuperAdmin;
                    if (!isSuperAdmin && senderChat != null && senderChat.IsAuthUser) isAuthUser = true;

                    if (receivedMessageText[0] == '/')
                    {
                        var command = receivedMessageText;
                        switch (command)
                        {
                            case "/start":
                                if (isSuperAdmin && senderChat == null)
                                {
                                    telegramChatDao.Insert(new TelegramChat
                                    {
                                        Id = chatId,
                                        Name = senderUsername,
                                        Role = TelegramChat.ROLE_ADMIN
                                    });
                                    string replyMessageText = $"Welcome!";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                }
                                else if (senderChat != null && senderChat.Id != chatId)
                                {
                                    telegramChatDao.UpdateId(senderUsername, chatId);
                                    string replyMessageText = $"Welcome!";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                }
                                break;
                            case "/list_github_account":
                                if (isSuperAdmin)
                                {
                                    var githubAccountList = githubAccountDao.SelectAll();
                                    if (githubAccountList.Count == 0)
                                    {
                                        string replyMessageText = $"No github account registered.";
                                        //await botClient.SendTextMessageAsync(chatId: chatId, replyToMessageId: messageId, text: replyMessageText, cancellationToken: cancellationToken);
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    }
                                    else
                                    {
                                        string replyMessageText = $"{githubAccountList.Count} github accounts:\n";
                                        foreach (var githubAccount in githubAccountList)
                                        {
                                            string token = githubAccount.Token!;
                                            Credentials tokenAuth = new(token);
                                            GitHubClient githubClient = new(new ProductHeaderValue(token.GetHashCode().ToString("X")))
                                            {
                                                Credentials = tokenAuth
                                            };
                                            var githubUsername = githubClient.User.Current().Result.Login;
                                            replyMessageText += "\n" + githubAccount.Name;
                                            if (githubUsername != githubAccount.Name)
                                                replyMessageText += $" (username does not match: {githubUsername})";
                                        }
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    }
                                }
                                else
                                {
                                    string replyMessageText = $"Permission denied.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                }
                                LastCommand.Remove(senderUsername!);
                                break;
                            case "/add_github_account":
                                if (isSuperAdmin)
                                {
                                    string replyMessageText = "Type github access token to add or type 'exit' to cancel.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand[senderUsername!] = command;
                                }
                                else
                                {
                                    string replyMessageText = $"Permission denied.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand.Remove(senderUsername!);
                                }
                                break;
                            case "/remove_github_account":
                                if (isSuperAdmin)
                                {
                                    string replyMessageText = "Type github name to remove or type 'exit' to cancel.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand[senderUsername!] = command;
                                }
                                else
                                {
                                    string replyMessageText = $"Permission denied.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand.Remove(senderUsername!);
                                }
                                break;
                            //case "/list_all_repo":
                            //    if (isSuperAdmin)
                            //    {
                            //        var githubAccountList = githubAccountDao.SelectAll();
                            //        var repoListAll = new List<GithubRepo>();
                            //        foreach (var githubAccount in githubAccountList)
                            //        {
                            //            string token = githubAccount.Token!;
                            //            Credentials tokenAuth = new(token);
                            //            GitHubClient githubClient = new(new ProductHeaderValue(token.GetHashCode().ToString("X")))
                            //            {
                            //                Credentials = tokenAuth
                            //            };
                            //            var githubUsername = githubClient.User.Current().Result.Login;
                            //            if (githubUsername != githubAccount.Name)
                            //                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <WARN>  github username does not match: original = {githubAccount.Name}, response = {githubUsername}", ConsoleColor.DarkYellow);
                            //            var repoList = githubClient.Repository.GetAllForCurrent().Result;
                            //            foreach (var repo in repoList)
                            //            {
                            //                repoListAll.Add(new GithubRepo
                            //                {
                            //                    Account = repo.Owner.Login,
                            //                    Name = repo.Name,
                            //                    CreatedAt = repo.CreatedAt.UtcDateTime
                            //                });
                            //            }
                            //        }
                            //        if (repoListAll.Count == 0)
                            //        {
                            //            string replyMessageText = $"No repository available.";
                            //            //await botClient.SendTextMessageAsync(chatId: chatId, replyToMessageId: messageId, text: replyMessageText, cancellationToken: cancellationToken);
                            //            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            //            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            //        }
                            //        //else if (githubAccountList.Count == 1)
                            //        //{
                            //        //    string replyMessageText = $"{repoListAll.Count} repositories:";
                            //        //    foreach (var repo in repoListAll)
                            //        //    {
                            //        //        replyMessageText += "\n\n" + repo.Name;
                            //        //    }
                            //        //    await botClient.SendTextMessageAsync(chatId: chatId,text: replyMessageText, cancellationToken: cancellationToken);
                            //        //}
                            //        else
                            //        {
                            //            string replyMessageText = $"{repoListAll.Count} repositories in {githubAccountList.Count} users:\n";
                            //            int maxIndex = Math.Min(repoListAll.Count, 100);
                            //            for (int i = 0; i < maxIndex; i++)
                            //            {
                            //                var repo = repoListAll[i];
                            //                replyMessageText += $"\n{repo.Account}/{repo.Name}";
                            //            }
                            //            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            //            //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            //        }
                            //    }
                            //    else
                            //    {
                            //        string replyMessageText = $"Permission denied.";
                            //        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                            //        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                            //    }
                            //    LastCommand.Remove(senderUsername!);
                            //    break;
                            case "/list_added_repo":
                                if (isSuperAdmin)
                                {
                                    var githubRepoList = githubRepoDao.SelectAll();
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
                                LastCommand.Remove(senderUsername!);
                                break;
                            case "/add_repo":
                                if (isSuperAdmin)
                                {
                                    string replyMessageText = "Type [Github Username]/[Repository Name] to add or type 'exit' to cancel.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand[senderUsername!] = command;
                                }
                                else
                                {
                                    string replyMessageText = $"Permission denied.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand.Remove(senderUsername!);
                                }
                                break;
                            case "/remove_repo":
                                if (isSuperAdmin)
                                {
                                    string replyMessageText = "Type [Github Username]/[Repository Name] to remove or type 'exit' to cancel.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand[senderUsername!] = command;
                                }
                                else
                                {
                                    string replyMessageText = $"Permission denied.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand.Remove(senderUsername!);
                                }
                                break;
                            case "/list_auth_user":
                                if (isSuperAdmin)
                                {
                                    var accountList = telegramChatDao.SelectByRole(TelegramChat.ROLE_AUTH);
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
                                LastCommand.Remove(senderUsername!);
                                break;
                            case "/add_auth_user":
                                if (isSuperAdmin)
                                {
                                    string replyMessageText = "Type Telegram Username to add or type 'exit' to cancel.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand[senderUsername!] = command;
                                }
                                else
                                {
                                    string replyMessageText = $"Permission denied.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand.Remove(senderUsername!);
                                }
                                break;
                            case "/remove_auth_user":
                                if (isSuperAdmin)
                                {
                                    string replyMessageText = "Type Telegram Username to remove or type 'exit' to cancel.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand[senderUsername!] = command;
                                }
                                else
                                {
                                    string replyMessageText = $"Permission denied.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand.Remove(senderUsername!);
                                }
                                break;
                            case "/list_notify_user":
                                if (isSuperAdmin)
                                {
                                    var accountList = telegramChatDao.SelectByRole(TelegramChat.ROLE_NOTIFY);
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
                                LastCommand.Remove(senderUsername!);
                                break;
                            case "/add_notify_user":
                                if (isSuperAdmin)
                                {
                                    string replyMessageText = "Type Telegram Username to add or type 'exit' to cancel.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand[senderUsername!] = command;
                                }
                                else
                                {
                                    string replyMessageText = $"Permission denied.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand.Remove(senderUsername!);
                                }
                                break;
                            case "/remove_notify_user":
                                if (isSuperAdmin)
                                {
                                    string replyMessageText = "Type Telegram Username to remove or type 'exit' to cancel.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand[senderUsername!] = command;
                                }
                                else
                                {
                                    string replyMessageText = $"Permission denied.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand.Remove(senderUsername!);
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
                                                    offset = Convert.ToInt32(array[2].Trim());
                                                    if (array.Length >= 4)
                                                    {
                                                        limit = Convert.ToInt32(array[3].Trim());
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger.WriteLine(ex.ToString(), ConsoleColor.Red);
                                            }
                                            var commitList = githubCommitDao.SelectLast(repoUsername, repoName, offset, limit);
                                            if (commitList.Count >= 5)
                                            {
                                                string replyMessageText = $"Recent commits on {repoUsername}/{repoName}\n";
                                                foreach (var a in commitList)
                                                {
                                                    replyMessageText += $"\n{a.CommittedAt:yyyy-MM-dd HH:mm}    {a.Committer}    <a href=\"{a.Url}\">{a.Sha![..6]}</a>\n<pre>    {a.Message}</pre>";
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
                                                    replyMessageText += $"\n{a.CommittedAt:yyyy-MM-dd HH:mm}    {a.Committer}    <a href=\"{a.Url}\">{a.Sha![..6]}</a>\n<pre>    {a.Message}</pre>";
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
                                        LastCommand.Remove(senderUsername!);
                                    }
                                    else
                                    {
                                        string replyMessageText = "Type [Github Username]/[Repository Name] to list most commits or type 'exit' to cancel.";
                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                        //Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                        LastCommand[senderUsername!] = command;
                                    }
                                }
                                else
                                {
                                    string replyMessageText = $"Permission denied.";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                    LastCommand.Remove(senderUsername!);
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
                                    var commitList = githubCommitDao.SelectLast(offset, limit);
                                    if (commitList.Count >= 5)
                                    {
                                        string replyMessageText = $"Recent commits:\n";
                                        foreach (var a in commitList)
                                        {
                                            replyMessageText += $"\n{a.CommittedAt:yyyy-MM-dd HH:mm}    {a.Account}/{a.Repo}    {a.Committer}    <a href=\"{a.Url}\">{a.Sha![..6]}</a>\n<pre>    {a.Message}</pre>";
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
                                            replyMessageText += $"\n{a.CommittedAt:yyyy-MM-dd HH:mm}    {a.Account}/{a.Repo}    {a.Committer}    <a href=\"{a.Url}\">{a.Sha![..6]}</a>\n<pre>    {a.Message}</pre>";
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
                                LastCommand.Remove(senderUsername!);
                                break;
                            default:
                                {
                                    string replyMessageText = $"Unknown command: {command}";
                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                }
                                LastCommand.Remove(senderUsername!);
                                break;
                        }
                    }
                    else if (LastCommand.ContainsKey(senderUsername!))
                    {
                        if (receivedMessageText == "exit" || receivedMessageText == "/exit")
                            LastCommand.Remove(senderUsername!);
                        else
                            switch (LastCommand[senderUsername!])
                            {
                                case "/add_github_account":
                                    {
                                        string token = receivedMessageText.Trim();
                                        var account = githubAccountDao.SelectByToken(token);
                                        if (account == null)
                                        {
                                            Credentials tokenAuth = new(token);
                                            GitHubClient githubClient = new(new ProductHeaderValue(token.GetHashCode().ToString("X")))
                                            {
                                                Credentials = tokenAuth
                                            };
                                            try
                                            {
                                                var githubUsername = githubClient.User.Current().Result.Login;
                                                githubAccountDao.Insert(new GithubAccount
                                                {
                                                    Name = githubUsername,
                                                    Token = token,
                                                });
                                                string replyMessageText = $"Successfully added \'{githubUsername}\'.";
                                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                                LastCommand.Remove(senderUsername!);
                                            }
                                            catch (Exception ex)
                                            {
                                                if (ex is MySql.Data.MySqlClient.MySqlException mySqlException && mySqlException.Number == 1062)
                                                {
                                                    string replyMessageText = $"Already added.";
                                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                                    LastCommand.Remove(senderUsername!);
                                                }
                                                else
                                                {
                                                    string replyMessageText = $"Failed to add github account: {(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}";
                                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <ERROR>  failed to add github account.\n\t{(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red);
                                                    LastCommand.Remove(senderUsername!);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            string replyMessageText = $"Already added.";
                                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                        }
                                        LastCommand.Remove(senderUsername!);
                                    }
                                    break;
                                case "/remove_github_account":
                                    {
                                        string name = receivedMessageText;
                                        int result = githubAccountDao.Delete(name);
                                        if (result == 0)
                                        {
                                            string replyMessageText = $"Github account '{name}' does not exist.";
                                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                        }
                                        else
                                        {
                                            string replyMessageText = $"Successfully removed.";
                                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                        }
                                        LastCommand.Remove(senderUsername!);
                                    }
                                    break;
                                case "/add_repo":
                                    {
                                        string[] paramArray = receivedMessageText.Split('/');
                                        if (paramArray.Length >= 2)
                                        {
                                            string repoUsername = paramArray[0].Trim();
                                            string repoName = paramArray[1].Trim();
                                            var account = githubAccountDao.SelectByName(repoUsername);
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
                                                    githubRepoDao.Insert(new GithubRepo
                                                    {
                                                        Account = repoUsername,
                                                        Name = repoName,
                                                        //AddedBy=
                                                    });
                                                    string replyMessageText = $"Successfully added.";
                                                    await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                                    Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                                    LastCommand.Remove(senderUsername!);
                                                }
                                                catch (Exception ex)
                                                {
                                                    if (ex is MySql.Data.MySqlClient.MySqlException mySqlException && mySqlException.Number == 1062)
                                                    {
                                                        string replyMessageText = $"Repository '{repoUsername}/{repoName}' already added.";
                                                        await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                                        Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                                        LastCommand.Remove(senderUsername!);
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
                                                        LastCommand.Remove(senderUsername!);
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
                                            int result = githubRepoDao.Delete(repoUsername, repoName);
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
                                            LastCommand.Remove(senderUsername!);
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
                                            telegramChatDao.Insert(new TelegramChat
                                            {
                                                Name = value,
                                                Role = TelegramChat.ROLE_AUTH
                                            });
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
                                    LastCommand.Remove(senderUsername!);
                                    break;
                                case "/remove_auth_user":
                                    {
                                        string value = receivedMessageText.Trim();
                                        var user = telegramChatDao.SelectByName(value);
                                        if (user == null)
                                        {
                                            string replyMessageText = $"Telegram User '{value}' does not exist.";
                                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                        }
                                        else if (user.Role != TelegramChat.ROLE_AUTH)
                                        {
                                            string replyMessageText = $"'{value}' is not auth user.";
                                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                        }
                                        else
                                        {
                                            int result = telegramChatDao.DeleteByName(value);
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
                                    LastCommand.Remove(senderUsername!);
                                    break;
                                case "/add_notify_user":
                                    {
                                        string value = receivedMessageText.Trim();
                                        try
                                        {
                                            telegramChatDao.Insert(new TelegramChat
                                            {
                                                Name = value,
                                                Role = TelegramChat.ROLE_NOTIFY
                                            });
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
                                    LastCommand.Remove(senderUsername!);
                                    break;
                                case "/remove_notify_user":
                                    {
                                        string value = receivedMessageText.Trim();
                                        var user = telegramChatDao.SelectByName(value);
                                        if (user == null)
                                        {
                                            string replyMessageText = $"Telegram User '{value}' does not exist.";
                                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                        }
                                        else if (user.Role != TelegramChat.ROLE_NOTIFY)
                                        {
                                            string replyMessageText = $"'{value}' is not notify user.";
                                            await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  replied: \"{replyMessageText}\"", ConsoleColor.DarkGray);
                                        }
                                        else
                                        {
                                            int result = telegramChatDao.DeleteByName(value);
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
                                        LastCommand.Remove(senderUsername!);
                                    }
                                    break;
                                case "/view_commits_by_repo":
                                    {
                                        string[] paramArray = receivedMessageText.Split('/');
                                        if (paramArray.Length == 2)
                                        {
                                            int offset = 0, limit = 5;
                                            string repoUsername = paramArray[0].Trim();
                                            string repoName = paramArray[1].Trim();
                                            var commitList = githubCommitDao.SelectLast(repoUsername, repoName, 0, 5);
                                            if (commitList.Count >= 5)
                                            {
                                                string replyMessageText = $"Recent commits on {repoUsername}/{repoName}\n";
                                                foreach (var a in commitList)
                                                {
                                                    replyMessageText += $"\n{a.CommittedAt:yyyy-MM-dd HH:mm}    {a.Committer}    {a.Sha![..6]}    <a href=\"{a.Url}\">{a.Sha![..6]}</a>\n<pre>    {a.Message}</pre>";
                                                }
                                                InlineKeyboardMarkup replyMarkup = new(new[]
                                                {
                                            new []
                                            {
                                                InlineKeyboardButton.WithCallbackData(text: "More...", callbackData: $"/view_commits_by_repo {repoUsername}/{repoName} {offset+limit}"),
                                            },
                                        });
                                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, parseMode: ParseMode.Html, disableNotification: true, disableWebPagePreview: true, replyMarkup: replyMarkup, cancellationToken: cancellationToken);
                                            }
                                            else if (commitList.Count > 0)
                                            {
                                                string replyMessageText = $"";
                                                foreach (var a in commitList)
                                                {
                                                    replyMessageText += $"\n{a.CommittedAt:yyyy-MM-dd HH:mm}    {a.Committer}    {a.Sha![..6]}    <a href=\"{a.Url}\">{a.Sha![..6]}</a>\n<pre>    {a.Message}</pre>";
                                                }
                                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, parseMode: ParseMode.Html, disableNotification: true, disableWebPagePreview: true, cancellationToken: cancellationToken);
                                            }
                                            else
                                            {
                                                string replyMessageText = "No commit.";
                                                await botClient.SendTextMessageAsync(chatId: chatId, text: replyMessageText, cancellationToken: cancellationToken);
                                            }
                                            LastCommand.Remove(senderUsername!);
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
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {(ex.InnerException == null ? ex.Message : ex.InnerException.Message)}", ConsoleColor.Red, false);
                Logger.WriteFile($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  <ERROR>  {ex}");
            }
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
            using TelegramChatDao telegramChatDao = new();
            var chatList = telegramChatDao.SelectAll();
            foreach (var chat in chatList)
            {
                if (chat.Id != 0)
                {
                    Client!.SendTextMessageAsync(chatId: chat.Id, text: text, disableWebPagePreview: true, parseMode: parseMode);
                }
                else
                {
                    string target = chat.Name!;
                    if (!target.StartsWith("@")) target = "@" + target;
                    Client!.SendTextMessageAsync(chatId: target, text: text, disableWebPagePreview: true, parseMode: parseMode);
                }
            }
            Logger.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]  Message sent to {chatList.Count} chats: {text}", ConsoleColor.DarkGray);
        }

    }
}
