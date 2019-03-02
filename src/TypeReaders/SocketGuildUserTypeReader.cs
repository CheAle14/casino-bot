using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;

namespace DiscordBot
{
    public class SocketGuildUserTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            if(input.Contains("@") || input.Contains("#"))
            {
                try
                {
                    input = input.StartsWith("@") ? input.Substring(1) : input;
                    input = input.Contains("#") ? input.Substring(0, input.LastIndexOf("#")) : input;
                } catch
                {
                }
            }
            var usr = Program.GetUserByAny(input, (SocketGuild)context.Guild);
            if(usr != null)
            {
                return Task.FromResult(TypeReaderResult.FromSuccess(usr));
            } else
            {
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Your input could not be recognised as any Discord server account (check case and spelling, accepts Nickname, Username and ID)"));
            }
        }
    }
}
