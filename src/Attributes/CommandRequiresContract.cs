using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FAC = Casino.FourAcesCasino;

namespace DiscordBot.Attributes
{
    public class CommandRequiresContract : PreconditionAttribute
    {
        public readonly int Version;
        public readonly string Reason;
        public CommandRequiresContract(int version, string reason = "")
        {
            Version = version;
            Reason = reason;
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            int current = Program.Storage.CurrentContractVersion;
            if(current >= Version)
                return Task.FromResult(PreconditionResult.FromSuccess());
            return Task.FromResult(PreconditionResult.FromError($"Command requires Membership contract version {Version}{(string.IsNullOrWhiteSpace(Reason) ? "" : "\r\nBecause: " + Reason)}"));
        }
    }
}
