using System.Windows;
using ColtonStack.Client.Configuration;
using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using ColtonStack.Client.ViewModels;
using ColtonStack.Client.Views;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Retry;
using Polly.Timeout;

namespace ColtonStack.Client;

/// <summary>
/// The WPF entry point hosts the app in the same Generic Host used by the ASP.NET Core server:
/// one composition root, one DI container, one configuration/logging stack — no service locator.
/// </summary>
public partial class App : Application
{
    private IHost? _host;

#pragma warning disable VSTHRD100 // WPF lifecycle overrides are void; awaiting here is the app's actual entry point.
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = BuildHost();

        // The composition root wires messenger subscriptions: each view model declares what it
        // handles via IRecipient<T>, and here every one is handed to the messenger. Weak
        // references mean VMs are collected without unregistering by hand. This happens before
        // the host starts, so no hub or pipeline message is ever published to nobody.
        var messenger = _host.Services.GetRequiredService<IMessenger>();
        messenger.RegisterAll(_host.Services.GetRequiredService<ChatViewModel>());
        messenger.RegisterAll(_host.Services.GetRequiredService<ChannelListViewModel>());
        messenger.RegisterAll(_host.Services.GetRequiredService<StatusBarViewModel>());

        await _host.StartAsync();

        // Reflect the server's simulator state on the status bar toggle (fire-and-forget: the
        // client works fine even if the server is still down).
        _ = _host.Services.GetRequiredService<StatusBarViewModel>().InitializeAsync();

        // The window is pure XAML; the composition root hands it its DataContext.
        var window = _host.Services.GetRequiredService<MainWindow>();
        window.DataContext = _host.Services.GetRequiredService<MainViewModel>();
        MainWindow = window;
        window.Show();

        // Load channels once the window is visible (fire-and-forget: failures surface on the
        // status bar through the messenger, not as exceptions).
        _ = _host.Services.GetRequiredService<MainViewModel>().InitializeAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
            _host.Dispose();
        }

        base.OnExit(e);
    }
#pragma warning restore VSTHRD100

    private static IHost BuildHost()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            // appsettings.json ships next to the exe; don't depend on the working directory.
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.AddDebug();

        builder.Services
            .Configure<ColtonStackOptions>(builder.Configuration.GetSection(ColtonStackOptions.SectionName));

        // One messenger for the whole app, wrapped in a decorator (composition, not inheritance)
        // that delivers every message on the UI thread. Hub callbacks and resilience events are
        // published from thread-pool threads; because marshaling happens once, here at the
        // boundary, no view model ever sees a Dispatcher. Weak references mean recipients are
        // cleaned up without anyone unregistering by hand.
        builder.Services.AddSingleton<IMessenger>(new UiThreadMessenger(WeakReferenceMessenger.Default));

        AddResilientApiClient(builder.Services);

        // The SignalR connection is a hosted service: the host starts and stops it, so no view
        // model ever manages connection lifetime.
        builder.Services.AddSingleton<ChatHubClient>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ChatHubClient>());

        // Everything the window needs, constructor-injected all the way down.
        builder.Services.AddSingleton<ChannelListViewModel>();
        builder.Services.AddSingleton<ChatViewModel>();
        builder.Services.AddSingleton<StatusBarViewModel>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        return builder.Build();
    }

    /// <summary>
    /// Typed HttpClient + resilience pipeline: retry with exponential backoff and jitter, a
    /// circuit breaker for sustained failures, per-attempt timeout. The pipeline reports
    /// retries and circuit changes through the messenger — the status bar just listens.
    /// </summary>
    private static void AddResilientApiClient(IServiceCollection services)
    {
        services
            .AddHttpClient<ColtonStackApiClient>((services, client) =>
            {
                var options = services.GetRequiredService<IOptions<ColtonStackOptions>>().Value;
                client.BaseAddress = new Uri(options.ServerUrl);
                client.Timeout = Timeout.InfiniteTimeSpan; // timeouts are the pipeline's job now
            })
            .AddResilienceHandler("coltonstack-api", (pipeline, context) =>
            {
                var messenger = context.ServiceProvider.GetRequiredService<IMessenger>();

                pipeline.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    OnRetry = arguments =>
                    {
                        messenger.Send(new HttpRetryMessage(arguments.AttemptNumber, string.Empty));
                        return default;
                    },
                });

                pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    MinimumThroughput = 8,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    BreakDuration = TimeSpan.FromSeconds(15),
                    OnOpened = _ =>
                    {
                        messenger.Send(new HttpRetryMessage(0, "Circuit OPEN — the server keeps failing; pausing requests for 15s"));
                        return default;
                    },
                    OnClosed = _ =>
                    {
                        messenger.Send(new HttpRetryMessage(0, "Circuit closed — server recovered"));
                        return default;
                    },
                });

                pipeline.AddTimeout(new TimeoutStrategyOptions { Timeout = TimeSpan.FromSeconds(10) });
            });
    }
}
