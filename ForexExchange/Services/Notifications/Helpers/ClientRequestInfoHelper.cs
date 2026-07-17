using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;

namespace ForexExchange.Services.Notifications.Helpers
{
    public static class ClientRequestInfoHelper
    {
        public static string? GetClientIpAddress(HttpContext httpContext)
        {
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            if (ipAddress != null && ipAddress.Contains("::ffff:", StringComparison.Ordinal))
            {
                ipAddress = ipAddress.Replace("::ffff:", "", StringComparison.Ordinal);
            }

            return ipAddress;
        }

        public static string ParseBrowser(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
                return "نامشخص";

            if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase))
                return $"Microsoft Edge {ExtractVersion(userAgent, @"Edg/([\d.]+)")}";

            if (userAgent.Contains("OPR/", StringComparison.OrdinalIgnoreCase))
                return $"Opera {ExtractVersion(userAgent, @"OPR/([\d.]+)")}";

            if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase))
                return $"Google Chrome {ExtractVersion(userAgent, @"Chrome/([\d.]+)")}";

            if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase))
                return $"Mozilla Firefox {ExtractVersion(userAgent, @"Firefox/([\d.]+)")}";

            if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase))
                return $"Apple Safari {ExtractVersion(userAgent, @"Version/([\d.]+)")}";

            return "مرورگر نامشخص";
        }

        public static string ParseOperatingSystem(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
                return "نامشخص";

            if (userAgent.Contains("Windows NT 10.0", StringComparison.OrdinalIgnoreCase))
                return "Windows 10/11";
            if (userAgent.Contains("Windows NT 6.3", StringComparison.OrdinalIgnoreCase))
                return "Windows 8.1";
            if (userAgent.Contains("Windows NT 6.1", StringComparison.OrdinalIgnoreCase))
                return "Windows 7";
            if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
                return "Windows";

            if (userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase))
            {
                var version = ExtractVersion(userAgent, @"Mac OS X ([\d_]+)").Replace('_', '.');
                return string.IsNullOrWhiteSpace(version) ? "macOS" : $"macOS {version}";
            }

            if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
                return $"Android {ExtractVersion(userAgent, @"Android ([\d.]+)")}".Trim();

            if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
                || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
                return $"iOS {ExtractVersion(userAgent, @"OS ([\d_]+)").Replace('_', '.')}".Trim();

            if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
                return "Linux";

            return "نامشخص";
        }

        private static string ExtractVersion(string input, string pattern)
        {
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }
    }
}
