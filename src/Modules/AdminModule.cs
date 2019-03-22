using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Casino;
using static DiscordBot.Program;
using System.Threading.Tasks;
using System.Linq;
using DiscordBot.Attributes;

namespace DiscordBot.Modules
{
    [Name("The Grand Coding Commands")]
    public class AdminModule : CasinoInteractiveBase<SocketCommandContext> // Inherited class implements some helper function
    {                                                                      // such as the current CasinoMember class.
        public TGCUser Self;

        public IRole Marvel => TheGrandCodingGuild.Roles.FirstOrDefault(x => x.Id == 545279460711202817 || x.Name == "Marvel");
        public IRole Developer => TheGrandCodingGuild.Roles.FirstOrDefault(x => x.Name == "Developers");
        public IRole Tester => TheGrandCodingGuild.Roles.FirstOrDefault(x => x.Name == "Testers");
        public IRole Sports => TheGrandCodingGuild.Roles.FirstOrDefault(x => x.Name == "Sports");

        public IRole Seperator => TheGrandCodingGuild.GetRole(558692386507849738);


        [Command("setnick"), Alias("nick"), Summary("Changes your own nickname")]
        [RequireTGCPerm(TGCPermissions.a_ChangeSelfNickName)]
        public async Task ChangeSelfNickname(string newnick)
        {
            try
            {
                await Self.User.ModifyAsync(x =>
                {
                    x.Nickname = newnick;
                }, new RequestOptions() { AuditLogReason = $"Changed by: [self]" });
                await ReplyAsync("Nickname updated!");
            } catch (Exception)
            {
                await ReplyAsync("Errored.\nBot does not have permissions needed");
                throw;
            }
        }

        [Command("setnick"), Alias("nick"), Summary("Changes someone else's nickname")]
        [RequireTGCPerm(TGCPermissions.k_ChangeOtherNickname)]
        public async Task ChangeOtherNickname(SocketGuildUser target, string newnick)
        {
            try
            {
                await target.ModifyAsync(x =>
                {
                    x.Nickname = newnick;
                }, new RequestOptions() { AuditLogReason = $"Changed by: {(Self.User.Username)}" });
                await ReplyAsync("Their nickname has been set!");

            } catch (Exception)
            {
                await ReplyAsync("Errored.\nBot does not have permissions needed");
                throw;
            }
        }

        [Command("locknick"), Summary("Prevents someone from changing their nickname")]
        [RequireTGCPerm(TGCPermissions.l_PreventOtherNickname)]
        public async Task PreventNicknameChange(SocketGuildUser target)
        {
            TGCUser user = FourAcesCasino.GetTGCUser(target);
            if(user.Blocked_Nickname_By != 0)
            {
                user.Blocked_Nickname_By = 0;
                await ReplyAsync("Removed the mark preventing that user from changing their nickname");
            } else
            {
                user.Blocked_Nickname_By = Context.User.Id;
                await ReplyAsync("User has been marked as unable to change their nickname\nNote that if they have the Discord permission (or have admin perms) they can set it anyway");
            }
        }

        [Command("locktoggle"), Summary("Prevents someone from toggling their roles")]
        [RequireTGCPerm(TGCPermissions.j_PreventOtherToggle)]
        public async Task PreventToggleRoles(SocketGuildUser target)
        {
            var user = FourAcesCasino.GetTGCUser(target);
            if(user.Blocked_Toggle_By != 0)
            {
                user.Blocked_Toggle_By = 0;
                await ReplyAsync("Removed mark that prevented them toggling the role.");
            } else
            {
                user.Blocked_Toggle_By = Context.User.Id;
                await ReplyAsync("Added a mark preventing them from changing their roles.\r\nIf they have the Discord permission (or admin perms) they can bypass this");
            }
        }


        [Command("toggle marvel"), Summary("Toggles your marvel role")]
        [RequireContext(ContextType.Guild)]
        [RequireAllowedToggleRole]
        public async Task ToggleMarvelRole()
        {
            if(Self.User.HasRole(Marvel))
            {
                await Self.User.RemoveRoleAsync(Marvel, new RequestOptions() { AuditLogReason = $"Self remove" });
                await ReplyAsync("Removed role.");
            } else
            {
                await Self.User.AddRoleAsync(Marvel, new RequestOptions() { AuditLogReason = $"Self add" });
                await ReplyAsync("Added role.");
            }
        }

        [Command("toggle sports"), Summary("Toggles your Sports role")]
        [RequireContext(ContextType.Guild)]
        [RequireAllowedToggleRole]
        public async Task ToggleSportsRole()
        {
            if (Self.User.HasRole(Sports))
            {
                await Self.User.RemoveRoleAsync(Sports, new RequestOptions() { AuditLogReason = $"Self remove" });
                await ReplyAsync("Removed role.");
            }
            else
            {
                await Self.User.AddRoleAsync(Sports, new RequestOptions() { AuditLogReason = $"Self add" });
                await ReplyAsync("Added role.");
            }
        }

        [Command("toggle tester"), Summary("Toggles your Testers role")]
        [RequireContext(ContextType.Guild)]
        [RequireAllowedToggleRole]
        public async Task ToggleTestersRole()
        {
            if (Self.User.HasRole(Tester))
            {
                await Self.User.RemoveRoleAsync(Tester, new RequestOptions() { AuditLogReason = $"Self remove" });
                await ReplyAsync("Removed role.");
            }
            else
            {
                await Self.User.AddRoleAsync(Tester, new RequestOptions() { AuditLogReason = $"Self add" });
                await ReplyAsync("Added role.");
            }
        }

        [Command("toggle developer"), Summary("Toggles your Developers role")]
        [RequireContext(ContextType.Guild)]
        [RequireAllowedToggleRole]
        public async Task ToggleDevelopersRole()
        {
            if (Self.User.HasRole(Developer))
            {
                await Self.User.RemoveRoleAsync(Developer, new RequestOptions() { AuditLogReason = $"Self remove" });
                await ReplyAsync("Removed role.");
            }
            else
            {
                await Self.User.AddRoleAsync(Developer, new RequestOptions() { AuditLogReason = $"Self add" });
                await ReplyAsync("Added role.");
            }
        }

        protected override void BeforeExecute(CommandInfo command)
        {
            var usr = TheGrandCodingGuild.GetUser(Context.User.Id);
            var tgcUser = FourAcesCasino.GetTGCUser(usr);
            Self = tgcUser;
            if(Self.User.Guild.Id != TheGrandCodingGuild.Id)
            {
                LogMsg("Invalid guild for Self TGC user " + Self.User.Guild.Name);
                Self.User = TheGrandCodingGuild.GetUser(Self.User.Id);
            }
            if(command.Name.StartsWith("toggle "))
            {
                if(Self.Blocked_Toggle_By != 0)
                {

                }
            }
            base.BeforeExecute(command);
        }

        protected override void AfterExecute(CommandInfo command)
        {
            FourAcesCasino.Save();
            bool hasAnyOfTheToggleAble = false;
            SocketGuildUser g = (SocketGuildUser)Context.User;  
            foreach(var role in new IRole[] { Marvel, Tester, Developer, Sports})
            {
                if (g.Roles.Contains(role))
                {
                    hasAnyOfTheToggleAble = true;
                    break;
                }
            }
            if(hasAnyOfTheToggleAble)
            {
                g.AddRoleAsync(Seperator);
            } else
            {
                g.RemoveRoleAsync(Seperator);
            }
        }
    }

}
