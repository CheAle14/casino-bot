using Discord;
using static DiscordBot.Program;

namespace Casino
{
    public class MOA_Division : Division
    {
        public MOA_Division() : base ("M.O.A.", U_SNEAKYBOY)
        {
        }
        public override Embed ToEmbed(InvestPermissions viewerPermissions)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = this.Name,
                Description = "Division Head: " + (this.DivisionHead.Nickname ?? this.DivisionHead.Username),
                Color = this.Color
            };
            builder.AddField(x =>
            {
                x.Name = "Budget";
                x.Value = this.ChipBudget.ToString();
            });
            builder.AddField(x =>
            {
                x.Name = "Employees";
                string meh = "No employees";
                if (this.Employees.Count > 0)
                {
                    meh = "";
                    foreach (var e in this.Employees)
                    {
                        meh += $"{e.Nickname ?? e.Username}\n";
                    }
                }
                x.Value = meh;
            });
            return builder.Build();
        }
    }
}

