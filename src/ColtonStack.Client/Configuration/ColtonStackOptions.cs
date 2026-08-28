namespace ColtonStack.Client.Configuration;

/// <summary>Client settings from appsettings.json — the same IConfiguration stack the server uses.</summary>
public sealed class ColtonStackOptions
{
    public const string SectionName = "ColtonStack";

    public string ServerUrl { get; set; } = "http://localhost:5080/";
}
