namespace TroyWebProperty.Web.Models;

internal static class ConcurrencyTokens
{

    public static bool Matches(byte[] current, string? posted)
    {
        if (string.IsNullOrEmpty(posted))
        {
            return false;
        }

        Span<byte> token = stackalloc byte[8];

        return Convert.TryFromBase64String(posted, token, out var length)
               && length == 8 && current.AsSpan().SequenceEqual(token);
    }

}