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
    }
}