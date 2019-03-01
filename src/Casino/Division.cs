using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using static DiscordBot.Program;
using System.ComponentModel;

namespace Casino
{
    public abstract class Division
    {
        [JsonIgnore]
        public readonly string Name;
        [JsonProperty("ChipBudget", DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(0)]
        protected int _ChipBudget = 0;
        [JsonIgnore]
        public virtual int ChipBudget
        {
            get { return _ChipBudget; }
            set
            {
                SetBudget(value, "unknown reason - edited via Property::set");
            }
        }

        [JsonIgnore]
        public Color Color { get
            {
                if (Name == "M.O.A.")
                {
                    return Color.Red;
                } else if(Name == "M.O.L.")
                {
                    return Color.Purple;
                } else
                {
                    return Color.Green;
                }
            } }

        public void SetBudget(int amount, string reason)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = $"{Name} Budget Changed",
                Description = "Reason: " + reason,
                Footer = DateTimeFooter,
                Color = this.Color
            };
            int diff = _ChipBudget - amount;
            builder.AddField(x =>
            {
                x.Name = "Was";
                x.Value = _ChipBudget;
                x.IsInline = true;

            });
            builder.AddField(x =>
            {
                x.Name = "Is";
                x.Value = amount;
                x.IsInline = true;
            });
            builder.AddField(x =>
            {
                x.Name = "Difference";
                x.Value = (diff > 0 ? "Removed" : (diff == 0 ? "No Change" : "Added")) + $" {Math.Abs(diff)}";
                x.IsInline = true;
            });
            if(reason != "TEST")
                C_LOGS_FAC_DIVISION.SendMessageAsync("", false, builder.Build());
            _ChipBudget = amount;
        }
        public void DecreaseBudget(int amountBy, string reason)
        {
            if (amountBy < 0)
                throw new ArgumentException("Cannot be negative", nameof(amountBy));
            SetBudget(_ChipBudget - amountBy, reason);
        }
        public void IncreaseBudget(int amountBy, string reason)
        {
            if (amountBy < 0)
                throw new ArgumentException("Cannot be negative", nameof(amountBy));
            SetBudget(_ChipBudget + amountBy, reason);
        }

        public SocketGuildUser DivisionHead;

        public class DivisionWage
        {
            public SocketGuildUser User;
            public int Wage;
            public DivisionWage(SocketGuildUser user, int wage)
            {
                User = user;
                Wage = wage;
            }
        }

        public List<DivisionWage> DivisionWages = new List<DivisionWage>();

        [JsonIgnore]
        public virtual DivisionEnum EnumName
        {
            get
            {
                return (DivisionEnum)Enum.Parse(typeof(DivisionEnum), Name.Replace(".", ""));
            }
        }

        public ICategoryChannel DivisionCategory;
        public ITextChannel DivisionStaffChannel;

        public ITextChannel ExtraCreatedChannel;

        [JsonIgnore]
        public IRole EmployeeRole => CasinoGuild.Roles.FirstOrDefault(x => x.Name == Name);

        [JsonIgnore]
        public IRole DivisionHeadRole => CasinoGuild.Roles.FirstOrDefault(x => x.Name.StartsWith(Name + " "));

        public List<SocketGuildUser> Employees = new List<SocketGuildUser>();

        public Division(string name, int budget, SocketGuildUser head)
        {
            Name = name;
            ChipBudget = budget;
            DivisionHead = head;
        }
        public Division(string name, SocketGuildUser head)
        {
            Name = name;
            DivisionHead = head;
        }
        public Division()
        {
        }

        public abstract Embed ToEmbed(InvestPermissions viewerPermissions);
    }
}

