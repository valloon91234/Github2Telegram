// See https://aka.ms/new-console-template for more information

using Github2Telegram;

AppHelper.QuickEditMode(false);
//Console.BufferHeight = Int16.MaxValue - 1;
//AppHelper.MoveWindow(AppHelper.GetConsoleWindow(), 24, 0, 1080, 280, true);
AppHelper.FixCulture();

// See https://github.com/tonerdo/dotnet-env
DotNetEnv.Env.Load("config.env");
TelegramClient.Init();
GithubClient.Run();

Console.WriteLine("Press any key to exit...");
Console.Read();
