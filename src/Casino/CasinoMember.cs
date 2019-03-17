using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using static DiscordBot.Program;
using System.ComponentModel;
using DiscordBot.Permissions;

namespace Casino
{
    public class CasinoMember
    {
        public override string ToString()
        {
            return $"{User.Nickname ?? User.Username} - {PlayerChips} - {Balance.ToString()}";
        }
        [JsonConverter(typeof(UlongConverter))]
        public SocketGuildUser User;

        public virtual string Name => User.Nickname ?? User.Username;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string WikiAccount;

        public bool IsEntitledWage => CurrentAudit != null && !(CurrentAudit.TotalValue > FourAcesCasino.MaximumAmountForWages || CurrentAudit.TotalValue > (FourAcesCasino.FourAcesMember.CurrentAudit.TotalValue * FourAcesCasino.MaximumPercentageOfCasinoForWages));

        public List<Permission> Permissions = new List<Permission>();
        public Embed DisplayPermissions(Division onlyForDivision = null)
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = "Global Permissions for " + this.Name,
                Description = "These are the permissions globally recorded for the above user.",
            };
            var facPerms = Permissions.Where(x => x.Node.StartsWith("casino."));
            var tgcPerms = Permissions.Where(x => x.Node.StartsWith("tgc."));
            var otherPerms = Permissions.Where(x => !(x.Node.StartsWith("casino.") || x.Node.StartsWith("tgc.")));
            if(facPerms.Count() > 0)
            {
                if(onlyForDivision != null)
                {
                    facPerms = facPerms.Where(x => x.Division == onlyForDivision.Name.Replace(".", "").ToLower());
                }
                builder.AddField(x =>
                {
                    x.Name = "Casino Permissions";
                    x.Value = string.Join("\r\n", facPerms.Select(y => $"{(y.Division == "" ? "" : y.Division.ToUpper() + " ")}`{y.Node}`: {y.Description}"));
                });
            }
            if(tgcPerms.Count() > 0 && onlyForDivision == null)
            {
                builder.AddField(x =>
                {
                    x.Name = "The Grand Coding Permissions";
                    x.Value = string.Join("\r\n", tgcPerms.Select(y => $"`{y.Node}`{(y.Division == "" ? "" : " for " + y.Division)}: {y.Description}"));
                });
            }
            if(otherPerms.Count() > 0 && onlyForDivision == null)
            {
                builder.AddField(x =>
                {
                    x.Name = "Other / Misc Permissions";
                    x.Value = string.Join("\r\n", otherPerms.Select(y => $"`{y.Node}`{(y.Division == "" ? "" : " for " + y.Division)}: {y.Description}"));
                });
            }
            if (builder.Fields.Count == 0)
                builder.AddField("No Permissions", "There are no global permissions recorded.\r\nUser's role may still determine some actions");
            return builder.Build();
        }
        public bool HasPermission(Permission perm, Division division = null, PermDivisionStatus status = PermDivisionStatus.NoDivision)
        {
            if (perm == null)
                return false;
            if (!string.IsNullOrWhiteSpace(perm.Division) && status == PermDivisionStatus.NoDivision)
                status = PermDivisionStatus.RequirePermissionInContextDivision;
            foreach (var node in Permissions)
                if (node.HasPerms(perm, status, division))
                    return true;
            return false;
        }
        public bool HasPermission(string perm, Division division = null, PermDivisionStatus status = PermDivisionStatus.NoDivision) => HasPermission(Perms.Parse(perm), division, status);
        public bool HasPermission(string permString, out Permission perm, Division division = null, PermDivisionStatus status = PermDivisionStatus.NoDivision)
        {
            perm = Perms.Parse(permString);
            return HasPermission(perm, division, status);
        }
        public void AddPermission(Permission perm, bool throwErrorIfDuplicate = false)
        {
            bool contains = false;
            foreach(var p in Permissions)
            {
                if(perm.FullNode == p.FullNode)
                {
                    contains = true;
                    break;
                }
            }
            if(contains)
            {
                if (throwErrorIfDuplicate)
                    throw new InvalidOperationException($"{Name} already has permission {perm.FullNode}");
            } else
            {
                Permissions.Add(perm);
            }
        }
        public void RemovePermission(Permission perm, bool throwErrorIfMissing = false)
        {
            bool contains = false;
            foreach(var p in Permissions)
            {
                if(p.FullNode == perm.FullNode)
                {
                    contains = true;
                    break;
                }
            }
            if(contains)
            {
                Permissions.RemoveAll(x => x.FullNode == perm.FullNode);
            } else
            {
                if (throwErrorIfMissing)
                    throw new InvalidOperationException($"{Name} does not have permission {perm.FullNode}");
            }
        }

        public VIPBalance Balance;

        public List<Audit> Audits = new List<Audit>();
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public List<Loan> Loans = new List<Loan>();
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public List<Award> Awards = new List<Award>();
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public List<Citation> Citations = new List<Citation>();

        [JsonIgnore]
        public Audit CurrentAudit => Audits.LastOrDefault();

        [JsonIgnore]
        public List<Loan> CompletedLoans => Loans.Where(x => x.Finished).ToList();
        [JsonIgnore]
        public List<Loan> CurrentLoans => Loans.Where(x => !x.Finished).ToList();

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int LastDayOfBlackjackEarnings;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int TodaysBlackjackEarnings;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int PokerEarnings;

        public int PokerCanEarnMaximum { get
            {
                int val = FourAcesCasino.D_MOG.MaximumPokerEarningsInOneDay - PokerEarnings;
                if (val < 0)
                    return 0;
                return val;
            } }


        [System.ComponentModel.DefaultValue(3000)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        private int _playerchips;

        public virtual void SetPlayerChips(int amount, string reason)
        {
            if (FourAcesCasino.Initialised == true)
            {
                EmbedBuilder builder = new EmbedBuilder()
                {
                    Title = $"{User.Nickname ?? User.Username} Chips Updated",
                    Description = "Reason: " + reason,
                    Footer = DateTimeFooter
                };
                int difference = _playerchips - amount;
                builder.AddField(x =>
                {
                    x.Name = "Was";
                    x.Value = _playerchips;
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
                    x.Value = (difference > 0 ? "Removed" : "Added") + $" {Math.Abs(difference)}";
                    x.IsInline = true;
                });
                C_LOGS_FAC_CHIP.SendMessageAsync("", false, builder.Build());
                _playerchips = amount;
            }
        }
        public void IncreasePlayerChips(int amountBy, string reason)
        {
            SetPlayerChips(_playerchips + amountBy, reason);
            if (reason.ToLower().Contains("blackjack"))
            {
                this.LastDayOfBlackjackEarnings = DateTime.Now.DayOfYear;
                this.TodaysBlackjackEarnings += amountBy;
            }
        }
        public void DecreasePlayerChips(int amountBY, string reason)
        {
            SetPlayerChips(_playerchips - amountBY, reason);
            if(reason.ToLower().Contains("blackjack"))
            {
                this.LastDayOfBlackjackEarnings = DateTime.Now.DayOfYear;
                this.TodaysBlackjackEarnings -= amountBY;
            }
            if(reason.ToLower().Contains("poker"))
            { // reduce earnings too
                this.PokerEarnings -= amountBY;
                if (this.PokerEarnings < 0)
                    this.PokerEarnings = 0;
            }
        }


        [JsonIgnore]
        public virtual int PlayerChips { get { return _playerchips; }  set
            {
                if(FourAcesCasino.Initialised == true)
                {
                    EmbedBuilder builder = new EmbedBuilder()
                    {
                        Title = $"{User.Nickname ?? User.Username} Chips Updated",
                        Description = "Reason for update is unknown.",
                        Footer = DateTimeFooter
                    };
                    builder.AddField(x =>
                    {
                        x.Name = "Was";
                        x.Value = _playerchips;
                        x.IsInline = true;
                    });
                    builder.AddField(x =>
                    {
                        x.Name = "Is";
                        x.Value = value;
                        x.IsInline = true;
                    });
                    C_LOGS_FAC_CHIP.SendMessageAsync("", false, builder.Build());
                }
                _playerchips = value;
            } }

        [JsonConstructor]
        public CasinoMember(ulong id, VIPBalance balance, List<Audit> audits, List<Loan> loans, List<Award> awards, List<Citation> citations, int _playerChips)
        {
            User = DiscordBot.Program.CasinoGuild.GetUser(id);
            Balance = balance ?? new VIPBalance(this);
            //Balance.Member = this;
            Audits = audits;
            Loans = loans ?? new List<Loan>();
            Awards = awards ?? new List<Award>();
            Citations = citations ?? new List<Citation>();
            _playerchips = _playerChips;
            // Commented out because I have VIP checks elsewhere
            /*if(Balance.RemainingBalance >= 200)
            {
                if(!User.HasVIPAbility())
                {
                    if(Balance.RemainingPlayerChips > 0)
                    {
                        User.AddRoleAsync(VIP);
                    } else
                    {
                        // high roller
                        User.AddRoleAsync(HighRollerRole);
                    }
                }
            } else
            {
                if(User.HasVIPAbility())
                {
                    User.RemoveRolesAsync(new List<IRole> { VIP, HighRollerRole });
                }
            }*/
        }

        public CasinoMember(SocketGuildUser user)
        {
            User = user;
            Audits = new List<Audit>();
            _playerchips = 4000;
            Balance = new VIPBalance(this);
        }
    }
}

