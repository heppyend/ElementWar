using System;
using System.Text;
using System.Security.Cryptography;

namespace ElementWar.Rules
{

public static class GameRulesHash
{
    public static string Compute(byte[] utf8Content)
    {
        if (utf8Content is null) throw new ArgumentNullException(nameof(utf8Content));
        using var sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(utf8Content);
        var builder = new StringBuilder(digest.Length * 2);
        for (int i = 0; i < digest.Length; i++)
            builder.Append(digest[i].ToString("x2"));
        return builder.ToString();
    }
}
}
