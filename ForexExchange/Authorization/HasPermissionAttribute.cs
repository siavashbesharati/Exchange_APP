using Microsoft.AspNetCore.Authorization;
using System;

namespace ForexExchange.Authorization
{
    public class HasPermissionAttribute : AuthorizeAttribute
    {
        public HasPermissionAttribute(string permissionName)
        {
            Policy = permissionName; // Policy name is the permission name
        }
    }
}
