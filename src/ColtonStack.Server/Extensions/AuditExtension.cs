using ColtonStack.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ColtonStack.Server.Extensions;

/// <summary>
/// The audit feature's server half: it owns the <c>/api/audit</c> read endpoint. The core
/// app keeps writing audit entries (every save goes through <c>IAuditService</c>); this
/// extension is what exposes them to clients — paired with the audit pane on the client,
/// the whole feature ships as one unit without touching shared core files.
/// </summary>
public sealed class AuditExtension : IServerStartup
{
    public void ConfigureServices(IServiceCollection services, ConfigurationManager configuration)
    {
        // The core composition root registers IAuditService (it writes entries on every
        // save). The extension only adds the read surface — nothing to register here.
    }

    public void ConfigureApp(WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/audit", async Task<IResult> (int limit, IAuditService audit, CancellationToken cancellationToken) =>
        {
            var entries = await audit.GetRecentAsync(limit <= 0 ? 50 : Math.Min(limit, 500), cancellationToken);
            return TypedResults.Ok(entries);
        });
    }
}