using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Linq;
using static DiscordBot.Program;
using System.Globalization;

namespace DiscordBot
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
                        extraExplaination += $"You are blocked from doing that action, contact " + (blockedBy?.Nickname ?? "an admin");
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

    /// <summary>
    /// Designates that the command is to be hidden from /help
    /// Commands are still directly callable.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class HiddenAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class HideForManagement : HiddenAttribute
    { }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class HiddenWhenNoApplicant : HiddenAttribute
    {
        public bool IsHidden()
        {
            return Casino.FourAcesCasino.Applications.Count == 0;
        }
    }

    /// <summary>
    /// Indicates that the user should see more a more detailed help message
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple =false)]
    public sealed class NotifyFurtherHelpAttribute : Attribute
    {
    }

    public  sealed class HasWikiEntiry : Attribute
    {
        public const string wikiUrl = "https://masterlist.uk.ms/wiki/index.php/";
        public Uri WikiPage { get; }
        public string LinkText { get; }
        public string AdditionalText { get; }
        public string DiscordMarkDown => $"[{LinkText}]({WikiPage.AbsoluteUri})";
        public string FullText => DiscordMarkDown + (string.IsNullOrWhiteSpace(AdditionalText) ? "": " " + AdditionalText);

        public HasWikiEntiry(string pageName, string linkText = "W", string additional = "")
        {
            WikiPage = new Uri(wikiUrl + Uri.EscapeUriString(pageName));
            LinkText = linkText;
            AdditionalText = additional;
        }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequireInvestigation : PreconditionAttribute
    {
        private readonly string _name;
        private readonly bool _startsWith;

        public RequireInvestigation(string name = "", bool startsWith = true)
        {
            _name = name == "" ? "Investigation" : name;
            _startsWith = startsWith;
        }
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if((CasinoGuild.Channels.Where(x => x.Id == context.Channel.Id).Count() <= 0) || context.Guild == null)
            { // Channel not in Four Aces
                return Task.FromResult(PreconditionResult.FromError($"Command must be used in the Four Aces Server, in a channel."));
            }
            ITextChannel chnl = CasinoGuild.Channels.FirstOrDefault(x => x.Id == context.Channel.Id) as ITextChannel;
            SocketCategoryChannel category = CasinoGuild.Channels.FirstOrDefault(x => x.Id == chnl.CategoryId) as SocketCategoryChannel;
            bool inValid = false;
            if(category != null)
            {
                inValid = category.Name.StartsWith("Investigation");
                if(_startsWith)
                {
                    inValid = category.Name.StartsWith(_name) || (string.IsNullOrWhiteSpace(chnl.Topic) ? "":chnl.Topic).Contains(_name);
                } else
                {
                    bool nameCat = CultureInfo.InvariantCulture.CompareInfo.IndexOf(category.Name, _name, CompareOptions.IgnoreCase) >= 0;
                    bool topicCat = chnl.Topic.ToLower().Contains(_name.ToLower());
                    inValid = nameCat || topicCat;
                }
            }
            if (inValid)
                return Task.FromResult(PreconditionResult.FromSuccess());
            return Task.FromResult(
                PreconditionResult.FromError($"Command can not be used in this channel"));
        }
    }


    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class IgnoreGamesChannelAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequireGamesChannel : PreconditionAttribute
    {
        private readonly string _gameType;
        public RequireGamesChannel(string GameType = "")
        {
            _gameType = GameType;
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            foreach(var attr in command.Attributes)
            {
                if(attr is IgnoreGamesChannelAttribute)
                    return Task.FromResult(PreconditionResult.FromSuccess());
            }
            if(!(context.Channel is SocketGuildChannel))
            {
                return Task.FromResult(PreconditionResult.FromError($"Not within the {CasinoGuild.Name} Guild"));
            }
            var channel = context.Channel as SocketTextChannel;
            var category = channel.Category;
            if(category == null || category.Id != 476315471566864384)
            {
                return Task.FromResult(PreconditionResult.FromError("Must be within the Games category"));
            }
            return Task.FromResult(PreconditionResult.FromSuccess());
        }
    }



    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple =true)]
    public sealed class RequireRole : PreconditionAttribute
    {
        [Flags] // might use this in the future
        private enum Roles_FA
        {
            Member =       0b000000000000001,
            Terminated   = 0b000000000000010,
            Dealer =       0b000000000001000,
            HeadDealer =   0b000000000010000,
            MOLSupervis =  0b000000000100000,
            MOLMember =    0b000000001000000,
            MOGMember =    0b000000010000000,
            MOAMember =    0b000000100000000,
            VIP =          0b000001000000000,
            HighRoller =   0b000010000000000,
            MOGSupervis =  0b000100000000000,
            MOAInsp      = 0b001000000000000,
            Owner =        0b100000000000000,
            Council = MOLSupervis | MOAInsp | MOGSupervis | Owner,
            Staff = Dealer | HeadDealer | Council
        }
        private readonly string[] _names;
        private IRole _role;
        private readonly bool _requireAll;
        private readonly bool _requireNone;
       

        public RequireRole(bool requireAll = false, bool requireNone = false, params string[] names)
        {
            _names = names;
            _requireAll = requireAll;
            _requireNone = requireNone;
        }
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider prov)
        {
            int matchingRoles = 0;
            IGuild guild = context.Guild;
            if(context.Guild is null)
            {
                guild = Program._discord_.GetGuild(402839443813302272);
            } 
            foreach(string _name in _names)
            {
                _role = guild.Roles.FirstOrDefault(x => x.Name == _name);
                if (_role == null)
                    continue;
                SocketGuildUser user = ((SocketGuild)guild).Users.FirstOrDefault(x => x.Id == context.User.Id);
                if(user == null)
                {
                    if(_requireNone)
                    {
                        return Task.FromResult(PreconditionResult.FromSuccess());
                    }
                    return Task.FromResult(PreconditionResult.FromError("Unable to locate your user.\nYou may not be a Four Aces Casino member"));
                }
                if (user.Roles.Contains(_role))
                {
                    matchingRoles += 1;
                } else
                {
                    if(_role.Name.Contains("M.O.A.") && user.Id == 392748367207333888)
                    {
                        matchingRoles += 1;
                    }
                }
            }
            if(_requireNone)
            {
                if(matchingRoles >= 1)
                {
                    return Task.FromResult(PreconditionResult.FromError($"You have a role that prevents you from doing that"));
                } else
                {
                    matchingRoles = _names.Count();
                }
            }
            if(_requireAll)
            {
                if(matchingRoles == _names.Count())
                {
                    return Task.FromResult(PreconditionResult.FromSuccess());
                } else
                {
                    return Task.FromResult(PreconditionResult.FromError($"You do not have all the roles required"));
                }
            }
            if(matchingRoles >= 1)
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            } else
            {
                return Task.FromResult(PreconditionResult.FromError($"You don't have the require role(s) to do that"));
            }
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple=false)]
    public sealed class CasinoCommandAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class RequireGuild : PreconditionAttribute
    {
        private readonly string _name;
        private readonly bool _inverse;
        private readonly bool _allowDM;
        private readonly bool _allowLoggingGuild;

        public RequireGuild(string name, bool notInGuild = false, bool allowDm = false, bool allowLoggingGuild = false)
        {
            _name = name;
            _inverse = notInGuild;
            _allowDM = allowDm;
            _allowLoggingGuild = allowLoggingGuild;
        }
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider prov)
        {
            bool isValid = false;
            if(context.Guild == null)
            { // DM channel
                var guild = _discord_.Guilds.FirstOrDefault(x => x.Name == _name);
                if(guild != null)
                {
                    var user = guild.Users.FirstOrDefault(x => x.Id == context.User.Id);
                    isValid = user != null && _allowDM;
                }
            } else
            {
                if(_inverse)
                {
                    isValid = context.Guild.Name != _name;
                } else
                {
                    isValid = context.Guild.Name == _name;
                }
                if(context.Guild.Id == 508229402325286912)
                {
                    if (_allowLoggingGuild)
                        isValid = true;
                }
            }
            if (isValid)
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }
            return Task.FromResult(PreconditionResult.FromError($"That command can not be used in this server"));
        }
    }
}
