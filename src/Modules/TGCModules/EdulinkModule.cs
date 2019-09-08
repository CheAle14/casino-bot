using Discord.Commands;
using Discord.Addons.Interactive;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EduLinkRPC.Classes;
using EduLinkRPC.Addons.Discord;
using Discord;
using Newtonsoft.Json;
using Discord.WebSocket;
using System.Linq;
using Newtonsoft.Json.Linq;
using DiscordBot.Extensions;
using static DiscordBot.Program;
using static DiscordBot.Extensions.EdulinkExtensions;

namespace DiscordBot.Modules
{
    using DiscordBot.Classes;
    using EduLinkRPC;
    using System.Text.RegularExpressions;

    [Group("edulink"), Name(Name.EdulinkModule)]
    public class EdulinkModule : BotInteractiveBase<SocketCommandContext>
    {
        public static IRole Separator = Program.TheGrandCodingGuild.GetRole(559765210945290271);

        public static ICategoryChannel HwkCategory = Program.TheGrandCodingGuild.GetChannel(560004545514831872) as ICategoryChannel;

        public static ITextChannel GetChannel(string subject)
        {
            var allChannels = Program.TheGrandCodingGuild.TextChannels.Where(x => x.CategoryId.HasValue && x.CategoryId.Value == HwkCategory.Id);
            subject = subject.Replace("R.E.", "RE");
            // any other things

            // end
            subject = subject.ToLower();
            subject = subject.Replace(" ", "-"); // see #15
            var channel = allChannels.FirstOrDefault(x => x.Name == subject);
            if(channel == null)
            {
                var rest = Program.TheGrandCodingGuild.CreateTextChannelAsync(subject, x =>
                {
                    x.CategoryId = HwkCategory.Id;
                }).Result;
                return rest.GetTextChannel();
            }
            return channel;
        }

        public static Dictionary<int, ClassHomework> AllHomework = new Dictionary<int, ClassHomework>();

        public static List<DiscordHwkUser> Users = new List<DiscordHwkUser>();

        public static Dictionary<string, Class> Classes = new Dictionary<string, Class>();

        public const string HomeworkRegex = @"(?<=homework\/|hwk\/)[0-9]*";

        public static async Task<DiscordHwkUser> GetUser(ICommandContext context, string input)
        {
            var typeReader = new DiscordBot.HwkUserTypeReader();
            var result = await typeReader.ReadAsync(context, input, services);
            if(result.IsSuccess)
            {
                return result.Values.FirstOrDefault().Value as DiscordHwkUser;
            }
            return null;
        }

        static DateTime lastSentRequests = new DateTime(2019, 01, 01); // so that it is always out of date, so first check gets through

        [Obsolete("Uses Services.HomeworkService for any further")]
        public static void RefreshHomework(bool overrideTimeout = false)
        {
            TimeSpan diff = DateTime.Now - lastSentRequests;
            if(diff.TotalMinutes < 15 && !overrideTimeout)
                return; // don't update if spammy
#pragma warning disable CS0162 // Unreachable code detected
            lastSentRequests = DateTime.Now;
#pragma warning restore CS0162 // Unreachable code detected
            AllHomework = new Dictionary<int, ClassHomework>();
            foreach(var u in Users)
            {
                u.Homework = new List<IHomework>();
                foreach(var cls in u.Classes)
                {
                    var clsObj = Classes.FirstOrDefault(x => x.Key == cls).Value;
                    if(clsObj != null && clsObj.Users.Contains(u) == false)
                    {
                        clsObj.Users.Add(u);
                    }
                }
                if(u.IsNotifyOnly)
                    continue; // the user hasn't given their password, and only wants to recieve Hwk via classes.
                IHomework[] homework = null;
                try
                {
                    homework = u.Client.GetHomework();
                } catch (EdulinkException ex)
                {
                    LogMsg($"For {u.UserName}: {ex}", LogSeverity.Error, "EdulinkClientError");
                    try
                    {
                        BotUser bUser = Program.GetUser(u.User);
                        int errdiff = DateTime.Now.DayOfYear - bUser.DayOfEdulinkLastError;
                        if(errdiff < 5)
                        {
                            continue; // don't send an error
                        }
                        bUser.DayOfEdulinkLastError = DateTime.Now.DayOfYear;
#if DEBUG
                        if(u.UserName != "cheale14")
                            continue; // don't send a message to others if we are debug
#endif
                        u.User.SendMessageAsync($"While attempting to login to your EduLink account to find your homework, the bot errored" +
                            $"The error is as follows: `{ex.Message}`");
                    } catch (Exception _ex)
                    {
                        LogMsg($"Failed to send to {u.Name ?? "<unable to get name>"}: {_ex}", LogSeverity.Error, "EdulinkInnerError");
                    }
                } catch (Exception ex)
                {
                    LogMsg(ex.ToString(), LogSeverity.Error, "EdulinkOtherError");
                }
                if(homework == null) // the above failed to get the hwk
                    continue;
                List<ClassHomework> current = homework.Where(x => x.Current).Select(x => ClassHomework.Create(x, ((Homework)x).Client)).ToList();
                foreach(var hwk in current)
                {
                    u.Homework.Add(hwk);
                    if(AllHomework.ContainsKey(hwk.Id))
                    {
                        // we have it, so add us to the applies to
                        AllHomework[hwk.Id].AppliesTo.Add(u);
                    } else
                    {
                        AllHomework.Add(hwk.Id, hwk);
                        hwk.AppliesTo.Add(u);
                        BaseClass cls = null;
                        foreach(var c in u.Classes)
                        {
                            var clas = Classes.FirstOrDefault(x => x.Key == c);
                            if(clas.Value.SameSubject(hwk))
                            {
                                cls = clas.Value;
                                break;
                            }
                        }
                        if(cls != null && hwk.GivenTo.Contains(cls) == false)
                        {
                            hwk.GivenTo.Add(cls);
                        }
                    }
                }
            }
        }

        [Obsolete("Uses Services.HomeworkService for any further")]
        public static void CheckNotify(bool overrideTimeout = false)
        {
            RefreshHomework(overrideTimeout);
            foreach(var hwk in AllHomework.Values)
            {
                EmbedBuilder builder = hwk.ToEmbed().ToEmbedBuilder();
                List<DiscordHwkUser> notify = new List<DiscordHwkUser>();
                // this adds in key info for us already.
                foreach(var user in hwk.TotalUsersApplied)
                {
                    if(user.ShouldNotify(hwk))
                    {
                        notify.Add(user);
                    }
                }
                string text = "";
                foreach(var u in notify)
                {
                    text += $"{u.User.Mention} ";
                }
                string classes = "";
                foreach(var cls in hwk.GivenTo)
                {
                    classes += $"{cls.Name} ";
                }
                if(classes.Length > 0)
                {
                    builder.AddField("Classes", classes, false);
                }
                if(notify.Count > 0)
                {
                    var channel = GetChannel(hwk.Subject);
                    channel.SendMessageAsync(text, false, builder.Build());
                }
            }
        }

        public static void EdulinkSentRequest(object sender, JsonRpcEventArgs e)
        {
            Edulink client = (Edulink)sender;
            Program.LogMsg($"{Edulink.Url}{e.Method}", LogSeverity.Info, $"{e.Username}/Edulink");
        }

        static IServiceProvider services;
        public EdulinkModule(DiscordSocketClient client, IServiceProvider _service)
        {
            services = _service;
            client.MessageReceived += Client_MessageReceived;
        }

        private Task Client_MessageReceived(SocketMessage arg)
        {
            if(arg.Source == MessageSource.User)
            {
                var msg = (SocketUserMessage)arg;
                if(msg.Channel is SocketTextChannel chnl)
                {
#if DEBUG
                    string chnlName = "test";
#else
                    string chnlName = "homework-help";
#endif
                    if(chnl.Name == chnlName)
                    {
                        string content = msg.Content;
                        var regex = new Regex(HomeworkRegex);
                        var match = regex.Matches(content);
                        RefreshHomework();
                        foreach(Match mat in match)
                        {
                            var text = mat.Value;
                            if(int.TryParse(text, out int id))
                            {
                                if(AllHomework.TryGetValue(id, out var hwk))
                                {
                                    msg.Channel.SendMessageAsync($"Respond: {arg.Author.Username}#{arg.Author.Discriminator}", false, hwk.ToEmbed());
                                }
                            }
                        }
                    }
                }
            }
            return Task.CompletedTask;
        }

        [Command("check")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ForceCheck()
        {
            CheckNotify(true); // overrided the timeout and force a re-check
        }

        [Command("timetable")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task dostuff()
        {
            var service = Services.Service.GetService<Services.TimetableService>();
            service.OnLoaded(false, Services.Service.ServiceStage.PostProgram);
            await ReplyAsync("Updated!");
        }

        [Command("classes"), Summary("Sets classes via EduLink token.")]
        [RequireContext(ContextType.DM)]
        public async Task OneTime(string username)
        {
            if(username.Length != "cheale14".Length && username.EndsWith("14") == false)
            {
                await ReplyAsync("Error: username inconsistent with proper naming schemes");
                return;
            }
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = "Check tutorial",
                Description = $"Please check the linked tutorial to get better detailed information.",
                Url = "https://masterlist.uk.ms/wiki/index.php/Bot:EduLink_OTP"
            };
            await ReplyAsync(embed: builder.Build());
            TimeSpan duration = TimeSpan.FromMinutes(15);
            await ReplyAsync($"Please provide your authentication token in your next message\nYou have {duration.TotalMinutes} minutes");

            var reply = await NextMessageAsync(timeout: TimeSpan.FromSeconds(duration.TotalSeconds));
            if(string.IsNullOrWhiteSpace(reply?.Content))
            {
                await ReplyAsync("Command timed out.");
                return;
            }
            var authToken = reply.Content;

            await ReplyAsync("Please provide your learner Id");

            reply = await NextMessageAsync(timeout: TimeSpan.FromSeconds(duration.TotalSeconds));
            if (string.IsNullOrWhiteSpace(reply?.Content))
            {
                await ReplyAsync("Command timed out.");
                return;
            }

            if(int.TryParse(reply.Content, out var id))
            {
                var edulink = new Edulink(username, id, authToken);
                try
                {
                    var timetable = edulink.GetTimetable();
                    var week = timetable.Weeks.OrderByDescending(x => x.Days.Count).First();
                    foreach(var day in week.Days)
                    {
                        foreach(var lesson in day.Lessons)
                        {
                            var className = lesson?.Class?.Name;
                            if (string.IsNullOrWhiteSpace(className))
                                continue;
                            if (!SelfBotUser.ManualClasses.Contains(className))
                                SelfBotUser.ManualClasses.Add(className);
                        }
                    }
                    Program.Save();
                    string ss = "Known classes:";
                    foreach (var c in SelfBotUser.ManualClasses)
                        ss += $"\n- {c}";
                    await ReplyAsync(ss);
                } catch (Exception ex)
                {
                    await ReplyAsync("**Error**\n" + ex.Message);
                    Program.LogMsg("EdulinkManualClasses", ex);
                }
            } else
            {
                await ReplyAsync("Error: learner id must be an integer.");
            }
        }

        [Command("class"), Summary("Adds the user to the given class, format: `subject/group`, eg: `Maths/X1`")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetInClass(DiscordHwkUser user, [Remainder]string class_)
        {
            class_ = class_.Replace("Maths", "Mathematics");
            class_ = class_.Replace("RE", "R.E.");
            Class cls = null;
            if(Classes.TryGetValue(class_, out cls))
            {
            } else
            {
                Discord.Rest.RestRole role = await Program.TheGrandCodingGuild.CreateRoleAsync(class_);
                SocketRole sRole = role.GetSocketRole(500, Program.TheGrandCodingGuild);
                cls = new Class(sRole);
                Classes.Add(cls.Name, cls);
            }
            if(user.Classes.Contains(cls.Name))
            {
                cls.Users.Remove(user);
                user.Classes.Remove(cls.Name);
                await user.User.RemoveRoleAsync(cls.Role);
                if(user.Classes.Count == 0)
                    await user.User.RemoveRoleAsync(Separator);
                await ReplyAsync($"Removed {user.Name} from {cls.Name}");
            }
            else
            {
                cls.Users.Add(user);
                user.Classes.Add(cls.Name);
                await user.User.AddRoleAsync(cls.Role);
                await user.User.AddRoleAsync(Separator);
                await ReplyAsync($"Added {user.Name} to {cls.Name}");
            }
        }

        [Command("class"), Summary("Views info about the given class")]
        public async Task ViewClass([Remainder]string class_)
        {
            class_ = class_.Replace("Maths", "Mathematics");
            class_ = class_.Replace("RE", "R.E.");
            Class cls = Classes.FirstOrDefault(x => x.Key == class_).Value;
            if(cls == null)
            {
                await ReplyAsync("Unknown class, allowed values: " + string.Join(", ", Classes.Values.Select(x => x.Name)));
                return;
            }
            await ReplyAsync("", false, cls.ToEmbed());
        }

        [Command("teacher"), Summary("View, or set, teacher information")]
        public async Task SetOrViewTeacher([Remainder]string name)
        {
            var clas = Classes.Values.FirstOrDefault(x => x.Name == name);
            if(clas == null)
            {
                await ReplyAsync("Unknown class name: `" + name + "`");
            } else
            {
                var tgcUser = TheGrandCodingGuild.GetUser(Context.User.Id);
                if(string.IsNullOrWhiteSpace(name))
                {
                    if(tgcUser.GuildPermissions.Administrator)
                    {
                        await ReplyAsync("Next message will set the name of the teacher.");
                        var nxt = await NextMessageAsync(timeout: TimeSpan.FromSeconds(30));
                        if(nxt != null && !string.IsNullOrWhiteSpace(nxt.Content))
                        {
                            clas.Teacher = nxt.Content;
                            await ReplyAsync("", false, clas.ToEmbed());
                        } else
                        {
                            await ReplyAsync("Cancelled.");
                        }
                    } else
                    {
                        await ReplyAsync("Class has no teacher set.");
                    }
                } else
                {
                    await ReplyAsync("Class information:", false, clas.ToEmbed());
                    if(tgcUser.GuildPermissions.Administrator)
                    {
                        await ReplyAsync("Next message will set the name of the teacher.");
                        var nxt = await NextMessageAsync(timeout: TimeSpan.FromSeconds(30));
                        if(nxt != null && !string.IsNullOrWhiteSpace(nxt.Content))
                        {
                            clas.Teacher = nxt.Content;
                            await ReplyAsync("", false, clas.ToEmbed());
                        }
                        else
                        {
                            await ReplyAsync("Cancelled.");
                        }
                    }
                }
            }
        }

        [Command("rename"), Summary("Changes the name of a class")]
        public async Task RenameClass([Remainder]string current)
        {
            var clas = Classes.Values.FirstOrDefault(x => x.Name == current);
            if(clas == null)
            {
                await ReplyAsync("Unknown class: `" + current + "`");
            } else
            {
                await ReplyAsync("Please enter the new name of the following class in your next message", false, clas.ToEmbed());
                var nxt = await NextMessageAsync(timeout: TimeSpan.FromSeconds(30));
                if(nxt != null && !string.IsNullOrWhiteSpace(nxt.Content))
                {
                    if(nxt.Content.Contains("/") == false)
                    {
                        await ReplyAsync("Error: you have a subject, but no class: `subject/class`, eg: `Mathematics/X1`");
                    } else
                    {
                        clas.Name = nxt.Content;
                        await clas.Role.ModifyAsync(x =>
                        {
                            x.Name = nxt.Content;
                        });
                        await ReplyAsync("Updated!");
                    }
                }
            }
        }

        [Command("alias")]
        public async Task AliasClass([Remainder]string name)
        {
            string input = "y";
            var clas = Classes.Values.FirstOrDefault(x => x.Name == name);
            if(clas != null)
            {
                await ReplyAsync("You are about to enter a while-loop.\r\nIf you enter `exit`, you will close.\r\nGive a new alias to add it.\r\nRepeat an existing alias to remove.");
                while(input != "exit")
                {
                    await ReplyAsync("Please enter a new alias, or an existing one to remove", false, clas.ToEmbed());
                    var nxt = await NextMessageAsync(timeout: TimeSpan.FromSeconds(30));
                    if(nxt.Content == "exit")
                        break;
                    if(nxt != null && !string.IsNullOrWhiteSpace(nxt.Content))
                    {
                        int numRemoved = clas.SubjectAliases.RemoveAll(x => x == nxt.Content);
                        if(numRemoved > 0)
                        {
                            await ReplyAsync("Removed.");
                        } else
                        {
                            clas.SubjectAliases.Add(nxt.Content);
                            await ReplyAsync("Added.");
                        }
                    } else
                    {
                        input = "exit";
                        await ReplyAsync("Goodbye!");
                    }
                }
            }
        }

        [Command("setup"), Summary("Adds your EduLink account to the bot")]
        [RequireContext(ContextType.DM)]
        public async Task AddNewUser(string username)
        {
            if(Users.FirstOrDefault(x => x.User.Id == Context.User.Id) != null)
            {
                await ReplyAsync("Error: your account has already been added.\r\nPlease contact Bob123#4008 to change your password");
                return;
            }
            await ReplyAsync(":warning: ***WARNING*** :warning: \r\n" +
                "Your username and password **will be stored in plain text**\r\n" +
                "This is **compeltely** unsecure.\r\n\r\n" +
                "***Only reply with your password if you do not use it elsewhere***");
            await ReplyAsync("Reply with only your password within 30 seconds, or wait for timeout.\r\n*You can ignore the message saying this is 'only' for commands*");
            var pwd = await NextMessageAsync(timeout: TimeSpan.FromSeconds(30));
            if(pwd == null || string.IsNullOrWhiteSpace(pwd.Content))
            {
                await ReplyAsync("Addition cancelled, timed out.");
            } else
            {
                var password = pwd.Content;
                var edulink = new EduLinkRPC.Edulink(username, password);
                edulink.SendingRequest += EdulinkSentRequest;
                JToken thing = null;
                try
                {
                    thing = edulink.Login();
                }
                catch (Exception ex)
                {
                    await ReplyAsync(ex.Message);
                    Program.LogMsg($"Failed login/edulink attempt for {username} by {Context.User.Username}@{Context.User.Id}, response: {ex}", LogSeverity.Warning, "EduLink");
                    return;
                }
                var user = thing["user"];
                var forename = user.Value<string>("forename");
                await ReplyAsync("Thank you for logging in, " + forename);
                var DiscordHwkUser = new DiscordHwkUser(Program.TheGrandCodingGuild.GetUser(Context.User.Id), username, password);
                Users.Add(DiscordHwkUser);
                await ReplyAsync("Please contact an Adminstrator with a list of your classes for you to be assigned.\r\nTo edit your preferences (on when you are messaged), please see `/edulink settings`");
            }
        }

        [Command("settings"), Summary("Edit settings on when you are mentioned")]
        public async Task DoPreferences()
        {
            TimeSpan timeout = TimeSpan.FromSeconds(30);
            DiscordHwkUser user = Users.FirstOrDefault(x => x.User.Id == Context.User.Id);
            if(user == null)
            {
                await ReplyAsync("You have no account, please do `/edulink setup` first.");
                return;
            }
            await ReplyAsync("Please reply with the days you want to be mentioned on, relative to when the homework is due." +
                "\r\nFor example, to be mentioned 7 days, 3 days, the day before, and on the day, of the due date:" +
                $"\r\nreply with: `0 1 3 7` (no special coding, just space-seperated)\r\nYour current value is: `{string.Join(" ", user.NotifyOnDays)}`");
            var replyOnDays = await NextMessageAsync(timeout:timeout);
            if(replyOnDays != null && !string.IsNullOrWhiteSpace(replyOnDays.Content))
            {
                string[] items = replyOnDays.Content.Split(' ');
                List<int> days = new List<int>();
                foreach(var i in items)
                {
                    if(int.TryParse(i, out int in_)) { days.Add(in_); }
                }
                user.NotifyOnDays = days;
                await ReplyAsync("Now set to: " + string.Join(" ", days));
            }

            await ReplyAsync("Please reply with whether you want to be notified for homeworks set by non-teachers, but for your class" +
                $"\r\nThis is either `true` or `false`, your current value is: `{user.NotifyForSelfHomework}`");
            var notifySelf = await NextMessageAsync(timeout: timeout);
            if(notifySelf != null && !string.IsNullOrWhiteSpace(notifySelf.Content))
            {
                if(bool.TryParse(notifySelf.Content, out bool result)) {
                    user.NotifyForSelfHomework = result;
                }
                await ReplyAsync("Now set to: " + result.ToString());
            }
            await ReplyAsync("Settings updated! (or remain as they were)");
        }

        [Command("user"), Summary("View user information")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ViewUser(DiscordHwkUser user)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = user.Name
            };
            string classes = string.Join("\r\n", user.Classes);
            builder.AddField("Classes", classes.Length > 0 ? classes : $"None, `{BOT_PREFIX}edulink class {user.Name} [class]`" );
            builder.AddField("Days", string.Join(", ", user.NotifyOnDays));
            builder.AddField("Self-Hwk", user.NotifyForSelfHomework.ToString());
            await ReplyAsync("", false, builder.Build());
        }

        [Command("current"), Summary("View your current homeworks not completed")]
        public async Task CurrentHomework()
        {
            DiscordHwkUser user = await GetUser(Context, Context.User.Id.ToString());
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = user.Name + " - Current Homework"
            };
            foreach(var hwk in user.Homework.OrderByDescending(x => x.DueDate.DayOfYear).Select(x => x as ClassHomework))
            {
                if(!hwk.Completed)
                {
                    string name = Clamp($"{hwk.Id}: {hwk.Activity}", 128);
                    string desc = Clamp($"Added {hwk.AvailableText}, due {hwk.DueText}\r\nSubject {hwk.Subject}\r\nSet {hwk.SetBy}", 1000);
                    builder.AddField(name, desc);
                }
            }
            if(builder.Fields.Count == 0)
                builder.AddField("No homework", "You do not have any current homework");
            await ReplyAsync("", false, builder.Build());
        }

        [Command("complete"), Alias("done"), Summary("Marks a homework with given ID as complete")]
        public async Task MarkComplete(int homework_id)
        {
            var selfUser = await GetUser(Context, Context.User.Id.ToString());
            // it will only be here if the homework was added by this current user, else it is null
            var ownHomework = selfUser.Homework.FirstOrDefault(x => x.Id == homework_id);
            if(ownHomework != null)
            {
                try
                {
                    selfUser.Client.CompleteHomework(ownHomework);
                    await ReplyAsync("Homework marked as complete.\r\nThe bot may not update as it caches queries");
                    // it doesnt actually cache but lol
                } catch (Exception ex)
                {
                    await ReplyAsync("Errored: " + ex.Message);
                }
            } else
            {
                // student homework set by another student
                AllHomework.TryGetValue(homework_id, out var homework);
                if(homework != null)
                {
                    selfUser.OtherHomeworksMarkedAsDone.Add(homework.Id);
                    await ReplyAsync("Homework marked as complete and will no longer be mentioned:\r\n" + homework.Activity);
                } else
                {
                    await ReplyAsync($"Error: unknown homework id.\r\nYou may get this by using `{Program.BOT_PREFIX}edulink current`, and seeing the number before the title" +
                        $"\r\nOr you may see it via the `Mention: homework/[id]` in the #homework channel");
                }
            }
        }

        [Command("password"), Summary("Changes your saved passowrd")]
        [RequireContext(ContextType.DM)]
        public async Task SetPassword([Remainder]string password)
        {
            var self = await GetUser(Context, Context.User.Id.ToString());
            if(self == null)
            {
                await ReplyAsync($"Error: you have not set up your account.\r\nTo do so, please use `{BOT_PREFIX}edulink setup`");
            } else
            {
                self.Password = password;
                await ReplyAndDeleteAsync($"Password for account {self.UserName} has been set to `{password}`");
                // delete message automatically
            }
        }

        [Command("add"), Summary("Adds the user without their password.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task AdminAddNotify(SocketGuildUser user, string username)
        {
            var existing = await GetUser(Context, user.Id.ToString());
            if(existing == null)
            {
                var DiscordHwkUser = new DiscordHwkUser(user, username, "");
                Users.Add(DiscordHwkUser);
                await ReplyAsync($"User added, use the `{BOT_PREFIX}edulink class {user.Username} [class]` to add a class to the user.");
            } else
            {
                await ReplyAsync("User already exists:", false, existing.ToEmbed());
            }
        }

        [Command("achievement")]
        public async Task getAchievement(DiscordHwkUser user)
        {
            var ach = user.Client.GetAchievements();
            ach.OrderByDescending(x => x.Date);
            EmbedBuilder builder = new EmbedBuilder();
            int count = 0;
            while(count < 25 && ach.Count > count)
            {
                var achievement = ach[count];
                count++;

                builder.AddField(x =>
                {
                    x.Name = $"{achievement.Id}: {achievement.Date}";
                    x.Value = $"{achievement.Points} - {achievement.Comments}";
                });
            }
            await ReplyAsync("", false, builder.Build());
        }

        protected override void BeforeExecute(CommandInfo command)
        {
            RefreshHomework();
            base.BeforeExecute(command);
        }

        protected override void AfterExecute(CommandInfo command)
        {
            Casino.FourAcesCasino.Save(); // saves user & classes
            base.AfterExecute(command);
        }
    }

    public class Class : EduLinkRPC.Addons.Discord.BaseClass
    {
        public override string Name { get; set; }

        [JsonIgnore]
        public override string Subject => Name.Substring(0, Name.IndexOf("/"));

        public string Teacher { get; set; }

        public override bool SameSubject(ClassHomework hwk)
        {
            var subject = hwk.Subject;
            subject = subject.Replace("Maths", "Mathematics");
            return (subject == Subject || SubjectAliases.Contains(subject)) && hwk.SetBy == Teacher;
        }

        [JsonProperty("Aliases", NullValueHandling = NullValueHandling.Ignore)]
        public override List<string> SubjectAliases { get; set; } = new List<string>();

        [JsonIgnore]
        public override List<DiscordHwkUser> Users { get; set; } = new List<DiscordHwkUser>();

        [JsonConverter(typeof(UlongConverter))]
        public SocketRole Role;
        public Class(SocketRole role)
        {
            Role = role;
            Name = role.Name;
            Users = new List<DiscordHwkUser>();
        }

        public Embed ToEmbed()
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = this.Name,
                Description = string.Join("\r\n", this.Users.Select(x => x.Name))
            };
            if(this.SubjectAliases.Count > 0)
                builder.AddField("Aliases", "Subject aliases:\r\n'" + string.Join("', '", this.SubjectAliases) + "'");
            if(!string.IsNullOrWhiteSpace(this.Teacher))
                builder.AddField("Teacher", this.Teacher, true);
            return builder.Build();
        }

        [JsonConstructor]
        private Class(string name, SocketRole role, List<DiscordHwkUser> users)
        {
            Name = name;
            Role = role;
            List<DiscordHwkUser> remove = new List<DiscordHwkUser>();
            if(users == null)
                return;
            foreach(var u in users)
            {
                if(!u.User.HasRole(role))
                    remove.Add(u);
            }
            foreach(var u in remove) { users.Remove(u); }
            Users = users;
        }
    }
}
