using Microsoft.AspNetCore.Authorization;

namespace ForexExchange.Authorization
{
    /// <summary>
    /// Allows any active non-Customer staff user (seeded or custom panel roles).
    /// </summary>
    public class StaffRequirement : IAuthorizationRequirement
    {
    }
}
