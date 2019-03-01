using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using static DiscordBot.MyExtentsions;
using static DiscordBot.Program;
using System.ComponentModel;

namespace Casino
{
    public class Investigation
    {
        public int ID;
        public List<InvestMember> Investigators;
        [JsonIgnore]
        public InvestMember LeadInvestigator
        {
            get
            {
                var lead = Investigators.FirstOrDefault(x => x.Perms == InvestPermissions.Admin);
                if (lead == null)
                {
                    lead = Investigators.FirstOrDefault(x => x.Perms == InvestPermissions.Full);
                }
                return lead;
            }
        }

        [System.ComponentModel.DefaultValue("N/A")]
        public string Topic;

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [System.ComponentModel.DefaultValue("")]
        public string AuditDate;
        [JsonIgnore]
        public bool IsAuditInvest => !string.IsNullOrWhiteSpace(AuditDate);

        [DefaultValue(false)]
        [JsonProperty("IsMogInvest", DefaultValueHandling = DefaultValueHandling.Include)]
        public bool _IsMOGInvest;
        [JsonIgnore]
        public bool IsMOGInvestigation => IsAuditInvest || _IsMOGInvest;
        [JsonIgnore]
        public IRole GroupRole => IsMOGInvestigation ? MOG : MOA;
        [JsonIgnore]
        public IRole AdminRole => IsMOGInvestigation ? MOGSupv : MOAInsp;
        [JsonIgnore]
        public Division RelatedDivision => GroupRole == MOG ? (Division)FourAcesCasino.D_MOG : (Division)FourAcesCasino.D_MOA;

        public void SetLead(SocketGuildUser user)
        {
            var perms = InvestPermissions.Admin;
            if(IsMOGInvestigation && user.IsMOGSupervisor())
            {
                perms = InvestPermissions.Full;
            } else if (!IsMOGInvestigation && user.IsMOAInspector())
            {
                perms = InvestPermissions.Full;
            }
            InvestMember newMem = new InvestMember(user, perms);
            InvestMember currentLead = LeadInvestigator;
            if(currentLead != null)
            {
                Investigators.Remove(currentLead);
            }
            Investigators.Add(newMem);
        }

        public bool HasInvestigator(ulong id)
        {
            return Investigators.Where(x => x.User.Id == id).Count() == 1;
        }

        public InvestMember AddInvestigator(SocketGuildUser user, InvestPermissions perms)
        {
            if(IsMOGInvestigation && user.IsMOGSupervisor())
            {
                perms |= InvestPermissions.Full;
            } else if(!IsMOGInvestigation && user.IsMOAInspector())
            {
                perms |= InvestPermissions.Full;
            }

            InvestMember newMem = new InvestMember(user, perms);
            user.AddRoleAsync(InvestigationRole);
            Investigators.Add(newMem);
            return newMem;
        }

        public InvestMember GetInvestigator(SocketGuildUser user) => Investigators.FirstOrDefault(x => x.User.Id == user.Id);

        public InvestPermissions GetPermissions(SocketGuildUser user)
        {
            InvestPermissions perms = GetInvestigator(user)?.Perms ?? InvestPermissions.None;
            if(IsMOGInvestigation)
            {
                if (user.IsAlexChester())
                    perms |= InvestPermissions.Full;
                if (user.IsMOG())
                    perms |= InvestPermissions.Read;
            } else
            {
                if (user.IsMOAInspector())
                    perms |= InvestPermissions.Full;
                if (user.IsMOA())
                    perms |= InvestPermissions.Read;
            }
            var invest = GetInvestigator(user);
            if(invest != null)
                invest.Perms = perms;
            return perms;
        }

        public InterviewRoom GetRoom(ITextChannel chnl) => InterviewRooms.FirstOrDefault(x => x.Channel.Id == chnl.Id);

        public ICategoryChannel Category;
        public ITextChannel MainChannel;
        public ITextChannel InvestigatorsChannel;
        public IRole InvestigationRole;

        public List<InterviewRoom> InterviewRooms;

        [JsonConstructor]
        private Investigation(string topic, bool ismoginvest, int id, List<InvestMember> investigators, ulong categoryID, ulong mainchannelID, ulong investigatorschannelID, ulong investigationroleID, List<InterviewRoom> interviewRooms, string auditdate, bool _ismoginvest)
        {
            _IsMOGInvest = ismoginvest;
            AuditDate = auditdate;
            Topic = topic;
            ID = id;
            Investigators = investigators;
            Category = DiscordBot.Program.CasinoGuild.GetChannel(categoryID) as ICategoryChannel;
            MainChannel = DiscordBot.Program.CasinoGuild.GetChannel(mainchannelID) as ITextChannel;
            InvestigatorsChannel = DiscordBot.Program.CasinoGuild.GetChannel(investigatorschannelID) as ITextChannel;
            InvestigationRole = DiscordBot.Program.CasinoGuild.GetRole(investigationroleID) as IRole;
            InterviewRooms = interviewRooms;
        }

        public Investigation(SocketGuildUser lead, ITextChannel main, ITextChannel investigators, ICategoryChannel cate, IRole role, int id)
        {
            ID = id;
            Investigators = new List<InvestMember>
            {
                new InvestMember(lead, InvestPermissions.Admin)
            };
            Category = cate;
            MainChannel = main;
            InvestigatorsChannel = investigators;
            InvestigationRole = role;
            InterviewRooms = new List<InterviewRoom>();
        }

        public void CallInterview(SocketGuildUser interview, SocketGuildUser target)
        {
            InterviewRoom invRoom = new InterviewRoom(DiscordBot.Program.CasinoGuild.GetChannel(439450752692518914) as ITextChannel, interview, target);
            InterviewRooms.Add(invRoom);
        }

    }
}

