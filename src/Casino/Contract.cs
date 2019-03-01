using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Newtonsoft.Json;
using static DiscordBot.MyExtentsions;
using static DiscordBot.Program;

namespace Casino
{
    public class Contract
    {
        // current when voting new contract in
        [JsonIgnore]
        public IUserMessage StaffChannelMsg;
        [JsonRequired]
        ulong StaffChannelMsgID => StaffChannelMsg == null ? 0 : StaffChannelMsg.Id;
        public DateTime ContractSentTime;

        public string PendingContractFileURL;
        public string PendingContractRemarks;

        [JsonIgnore]
        public IUserMessage ContractFileMessage;
        [JsonRequired]
        ulong ContractFileMessageID => ContractFileMessage == null ? 0 : ContractFileMessage.Id;
        [JsonIgnore]
        public IUserMessage ContractVersionMessage;
        [JsonRequired]
        ulong ContractVersionMessageID => ContractVersionMessage == null ? 0 : ContractVersionMessage.Id;
        [JsonIgnore]
        public IUserMessage ContractRemarksMessage;
        [JsonRequired]
        ulong ContractRemarksMessageID => ContractRemarksMessage == null ? 0  : ContractRemarksMessage.Id;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [JsonIgnore]
        public GithubDLL.Entities.PullRequest PullRequest;

        [JsonProperty]
        int pullRequestID => PullRequest?.Number ?? 0;

        [JsonProperty]
        public List<ulong> VotesForApproval = new List<ulong>();

        public bool HasBeenCouncilApproved = false;

        [JsonIgnore]
        public int RequireVotesForApproval
        {
            get
            {
                if(HasBeenCouncilApproved)
                {
                    int numMembers = FourAcesCasino.Members.Count;
                    numMembers = numMembers / 2;
                    return numMembers;
                }
                int numCouncil = FourAcesCasino.Members.Where(x => x.User.IsCouncilMember()).Count();
                double divThree = numCouncil / 3;
                double multTwo = divThree * 2;
                return (int)multTwo;
            }
        }


        [JsonIgnore]
        public bool HasBeenApproved
        {
            get
            {
                return (ContractFileMessage != null) && (ContractVersionMessage != null);
            }
        }
        [JsonIgnore]
        public bool IsPendingContract
        {
            get
            {
                return StaffChannelMsg != null;
            }
        }

        public int ContractVersion;

        [JsonConstructor]
        private Contract(bool hasbeencouncilapproved, ulong staffchannelmsgid, ulong contractfilemessageid, ulong contractversionmessageid, ulong contractremarksmessageid, int contractversion, DateTime contractsenttime, string pendingContractFileURL, string pendingContractRemarks, int pullrequestid)
        {
            PendingContractFileURL = pendingContractFileURL;
            PendingContractRemarks = pendingContractRemarks;
            ContractSentTime = contractsenttime;
            ContractVersion = contractversion;
            HasBeenCouncilApproved = hasbeencouncilapproved;
            if(!HasBeenCouncilApproved)
                StaffChannelMsg = staffchannelmsgid == 0 ? null : C_COUNCIL.GetMessageAsync(staffchannelmsgid).Result as IUserMessage;
            else
                StaffChannelMsg = staffchannelmsgid == 0 ? null :  C_MEMBER_CHANGES.GetMessageAsync(staffchannelmsgid).Result as IUserMessage;
            ContractFileMessage = contractfilemessageid == 0 ? null :  C_MEMBER_CONTRACT.GetMessageAsync(contractfilemessageid).Result as IUserMessage;
            ContractVersionMessage = contractversionmessageid == 0 ? null : C_MEMBER_CONTRACT.GetMessageAsync(contractversionmessageid).Result as IUserMessage;
            ContractRemarksMessage = contractremarksmessageid == 0 ? null : C_MEMBER_CONTRACT.GetMessageAsync(contractremarksmessageid).Result as IUserMessage;
            PullRequest = pullrequestid > 0 ? DiscordBot.Services.GithubService.Client.GetPullRequest(DiscordBot.Services.GithubService.Client.GetRepository("CheAle14", "FourAcesContracts"), pullrequestid) : null;
        }

        public Contract(int version, IUserMessage prior_voting, IUserMessage file, IUserMessage mversion, IUserMessage remarks)
        {
            ContractVersion = version;
            StaffChannelMsg = prior_voting;
            ContractFileMessage = file;
            ContractVersionMessage = mversion;
            ContractRemarksMessage = remarks;
            ContractSentTime = DateTime.Now;
        }

    }
}

