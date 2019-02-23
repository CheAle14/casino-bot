using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using static DiscordBot.Program;

namespace DiscordBot
{
    public class LoggingService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
#if DEBUG
        private static string _logDirectory = Program.MAIN_PATH + @"Logs";
#else
        private static string _logDirectory = Program.MAIN_PATH + @"Logs";
#endif
        private static string _logFile => Path.Combine(_logDirectory, $"{DateTime.UtcNow.ToString("yyyy-MM-dd")}.txt");

        // DiscordSocketClient and CommandService are injected automatically from the IServiceProvider
        public LoggingService(DiscordSocketClient discord, CommandService commands)
        {
            _discord = discord;
            _commands = commands;
            
            _discord.Log += OnLogAsync;
            _commands.Log += OnLogAsync;
        }

        private static string longest = LogSeverity.Warning.ToString() + 1;

        public static Object _logLock = new object();

        public class LogUserMessage {
            public ulong MessageID;
            public ulong ChannelID;
            public IUser Author;

            public string AuthorName;
            public string AuthorDiscrim;
            public ulong AuthorID;

            public LogUserMessage(IUser author)
            {
                AuthorName = author.Username;
                AuthorDiscrim = author.Discriminator;
                AuthorID = author.Id;
            }
            public LogUserMessage() { }
            public string Content;
            public DateTime SentAt; // not used in logging initially, but is used when read.
            public override string ToString()
            {
                return $"{ChannelID}/{MessageID}: {Author?.Username ?? AuthorName}#{Author?.Discriminator ?? AuthorDiscrim}-({Author?.Id ?? AuthorID}): {Content.Replace("\r\n", "\n")}";
            }
        }


        public static Task AlternateMessageLog(LogUserMessage msg)
        {
            lock (_logLock)
            {
                if (!Directory.Exists(JOINPATH(MAIN_PATH, "MsgLogs")))     // Create the log directory if it doesn't exist
                    Directory.CreateDirectory(JOINPATH(MAIN_PATH, "MsgLogs"));
                if (!File.Exists(JOINPATH(MAIN_PATH, "MsgLogs", $"{DateTime.Now.ToString("yyyy-MM-dd")}.txt")))               // Create today's log file if it doesn't exist
                    File.Create(JOINPATH(MAIN_PATH, "MsgLogs", $"{DateTime.Now.ToString("yyyy-MM-dd")}.txt")).Dispose();

                int startLength = "365230804734967842/495605541939314713: ".Length;
                string logText = $"{DateTime.Now.ToString("hh:mm:ss.fff").Replace(":", ";")} {msg.ToString()}";
                File.AppendAllText(JOINPATH(MAIN_PATH, "MsgLogs", $"{DateTime.Now.ToString("yyyy-MM-dd")}.txt"), logText + "\r\n");     // Write the log text to a file
                logText = logText.Replace("\n", "\n    ..." + (new string(' ', startLength)));
#if DEBUG
                        Console.ForegroundColor = ConsoleColor.DarkCyan;
#else
                        Console.ForegroundColor = ConsoleColor.Blue;
#endif
                return Console.Out.WriteLineAsync(logText);       // Write the log text to the console
            }
        }

        public static Task OnLogAsync(LogMessage msg)
        {
            lock(_logLock)
            {
                if (!Directory.Exists(_logDirectory))     // Create the log directory if it doesn't exist
                    Directory.CreateDirectory(_logDirectory);
                if (!File.Exists(_logFile))               // Create today's log file if it doesn't exist
                    File.Create(_logFile).Dispose();

                int spaces = longest.Length;
                spaces -= msg.Severity.ToString().Length;

                int startLength = "04:37:08.[Info] App: ".Length;

                string spaceGap = String.Concat(Enumerable.Repeat(" ", spaces));
                //for(int i = 0; i < spaces; i++) { spaceGap += " "; }

                string logText = $"{DateTime.Now.ToString("hh:mm:ss.fff")}{spaceGap}[{msg.Severity}] {msg.Source}: {msg.Exception?.ToString() ?? msg.Message}";
                File.AppendAllText(_logFile, logText + "\r\n");     // Write the log text to a file
                logText = logText.Replace("\n", "\n    ..." + (new string(' ', startLength)));
                switch (msg.Severity)
                {
                    case LogSeverity.Critical:
                    case LogSeverity.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case LogSeverity.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case LogSeverity.Info:
#if DEBUG
                        Console.ForegroundColor = ConsoleColor.Blue;
#else
                        Console.ForegroundColor = ConsoleColor.DarkCyan;
#endif
                        break;
                    case LogSeverity.Verbose:
                    case LogSeverity.Debug:
                        if(BOT_DEBUG)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkGray;
                        } else
                        {
                            Console.ForegroundColor = ConsoleColor.Gray;
                        }
                        break;
                }
                return Console.Out.WriteLineAsync(logText);       // Write the log text to the console
            }
        }
    }
}
