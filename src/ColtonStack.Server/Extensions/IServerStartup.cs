using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ColtonStack.Server.Extensions;

/// <summary>
/// One feature's server-side bootstrap. Same contract shape as the client's
/// <c>IClientStartup</c>: a plain class handed an explicit context, listed explicitly in
/// Program.cs — no reflection scanning, no attribute discovery, no assembly archaeology.
/// What's installed is a compile-checked line of code.
///
/// Two phases mirror the ASP.NET Core host: services are registered before the container is
/// built, endpoints after. In the full product each extension is its own assembly and can
/// ship and deploy independently of the core API.
/// </summary>
public interface IServerStartup
{
    /// <summary>Registers services for this extension. Runs before <c>builder.Build()</c>.</summary>
    void ConfigureServices(IServiceCollection services, ConfigurationManager configuration);

    /// <summary>Maps this extension's endpoints. Runs once the WebApplication exists.</summary>
    void ConfigureApp(WebApplication app);
}