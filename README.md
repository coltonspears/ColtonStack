# ColtonStack — A Modern WPF Demo Against "God Class" Architecture

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

ColtonStack is a live Slack‑like chat client built entirely on modern .NET patterns. **It exists to make one argument: that composition, source generators, and compiler‑checked APIs produce more reliable, more testable code than reflection‑powered base classes — with far less effort.**

The `src/ColtonStack.Client/Legacy/` folder recreates the exact patterns this project retires. Every other file in the solution is banned from using them.

---

## Quick Start

```bash
# Restore + build everything (the solution knows the build order)
dotnet build ColtonStack.sln

# Terminal 1 — start the server (SQLite database created and seeded automatically)
cd src/ColtonStack.Server
dotnet run

# Terminal 2 — start the webhook sink (the demo's "external service")
cd src/ColtonStack.WebhookSink
dotnet run

# Terminal 3 — start the WPF client
cd src/ColtonStack.Client
dotnet run

# Run the test suite (unit tests + executable architecture rules)
dotnet test ColtonStack.sln
```

Then:
1. Click **sim** in the status bar — teammates start typing and posting every 5–15 seconds.
2. Click **chaos** in the status bar — ~40% of API requests fail with 503. Watch the retry counter and circuit breaker messages in the status bar.
3. Click the **search icon** (🔍) in the conversation header — type any word to filter messages with debounced live search.
4. Click **logs** in the status bar — the diagnostics panel shows the app's own ILogger output live (retries, reconnects, catch‑ups).
5. Click the **audit icon** in the rail (History glyph) — the audit trail pane, contributed by the audit extension without a single core‑app edit.
6. Click your avatar color in the rail (bottom‑left) to open **Preferences** — change your display name and color, saved to the server through the resilience pipeline. The same FluentValidation rules enforce valid input client-side (before the save button enables) and server-side (before the database is touched).
7. Register a webhook at the `WebhookSink` URL (`http://localhost:5090/webhook`) via the server's `/api/webhooks` endpoint and watch HMAC‑signed deliveries arrive.

---

## Libraries Used

| Library | Where | What It Replaces |
|---|---|---|
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (MVVM Toolkit) | Client | Hand‑written `INotifyPropertyChanged`, `ICommand`, manual field declarations |
| [Microsoft.Extensions.Hosting](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host) | Client + Server | Service locator, ad‑hoc startup logic |
| [Microsoft.Extensions.Http.Resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience) | Client | Hand‑rolled retry loops, `Thread.Sleep` |
| [Polly](https://github.com/App-vNext/Polly) | Client + Server | Custom timeout and retry code |
| [Dapper + Dapper.Contrib](https://github.com/DapperLib/Dapper) | Server | Reflection‑based ORM, string‑concatenated SQL |
| [Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite) | Server | Heavy external database dependency |
| [System.Text.Json (source gen)](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) | Client + Server | Runtime `JsonSerializer` (reflection) |
| [Microsoft.AspNetCore.SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/) | Client + Server | Polling, manual WebSocket code, event name strings |
| [FluentValidation](https://docs.fluentvalidation.net/) | Client + Server | Ad‑hoc inline validation in endpoint and ViewModel |

---

## Architecture — Composition Over Inheritance

```
┌─────────────────────────────────────────────────────────────┐
│  App.xaml.cs (Composition Root)                             │
│  • Builds Generic Host with DI                              │
│  • Registers IMessenger (UiThreadMessenger wrapper)          │
│  • Configures resilience pipeline (retry + circuit breaker) │
│  • Starts ChatHubClient (hosted service)                    │
│  • Wires messenger subscriptions ➜ _host.StartAsync()      │
│  • Assigns MainWindow.DataContext                           │
│  • Calls MainViewModel.InitializeAsync()                    │
└──────────────────────┬──────────────────────────────────────┘
                       │ constructor‑injected
          ┌────────────┼──────────────────┐
          ▼            ▼                  ▼
   ┌──────────┐ ┌──────────┐ ┌──────────────────┐
   │ Channels │ │   Chat   │ │  StatusBar       │
   │ List VM  │ │  ViewModel│  ViewModel        │
   │          │ │          │ │                  │
   │IMessenger│ │IMessenger│ │IMessenger         │
   │←receive──│ │←receive──│ │←receive──────────│
   │Channel   │ │Channel   │ │ConnectionStatus  │
   │Created   │ │Selected  │ │HttpRetry          │
   │Message   │ │Message   │ │                  │
   │Posted    │ │Posted    │ │                  │
   └──────────┘ └──────────┘ └──────────────────┘
        │             │               │
        │         ┌───┘               │
        ▼         ▼                   ▼
   ┌───────────────────────────────────────┐
   │  IMessenger (UiThreadMessenger)       │
   │  • All messages delivered on UI thread │
   │  • Weak references — no unregistering  │
   │  • One marshaling boundary             │
   └───────────────────────────────────────┘
```

**Key principle:** View models communicate through a message bus (`IMessenger`), never by holding direct references to each other. `ChannelListViewModel` publishes `ChannelSelectedMessage`; `ChatViewModel` reacts by loading history and switching groups. Neither knows the other exists.

---

## Before / After — Side‑by‑Side Comparisons

Every comparison below is real code from this repository.

### 1. A Message ViewModel — 30 lines vs. 151 lines of base class

**Modern (`MessageViewModel.cs`):**
```csharp
public sealed class MessageViewModel(MessageDto message, bool isFirstOfGroup)
{
    public long Id { get; } = message.Id;
    public string AuthorName { get; } = message.AuthorName;
    public string AvatarColor { get; } = message.AvatarColor;
    public string Text { get; } = message.Text;
    public DateTimeOffset CreatedAtUtc { get; } = message.CreatedAtUtc;
    public bool IsFirstOfGroup { get; } = isFirstOfGroup;
    public string Initials { get; } = NameInitials.From(message.AuthorName);
    public string TimeText { get; } = message.CreatedAtUtc.ToLocalTime().ToString("t", CultureInfo.CurrentCulture);
}
```

**Legacy (`LegacyMessageViewModel.cs` — inherits `LegacyEntityBase`):**
```csharp
[LegacyTable("Messages")]
public sealed class LegacyMessageViewModel : LegacyEntityBase
{
    private string _text = string.Empty;
    private string _authorName = string.Empty;

    [LegacyColumn("text")]
    public string Text
    {
        get => _text;
        set => SetAndRaise(ref _text, value, "Text");
    }

    [LegacyColumn("author")]
    public string AuthorName
    {
        get => _authorName;
        set
        {
            _authorName = value;
            RaisePropertyChanged("AutherName"); // ← TYPO: compiles, notifies nothing
            RaiseAllChanged();
        }
    }

    public LegacyMessageViewModel()
    {
        EnableAuditing = true; // ← inherits SQL persistence, soft delete, dirty tracking
    }
}
```

**What changed:**
- Modern: zero base class, zero reflection, zero magic strings
- Modern: all properties are `{ get; }` — set once in the constructor, never change
- Modern: not `INotifyPropertyChanged` — because immutable things don't need notifications
- Legacy: one typo (`"AutherName"` → `"AuthorName"`) compiles but never notifies
- Legacy: one property (`EnableAuditing = true`) activates SQL, auditing, and dirty tracking — for a UI object

### 2. Dependency Injection — One Composition Root

**Modern (`App.xaml.cs`):**
```csharp
builder.Services.AddSingleton<ChannelListViewModel>();
builder.Services.AddSingleton<ChatViewModel>();
builder.Services.AddSingleton<StatusBarViewModel>();
// ... every dependency is visible, registered in one place
```

**Legacy (implicit):**
```csharp
var vm = new LegacyMessageViewModel(); // creates a new SQL connection internally
// ... or worse: Activator.CreateInstance with string type name
```

### 3. JSON Serialization — Compile Time vs. Runtime

**Modern (`ColtonStackJsonContext.cs`):**
```csharp
[JsonSerializable(typeof(MessageDto))]
[JsonSerializable(typeof(IReadOnlyList<MessageDto>))]
public sealed partial class ColtonStackJsonContext : JsonSerializerContext;
```

**Legacy (inferred):**
```csharp
// Runtime reflection: slow cold start, can fail for types the trimmer removed
JsonSerializer.Serialize(value);
```

### 4. Commands — Source Generator vs. Reflection

**Modern (`[RelayCommand]` + `[ObservableProperty]`):**
```csharp
[ObservableProperty]
public partial string Draft { get; set; } = string.Empty;

partial void OnDraftChanged(string value)
{
    SendMessageCommand.NotifyCanExecuteChanged();
}

[RelayCommand(CanExecute = nameof(CanSend))]
private async Task SendMessageAsync(CancellationToken ct) { ... }

private bool CanSend() => CurrentChannel is not null && !string.IsNullOrWhiteSpace(Draft);
```

**Legacy:**
```csharp
// Manual ICommand implementation with CanExecute, or
// wire commands by string name — a typo produces a runtime binding failure
```

---

## What the BannedSymbols.txt Prevents

Every project in the solution includes `BannedSymbols.txt` via `Microsoft.CodeAnalysis.BannedApiAnalyzers`. These are **compile errors** — not warnings, not conventions:

```text
P:System.Threading.Tasks.Task`1.Result;         // .Result = deadlock
M:System.Reflection.Assembly.GetTypes;           // Reflection scanning = fragile
M:System.Activator.CreateInstance;               // String-based instantiation = runtime failure
M:System.Type.GetProperties;                     // Property scanning = bypasses compiler
```

The only files allowed to use these APIs are in `Client/Legacy/`, with explicit `#pragma warning disable RS0030` and a loud comment explaining why.

---

## Error Handling Strategy

Every layer handles its own errors:

| Layer | Strategy | Example |
|---|---|---|
| **HTTP** | Resilience pipeline (retry 3× + circuit breaker + timeout) | `OnRetry` publishes `HttpRetryMessage` to messenger |
| **ViewModel** | `try/catch` per command, publishes error to messenger | `SendFailed(ex, channelId)` via `LoggerMessage` |
| **SignalR** | `WithAutomaticReconnect` + reconnect loop | `ConnectLoopAsync()` retries every 2 seconds |
| **Webhooks** | Per‑delivery resilience pipeline, isolated per endpoint | One flaky endpoint cannot delay others |
| **Audit** | `catch (Exception)` that logs and moves on | Auditing never takes the main operation down |

No view model ever blocks on `.Result` or uses `Thread.Sleep`. The banned‑API list makes those compile errors.

---

## Testability — Proved by Tests

The `src/ColtonStack.Tests/` project contains unit tests for the pure‑logic surface:

```
NameInitialsTests              → Tests a static function with no DI
MessageViewModelTests          → Tests an immutable projection (no notifications)
ChannelSummaryRowTests         → Tests explicit DTO mapping
EnumEqualsConverterTests       → Tests two‑way conversion logic
ChannelListItemViewModelTests  → Tests observable properties + UpdateFrom
StatusBarViewModelTests        → Tests messenger‑driven state changes
PeopleViewModelTests           → Tests profile‑update propagation
ValidatorTests                 → Tests shared FluentValidation rules for profile, messages, channels
SidebarPaneRegistryTests       → Tests the extension surface: ordering, duplicate guard, lazy content
ArchitectureTests              → EXECUTED ARCHITECTURE — the conventions are build failures if broken
```

**These tests exist because the architecture allows it.** The legacy `LegacyMessageViewModel` requires `LegacyEntityBase`, which depends on `LegacySqlMapper`'s static global state, which requires a database connection. The modern `MessageViewModel` has zero dependencies — it's a constructor and readonly properties.

### Architecture Tests — the rules are executable

`ArchitectureTests.cs` turns the codebase's conventions into build‑breaking tests (via NetArchTest):

| Rule | What it prevents |
|---|---|
| View models are sealed, never abstract | God‑class base classes creeping back in |
| View models never depend on `ColtonStack.Server` | Client/server internals merging |
| View models + services never touch `System.Reflection` | String‑based runtime surprises returning |
| Nothing outside `Legacy/` references `Legacy/` | The contrast sample becoming a dependency |
| Contracts stay pure (no WPF, no ASP.NET, no host projects) | A DTO dragging its runtime into both processes |
| Server service classes are sealed and concrete | Inheritance hierarchies in the data layer |
| Server never depends on the client | Reverse dependency (always a design smell) |

A violation fails `dotnet test` — same category of failure as a compile error, which is the point.

---

## The "Chaos" Demo — Seeing Resilience Work Live

1. Start the server, client, and webhook sink.
2. Click **chaos** in the client's status bar. The server begins failing ~40% of API requests.
3. Try sending a message. The status bar shows:
   - `Retrying (attempt 1)…`
   - `Retrying (attempt 2)…`
   - `Retrying (attempt 3)…`
   - After 8 failures in 30 seconds: `Circuit OPEN — pausing requests for 15s`
   - Then: `Circuit closed — server recovered`
4. Click **chaos** again to turn failures off. Everything recovers.

The webhook sink has its own chaos mode — toggle it at `POST http://localhost:5090/chaos/true` and watch HMAC‑signed deliveries fail and retry on the server side.

---

## The Extension System — Plugins Without the God Class

Real enterprise apps grow by *adding features without re‑releasing the core*. The usual way this happens is a plugin framework with attribute scanning, reflection discovery, and a base class every extension must inherit — powerful, and exactly how the next god class is born.

ColtonStack demonstrates the other way: **extensions are plain classes, listed explicitly in the composition root.**

```csharp
// Server — Program.cs. The whole extension surface is this line:
IServerStartup[] extensions = [new AuditExtension()];
foreach (var extension in extensions) extension.ConfigureServices(builder.Services, builder.Configuration);
// ...after build:
foreach (var extension in extensions) extension.ConfigureApp(app);

// Client — App.xaml.cs. Same shape:
private static readonly IClientStartup[] ClientExtensions = [new CorePanesExtension(), new AuditPaneExtension()];
```

What's installed is a compile‑checked line of code — no assembly scanning, no attribute discovery, no runtime archaeology. A typo is a build error, not a missing feature in production. In the full product each extension is its own assembly shipping on its own cadence; the contract is identical.

### Adding a sidebar pane without editing the shell

The old design had a `SidebarPane` enum in the core app: every new pane meant a core release. The new design has a registry:

```csharp
public sealed class AuditPaneExtension : IClientStartup
{
    public void Configure(IClientStartupContext context)
    {
        context.Services.AddSingleton<AuditViewModel>();
        context.Panes.Register(new SidebarPaneDefinition(
            id: "audit", title: "Audit trail", iconGlyph: "\uE81C", order: 30,
            contentFactory: services => services.GetRequiredService<AuditViewModel>(),
            activatedAsync: services => services.GetRequiredService<AuditViewModel>().LoadCommand.ExecuteAsync(null)));
        context.AddResourceDictionary("pack://application:,,,/ColtonStack.Client;component/Extensions/Audit/AuditPaneTemplates.xaml");
    }
}
```

The shell itself contains **zero pane knowledge**:

- The rail is a `ListBox` bound to the registry — selection sync is the platform's job, not per‑pane RadioButton wiring.
- The sidebar is a single `ContentControl` bound to `ActivePane.Content`; each pane's view model renders through an implicit `DataTemplate`.
- Panes are identified by string id and ordered by explicit `Order` — an extension slots before or after core panes without touching them.
- Content is created lazily: an extension's view models don't exist until its pane is first visited, and its `activatedAsync` hook is the lazy‑load point.

The audit feature shows the full pattern across both planes: `AuditExtension` (server) owns the `/api/audit` endpoint, `AuditPaneExtension` (client) owns the pane and its XAML dictionary — one feature, two small classes, no core file edited.

**Why explicit lists instead of scanning?** Deliberate, and it's the same trade as everywhere else in this demo: explicit registration is testable, debuggable, and compile‑checked. The `SidebarPaneRegistryTests` prove the registry's contract (ordering, duplicate detection, lazy creation) without any host running. Legacy‑style discovery would reintroduce the exact "mysterious things happen behind the scenes" problem this app exists to argue against.

### The diagnostics panel — ILogger made visible

The **logs** toggle on the status bar opens a live slide‑over of the app's own `ILogger` output — retries, hub reconnects, audit failures, catch‑ups. Implementation: one `DiagnosticsLoggerProvider` publishes `DiagnosticEntryMessage` records onto the messenger (UiThreadMessenger marshals off‑thread logs), and `DiagnosticsViewModel` subscribes like any other recipient. The buffer is capped at 400 entries — a tail, not a database. No component knows the panel exists; that's the point of provider boundaries.

### Reconnect catch‑up — bounded re‑sync

When the SignalR connection recovers from a hard drop, `ChatHubClient` publishes `HubReconnectedMessage`, and the chat pane re‑fetches **only messages newer than the newest one it holds** (`GetMessagesAsync(channelId, afterId: lastId)`) instead of reloading full history. Bounded catch‑up is *the* core habit of database‑heavy clients: the server's `LIMIT` caps the query, the client's id‑dedupe absorbs overlap with live pushes, and the diagnostics panel shows the whole sequence live.

### CI — the compiler, enforced everywhere

`.github/workflows/ci.yml` builds the solution with `TreatWarningsAsErrors` + banned‑API analyzers and runs the test suite (architecture tests included) on every push and PR. The rules that make the codebase safe don't depend on anyone remembering them.

---

## Project Structure

```
src/
├── ColtonStack.Client/        # WPF application
│   ├── Behaviors/             # Attached behaviors (auto‑scroll, formatting, title bar)
│   ├── Configuration/         # IOptions<T> from appsettings.json
│   ├── Converters/            # IValueConverter implementations
│   ├── Extensions/            # CLIENT PLUGIN SYSTEM — IClientStartup, pane registry, in-box extensions
│   ├── Legacy/                # THE OLD WORLD — contrast sample only
│   ├── Messages/              # Record types published through IMessenger
│   ├── Services/              # Typed HTTP client, SignalR hub client, UI‑thread messenger, log provider
│   ├── Themes/                # Dark theme (no third‑party library)
│   ├── ViewModels/            # MVVM Toolkit partial classes (primary constructors)
│   └── Views/                 # XAML — MainWindow + EmojiCatalog view data
├── ColtonStack.Contracts/     # Shared DTOs, hub contract, JSON source‑gen context
├── ColtonStack.Server/        # ASP.NET Core Minimal API server
│   ├── Data/                  # Dapper.Contrib row classes (one per table)
│   ├── Endpoints/             # Minimal API route handlers
│   ├── Extensions/            # SERVER PLUGIN SYSTEM — IServerStartup + the audit extension
│   ├── Hubs/                  # Typed SignalR hub (Hub<IChatHubClient>)
│   ├── Infrastructure/        # SQLite connection factory, schema init, seed data
│   ├── Middleware/             # Chaos middleware (demo failure injector)
│   ├── Services/              # Business logic (ChannelService, MessageService, AuditService)
│   ├── Simulation/            # Background chat‑activity simulator
│   └── Webhooks/              # Background webhook dispatcher with retries
├── ColtonStack.Tests/         # xUnit tests + executable architecture rules (NetArchTest)
└── ColtonStack.WebhookSink/   # Demo "external service" receiving HMAC‑signed webhooks
```

---

## What's Not in This Demo (And Why)

| Feature | Reason |
|---|---|
| **Authentication / authorization** | Adds zero architectural insight for the patterns being demonstrated |
| **OpenAPI / Swagger** | The API surface is 10 endpoints — Swagger is noise for a demo |
| **Docker / containerization** | Not needed to show composition, source gen, or resilience |
| **EF Core** | Dapper.Contrib is deliberately lighter — the point is typed row classes, not ORM features |