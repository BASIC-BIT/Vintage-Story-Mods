using System;
using System.Security.Cryptography;
using System.Text;

namespace thebasics.ModSystems.Analytics;

public static class AnalyticsPseudonymizer
{
    public const int PlayerPseudonymSaltHexLength = 64;
    public const int PlayerPseudonymHexLength = 64;
    public const int ServerInstallIdHexLength = 32;
    public const int ServerSessionIdHexLength = 32;

    public static string NewHexId(int byteCount)
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(byteCount)).ToLowerInvariant();
    }

    public static string CreatePlayerPseudonym(string saltHex, string playerUid)
    {
        if (!IsHexString(saltHex, PlayerPseudonymSaltHexLength) || string.IsNullOrWhiteSpace(playerUid))
        {
            return null;
        }

        using var hmac = new HMACSHA256(Convert.FromHexString(saltHex));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(playerUid))).ToLowerInvariant();
    }

    public static bool IsHexString(string value, int length)
    {
        if (value?.Length != length)
        {
            return false;
        }

        foreach (var c in value)
        {
            var isHex = c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }
}
