using System;
using System.Threading.Tasks;
using Discord.Commands;
using System.Linq;

namespace DiscordBot.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple =false)]
    public sealed class CommandDisabled : PreconditionAttribute
    {

        public readonly string _reason;
        public readonly CommandInfo _command;
        public readonly ModuleInfo _module;

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (obj is ModuleInfo && _module != null)
            {
                return obj.Equals(_module);
            }
            if (obj is CommandInfo && _command != null)
            {
                return obj.Equals(_command);
            }
            return base.Equals(obj);
        }
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public CommandDisabled(string reason)
        {
            _reason = reason;
        }
        public CommandDisabled(string reason, CommandInfo command)
        {
            _reason = reason;
            _command = command;
        }
        public CommandDisabled(string reason, ModuleInfo module)
        {
            _reason = reason;
            _module = module;
        }
        public string GetMsg()
        {
            return $":no_entry: {(_module == null ? "Command" : "Module")} is disabled: " + _reason;
        }
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return Task.FromResult(PreconditionResult.FromError(this.GetMsg()));
        }
        public override string ToString()
        {
            return $"{(_command == null ? $"M {_module.Aliases.FirstOrDefault()}" : $"C {_command.Aliases.FirstOrDefault()}")} - {_reason}";
        }
    }
}
