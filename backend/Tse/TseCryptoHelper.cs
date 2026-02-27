using System.Security.Cryptography;
using System.Text;

namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// RKSV Checklist 5: Base64URL no-padding ve Checklist 3: SHA-256 yardımcıları.
    /// </summary>
    public static class TseCryptoHelper
    {
        private const string Base64UrlAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
        private static readonly char[] PaddingChars = ['='];

        /// <summary>
        /// Base64URL encode without padding (RKSV Checklist 5).
        /// Hata: BASE64URL_PADDING_ERROR eğer giriş padding içeriyorsa.
        /// </summary>
        public static string ToBase64UrlNoPadding(byte[] data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            var base64 = Convert.ToBase64String(data);
            var base64Url = base64
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd(PaddingChars);

            if (!IsUrlSafeBase64(base64Url))
                throw new TsePipelineException("BASE64URL_PADDING_ERROR", "Output contains invalid characters");

            return base64Url;
        }

        /// <summary>
        /// Base64URL decode; padding kabul etmez.
        /// </summary>
        public static byte[] FromBase64UrlNoPadding(string base64Url)
        {
            if (string.IsNullOrEmpty(base64Url))
                return Array.Empty<byte>();

            if (base64Url.Contains('='))
                throw new TsePipelineException("BASE64URL_PADDING_ERROR", "Base64URL must not contain padding");

            if (!IsUrlSafeBase64(base64Url))
                throw new TsePipelineException("BASE64URL_PADDING_ERROR", "Invalid Base64URL characters");

            var base64 = base64Url.Replace('-', '+').Replace('_', '/');
            var padding = 4 - (base64.Length % 4);
            if (padding != 4)
                base64 += new string('=', padding);

            return Convert.FromBase64String(base64);
        }

        private static bool IsUrlSafeBase64(string s)
        {
            foreach (var c in s)
            {
                if (!char.IsLetterOrDigit(c) && c != '-' && c != '_')
                    return false;
            }
            return true;
        }

        /// <summary>
        /// SHA-256 hash (RKSV Checklist 3).
        /// </summary>
        public static byte[] Sha256Hash(byte[] data)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(data);
        }

        public static byte[] Sha256Hash(string data) => Sha256Hash(Encoding.UTF8.GetBytes(data));
    }
}
