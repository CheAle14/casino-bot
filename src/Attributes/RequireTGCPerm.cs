using System;
using System.Threading.Tasks;
using Discord.Commands;
using static DiscordBot.Program;

namespace DiscordBot.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequireTGCPerm : PreconditionAttribute
    {
        private Casino.TGCPermissions perm;

        public RequireTGCPerm(Casino.TGCPermissions _perm)
        {
            perm = _perm;
        }
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var usr = TheGrandCodingGuild.GetUser(context.User.Id);
            if(usr == null)
            {
                // they arent even in the guild
                return Task.FromResult(PreconditionResult.FromError("Not in Guild - no permissions"));
            }
            var tgcuser = Casino.FourAcesCasino.GetTGCUser(usr);
            string extraExplaination = "\r\n";
            if(tgcuser.Permissions.HasFlag(perm))
            {
                bool secondCheck = true;
                if(perm.HasFlag(Casino.TGCPermissions.a_ChangeSelfNickName) || perm.HasFlag(Casino.TGCPermissions.k_ChangeOtherNickname))
                {
                    if(tgcuser.Blocked_Nickname_By != 0)
                    {
                        var blockedBy = TheGrandCodingGuild.GetUser(tgcuser.Blocked_Nickname_By);
                        extraExplaination += $"You are blocked from doing that action, contact an admin";
                        secondCheck = false;
                    }
                }
                if(perm.HasFlag(Casino.TGCPermissions.b_ToggleSelfDeveloper) || perm.HasFlag(Casino.TGCPermissions.c_ToggleSelfTester))
                {
                    if(tgcuser.Blocked_Toggle_By != 0)
                    {
                        var blockedBy = TheGrandCodingGuild.GetUser(tgcuser.Blocked_Toggle_By);
                        extraExplaination += $"You are blocked from doing that action, contact " + (blockedBy?.Nickname ?? "an admin");
                        secondCheck = false;
                    }
                }
                if(secondCheck)
                    return Task.FromResult(PreconditionResult.FromSuccess());
            }
            return Task.FromResult(
                PreconditionResult.FromError($"You do not have the required permission for that" + extraExplaination));
        }
    }
}
