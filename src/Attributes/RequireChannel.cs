using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace DiscordBot.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple =true)]
    public sealed class RequireChannel : PreconditionAttribute
    {
        private readonly string _name;

        public RequireChannel(string name)
        {
            _name = name;
        }
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (context.Channel.Name == _name || context.Channel.Name.StartsWith(_name + "-") || context.Channel is IDMChannel)
                return Task.FromResult(PreconditionResult.FromSuccess());
            return Task.FromResult(
                PreconditionResult.FromError($"Command can not be used in this channel"));
        }
    }
}
