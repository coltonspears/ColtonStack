using System.Windows;
using ColtonStack.Client.Configuration;
using ColtonStack.Client.Extensions;
using ColtonStack.Client.Extensions.Audit;
using ColtonStack.Client.Extensions.Pokemon;
using ColtonStack.Client.Services;
using ColtonStack.Client.ViewModels;
using ColtonStack.Client.Views;
using CommunityToolkit.Mvvm.Messaging;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client;

/// <summary>
/// The WPF entry point hosts the app in the same Generic Host used by the ASP.NET Core server:
/// one composition root, one DI container, one configuration/logging stack — no service locator.
///
/// The client extension list lives here: the single compile-checked place that decides which
/// extensions are installed. Each one registers services, sidebar panes, commands, settings
/// sections, attachment renderers and its own XAML.
/// </summary>
public sealed partial class App : Application
{
    private static readonly IClientStartup[] ClientExtensions =
    [
        new CorePanesExtension(),
        new AuditPaneExtension(),
        new PokemonExtension(),
    ];

    private IHost? _host;
    private ClientStartupContext? _extensions;

#pragma warning disable VSTHRD100 // WPF lifecycle overrides are void; awaiting here is the app's actual entry point.
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        (_host, _extensions) = BuildHost();

        // Extension XAML (implicit DataTemplates for pane, section and attachment view models)
        // merges before any window renders, so every template is resolvable.
        foreach (var source in _extensions.ResourceDictionaries)
        {
            Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Absolute) });
        }

        // The composition root wires messenger subscriptions: each view model declares what it
        // handles via IRecipient<T>, and here every one is handed to the messenger. Weak
        // references mean VMs are collected without unregistering by hand. This happens before
        // the host starts, so no hub or pipeline message is ever published to nobody.
        var messenger = _host.Services.GetRequiredService<IMessenger>();
        messenger.RegisterAll(_host.Services.GetRequiredService<ChatViewModel>());
        messenger.RegisterAll(_host.Services.GetRequiredService<ChannelListViewModel>());
        messenger.RegisterAll(_host.Services.GetRequiredService<StatusBarViewModel>());
        messenger.RegisterAll(_host.Services.GetRequiredService<PeopleViewModel>());
        messenger.RegisterAll(_host.Services.GetRequiredService<DiagnosticsViewModel>());
        messenger.RegisterAll(_host.Services.GetRequiredService<SettingsViewModel>());
        messenger.RegisterAll(_host.Services.GetRequiredService<MainViewModel>());

        await _host.StartAsync();

        // Panes, commands and settings sections build their content lazily through the DI
        // provider — hand the registries their provider now that the container exists.
        _extensions.Attach(_host.Services);

        // Reflect the server's simulator state on the status bar toggle and pull persisted
        // settings (fire-and-forget: the client works fine even if the server is still down).
        _ = _host.Services.GetRequiredService<StatusBarViewModel>().InitializeAsync();
        _ = _host.Services.GetRequiredService<ISettingsStore>().LoadAsync(CancellationToken.None);

        // The window is pure XAML; the composition root hands it its DataContext.
        var window = _host.Services.GetRequiredService<MainWindow>();
        window.DataContext = _host.Services.GetRequiredService<MainViewModel>();
        MainWindow = window;
        window.Show();

        // Select the first pane once the window is visible (fire-and-forget: failures surface
        // on the status bar through the messenger, not as exceptions).
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

    private static (IHost Host, ClientStartupContext Extensions) BuildHost()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            // appsettings.json ships next to the exe; don't depend on the working directory.
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.AddDebug();

        // In-app diagnostics: the same ILogger stream also lands on the messenger, so the
        // slide-over panel can show retries, hub reconnects and failures live. One provider,
        // one boundary — nothing else knows the panel exists.
        var messenger = new UiThreadMessenger(WeakReferenceMessenger.Default);
        builder.Logging.AddProvider(new DiagnosticsLoggerProvider(messenger));

        builder.Services
            .Configure<ColtonStackOptions>(builder.Configuration.GetSection(ColtonStackOptions.SectionName));

        // One messenger for the whole app, wrapped in a decorator (composition, not inheritance)
        // that delivers every message on the UI thread. Hub callbacks and resilience events are
        // published from thread-pool threads; because marshaling happens once, here at the
        // boundary, no view model ever sees a Dispatcher. Weak references mean recipients are
        // cleaned up without anyone unregistering by hand.
        builder.Services.AddSingleton<IMessenger>(messenger);

        AddCoreServices(builder.Services);
        AddViewModels(builder.Services);

        // Phase 1 of the client extension contract: each extension adds services, panes,
        // commands, settings sections, attachment renderers and XAML before the container is
        // built. Runs last so extensions can decorate or depend on core registrations.
        var extensions = new ClientStartupContext(builder.Services);
        foreach (var extension in ClientExtensions)
        {
            extension.Configure(extensions);
        }

        extensions.RegisterRegistries();

        return (builder.Build(), extensions);
    }

    private static void AddCoreServices(IServiceCollection services)
    {
        // FluentValidation validators — shared with the server through the Contracts project.
        // The same UpdateProfileRequestValidator runs client-side (CanExecute / SaveAsync)
        // and server-side (the PUT /me endpoint).
        services.AddValidatorsFromAssemblyContaining<Contracts.UpdateProfileRequestValidator>();

        // Typed HttpClient + shared resilience pipeline. View models depend on the interface.
        services.AddColtonStackHttpClient<ColtonStackApiClient>("coltonstack-api");
        services.AddSingleton<IColtonStackApiClient>(sp => sp.GetRequiredService<ColtonStackApiClient>());
        services.AddSingleton<ISettingsStore, SettingsStore>();

        // The SignalR connection is a hosted service: the host starts and stops it, so no view
        // model ever manages connection lifetime. View models see it only as IChatConnection.
        services.AddSingleton<ChatHubClient>();
        services.AddSingleton<IChatConnection>(sp => sp.GetRequiredService<ChatHubClient>());
        services.AddHostedService(sp => sp.GetRequiredService<ChatHubClient>());
    }

    private static void AddViewModels(IServiceCollection services)
    {
        // Everything the window needs, constructor-injected all the way down.
        services.AddSingleton<ChannelListViewModel>();
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<StatusBarViewModel>();
        services.AddSingleton<PeopleViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();
        services.AddSingleton<CommandPaletteViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
