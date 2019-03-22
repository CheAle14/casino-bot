using Casino;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Attributes
{
    public class RequireAllowedToggleRole : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var self = FourAcesCasino.GetTGCUser(context.User as SocketGuildUser);
            if(self.Blocked_Toggle_By != 0)
            {
                return Task.FromResult(PreconditionResult.FromError($"You are prevented from changing your own roles\r\nYou may wish to contact any Admin. (ref code {self.Blocked_Toggle_By})"));
            }
            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
