using System.Security.Cryptography;
using System.Text;

namespace ColtonStack.Server.Webhooks;

/// <summary>Signs webhook bodies with HMAC-SHA256 so receivers can verify authenticity.</summary>
public static class WebhookSigner
{
    public const string HeaderName = "X-ColtonStack-Signature";

    /// <summary>Returns the hex-encoded HMAC-SHA256 of <paramref name="body"/>, or an empty string when no secret is configured.</summary>
    public static string Sign(string? secret, ReadOnlySpan<byte> body)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return string.Empty;
        }

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        return Convert.ToHexString(hash);
    }
}
