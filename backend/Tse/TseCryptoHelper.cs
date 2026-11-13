using System.Security.Cryptography;
using System.Text;

namespace KasseAPI_Final.Tse
{
    /// <summary>
    /// RKSV Checklist 5: Base64URL no-padding ve Checklist 3: SHA-256 yardımcıları.
    /// </summary>
    public static class TseCryptoHelper
    {
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

            return DecodeBase64Alphabet(base64Url);
        }

        /// <summary>
        /// Decodes Base64URL or standard Base64, with or without padding.
        /// Fiskaly SIGN AT QR <c>Sig-Wert</c> uses standard Base64 (<c>+</c>/<c>/</c>/<c>=</c>);
        /// compact JWS uses Base64URL without padding.
        /// </summary>
        public static byte[] FromBase64UrlOrStd(string value)
        {
            if (string.IsNullOrEmpty(value))
                return Array.Empty<byte>();

            var trimmed = value.Trim();
            if (trimmed.Length == 0)
                return Array.Empty<byte>();

            foreach (var c in trimmed)
            {
                if (char.IsLetterOrDigit(c) || c is '-' or '_' or '+' or '/' or '=')
                    continue;
                throw new TsePipelineException("BASE64URL_PADDING_ERROR", "Invalid Base64 / Base64URL characters");
            }

            try
            {
                return DecodeBase64Alphabet(trimmed);
            }
            catch (FormatException ex)
            {
                throw new TsePipelineException("BASE64URL_PADDING_ERROR", "Invalid Base64 / Base64URL payload", ex);
            }
        }

        /// <summary>Encode bytes as Base64URL without padding, accepting either alphabet on input.</summary>
        public static string NormalizeToBase64UrlNoPadding(string value) =>
            ToBase64UrlNoPadding(FromBase64UrlOrStd(value));

        private static byte[] DecodeBase64Alphabet(string value)
        {
            var base64 = value.Replace('-', '+').Replace('_', '/').TrimEnd(PaddingChars);
            var pad = (4 - (base64.Length % 4)) % 4;
            if (pad != 0)
                base64 += new string('=', pad);

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
