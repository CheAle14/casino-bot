using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace DiscordBot.Permissions
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class SuppressWarningAttribute : Attribute { }
    /// <summary>
    /// Suppresses the warning indicating that the node has no '*' permission
    /// </summary>
    public class SuppressNoAsteriskAttribute : SuppressWarningAttribute { }
    /// <summary>
    /// Suppresses the warning if there is no description on the node.
    /// </summary>
    public class SuppressNoDescriptionAttribute : SuppressWarningAttribute { }

    public static partial class Perms
    {
        [Description("All permissions across all servers")]
        public const string All = "*";

        static List<Type> getTypes(Type type) => type.GetNestedTypes().ToList();
        static Dictionary<string, Permission> permissions;

        public static List<Permission> AllPermissions => permissions.Values.ToList();

        static List<Permission> getAllPerms(Type mainType)
        {
            List<Permission> perms = new List<Permission>();

            var fields = from f in mainType.GetFields()
                         where f.FieldType == typeof(string)
                         select f;

            bool hasAllPermNode = false; // indicates whether there is a '*' node in here.
            Permission firstNode = null;
            foreach(var perm in fields)
            {
                var permission = new Permission(perm, mainType);
                firstNode = firstNode ?? permission;
                if (permission.Node.EndsWith("*"))
                    hasAllPermNode = true;
                perms.Add(permission);
            }
            if (!hasAllPermNode && firstNode.NotSuppressed<SuppressNoAsteriskAttribute>())
                    Program.LogMsg("Node is missing a '*' permission", Discord.LogSeverity.Warning, firstNode.FullNode);

            foreach (var t in getTypes(mainType))
            {
                perms.AddRange(getAllPerms(t));
            }
            return perms;
        }
        public static Permission Parse(string inpt)
        {
            if(permissions == null)
            {
                var p = getAllPerms(typeof(Perms));
                permissions = new Dictionary<string, Permission>();
                foreach (var perm in p) {
                    permissions.Add($"{perm.FullNode}", perm);
                }
            }
            if (inpt == "*")
                return permissions["*"];
            string[] split = inpt.Split('.');
            foreach(var p in permissions)
            {
                if (p.Key.ToLower() == inpt.ToLower())
                    return p.Value;
            }
            return null;
        }
    }
}
