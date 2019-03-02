using System;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using System.Linq;
using System.Reflection;


namespace DiscordBot
{
    /// <summary>
    /// Allows CasinoMember to be provided as 
    /// </summary>
    public class CasinoMemberTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            Casino.CasinoMember member;
            var typereader = new SocketGuildUserTypeReader();
            var read = typereader.ReadAsync(context, input, services).GetAwaiter().GetResult();
            if (read.IsSuccess)
            {
                SocketGuildUser result = (SocketGuildUser)read.Values.FirstOrDefault().Value;
                member = Casino.FourAcesCasino.GetMember(result);
            } else
            {
                member = Casino.FourAcesCasino.GetMember(input);
            }
            if (member != null)
            {
                return Task.FromResult(TypeReaderResult.FromSuccess(member));
            }
            return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Your input could not be understood as any Casino Member (check your case and spelling, accepts Nickname, Username or ID)"));
        }
    }
}
