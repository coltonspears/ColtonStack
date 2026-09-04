using ColtonStack.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ColtonStack.Server.Extensions;

/// <summary>
/// The audit feature's server half: it owns the <c>/api/audit</c> read endpoint and its own
/// options section. The core app keeps writing audit entries (every save goes through
/// <c>IAuditService</c>); this extension is what exposes them to clients — paired with the
/// audit pane on the client, the whole feature ships as one unit without touching shared core files.
/// </summary>
public sealed class AuditExtension : IServerStartup
{
    public void ConfigureServices(IServiceCollection services, ConfigurationManager configuration)
    {
        // The core composition root registers IAuditService (it writes entries on every
        // save). The extension adds the read surface and binds its own config section.
        services.Configure<AuditOptions>(configuration.GetSection(AuditOptions.SectionName));
    }

    public void ConfigureApp(WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/audit", async Task<IResult> (int limit, IAuditService audit, IOptions<AuditOptions> options, CancellationToken cancellationToken) =>
        {
            var settings = options.Value;
            var page = limit <= 0 ? settings.DefaultPageSize : Math.Min(limit, settings.MaxPageSize);
            var entries = await audit.GetRecentAsync(page, cancellationToken);
            return TypedResults.Ok(entries);
        });
    }
}