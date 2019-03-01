using System.ComponentModel;

namespace DiscordBot.Permissions
{
    public static partial class Perms
    {
        public static partial class Casino
        {
            public static partial class Division
            {
                public static class MOA
                {
                    [Description("Division head")]
                    public const string All = "moa:casino.division.*";
                    [Description("Employee")]
                    public const string Employee = "moa:casino.division.employee";

                    // Permissions
                    [Description("Set other employee wages")]
                    public const string SetOtherWage = "moa:casino.division.staff.wages.other";
                    [Description("Set own wages")]
                    public const string SetSelfWage = "moa:casino.division.staff.wages.self";
                    [Description("Set all wages")]
                    public const string SetAllWages = "moa:casino.division.staff.wages.*";
                    [Description("Set a wage to zero")]
                    public const string SetZeroWage = "moa:casino.division.staff.wages.0";
                    [Description("Set wages to a maximum of 50")]
                    public const string SetMaxWage50 = "moa:casino.division.staff.wages.50";
                    [Description("Set wages to a maximum of 100")]
                    public const string SetMaxWage100 = "moa:casino.division.staff.wages.100";
                    [Description("Set wages to a maximum of 200")]
                    public const string SetMaxWage200 = "moa:casino.division.staff.wages.200";
                    [Description("Set wages to a maximum of 300")]
                    public const string SetMaxWage300 = "moa:casino.division.staff.wages.300";

                    [Description("Manage the division's budget")]
                    public const string ManageBudget = "moa:casino.division.budget.*";

                    [Description("Hire any employee")]
                    public const string Hire = "moa:casino.division.staff.hire";
                    [Description("Fire any employee")]
                    public const string Fire = "moa:casino.division.staff.fire";
                    [Description("Set permissions")]
                    public const string Permissions = "moa:casino.division.staff.perms";
                    [Description("Manage any employee")]
                    public const string ManageEmployees = "moa:casino.division.staff.*";


                    // Investigations
                    [Description("Start an investigation")]
                    public const string StartInvestigation = "moa:casino.division.investigations.start";
                    [Description("Approve citations against any Member via an Investigation")]
                    public const string CitateMember = "moa:casino.division.investigations.citate";
                    [Description("All Investigation permissions")]
                    public const string InvestAll = "moa:casino.division.investigations.*";
                }
            }
        }
    }
}
