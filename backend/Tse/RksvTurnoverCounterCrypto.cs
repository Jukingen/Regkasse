using System.Security.Cryptography;

using System.Text;

using Org.BouncyCastle.Crypto;

using Org.BouncyCastle.Crypto.Engines;

using Org.BouncyCastle.Crypto.Modes;

using Org.BouncyCastle.Crypto.Parameters;



namespace KasseAPI_Final.Tse;



/// <summary>

/// RKSV Stand-Umsatz-Zaehler-AES256-ICM encryption (Detailspezifikation Abs. 8–9).

/// Uses BouncyCastle AES/CTR/NoPadding to match BMF CryptoUtil.encryptCTR / decryptTurnOverCounter.

/// </summary>

public static class RksvTurnoverCounterCrypto

{

    public static string Encrypt(long turnoverCounterCents, string kassenId, string belegnummer, byte[] aesKey) =>

        Encrypt(turnoverCounterCents, kassenId, belegnummer, aesKey, RksvSuite.TurnoverCounterLengthBytes);



    public static string Encrypt(

        long turnoverCounterCents,

        string kassenId,

        string belegnummer,

        byte[] aesKey,

        int turnoverCounterLengthBytes)

    {

        if (aesKey == null || aesKey.Length != 32)

            throw new ArgumentException("AES-256 key must be 32 bytes.", nameof(aesKey));



        var iv = BuildIv(kassenId, belegnummer);



        var plain = new byte[16];

        var counterBytes = GetTwoComplementBytes(turnoverCounterCents, turnoverCounterLengthBytes);

        Buffer.BlockCopy(counterBytes, 0, plain, 0, counterBytes.Length);



        var cipher = AesCtrCrypt(aesKey, iv, plain);



        var truncated = new byte[turnoverCounterLengthBytes];

        Buffer.BlockCopy(cipher, 0, truncated, 0, turnoverCounterLengthBytes);

        return Convert.ToBase64String(truncated);

    }



    /// <summary>Decrypt for tests / diagnostics only.</summary>

    public static long Decrypt(string base64Encrypted, string kassenId, string belegnummer, byte[] aesKey)

    {

        var iv = BuildIv(kassenId, belegnummer);

        var encrypted = Convert.FromBase64String(base64Encrypted);



        // BMF decryptTurnOverCounter: CTR-decrypt only the stored N bytes (not the full 16-byte block).

        var plain = AesCtrCrypt(aesKey, iv, encrypted);



        var be = new byte[8];

        Buffer.BlockCopy(plain, 0, be, 8 - encrypted.Length, encrypted.Length);

        return System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(be);

    }



    private static byte[] BuildIv(string kassenId, string belegnummer)

    {

        var ivInput = kassenId + belegnummer;

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ivInput));

        var iv = new byte[16];

        Buffer.BlockCopy(hash, 0, iv, 0, 16);

        return iv;

    }



    private static byte[] AesCtrCrypt(byte[] aesKey, byte[] iv, byte[] input)

    {

        var cipher = new BufferedBlockCipher(new SicBlockCipher(new AesEngine()));

        cipher.Init(true, new ParametersWithIV(new KeyParameter(aesKey), iv));



        var output = new byte[cipher.GetOutputSize(input.Length)];

        var len = cipher.ProcessBytes(input, 0, input.Length, output, 0);

        len += cipher.DoFinal(output, len);

        if (len == output.Length)

            return output;



        var trimmed = new byte[len];

        Buffer.BlockCopy(output, 0, trimmed, 0, len);

        return trimmed;

    }



    private static byte[] GetTwoComplementBytes(long value, int length)

    {

        if (length < 5 || length > 8)

            throw new ArgumentOutOfRangeException(nameof(length));



        Span<byte> raw = stackalloc byte[8];

        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(raw, value);

        return raw.Slice(8 - length, length).ToArray();

    }

}


