using Discord.Commands;
using Discord.Addons.Interactive;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using EduLinkRPC.Classes;
using Discord;
using Newtonsoft.Json;
using Discord.WebSocket;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using static DiscordBot.Program;

namespace DiscordBot.Modules
{
    using EduLinkRPC;
    using static EdulinkExtensions;
    [Group("edulink"), Name("Edulink/Homework Commands")]
    public class EdulinkModule : InteractiveBase
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

        public static Dictionary<int, Homework> AllHomework = new Dictionary<int, Homework>();

        public static List<HwkUser> Users = new List<HwkUser>();

        public static Dictionary<string, Class> Classes = new Dictionary<string, Class>();

        public const string HomeworkRegex = @"(?<=homework\/|hwk\/)[0-9]*";

        public static async Task<HwkUser> GetUser(ICommandContext context, string input)
        {
            var typeReader = new DiscordBot.HwkUserTypeReader();
            var result = await typeReader.ReadAsync(context, input, services);
            if(result.IsSuccess)
            {
                return result.Values.FirstOrDefault().Value as HwkUser;
            }
            return null;
        }


        static DateTime lastSentRequests = new DateTime(2019, 01, 01); // so that it is always out of date, so first check gets through
        public static void RefreshHomework(bool overrideTimeout = false)
        {
            TimeSpan diff = DateTime.Now - lastSentRequests;
            if (diff.TotalMinutes < 15 && !overrideTimeout)
                return; // don't update if spammy
            lastSentRequests = DateTime.Now;
            AllHomework = new Dictionary<int, Homework>();
            foreach(var u in Users)
            {
                u.Homework = new List<Homework>();
                foreach(var cls in u.Classes)
                {
                    var clsObj = Classes.FirstOrDefault(x => x.Key == cls).Value;
                    if(clsObj != null && clsObj.Users.Contains(u) == false)
                    {
                        clsObj.Users.Add(u);
                    }
                }
                if (u.IsNotifyOnly)
                    continue; // the user hasn't given their password, and only wants to recieve Hwk via classes.
                var homework = u.EdulinkClient.GetHomework();
                var current = homework.Where(x => x.Current);
                foreach(var hwk in current)
                {
                    u.Homework.Add(hwk);
                    if (AllHomework.ContainsKey(hwk.Id))
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

        public static void CheckNotify(bool overrideTimeout = false)
        {
            RefreshHomework(overrideTimeout);
            foreach(var hwk in AllHomework.Values)
            {
                EmbedBuilder builder = hwk.ToEmbed().ToEmbedBuilder();
                List<HwkUser> notify = new List<HwkUser>();
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
                    if (chnl.Name == chnlName)
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

        [Command("class"), Summary("Adds the user to the given class, format: `subject/group`, eg: `Maths/X1`")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task SetInClass(HwkUser user, [Remainder]string class_)
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
            if (user.Classes.Contains(cls.Name))
            {
                cls.Users.Remove(user);
                user.Classes.Remove(cls.Name);
                await user.User.RemoveRoleAsync(cls.Role);
                if (user.Classes.Count == 0)
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
                    if (nxt.Content == "exit")
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
            if (pwd == null || string.IsNullOrWhiteSpace(pwd.Content))
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
                var hwkUser = new HwkUser(Program.TheGrandCodingGuild.GetUser(Context.User.Id), username, password);
                Users.Add(hwkUser);
                await ReplyAsync("Please contact an Adminstrator with a list of your classes for you to be assigned.\r\nTo edit your preferences (on when you are messaged), please see `/edulink settings`");
            }
        }

        [Command("settings"), Summary("Edit settings on when you are mentioned")]
        public async Task DoPreferences()
        {
            TimeSpan timeout = TimeSpan.FromSeconds(30);
            HwkUser user = Users.FirstOrDefault(x => x.User.Id == Context.User.Id);
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
        public async Task ViewUser(HwkUser user)
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
            HwkUser user = await GetUser(Context, Context.User.Id.ToString());
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = user.Name + " - Current Homework"
            };
            foreach(var hwk in user.Homework.OrderByDescending(x => x.DueDate.DayOfYear))
            {
                if(!hwk.Completed)
                {
                    string name = Clamp($"{hwk.Id}: {hwk.Activity}", 128);
                    string desc = Clamp($"Added {hwk.AvailableText}, due {hwk.DueText}\r\nSubject {hwk.Subject}\r\nSet {hwk.SetBy}", 1000);
                    builder.AddField(name, desc);
                }
            }
            if (builder.Fields.Count == 0)
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
                    selfUser.EdulinkClient.CompleteHomework(ownHomework);
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
                await ReplyAndDeleteAsync($"Password for account {self.Username} has been set to `{password}`");
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
                var hwkUser = new HwkUser(user, username, "");
                Users.Add(hwkUser);
                await ReplyAsync($"User added, use the `{BOT_PREFIX}edulink class {user.Username} [class]` to add a class to the user.");
            } else
            {
                await ReplyAsync("User already exists:", false, existing.ToEmbed());
            }
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

    public class Class : BaseClass
    {
        public override string Name { get; set; }

        [JsonIgnore]
        public override string Subject => Name.Substring(0, Name.IndexOf("/"));

        [JsonProperty("Aliases", NullValueHandling = NullValueHandling.Ignore)]
        public override List<string> SubjectAliases { get; set; } = new List<string>();

        [JsonIgnore]
        public override List<HwkUser> Users { get; set; } = new List<HwkUser>();

        [JsonConverter(typeof(Casino.UlongConverter))]
        public SocketRole Role;
        public Class(SocketRole role)
        {
            Role = role;
            Name = role.Name;
            Users = new List<HwkUser>();
        }

        public Embed ToEmbed()
        {

            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = this.Name,
                Description = string.Join("\r\n", this.Users.Select(x => x.Name))
            };
            if (this.SubjectAliases.Count > 0)
                builder.AddField("Aliases", "Subject aliases:\r\n'" + string.Join("', '", this.SubjectAliases) + "'");
            return builder.Build();
        }

        [JsonConstructor]
        private Class(string name, SocketRole role, List<HwkUser> users)
        {
            Name = name;
            Role = role;
            List<HwkUser> remove = new List<HwkUser>();
            if (users == null)
                return;
            foreach(var u in users)
            {
                if (!u.User.HasRole(role))
                    remove.Add(u);
            }
            foreach(var u in remove) { users.Remove(u); }
            Users = users;
        }
    }


    // Extensions
    public static class EdulinkExtensions
    {
        public const string SpanOuterFind = @"(?:\<span).*(?:\<\/span>)"; // @"(?<=\<span).*(?=\<\/span\>)";
        public static string Markdown(string input)
        {
            // handle any input that the converted below cant
            input = input.Replace("<p>", "");
            input = input.Replace("</p>", "");
            input = input.Replace("<br>", "\r\n");
            input = input.Replace("<u>", "");
            input = input.Replace("</u>", "");

            var spanRemove = new Regex(SpanOuterFind);
            var matches = spanRemove.Matches(input);
            foreach(Match match in matches)
            {
                var str = match.Value;
                str = str.Replace("</span>", "");
                str = str.Substring(str.IndexOf(">") + 1); // assume first > closes the span tag
                input = input.Replace(match.Value, str); // replace it.
            }

            var converter = new Html2Markdown.Converter();
            string markdown = converter.Convert(input);

            return markdown;
        }
        public static string Clamp(string input, int max = 200)
        {
            string cutof = " [...]";
            input = Markdown(input);
            try
            {
                return input.Substring(0, max - cutof.Length) + cutof;
            } catch 
            {
            }
            return input;
        }

        public static bool IsUrgent(this Homework hwk)
        {
            return hwk.DueText.Contains("today") || hwk.DueText.Contains("tomorrow");
        }

        public static Embed ToEmbed(this EduLinkRPC.Classes.Homework hwk)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = hwk.Activity,
                Description = Clamp(Markdown(hwk.Description), 2000),
                Footer = new EmbedFooterBuilder().WithText($"Mention: homework/{hwk.Id}")
            };
            if(hwk.IsUrgent())
            {
                builder.Color = Color.Red;
            }
            builder.AddField(x =>
            {
                x.Name = "Subject";
                x.Value = hwk.Subject;
                x.IsInline = true;
            });
            builder.AddField(x =>
            {
                x.Name = "Due";
                x.Value = hwk.IsUrgent() ? "**" + hwk.DueText + "**" : hwk.DueText;
                x.IsInline = true;
            });
            builder.AddField(x =>
            {
                x.Name = "Set by";
                x.Value = hwk.SetBy;
                x.IsInline = true;
            });
            if(hwk.Attachments.Count > 0)
            {
                builder.AddField(x =>
                {
                    x.Name = $"Attachments";
                    x.Value = string.Join("\r\n", hwk.Attachments.Select(y => y.FileName));
                    x.IsInline = false;
                });
            }
            return builder.Build();
        }


        public static Embed ToEmbed(this HwkUser user)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = user.Name,
                Description = $"Due homework: {user.Homework.Where(x => x.Completed == false && x.DueDate > DateTime.Now)}"
            };
            if(user.Classes.Count > 0)
            {
                builder.AddField("Classes", string.Join("\r\n", user.Classes));
            }
            return builder.Build();
        }


    }
}
