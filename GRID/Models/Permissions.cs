namespace GRID.Models
{
    public static class Permissions
    {
        // Admin area access
        public const string AdminAccess = "admin.access";
        public const string AdminUsers = "admin.users";
        public const string AdminRoles = "admin.roles";
        public const string AdminInvites = "admin.invites";
        public const string AdminContacts = "admin.contacts";
        public const string AdminServices = "admin.services";
        public const string AdminAuditLog = "admin.auditlog";

        // Service usage
        public const string ServicesUse = "services.use";

        // Documentation
        public const string DocsView = "docs.view";
        public const string AdminDocs = "admin.docs";

        public static readonly string[] All =
        [
            AdminAccess,
            AdminUsers,
            AdminRoles,
            AdminInvites,
            AdminContacts,
            AdminServices,
            AdminAuditLog,
            AdminDocs,
            ServicesUse,
            DocsView,
        ];

        public static readonly Dictionary<string, string> Labels = new()
        {
            [AdminAccess]   = "Admin Dashboard",
            [AdminUsers]    = "Manage Users",
            [AdminRoles]    = "Manage Roles",
            [AdminInvites]  = "Manage Invites",
            [AdminContacts] = "Manage Contact Requests",
            [AdminServices] = "Manage Services",
            [AdminAuditLog] = "View Audit Log",
            [AdminDocs]     = "Manage Documentation",
            [ServicesUse]   = "Use Quick Services",
            [DocsView]      = "View Private Documentation",
        };
    }
}
