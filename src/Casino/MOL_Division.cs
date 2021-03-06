﻿using System;
using Discord;
using Newtonsoft.Json;
using static DiscordBot.Program;
using System.ComponentModel;

namespace Casino
{
    public class MOL_Division : Division
    {
        public MOL_Division() : base("M.O.L.", CasinoGuild.GetUser(367711200034947084)) { }

        public int LastDayOfTimeSent;

        public LoanInfo SmallLoan /*= new LoanInfo(LoanType.Small, 25, 100, 14)
            .WithMaxChips(5000)
            .WithForbidOthers(true)
            .WithMustCouncilMustApprove(false)
            .WithInterest(new DailyInterest(0.01))*/;
        public LoanInfo MediumLoan /*= new LoanInfo(LoanType.Medium, 125, 250, 21)
            .WithMaxChips(6000)
            .WithMaxDebt(3000)
            .WithInterest(new DailyInterest(0.015))
            .WithForbidOthers(false)*/;
        public LoanInfo LargeLoan /*= new LoanInfo(LoanType.Starter, 250, 375, 28)
            .WithMaxChips(7000)
            .WithInterest(new DailyInterest(0.02))
            .WithForbidOthers(true)*/;

        public LoanInfo GetLoanInfo(LoanType type)
        {
            if (type == LoanType.Small)
                return SmallLoan;
            if (type == LoanType.Medium)
                return MediumLoan;
            return LargeLoan;
        }

        public class LoanInfo
        {
            public LoanType LType;

            [JsonIgnore]
            public int ChipsGivenToPlayer => (int)LType;
            [JsonIgnore]
            public int PlayerStartsPaying => RoundToChip(((int)LType) * 1.1);

            public int MinimumDaily;
            public int MaximumDaily;
            public int DaysForPayBack;
            [JsonConverter(typeof(UlongConverter))]
            public LoanInterest Interest;

            [JsonConstructor]
            private LoanInfo(LoanType ltype, int minimumdaily, int maximumdaily, int daysforpayback, LoanInterest interest)
            {
                LType = ltype;
                MinimumDaily = minimumdaily;
                MaximumDaily = maximumdaily;
                DaysForPayBack = daysforpayback;
                Interest = interest;
                if (Interest == null)
                {
                    if (ltype == LoanType.Small)
                        Interest = new DailyInterest(0.01);
                    else if (ltype== LoanType.Medium)
                        Interest = new DailyInterest(0.015);
                    else
                        Interest = new DailyInterest(0.02);
                }
            }

            public LoanInfo(LoanType type, int min, int max, int days)
            {
                LType = type;
                MinimumDaily = min;
                MaximumDaily = max;
                DaysForPayBack = days;
            }

            public LoanInfo WithInterest(LoanInterest interest)
            {
                Interest = interest;
                return this;
            }

            public LoanInfo WithForbidOthers(bool cannotTakeOtherLoans)
            {
                CannotTakeWithOtherLoan = cannotTakeOtherLoans;
                return this;
            }
            public LoanInfo WithMaxDebt(int debt)
            {
                if (debt % 25 != 0)
                    throw new ArgumentException("Not a correct chip amount", nameof(debt));
                MaximumDebt = debt;
                return this;
            }
            public LoanInfo WithMaxChips(int chips)
            {
                if (chips % 25 != 0)
                    throw new ArgumentException("Not a correct chip amount", nameof(chips));
                MaximumChips = chips;
                return this;
            }
            public LoanInfo WithMustCouncilMustApprove(bool requireApprov)
            {
                MustBeApprovedByManagement = requireApprov;
                return this;
            }

            public string ToDisplay()
            {
                string msg = "Value (you are given): " + this.ChipsGivenToPlayer + "\n";
                msg += $"Starting Value: {this.PlayerStartsPaying}\n";
                msg += $"Maximum time: {this.DaysForPayBack} days\n";
                msg += $"Interest: {this.Interest.Display}\n";
                msg += $"Minimum / Maximum daily: {this.MinimumDaily} / {this.MaximumDaily}\n";
                if(CannotTakeWithOtherLoan != true || MaximumDebt != 0 || MaximumChips != 0 || MustBeApprovedByManagement != true)
                {
                    msg += $"**Notes:**\n";
                    if(CannotTakeWithOtherLoan)
                    {
                        msg += $"* Cannot take with another loan oustanding.\n";
                    }
                    if(MaximumDebt > 0)
                    {
                        msg += $"* Cannot take with outstanding debt of more than {MaximumDebt}\n";
                    }
                    if(MaximumChips > 0)
                    {
                        msg += $"* Cannot take with more than {MaximumChips} chips\n";
                    }
                    if(MustBeApprovedByManagement)
                    {
                        msg += $"* **This loan must be approved by The Council before it is issued.**";
                    }
                }
                return msg;
            }

            [DefaultValue(true)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool CannotTakeWithOtherLoan = true;
            [DefaultValue(0)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int MaximumDebt = 0;
            [DefaultValue(0)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int MaximumChips = 0;
            [DefaultValue(true)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool MustBeApprovedByManagement = true;

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

