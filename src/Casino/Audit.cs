using System;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace Casino
{
    public class Audit
    {
        /// <summary>
        /// The sunday of the audit
        /// </summary>
        public DateTime Day;
        [JsonConverter(typeof(UlongConverter))]
        public SocketGuildUser User;
        public int Whites;
        [JsonIgnore]
        public int WhiteValue => Whites * 25;

        public int Reds;
        [JsonIgnore]
        public int RedValue => Reds * 50;

        public int Greens;
        [JsonIgnore]
        public int GreenValue => Greens * 100;

        public int Blues;
        [JsonIgnore]
        public int BlueValue => Blues * 500;

        public int Blacks;
        [JsonIgnore]
        public int BlackValue => Blacks * 1000;
        [JsonIgnore]
        public int TotalChipAmount => Whites + Reds + Greens + Blues + Blacks;

        public int TotalValue => WhiteValue + RedValue + GreenValue + BlueValue + BlackValue;

        [JsonConstructor]
        private Audit(DateTime day, int whites, int reds, int greens, int blues, int blacks)
        {
            //User = CasinoGuild.GetUser(userID);
            Day = day.Next(DayOfWeek.Sunday); // backwards compatibility
            Whites = whites;
            Reds = reds;
            Greens = greens;
            Blues = blues;
            Blacks = blacks;
        }

        public Audit(SocketGuildUser user = null, int whites = 0, int reds = 0, int greens = 0, int blues = 0, int blacks = 0)
        {
            User = user;
            Day = DateTime.Now;
            Whites = whites;
            Reds = reds;
            Greens = greens;
            Blues = blues;
            Blacks = blacks;
        }

        public Audit(IUser user = null, int whites = 0, int reds = 0, int greens = 0, int blues = 0, int blacks = 0)
        {
            User = user as SocketGuildUser;
            Day = DateTime.Now;
            Whites = whites;
            Reds = reds;
            Greens = greens;
            Blues = blues;
            Blacks = blacks;
        }

        public string ToDisplay(bool includeDate = false)
        {
            string value = User == null ? "" : $"Audit of {User.Nickname.Replace("M.O.A. Inspector", "Shafaib")}\n"; // backwards compat: MOAInsp account used to do Shafaib's audits
            value += $"**Whites**: {Whites} ({WhiteValue})\n";
            value += $"**Reds**: {Reds} ({RedValue})\n";
            value += $"**Greens**: {Greens} ({GreenValue})\n";
            value += $"**Blues**: {Blues} ({BlueValue})\n";
            value += $"**Blacks**: {Blacks} ({BlackValue})\n";
            value += $"**Total Amount: {TotalValue}**\n";
            if(includeDate)
            {
                value += $"Date done: {Day.Next(DayOfWeek.Sunday).ToShortDateString()}";
            }
            return value;
        }

        public override string ToString()
        {
            if(User != null)
            {
                return $"{(User.Nickname ?? User.Username).Replace("M.O.A. Inspector", "Shafaib")} {TotalValue} {Day.ToShortDateString()}";
            } else
            {
                return $"Unknown {TotalValue} {Day.ToShortDateString()}";
            }
        }
    }
}

