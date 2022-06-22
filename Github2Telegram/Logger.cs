using System;
using System.IO;
using System.Threading;

/**
 * @author Valloon Present
 * @version 2022-06-08
 */
namespace Github2Telegram
{
    public class Logger
    {
        public static readonly string LOG_DIRECTORY = "log";

        public static void WriteLine(string? text = null, ConsoleColor color = ConsoleColor.White, bool writeFile = true)
        {
            if (text == null)
            {
                Console.WriteLine();
                return;
            }
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = ConsoleColor.White;
            if (writeFile) WriteFile(text);
        }

        public static void WriteFile(string text)
        {
            try
            {
                DirectoryInfo logDirectoryInfo = new(LOG_DIRECTORY);
                if (!logDirectoryInfo.Exists) logDirectoryInfo.Create();
                string logFilename = Path.Combine(LOG_DIRECTORY, $"{DateTime.Now:yyyy-MM-dd}.log");
                using var streamWriter = new StreamWriter(logFilename, true);
                streamWriter.WriteLine(text);
            }
            catch (Exception ex)
            {
                WriteLine("Cannot write log file : " + ex.Message, ConsoleColor.Red, false);
            }
        }

        public static void WriteWait(string text, int seconds, int intervalSeconds = 1, ConsoleColor color = ConsoleColor.DarkGray)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            for (int i = 0; i < seconds; i += intervalSeconds)
            {
                Console.Write('.');
                Thread.Sleep(intervalSeconds * 1000);
            }
            Thread.Sleep((seconds % intervalSeconds) * 1000);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine();
        }

    }
}