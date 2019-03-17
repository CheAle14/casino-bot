using Casino;
using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Addons.Interactive;
using Discord.WebSocket;
using DiscordBot.Attributes;

namespace DiscordBot.Modules
{
    /// <summary>
    /// Handles any and all calls for impeachment.
    /// </summary>
    [Name("Council Impeachment"), Group("impeachment"), Alias("impeach"), CasinoCommand]
    public class ImpeachmentModule : CasinoInteractiveBase<SocketCommandContext>
    {
        public static ImpeachmentProceeding Impeachment;
        public static Services.MediaWikiService Wiki;

        public ImpeachmentModule(Services.MediaWikiService wiki) { Wiki = wiki; }

        [Command("start"), Summary("Begins a new impeachment proceeding against the target, for a given reason")]
        [RequireRole(false, false, "Council Member")]
        public async Task StartProceedings(CasinoMember target, string reason)
        {
            if (Impeachment != null)
            {
                await ReplyAsync("Error: there is already a proceeding:", false, Impeachment.ToEmbed());
            }
            else
            {
                var im = new ImpeachmentProceeding(target, SelfMember, reason);
                var divisions = FourAcesCasino.Divisions.Where(x => x.Employees.Contains(target.User));
                if (divisions.Count() == 0)
                {
                    await ReplyAsync("Error: that target is not employed at all");
                    return;
                } else if(divisions.Count() == 1)
                {
                    im.RemovingFrom.Add(divisions.First());
                } else
                {
                    EmbedBuilder builder = new EmbedBuilder()
                    {
                        Title = "Multiple Options",
                        Description = "Which Division is the person to be impeached from?\nPlease chose an option by giving the number"
                    };

                    builder.AddField("0 - All Divisions", "From ***every*** Division");
                    int opt = 1;
                    foreach(var d in divisions)
                    {
                        builder.AddField($"{opt++} - {d.Name}", d.DivisionHead.Id == target.User.Id ? "Division Head" : "Employee");
                    }

                    await ReplyAsync("Please chose:", false, builder.Build());

                    var next = await NextMessageAsync(timeout: TimeSpan.FromMinutes(3));

                    if(next == null)
                    {
                        await ReplyAsync("Error: no reply at all");
                        return;
                    }
                    if (int.TryParse(next.Content, out int id))
                    {
                        if(id == 0)
                        {
                            im.RemovingFrom.AddRange(divisions);
                        } else
                        {
                            int count = 1;
                            foreach(var d in divisions)
                            {
                                if(count == id)
                                {
                                    im.RemovingFrom.Add(d); break;
                                }
                                count++;
                            }
                        }
                    } else
                    {
                        await ReplyAsync("Error: could not parse as integer");
                        return;
                    }

                }


                await ReplyAsync("Please reply with `confirm` to submit the following impeachment:", false, im.ToEmbed());

                var confirm = await NextMessageAsync(timeout: TimeSpan.FromMinutes(2));
                if(confirm != null && confirm.Content == "confirm")
                {
                    Impeachment = im;
                    ITextChannel channel = null;
                    if(Program.BOT_DEBUG)
                    {
                        channel = Program.CasinoGuild.GetTextChannel(443073939028049921);
                    } else
                    {
                        channel = Program.C_GENERAL;
                    }
                    var message = await channel.SendMessageAsync($"{Program.CasinoGuild.EveryoneRole.Mention} \r\nAn impeachment proceeding has now been brought" +
                        $"\r\nAn impeachment should only take place for the most serious of offences of the Contract.");
                    Impeachment.SentMessage = message;
                    Impeachment.ToEmbed();

                } else
                {
                    await ReplyAsync("Impeachment canceled");
                }
            }
        }

        public async Task CheckForEnd()
        {
            if(Impeachment != null)
            {
                if(Impeachment.TargetIndicted)
                {
                    if(Impeachment.TargetRemoved)
                    {
                        await Impeachment.SentMessage.Channel.SendMessageAsync($"{Impeachment.Target.Name} has been impeached and removed!");
                        foreach(var div in Impeachment.RemovingFrom)
                        {
                            div.Employees.RemoveAll(x => x.Id == Impeachment.Target.User.Id);
                            if(div.DivisionHead.Id == Impeachment.Target.User.Id)
                            {
                                if(div.Employees.Count > 0)
                                {
                                    div.DivisionHead = div.Employees.FirstOrDefault();
                                    await div.DivisionHead.AddRoleAsync(div.DivisionHeadRole);
                                    await div.DivisionStaffChannel.SendMessageAsync($"Division Head was impeached and {div.DivisionHead.Username} has been promoted");
                                } else
                                {
                                    div.DivisionHead = FourAcesCasino.GetMember("Four Aces Casino").User;
                                    await div.DivisionStaffChannel.SendMessageAsync($"Division Head was impeached\r\nDivision currently has no employees at all.");
                                }
                            } else
                            {
                                await div.DivisionStaffChannel.SendMessageAsync($"{Impeachment.Target.Name} was impeached for {Impeachment.Reason}\r\nThey ***must not*** be re-hired");
                            }
                        }
                        await Program.C_LOGS_FAC_DIVISION.SendMessageAsync("Logging impeachment vote that passed successfully", false, Impeachment.ToEmbed());
                        Impeachment = null;
                        FourAcesCasino.Save();
                    }
                }
            }
        }

        [Command("approve"), Summary("Records your approval vote"), RequireRole(false, false, "Member")]
        public async Task ApproveVote()
        {
            if(Impeachment == null)
            {
                await ReplyAsync("There is no Impeachment to approve");
            } else
            {
                if(Impeachment.MemberApprovals.Contains(SelfMember))
                {
                    await ReplyAndDeleteAsync("Error: you have already voted");
                    return;
                }
                await ReplyAndDeleteAsync("Please confirm that you would like to vote ***that this person be impeached***\nReply with `confirm`", false, Impeachment.ToEmbed(), timeout:TimeSpan.FromSeconds(58));
                var next = await NextMessageAsync(timeout: TimeSpan.FromSeconds(60));
                if(next != null && next.Content == "confirm")
                {
                    if(SelfMember.User.IsCouncilMember())
                    {
                        Impeachment.CouncilApprovals.Add(SelfMember);
                    }
                    Impeachment.MemberApprovals.Add(SelfMember); // no point having them do it twice
                }
                else
                {
                    await ReplyAndDeleteAsync("Cancelled vote");
                }
                if(Context.Channel.Id != Impeachment.SentMessage.Channel.Id)
                    await ReplyAsync("Impeachment now:", false, Impeachment.ToEmbed());
                await CheckForEnd();
            }
        }

    }

    public class ImpeachmentProceeding
    {
        public ImpeachmentProceeding(CasinoMember target, CasinoMember from, string reason)
        {
            Target = target;
            Reason = reason;
            Initiator = from;
            CouncilApprovals = new List<CasinoMember>() { from };
            MemberApprovals = new List<CasinoMember>() { from };
            RemovingFrom = new List<Division>();
        }

        /// <summary>
        /// Target who is being impeached
        /// </summary>
        public CasinoMember Target;

        /// <summary>
        /// A list of Divisions that the Target should be impeached from.
        /// </summary>
        public List<Division> RemovingFrom;


        /// <summary>
        /// Message sent to #general to notify all people
        /// </summary>
        public IUserMessage SentMessage;


        /// <summary>
        /// A reason for impeachment
        /// </summary>
        public string Reason;

        /// <summary>
        /// Are we trying to impeach another Council Member from their position?
        /// </summary>
        public bool ImpeachingCouncilMember => FourAcesCasino.Divisions.Where(x => x.DivisionHead.Id == Target.User.Id).Count() > 0;

        /// <summary>
        /// Who introduced the impeachment
        /// </summary>
        public CasinoMember Initiator;

        /// <summary>
        /// Council Members who agreed to the impeachment
        /// </summary>
        public List<CasinoMember> CouncilApprovals;

        /// <summary>
        /// Has the Target been indicted? (ie: Has the Council approved)
        /// </summary>
        public bool TargetIndicted { get
            {
                return CouncilApprovals.Count >= Program.COUNCIL_MAJORITY;
            } }

        /// <summary>
        /// Casino Members who agreed to the impeachment
        /// </summary>
        public List<CasinoMember> MemberApprovals;

        public bool TargetRemoved { get
            {
                return MemberApprovals.Count >= Program.MEMBER_SUPER_MAJORITY;
            } }

        public Embed ToEmbed()
        {
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = "Impeaching " + Target.Name,
                Description = $"Removing from:\n" + string.Join("\n", RemovingFrom.Select(x => (x.DivisionHead.Id == Target.User.Id ? "Division Head " : "") + x.Name)),
                Color = Color.Red,
                ThumbnailUrl = Target.User.GetAvatarUrl()
            };
            string impeach = "*Pending*:";
            if (TargetIndicted)
                impeach = "**Passed**:";
            foreach(var m in CouncilApprovals) { impeach += "\n- " + m.Name; }

            builder.AddField(x =>
            {
                x.Name = "Impeachment";
                x.Value = impeach;
                x.IsInline = true;
            });

            string remove = "Not yet started";
            if(TargetIndicted)
            {
                if(TargetRemoved)
                {
                    remove = "**Passed**:";
                } else
                {
                    remove = "*Pending*:";
                }
                foreach(var m in MemberApprovals) { remove += "\n- " + m.Name; }
            }
            builder.AddField(x =>
            {
                x.Name = "Removal";
                x.Value = remove;
                x.IsInline = true;
            });

            builder.AddField(x =>
            {
                x.Name = "Reason";
                x.Value = this.Reason;
                x.IsInline = false;
            });

            builder.Footer = new EmbedFooterBuilder().WithIconUrl(Initiator.User.GetAvatarUrl()).WithText($"Started by {Initiator.Name}");

            if(SentMessage != null)
            {
                SentMessage.ModifyAsync(x =>
                {
                    x.Embed = builder.Build();
                });
            }
            return builder.Build();
        }
    }
}
