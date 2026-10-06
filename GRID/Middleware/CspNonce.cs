using System.Security.Cryptography;

namespace GRID.Middleware
{
    // Per-request nonce for the Content-Security-Policy script-src directive.
    // Inline <script> blocks must carry nonce="@ViewContext.HttpContext.GetCspNonce()" or the browser blocks them.
    public static class CspNonce
    {
        private const string ItemKey = "CspNonce";

        public static string GetCspNonce(this HttpContext context)
        {
            if (context.Items[ItemKey] is not string nonce)
            {
                nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
                context.Items[ItemKey] = nonce;
            }
            return nonce;
        }
    }
}
