namespace ForexExchange.Models
{
    public static class Permissions
    {
        // Document Management
        public const string Documents_View = "Permissions.Documents.View";
        public const string Documents_Create = "Permissions.Documents.Create";
        public const string Documents_Edit = "Permissions.Documents.Edit";
        public const string Documents_Delete = "Permissions.Documents.Delete";

        // User Management
        public const string Users_View = "Permissions.Users.View";
        public const string Users_Create = "Permissions.Users.Create";
        public const string Users_Edit = "Permissions.Users.Edit";
        public const string Users_ChangeRole = "Permissions.Users.ChangeRole";
        public const string Users_Delete = "Permissions.Users.Delete";
        public const string Users_RegenerateTotpSecret = "Permissions.Users.RegenerateTotpSecret";
        public const string Users_ResetAllSessions = "Permissions.Users.ResetAllSessions";

        // Add more permissions as needed
    }
}
