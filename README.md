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
1. Press **Ctrl+K** (or click the search box in the title bar) — the **command palette**. Every row was contributed by an extension: jump to a channel, open a pane, toggle the simulator or chaos mode, open a settings section. The palette itself holds no list of its own.
2. Type **`/pokemon pika`** in the composer — the autocomplete popup fills from the server's cached name index; press Enter to post a **Pokémon card** (artwork, types, abilities, moves, Pokédex entry, base stats). The card is fetched once from [PokeAPI](https://pokeapi.co), persisted in SQLite, and rendered by the extension through a message attachment the core knows nothing about. `/shrug` is the minimal slash command for contrast.
3. Click **sim** in the status bar — teammates start typing and posting every 5–15 seconds.
4. Click **chaos** in the status bar — ~40% of API requests fail with 503. Watch the retry counter and circuit breaker messages in the status bar.
5. Press **Ctrl+F** (or the 🔍 in the conversation header) — type any word to filter messages with debounced live search.
6. Click **logs** in the status bar — the diagnostics panel shows the app's own ILogger output live (retries, reconnects, catch‑ups).
7. Click the **audit icon** in the rail (History glyph) — the audit trail pane, contributed by the audit extension without a single core‑app edit.
8. Press **Ctrl+,** (or the gear in the rail) — **Settings**, now an in‑window page. *Profile* is the core section; *Pokémon cards* and *Audit* are contributed by their extensions and persist on the server under keys the extension owns (`pokemon.artwork`, `audit.pagesize`). Switch Pokémon artwork to "pixel sprite" and the next card you post uses it. The same FluentValidation rules enforce valid profile input client‑side (before the save button enables) and server‑side (before the database is touched).
9. Register a webhook at the `WebhookSink` URL (`http://localhost:5090/webhook`) via the server's `/api/webhooks` endpoint and watch HMAC‑signed deliveries arrive.

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

**Seams, not concretions:** view models depend on `IColtonStackApiClient` and `IChatConnection`, never on the typed `HttpClient` wrapper or the SignalR connection. That is what lets `ChatViewModelTests` substitute the whole network in two lines and run on a thread‑pool thread — and it is a `NetArchTest` rule that view models never reference `System.Windows` at all, so no `Dispatcher` can creep back in.

**Composition inside a view model:** `ChatViewModel` is the busiest class in the client, and it stays small by *owning* two focused collaborators rather than growing regions: `MessageSearch` (live text filter over the conversation, WPF‑free) and `SlashCommandInput` (parses `/command arg`, produces suggestions). Each has its own test file; the chat view model just wires them.

---

## Before / After — Side‑by‑Side Comparisons

Every comparison below is real code from this repository.

### 1. A Message ViewModel — 30 lines vs. 151 lines of base class

**Modern (`MessageViewModel.cs`):**
```csharp
public sealed class MessageViewModel(MessageDto message, bool isFirstOfGroup, bool isFirstOfDay = false, object? attachment = null)
{
    public long Id { get; } = message.Id;
    public string AuthorName { get; } = message.AuthorName;
    public string AvatarColor { get; } = message.AuthorColor;
    public string Text { get; } = message.Text;
    public DateTimeOffset CreatedAtUtc { get; } = message.CreatedAtUtc;
    public bool IsFirstOfGroup { get; } = isFirstOfGroup;
    public bool IsFirstOfDay { get; } = isFirstOfDay;
    public object? Attachment { get; } = attachment;   // extension-rendered rich content, or null
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
CommandRegistryTests           → Duplicate ids/slash names, late attach, palette matching
SlashCommandInputTests         → "/" → name completion → argument autocomplete → resolve
MessageSearchTests             → Live filter stays correct as messages keep arriving
ChatViewModelTests             → History grouping, dedupe, send/fail/restore, slash dispatch, attachments — no HTTP, no SignalR
SettingsStoreTests             → Snapshot, lenient typed getters, change broadcast, server‑down fallback
SettingsViewModelTests         → Sections come from the registry; content is lazy and built once
SettingKeyTests                → One key grammar validated on both sides
PokemonCardMapperTests         → PokeAPI → card flattening (slot order, hidden abilities, flavor text cleanup)
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
| View models never depend on `System.Windows` | A `Dispatcher.Invoke` or `ICollectionView` making view models untestable |
| Client core (view models, services, behaviors) never references an installed extension | Removing an extension from `App`'s list breaking the build |
| Server core (services, endpoints, infrastructure, hubs) never references an installed extension | Same rule, server side |
| The Pokémon extension never references a core view model | "Extension" becoming a folder inside the god class |

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
IServerStartup[] extensions = [new AuditExtension(), new PokemonExtension()];
foreach (var extension in extensions) extension.ConfigureServices(builder.Services, builder.Configuration);
// ...after build:
foreach (var extension in extensions) extension.ConfigureApp(app);

// Client — App.xaml.cs. Same shape:
private static readonly IClientStartup[] ClientExtensions = [new CorePanesExtension(), new AuditPaneExtension(), new PokemonExtension()];
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

### The whole extension surface

`IClientStartupContext` hands an extension five registries plus the service collection. Every one is the same shape as the pane registry — plain definition classes with delegates, duplicate ids throw at startup, content resolves lazily through DI:

| Registry | What an extension contributes | Where it shows up |
|---|---|---|
| `Panes` | `SidebarPaneDefinition` | The rail + sidebar |
| `Commands` | `CommandDefinition` (optionally with a `slashName` and argument `suggestAsync`) and `CommandItemSource` (dynamic rows) | The **Ctrl+K palette** and the composer's **`/` popup** |
| `Settings` | `SettingsSectionDefinition` | The in‑window **Settings** page |
| `Attachments` | a renderer for one attachment `Kind` | Rich content inside chat messages |
| `AddResourceDictionary` | XAML with implicit `DataTemplate`s for the above | Merged at startup, resolved by type |

On the server, `IServerStartup` gives an extension `ConfigureServices` + `ConfigureApp` (its own options, typed `HttpClient`s, endpoints) and `ISchemaContributor` lets it own a table without editing the core initializer.

Two cross‑cutting services exist so extensions never build their own plumbing:

- **`ISettingsStore`** (client) ↔ `/api/settings` (server): key/value preferences persisted in SQLite. Extensions read synchronously from the in‑memory snapshot (`GetString/GetBool/GetInt` with fallbacks) and write with `SetAsync`, which broadcasts `SettingChangedMessage`. Keys are validated by one shared `SettingKey` rule on both sides.
- **`ResilientHttpClient.AddColtonStackHttpClient<T>()`**: every typed client against the ColtonStack server — core or extension — gets the same base address and the same retry/circuit‑breaker/timeout pipeline. Nobody re‑implements retry logic.

### The Pokémon extension — a REST API, a cache, a card

`/pokemon` is the end‑to‑end sample of "call a third‑party REST API and persist what comes back":

```
composer "/pokemon pika"
   └─ SlashCommandInput → CommandDefinition.SuggestAsync
        └─ IPokemonApi.SearchAsync            GET /api/pokemon/search?q=pika       (client → server)
             └─ PokemonService (in-memory name index, loaded once from PokeAPI)
Enter
   └─ CommandDefinition.ExecuteAsync
        └─ IPokemonApi.ShareAsync             POST /api/channels/{id}/pokemon/pikachu
             └─ PokemonService.GetCardAsync
                  ├─ PokemonCards table hit?  → return cached CardJson            (Dapper.Contrib GetAsync)
                  └─ miss → PokeApiClient (typed HttpClient + standard resilience handler)
                             GET pokemon/pikachu + GET pokemon-species/25
                             → PokemonCardMapper.Map (pure function, unit-tested)
                             → InsertAsync into PokemonCards
             └─ IMessageService.SendAsync(text, attachment: { Kind: "pokemon", PayloadJson })
                  └─ SignalR broadcast → every client's IAttachmentRegistry.Materialize
                                          → PokemonCardViewModel → implicit DataTemplate
```

Nothing in the core knows what a Pokémon is. The core carries an opaque `MessageAttachmentDto(Kind, PayloadJson)`; the extension registers the `"pokemon"` kind on the client and ships the XAML. The card respects the extension's own settings section (official artwork vs. pixel sprite, shiny by default), read through the core settings store. PokeAPI payloads are parsed by a **second source‑generated JSON context** (`PokeApiJsonContext`, snake_case) — third‑party JSON gets the same zero‑reflection treatment as our own DTOs.

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
│   ├── Assets/                # Application icon
│   ├── Behaviors/             # Attached behaviors (auto‑scroll, formatting, caption buttons, focus, clipboard)
│   ├── Configuration/         # IOptions<T> from appsettings.json
│   ├── Converters/            # IValueConverter implementations
│   ├── Extensions/            # CLIENT PLUGIN SYSTEM — IClientStartup + registries (panes, commands, settings, attachments)
│   │   ├── Audit/             #   in-box extension: audit pane + settings section
│   │   └── Pokemon/           #   in-box extension: /pokemon command, card renderer, settings section
│   ├── Legacy/                # THE OLD WORLD — contrast sample only
│   ├── Messages/              # Record types published through IMessenger
│   ├── Services/              # API + hub seams (interfaces), settings store, UI‑thread messenger, resilience helper, log provider
│   ├── Themes/                # Dark theme (no third‑party library)
│   ├── ViewModels/            # MVVM Toolkit partial classes (primary constructors); MessageSearch + SlashCommandInput
│   └── Views/                 # XAML — MainWindow (WindowChrome title bar + palette) + EmojiCatalog view data
├── ColtonStack.Contracts/     # Shared DTOs, hub contract, validators, SettingKey, JSON source‑gen context
├── ColtonStack.Server/        # ASP.NET Core Minimal API server
│   ├── Data/                  # Dapper.Contrib row classes (one per table)
│   ├── Endpoints/             # Minimal API route handlers + shared EndpointValidation helper
│   ├── Extensions/            # SERVER PLUGIN SYSTEM — IServerStartup, the audit extension, the Pokémon extension
│   ├── Hubs/                  # Typed SignalR hub (Hub<IChatHubClient>)
│   ├── Infrastructure/        # SQLite connection factory, schema init (+ ISchemaContributor), seed data
│   ├── Middleware/             # Chaos middleware (demo failure injector) + injected ChaosState
│   ├── Services/              # Business logic (ChannelService, MessageService, UserService, SettingsService, AuditService)
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