using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Addons.Interactive;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Data;
using System.IO;
using DiscordBot.Modules;
using System.Runtime.InteropServices;
using System.Threading;
using System.Drawing;
using Newtonsoft.Json;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using System.Net;


namespace DiscordBot
{
    public partial class Program
    {
        public static int RoundToChip(double value)
        {
            return ((int)Math.Round(value / 25)) * 25;
        }


        public static DiscordSocketClient _discord_;
        public static CommandService _commands;

        public static IConfigurationRoot Configuration { get; set; }

        public static void Main(string[] args)
        {
            Console.WriteLine("Program started.");
#if DEBUG
            Console.WriteLine("Running debug - windows");
#else
            Console.WriteLine("Running release - linux");
#endif
            Console.WriteLine($"Version: {MasterList.MainMasterList._internal_masterlist_version}.{BOT_MAJOR}.{BOT_MINOR}");
            Console.WriteLine(MAIN_PATH);
            new Program().MainAsync(args).GetAwaiter().GetResult();
        }

        public async Task MainAsync(string[] args)
        {
#if DEBUG
            handler = new ConsoleEventDelegate(ConsoleEventCallback);
            SetConsoleCtrlHandler(handler, true);
#endif
            System.AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Directory.SetCurrentDirectory(MAIN_PATH);
#if DEBUG
            if (!System.IO.File.Exists("lock.txt"))
            {
                File.Create("lock.txt");
            }
            else
            {
                DateTime lastModified = File.GetLastWriteTime("lock.txt");
                TimeSpan diff = DateTime.Now - lastModified;
                if (diff.TotalHours > 5)
                {
                    File.WriteAllText("lock.txt", "");
                }
                else
                {
                    CLOSE_APPLICATION(32);
                }
            }
#endif
            if (BOT_DEBUG)
            {
                LogMsg("WARNING - BOT IS RUNNING AS DEBUG MODE");
            }
            LogMsg("Running version: " + Assembly.GetEntryAssembly().GetName().Version.ToString());
            var builder = new ConfigurationBuilder();
            try
            {
                builder.SetBasePath(MAIN_PATH);      // Specify the default location for the config file
                builder.AddJsonFile("_configuration.json");        // Add this (json encoded) file to the configuration
                Configuration = builder.Build();                // Build the configuration
            }
            catch (Exception ex)
            {
                LogMsg("===== ERRORED ======\nUnable to load configuration: " + ex.ToString() + "\n===== ERRORED ======");
                throw;
            }
            if (DEBUG_DOWNLOAD_SAVE_FILE)
            { // Get & load save file via SFTP
                GetSaveFile();
            }
            await RunAsync();
        }

        public async Task RunAsync()
        {

            var services = new ServiceCollection();             // Create a new instance of a service collection
            await ConfigureServices(services);

            var provider = services.BuildServiceProvider();     // Build the service provider
            provider.GetRequiredService<LoggingService>();      // Start the logging service
            provider.GetRequiredService<CommandHandler>(); 		// Start the command handler service
            provider.GetRequiredService<Services.GithubService>();
            provider.GetRequiredService<Services.PasswordService>();
            provider.GetRequiredService<Permissions.PermissionsService>();
            provider.GetRequiredService<Services.MutingService>();
            Preferences.LoadSettings();
            await provider.GetRequiredService<StartupService>().StartAsync(provider);       // Start the startup service
            ReadConsoleInput();
        }



        private Task ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
            {                                       // Add discord to the collection
                LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
                MessageCacheSize = 1000             // Cache 1,000 messages per channel
            }))
            .AddSingleton(new CommandService(new CommandServiceConfig
            {                                       // Add the command service to the collection
                LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
                DefaultRunMode = RunMode.Async,     // Force all commands to run async by default
                CaseSensitiveCommands = false       // Ignore case when executing commands
            }))
            .AddSingleton<StartupService>()         // Add startupservice to the collection
            .AddSingleton<LoggingService>()         // Add loggingservice to the collection
            .AddSingleton<CommandHandler>()
            .AddSingleton<Services.GithubService>()
            .AddSingleton<Services.PasswordService>()
            .AddSingleton<Permissions.PermissionsService>()
            .AddSingleton<Services.MutingService>()
            .AddSingleton<Random>()                 // Add random to the collection
            .AddSingleton(Configuration)           // Add the configuration to the collection
            .AddSingleton<InteractiveService>();
            return Task.CompletedTask;
        }
    }
}
