using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Permissions
{
    public class PermissionsService
    {
        /* // -- Permissions under CasinoMember now.
        static List<PermissionUser> PermissionUsers = new List<PermissionUser>();
        public static PermissionUser GetPerm(IUser user)
        {
            var uu = PermissionUsers.FirstOrDefault(x => x.User.Id == user.Id);
            if(uu == null)
            {
                uu = new PermissionUser(user);
                PermissionUsers.Add(uu);
            }
            return uu;
        }*/
        public PermissionsService()
        {
        }

    }

    [System.Diagnostics.DebuggerDisplay("{Description} (for {Division})", Name = "{Node}")]
    public class Permission
    {
        [JsonIgnore]
        public readonly string Node;
        [JsonIgnore]
        public readonly string Division;
        [JsonIgnore]
        public readonly string Description;
        public string FullNode => string.IsNullOrWhiteSpace(Division) ? Node : Division + ":" + Node;


        [JsonIgnore]
        public List<SuppressWarningAttribute> SuppressedWarnings = new List<SuppressWarningAttribute>();

        public bool NotSuppressed<T>() where T : SuppressWarningAttribute
        {
            var type = typeof(T);
            if (!type.IsSubclassOf(typeof(SuppressWarningAttribute)))
                throw new ArgumentException("Type given is not a valid SuppressWarningAttribute: " + type.FullName);
            foreach(var attribute in SuppressedWarnings)
            {
                if (attribute.GetType() ==  type)
                    return false;
            }
            return true;
        }

        string[] nodes;

        [JsonConstructor]
        private Permission(string fullnode)
        {
            var parsed = Perms.Parse(fullnode);
            Node = parsed.Node;
            nodes = Node.Split('.');
            Division = parsed.Division;
            Description = parsed.Description;
        }

        public Permission(System.Reflection.FieldInfo field, Type type)
        {
            var str = field.GetValue(type) as string;
            if (str.Contains(":"))
            {
                Division = str.Substring(0, str.IndexOf(':') + 1);
                str = str.Replace(Division, "");
                Division = Division.Replace(":", "");
            }
            else { Division = ""; }
            Node = str;
            nodes = Node.Split('.');
            var desc = field.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false).FirstOrDefault() as System.ComponentModel.DescriptionAttribute;
            var suppressions = field.GetCustomAttributes(typeof(SuppressWarningAttribute), false);
            foreach(SuppressWarningAttribute attr in suppressions)
            {
                SuppressedWarnings.Add(attr);
            }
            if (desc == null && this.NotSuppressed<SuppressNoDescriptionAttribute>())
                    Program.LogMsg($"{Node} {Division} does not have description attribute assigned.", LogSeverity.Warning, "Perms");
            Description = desc?.Description ?? "";
        }


        public bool HasPerms(Permission testPerm, PermDivisionStatus status, Casino.Division division)
        {
            if (testPerm.Division != "")
            {
                if(status == PermDivisionStatus.NoDivision)
                {
                    // nothing
                } else if(status == PermDivisionStatus.RequirePermissionInContextDivision)
                {
                    if (this.Division != division.Name.Replace(".", "").ToLower())
                        return false;
                } else if(status == PermDivisionStatus.RequirePermissionInAnyDivision)
                {
                    return false;
                }
            } else
            {
                if (status != PermDivisionStatus.NoDivision)
                    return false; // since there isnt a Division, but one is required.
            }
            string testNode = testPerm.Node;
            string[] testNodes = testNode.Split('.');
            if (testNode == Node || Node == "*")
                return true; // node is fully specified: so has perm
            for(int i = 0; i < nodes.Length && i < testNodes.Length; i++)
            {
                if (nodes[i] == "*")
                    return true;
                if (nodes[i] != testNodes[i])
                    return false;
            }
            return false;
        }

        public override string ToString()
        {
            var s = $"`{Node}`";
            if(!string.IsNullOrWhiteSpace(Division))
            {
                s += $" for {Division.ToUpper()}";
            }
            return s;
        }

    }

    [Obsolete("I don't think this is used")]
    public class PermissionUser
    {
        public IUser User;
        public List<Permission> Permissions = new List<Permission>();
        public SocketGuildUser SocketUser(SocketGuild guild)
        {
            return guild.GetUser(User.Id);
        }
        public PermissionUser(IUser user)
        {
            User = user;
            var sock = SocketUser(Program.CasinoGuild);
            if (sock.IsMOAInspector())
                Permissions.Add(Perms.Parse(Perms.Casino.Division.MOA.All));
            else if (sock.IsMOGSupervisor())
                Permissions.Add(Perms.Parse(Perms.Casino.Division.MOG.All));
            else if (sock.IsMOLSupervisor())
                Permissions.Add(Perms.Parse(Perms.Casino.Division.MOL.All));
            else if (sock.IsMOA())
                Permissions.Add(Perms.Parse(Perms.Casino.Division.MOA.Employee));
            else if (sock.IsMOG())
                Permissions.Add(Perms.Parse(Perms.Casino.Division.MOG.Employee));
            else if (sock.IsMOL())
                Permissions.Add(Perms.Parse(Perms.Casino.Division.MOL.Employee));
            if (sock.IsCouncilMember())
                Permissions.Add(Perms.Parse(Perms.Casino.Council.Member));
            if (sock.IsMember())
                Permissions.Add(Perms.Parse(Perms.Casino.Member.All));
            if (sock.IsAlexChester())
                Permissions.Add(Perms.Parse(Perms.Casino.Division.SetBudget));
        }
        public bool HasPermission(Permission perm, PermDivisionStatus status, Casino.Division context)
        {
            foreach(var node in Permissions)
            {
                if (node.HasPerms(perm, status, context))
                    return true;
            }
            return false;
        }
        public override string ToString()
        {
            return $"{User.Username}";
        }
    }

    public enum PermCombination
    {
        Any,
        All,
        None
    }

    public enum PermDivisionStatus
    {
        /// <summary>
        /// Permission does not involve Divisions
        /// </summary>
        NoDivision = 0,
        /// <summary>
        /// Requires the Permission to be from the Context division
        /// </summary>
        RequirePermissionInContextDivision,
        /// <summary>
        /// Requires the permission, but from any Division
        /// </summary>
        RequirePermissionInAnyDivision
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class RequirePermissionAttribute : PreconditionAttribute
    {
        public PermCombination Combination;
        public List<Permission> Permissions;
        public PermDivisionStatus DivisionStatus = PermDivisionStatus.NoDivision;
        public RequirePermissionAttribute(PermCombination combination, params string[] node)
        {
            Combination = combination;
            Permissions = new List<Permission>();
            
            foreach(var p in node)
            {
                var perm = Perms.Parse(p);
                if (perm == null)
                    throw new ArgumentException("Unknown permission node: " + p);
                if (perm.Division != "" && DivisionStatus == PermDivisionStatus.NoDivision)
                    DivisionStatus = PermDivisionStatus.RequirePermissionInContextDivision;
                Permissions.Add(perm);
            }
        }
        /*public RequirePermissionAttribute(PermCombination combination, PermDivisionStatus status, params string[] node)
        {
            Combination = combination;
            Permissions = new List<Permission>();
            DivisionStatus = status;
            foreach (var p in node) { Permissions.Add(Perms.Parse(p)); }
        }*/

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var self = Casino.FourAcesCasino.GetMember(context.User.Id);
            List<Permission> hasPermFor = new List<Permission>();
            List<Permission> noPermsFor = new List<Permission>();

            Casino.Division contextDivision = null;
            if (DivisionStatus != PermDivisionStatus.NoDivision)
                contextDivision = Modules.DivisionModule.GetDivisionFromContext(context);

            foreach (var perm in Permissions)
            {
                bool has = self.HasPermission(perm, contextDivision, DivisionStatus);
                if (has)
                    hasPermFor.Add(perm);
                else
                    noPermsFor.Add(perm);
            }

            if(Combination == PermCombination.All)
            {
                if (noPermsFor.Count == 0)
                {
                    return Task.FromResult(PreconditionResult.FromSuccess());
                }
                else
                {
                    string required = $"**Permissions error**\r\nYou lack the required permissions:\r\n";
                    foreach(var perm in noPermsFor) { required += $"- {perm}\r\n"; }
                    return Task.FromResult(PreconditionResult.FromError(required));
                }
            } else if(Combination == PermCombination.Any)
            {
                if(hasPermFor.Count > 0)
                {
                    return Task.FromResult(PreconditionResult.FromSuccess());
                }
                else
                {
                    string required = $"**Permissions error**\r\nYou require any one of the following permissions:\r\n";
                    foreach (var perm in noPermsFor) { required += $"- {perm}\r\n"; }
                    return Task.FromResult(PreconditionResult.FromError(required));
                }
            } else
            {
                // requires no permissions
                if(hasPermFor.Count > 0)
                {
                    string required = $"**Permissions error**\r\nYou must not have any of the following:\r\n";
                    foreach (var perm in noPermsFor) { required += $"- {perm}\r\n"; }
                    return Task.FromResult(PreconditionResult.FromError(required));
                } else
                {
                    return Task.FromResult(PreconditionResult.FromSuccess());
                }
            }
        }
    }

    public class PermissionEqualityComparer : IEqualityComparer<Permission>
    {
        public bool Equals(Permission x, Permission y)
        {
            return x.FullNode == y.FullNode;
        }

        public int GetHashCode(Permission obj)
        {
            return obj.FullNode.GetHashCode();
        }
    }
}
