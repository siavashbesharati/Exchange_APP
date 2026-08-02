using Microsoft.AspNetCore.Authorization;

namespace ForexExchange.Authorization
{
    /// <summary>
    /// Staff gate: any active non-Customer user (including custom roles created in the panel).
    /// Use with [HasPermission(...)] for fine-grained access.
    /// </summary>
    public class StaffAttribute : AuthorizeAttribute
    {
        public const string PolicyName = "StaffAccess";

        public StaffAttribute()
        {
            Policy = PolicyName;
        }
    }
}
