using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ColtonStack.Contracts;
using ColtonStack.WebhookSink;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5090");

var secret = builder.Configuration["WebhookSink:Secret"] ?? "demo-secret";

var app = builder.Build();

app.MapGet("/", () => TypedResults.Ok(new
{
    service = "coltonstack-webhook-sink",
    chaos = Chaos.Enabled,
    hint = "POST /webhook receives deliveries; POST /chaos/{enabled} toggles simulated failures",
}));

app.MapPost("/chaos/{enabled:bool}", (bool enabled) =>
{
    Chaos.Enabled = enabled;
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] chaos mode {(enabled ? "ENABLED (40% of deliveries fail with 500)" : "disabled")}");
    return TypedResults.Ok(new { chaos = enabled });
});

app.MapPost("/webhook", async (HttpRequest request) =>
{
    using var buffer = new MemoryStream();
    await request.Body.CopyToAsync(buffer, request.HttpContext.RequestAborted);
    var body = buffer.ToArray();

    // Chaos: simulate a flaky receiver so the server's retry pipeline is observable.
    if (Chaos.Enabled && Random.Shared.NextDouble() < 0.4)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] REJECTED delivery with 500 (chaos)");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
    }

    var signature = request.Headers[WebhookSigner.HeaderName].ToString();
    var validSignature = VerifySignature(secret, body, signature);

    WebhookPayload? payload;
    try
    {
        payload = JsonSerializer.Deserialize(body, ColtonStackJsonContext.Default.WebhookPayload);
    }
    catch (JsonException)
    {
        payload = null;
    }

    if (payload is null)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] received unparseable delivery ({body.Length} bytes)");
        return TypedResults.BadRequest(new { error = "invalid payload" });
    }

    Console.WriteLine(
        $"[{DateTime.Now:HH:mm:ss}] {payload.EventType,-15} #{payload.Message.Id,-4} [{payload.Message.AuthorName}] {payload.Message.Text}  (signature: {(validSignature ? "VALID" : "INVALID/MISSING")})");

    return TypedResults.Ok(new { received = true });
});

app.Run();

static bool VerifySignature(string secret, byte[] body, string headerValue)
{
    if (string.IsNullOrEmpty(headerValue) || string.IsNullOrEmpty(secret))
    {
        return false;
    }

    var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
    var provided = Convert.FromHexString(headerValue);
    return CryptographicOperations.FixedTimeEquals(expected, provided);
}
