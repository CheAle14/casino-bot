using Discord;
using static DiscordBot.Program;

namespace Casino
{
    public class MOG_Division : Division
    {
        public MOG_Division() : base("M.O.G.", U_BOB123) { }

        public int WeeklyProfits = 0;

        public int MaximumPokerEarningsInOneDay = 1500;
        public int PokerEarningsGivenPerDay = 1000;
        

        /*public int RakeBudget = 0;
        public int BlackjakcBudget = 0;

        public override int ChipBudget { get => RakeBudget + BlackjakcBudget; set => throw new InvalidOperationException("Modify Rake/Blackjack budgets"); }
        */
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
                x.Value = $"Actual: {ChipBudget}\nWeekly tracked: {WeeklyProfits}";
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

