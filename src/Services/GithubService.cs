using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;
using Discord;
using static DiscordBot.Program;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Http.Formatting;
using System.IO;
using GithubDLL.Entities;

namespace DiscordBot.Services
{
    public class GithubService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IConfigurationRoot _config;
        private readonly IServiceProvider _provider;

        public static GithubDLL.GithubClient Client;
        public GithubDLL.GithubClient client => Client;

        static List<ulong> channelsWeReplyTo = new List<ulong>()
        {
#if DEBUG
            523937457247354880, // mog test
            536147694440153089 // mog test-2
#else
            365233803217731584,
            368039397796610048,
            392719070912577546,
            497053965461225493
#endif
        };
            

        static string _auth;
        static string AuthToken
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_auth))
                    _auth = File.ReadAllText(Program.JOINPATH(Program.MAIN_PATH, "tgc-bot.token"));
                return _auth;
            }
        }
        public const string RepoRegex = @"(?<=repos)\/.*\/.*";
        public const string IssueFindRegex = @"\S*\/\S*#\d+";

        public static void Start()
        {
            if(Client == null)
            {
                Client = new GithubDLL.GithubClient(AuthToken, "tgc-bot");
            }
        }

        public GithubService(
            DiscordSocketClient discord,
            CommandService commands,
            IConfigurationRoot config,
            IServiceProvider provider)
        {
            _discord = discord;
            _commands = commands;
            _config = config;
            _provider = provider;
            Start();

            _discord.MessageReceived += _discord_MessageReceived;
        }


        // Parses the input to search for any text that matches owner/repository#issue
        public static List<Issue> GetIssues(string input)
        {
            List<Issue> issues = new List<Issue>();
            var regex = new Regex(IssueFindRegex);
            var match = regex.Matches(input);
            foreach (Match mat in match)
            {
                var text = mat.Value;
                string[] split = text.Split('/');
                var owner = split[0];
                string[] secondSplit = split[1].Split('#');
                var repo = secondSplit[0];
                var id = secondSplit[1];
                var issue = Client.GetIssue(owner, repo, int.Parse(id));
                issues.Add(issue);
            }
            return issues;
        }

        private async Task _discord_MessageReceived(SocketMessage arg)
        {
            if (arg.Author.IsBot || arg.Author.IsWebhook)
                return;
            if (!channelsWeReplyTo.Contains(arg.Channel.Id))
                return;
            var matches = GetIssues(arg.Content);
            int max = 0;
            foreach(var match in matches)
            {
                max++;
                if (max > 3)
                    return;
                await arg.Channel.SendMessageAsync("", false);
            }
        }
    }

    public static class GithubExtensions
    {
        public static Embed ToEmbed(this Issue issue)
        {
            string description = "Opened at " + issue.CreatedAt.ToString();
            if (issue.IsClosed)
                description += "\r\n**Closed** at: " + issue.ClosedAt.ToString();
            if (issue.labels != null && issue.labels.Count > 0)
                description += "\r\nLabels: " + string.Join(", ", issue.labels.Select(x => x.Name));
            if (issue.Assignees != null && issue.Assignees.Count > 0)
                description += "\r\nAssignee(s): " + string.Join(", ", issue.Assignees.Select(x => x.Login));
            description += "\r\n[Link to web](" + issue.HTMLURL + ")";
            EmbedBuilder builder = new EmbedBuilder()
            {
                Title = $"{(issue.IsPullRequest ? "PR" : "")}#{issue.Number}: {issue.Title}{(issue.State == "closed" ? " [CLOSED]" : "")}",
                Description = description,
                Color = issue.State == "closed" ? Color.Red : Color.Green,
                ThumbnailUrl = issue.Assignee == null ? "" : issue.Assignee.AvatarURL,
                Footer = new EmbedFooterBuilder() { Text = $"{issue.RepositoryOwner} • {issue.RepositoryName} {(issue.Locked ? "• Locked" : "")}" }
            };
            if (issue.IsPullRequest)
            {
                var str = "";
                issue.FetchPullRequest();
                str += $"\r\nThis {(issue.Pullrequest.Merged ? "merged" : "will merge")} **{issue.Pullrequest.Head.Name}** into {issue.Pullrequest.Base.Name}";
                string canBe = "might be mergeable - currently unknown";
                if (issue.Pullrequest.Mergeable.GetValueOrDefault(false))
                {
                    canBe = "can be merged";
                }
                else if (issue.Pullrequest.Merged)
                {
                    canBe = $"was merged by {issue.Pullrequest.MergedBy.Login} at {issue.Pullrequest.MergedAt}";
                }
                str += $"\r\nThis pull request " + canBe;
                builder.AddField(x =>
                {
                    x.Name = "Pull Request";
                    x.Value = str;
                });
            }
            return builder.Build();
        }

        public static Embed ToEmbed(this User user)
        {
            var builder = new EmbedBuilder()
            {
                Title = user.Login,
                ThumbnailUrl = user.AvatarURL,
                Url = user.HtmlURL
            };
            if (user.Type == UserType.Organization)
                builder.AddField("Organization", "This user is an organisation");
            return builder.Build();
        }
    }
}
