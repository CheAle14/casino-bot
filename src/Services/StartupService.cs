using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using static DiscordBot.Program;
using System.IO;
using Newtonsoft.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using static DiscordBot.Modules.MainModule.AuditCommands;
using DiscordBot.Modules;
using Casino;
using System.Threading;
using Discord.Rest;

namespace DiscordBot
{

    public partial class StartupService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;

        public Dictionary<ulong, List<Discord.Rest.RestInviteMetadata>> GuildInvites = new Dictionary<ulong, List<Discord.Rest.RestInviteMetadata>>();
        public static PreviousMessages PastMessages;
        static IServiceProvider ServiceProvider;

        // DiscordSocketClient, CommandService, and IConfigurationRoot are injected automatically from the IServiceProvider
        public StartupService(
            DiscordSocketClient discord,
            CommandService commands,
            IConfigurationRoot config)
        {
            _config = config;
            _discord = discord;
            _commands = commands;
            Program._commands = commands;
        }


        // READY:
        public async Task Ready()
        {
            CasinoGuild = _discord.GetGuild(402839443813302272);
            StatusGuild = _discord.GetGuild(420240046428258304);
            TheGrandCodingGuild = _discord.GetGuild(365230804734967840);
            LoggingGuild = _discord.GetGuild(508229402325286912);
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), ServiceProvider);     // Load commands and modules into the command service

            Services.GithubService.Client.RequestMade += GithubClient_RequestMade;

            foreach (var g in new SocketGuild[] { CasinoGuild, StatusGuild, TheGrandCodingGuild})
            {
                try
                {
                    string name = g.Name.Replace("Casino", "");
                    var usr = g.GetUser(_discord.CurrentUser.Id);
                    bool isSpecial = DateTime.Now.DayOfYear >= 355 && DateTime.Now.DayOfYear <= 359;
                    if (usr.Nickname != name || isSpecial)
                    {
                        await usr.ModifyAsync(x =>
                        {
                            if(isSpecial)
                            {
                                x.Nickname = $"{EM_RANDOM_CHRISTMAS} Merry Chistmas! {EM_RANDOM_CHRISTMAS}";
                            }
                            else
                            {
                                x.Nickname = name;
                            }
                        });
                    }
                } catch { }
            }

            await _discord.SetGameAsync($"{BOT_PREFIX}help - {DateTime.Now.ToShortTimeString()}");

            /*if(!BOT_DEBUG)
            {
                await BotModule.ActuallyRemoveAllCommands(false, C_LOGS_OTHER_MISC);
            }*/

            try
            {
                PastMessages = new PreviousMessages();
            }
            catch (Exception ex)
            {
                LogMessage bleh = new LogMessage(LogSeverity.Error, "PastMsgs", "", ex);
                LogMsg(bleh);
            }

            // Set up hour timer
            try
            {
                DateTime dueTime = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, DateTime.Now.Hour + 1, 0, 0);
                TimeSpan timeRemaining = dueTime.Subtract(DateTime.Now);
                HourTimer.Interval = 50;
                HourTimer.Elapsed += HourElapsed;
                HourTimer.Enabled = true;
            }
            catch (Exception ex)
            {
                LogMessage er = new LogMessage(LogSeverity.Error, "HourTimer", "", ex);
                LogMsg(er);
            }


            try
            {
                MasterList.MainMasterList.Run();
            }
            catch (Exception ex)
            {
                LogMsg("Error in Masterlist: " + ex.ToString(), LogSeverity.Error, "Masl");
                try
                {
                    await TheGrandCodingGuild.GetTextChannel(365233803217731584).SendMessageAsync($"Masterlist failed to start\nMasterlist will be unavailable.");
                } catch { }
                try
                {
                    var modl = _commands.Modules.FirstOrDefault(x => x.Name == "Masterlist Commands");
                    var newDisable = new CommandDisabled("Masterlist failed to start", modl);
                    DisabledCommands.Add(newDisable);
                } catch { }
            }

            try
            {
                Casino.FourAcesCasino.Initialise();
            }
            catch (Exception ex)
            {
                LogMsg(ex.ToString(), LogSeverity.Critical, "StrCas");
                LogMsg("", LogSeverity.Critical, "StrCas");
                LogMsg("Unable to continue since the Casino failed to initialise",  LogSeverity.Critical, "StrCas");
                try
                {
#if DEBUG
#else
                    await C_COUNCIL.SendMessageAsync($"Bot failed to start\nError message has been sent to Alex");
#endif
                    EmbedBuilder builder = new EmbedBuilder()
                    {
                        Title = "Erroed"
                    };
                    builder.AddField(x =>
                    {
                        x.Name = "Message";
                        x.Value = ex.Message;
                        x.IsInline = true;
                    });
                    builder.AddField(x =>
                    {
                        x.Name = ex.InnerException == null ? "Source" : "Inner Exception";
                        x.Value = ex.InnerException == null ? ex.Source : ex.InnerException.ToString();
                        x.IsInline = ex.InnerException == null ? true : false;
                    });
                    string errMsg = ex.ToString();
                    if(errMsg.Length > 1499 + " ...".Length)
                    {
                        errMsg = errMsg.Substring(0, 1499) + " ...";
                    }
                    builder.AddField(x =>
                    {
                        x.IsInline = false;
                        x.Name = "Full Error";
                        x.Value = "```" + errMsg.Replace("`", "'") + "```";
                    });
                    await U_BOB123.SendMessageAsync("Bot failure", false, builder.Build());
                    await C_LOGS_FAC_CHIP.SendMessageAsync("Unable to start bot due to Casino failure", false, builder.Build());
                }
                catch { }
                if (BOT_DEBUG)
                {
                    Console.ReadLine();
                }
                Environment.Exit(1);

            }
            var cp = new ChatProgramHandle();

            try
            {
                if (Program.Storage.LastDeletedBulkMessages != DateTime.Now.DayOfYear)
                {
                    Program.Storage.LastDeletedBulkMessages = DateTime.Now.DayOfYear;
                    await DeleteBulkMessages();
                }
            }
            catch (Exception ex)
            {
                LogMessage er = new LogMessage(LogSeverity.Critical, "NukeHWK", "", ex);
                LogMsg(er);
            }

            // Finished Loading all info
            LogMsg("Finished Ready() Function - Info Loaded.");
        }

        private void GithubClient_RequestMade(object sender, GithubDLL.RESTRequestEventArgs e)
        {
            bool success = ((int)e.ResponseCode >= 200) && ((int)e.ResponseCode <= 299);
            LogSeverity sev = success ? LogSeverity.Debug : LogSeverity.Warning;
            LogMsg(e.ToString(), sev, "GithubREST");
        }

        public async Task StartAsync(IServiceProvider provider)
        {
            string discordToken = _config["tokens:discord"];     // Get the discord token from the config file
            if (string.IsNullOrWhiteSpace(discordToken))
                throw new Exception("Please enter your bot's token into the `_configuration.json` file found in the applications root directory.");
            try
            {
                await _discord.LoginAsync(TokenType.Bot, discordToken);     // Login to discord
                await _discord.StartAsync();                                // Connect to the websocket
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                LogMsg("HTTP exception upon startup: " + ex.Message);
                File.WriteAllText("errlog.txt", ex.ToString());
                ReTryConnection();
            }
            // Add a LOT of event listeners.
            // Some are added twice (eg MessageDeleted, UserJoin) because
            // I want to do different things with them
            _discord.Ready += Ready;
            _discord.Disconnected += _discord_Disconnected;

            // User Logs
            _discord.UserJoined += UserJoin;
            _discord.UserJoined += UserJoinCheckInvites;
            _discord.UserJoined += _discord_UserJoined; // embd
            _discord.UserLeft += UserLeft;
            _discord.UserLeft += _discord_UserLeft;
            _discord.UserBanned += _discord_UserBanned;
            _discord.UserUnbanned += _discord_UserUnbanned;
            _discord.UserUpdated += _discord_UserUpdated; 
            // Guild member
            _discord.GuildMemberUpdated += _discord_GuildMemberUpdated; // embd
            _discord.GuildMemberUpdated += CasinoUserUpdatedCheckNickname;
            // Messages
            _discord.ReactionAdded += ReactionMessageHandler;
            _discord.MessageReceived += MessageRecieved;
            _discord.ReactionAdded += _discord_ReactionAdded;
            _discord.MessageUpdated += MessageUpdated;
            _discord.MessageUpdated += _discord_MessageUpdated;
            _discord.MessageDeleted += MessageDeleted;
            _discord.MessageDeleted += _discord_MessageDeleted;
            // Channels
            _discord.ChannelUpdated += _discord_ChannelUpdated; // embd
            _discord.ChannelCreated += _discord_ChannelCreated; // embd
            _discord.ChannelDestroyed += _discord_ChannelDestroyed; // embd
            // Roles
            _discord.RoleUpdated += _discord_RoleUpdated; // embd
            _discord.RoleCreated += _discord_RoleCreated; // embd
            _discord.RoleDeleted += _discord_RoleDeleted; // embd
            // Guilds
            _discord.GuildUpdated += _discord_GuildUpdated;

            Program._discord_ = _discord;
            // add typereaders, so you can have a function eg:
            // public async Task DoSomeCommand(SocketGuildUser user) { .... }
            //      or
            // public async Task DoAnotherCommand(CasinoMember member) { .... }
            // and you don't have to parse a string for *every* command

            _commands.AddTypeReader(typeof(SocketGuildUser), new SocketGuildUserTypeReader()); 
            _commands.AddTypeReader(typeof(CasinoMember), new CasinoMemberTypeReader());
            ServiceProvider = provider;
        }

        public const string PermissionsWebsite = "https://dpermcalc.neocities.org/#"; // website displays discord's permissions
        public static string FormatPerm(ulong permValue)
        {
            return FormatPerm(permValue.ToString());
        }
        public static string FormatPerm(string permValue)
        {
            return $"{PermissionsWebsite}{permValue}";
        }
        public static string FormatPerm(object permValue)
        {
            return FormatPerm(permValue.ToString());
        }

        [Flags]
        [Obsolete] // not gonna remove thsi because it might be helpful later.
        enum Perms
        {
            CreateInstantInvite = 1,
            KickMembers = 2,
            BanMembers = 4,
            Administrator = 8,
            ManageChannels = 16,
            ManageGuild = 32,
            AddReactions = 64,
            ViewAuditLog = 128,
            ViewChannel = 0x400,
            SendMessages = 0x800,
            SendTTSMessage = 0x1000,
            ManageMessages = 0x2000,
            EmbedLinks = 0x4000,
            AttachFiles = 0x8000,
            ReadMessageHistory = 0x10000,
            MentionEveryone = 0x20000,
            UseExternalEmojis = 0x40000,
            Connect = 0x100000,
            Speak = 0x200000,
            ChangeNickname = 0x4000000,
            ManageNicknames = 0x8000000,
            ManageRoles = 0x10000000,
            ManageWebHooks = 0x20000000,
            ManageEmojis = 0x40000000
        }
        
        private string GetUrlFromSFl(ulong snowflake)
        {
            return FormatPerm(snowflake);
        }
        private string FormatOverwritePerms(OverwritePermissions permissions)
        {
            string msg = "";
            msg += $"[Allow: {permissions.AllowValue}]({GetUrlFromSFl(permissions.AllowValue)})";
            msg += $" | [Deny: {permissions.DenyValue}]({GetUrlFromSFl(permissions.DenyValue)})";
            return msg;
        }
    }
}
