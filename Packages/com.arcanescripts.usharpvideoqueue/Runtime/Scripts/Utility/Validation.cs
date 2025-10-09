using System.Text.RegularExpressions;

namespace USharpVideoQueue.Runtime.Utility
{

    public static class Validation
    {
        /// <summary>
        /// Replicates Validation behavior of USharpVideo
        /// </summary>
        /// <param name="url"></param>
        public static bool ValidateURL(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            int idx = url.IndexOf("://", System.StringComparison.Ordinal);
            return idx >= 1 && idx <= 8;
        }


        /// <summary>
        /// Uses regex to determine whether the url matches one of the allowed domains;
        /// </summary>
        /// <param name="url"></param>
        /// <param name="allowedDomains"></param>
        /// <returns></returns>
        public static bool UrlIsOfAllowedDomain(string url, string[] allowedDomains)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            foreach (var domain in allowedDomains)
            {
                if (IsUrlOfDomain(url, domain))
                    return true;
            }

            return false;
        }

        private static bool IsUrlOfDomain(string url, string domain)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(domain))
                return false;

            // Normalize and escape domain for regex safety
            domain = domain.Trim().ToLower();
            while (domain.Length > 0 && domain[0] == '.')
                domain = domain.Substring(1);

            string escapedDomain = Regex.Escape(domain);

            // Regex explanation:
            // ^(?:https?:\/\/)?   — optional http/https prefix
            // (?:[^@\/\n]+@)?     — optional credentials
            // (?:[a-z0-9-]+\.)*   — optional subdomains
            // domain              — the actual domain (escaped)
            // (?=[:\/?#]|$)       — ensures it ends at port, path, query, or end of string
            string pattern = @"^(?:https?:\/\/)?(?:[^@\/\n]+@)?(?:[a-z0-9-]+\.)*" + escapedDomain + @"(?=[:\/?#]|$)";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);

            return regex.IsMatch(url);
        }
    }
}