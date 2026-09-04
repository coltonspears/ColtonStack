# ColtonStack demo script (~12 minutes)

A guided tour with expected outcomes at each step. Start with nothing running.

## 0. Setup (before the audience arrives)

```bash
dotnet build ColtonStack.slnx         # must be 0 warnings — that's part of the demo
dotnet run --project src/ColtonStack.Server        # terminal 1
dotnet run --project src/ColtonStack.WebhookSink    # terminal 2
dotnet run --project src/ColtonStack.Client         # terminal 3
dotnet run --project src/ColtonStack.Client         # terminal 4 — yes, twice
```

Delete `src/ColtonStack.Server/coltonstack.db` first if you want a fresh seed. Point out: **the client started fine even if the server wasn't up yet** — status bar says "Waiting for server", then flips to a green "Connected" dot.

Within seconds the **chat simulator** takes over: teammates start typing (watch the header indicator) and posting in random channels — audit, SignalR push, webhooks, the whole pipeline. The **sim** pill in the status bar toggles it live (state synced from the server at startup).

## 1. The product you'll judge (2 min)

Two client windows, side by side. Both show the seeded workspace: five channels, history, avatars, timestamps.

* In client A type a message in **#general** and press Enter. It appears **instantly in client B** — no polling, SignalR push over a typed hub.
* In client B start typing (don't send). Client A shows "**Colton is typing…**" — a stateless push broadcast, throttled to one call per 2 s.
* Click the **＋** next to "Channels" in the sidebar, name a channel, press the arrow. It appears **instantly in the other client** — created once, broadcast once, inserted once.
* Switch B to **#design** and send from A in **#general**: A sees its message; B's **#general** row in the sidebar gets an unread badge and a fresh preview — computed from a `MessagePostedMessage` the sidebar hears on the `WeakReferenceMessenger`, completely decoupled from the chat pane.
* Select text in the composer and hit **B** / **I** / **S** / code, or pick an emoji from the 😊 popup — Slack-style `*bold*`, `_italic_`, `~strike~`, `` `code` `` markers, rendered with real styling (and clickable links) in the message list.
* Click the **people** tile on the rail — the workspace directory, loaded from `GET /api/users`.
* Press **Ctrl+,** (or the gear on the rail) → **Settings**, an in-window page. Under *Profile* change your display name and avatar color, Save. It round-trips through the resilience pipeline, persists via a **Dapper.Contrib `UpdateAsync`** (still zero SQL), and the People pane updates through a `ProfileUpdatedMessage` — Settings and People never reference each other. New messages you send carry the new name.

Talking point: open `ChatViewModel` and `ChannelListViewModel` — neither references the other. The only shared thing is immutable message records on the messenger.

## 1b. The command palette and `/pokemon` (2 min)

* Press **Ctrl+K**. Type `gen` → "#general" (a dynamic row from the channel list); type `chaos` → "Toggle chaos mode"; type `pok` → "Settings: Pokémon cards". Enter runs it. Every row was registered by an extension through `ICommandRegistry`; `CommandPaletteViewModel` has no list of its own and references no other view model — open it and check.
* In the composer type `/` — the popup lists every slash command. Continue `/pokemon pika` — the argument autocomplete fills from the server's **cached PokeAPI name index** (`GET /api/pokemon/search?q=pika`). Arrow down to *pikachu*, press **Enter**.
* A **Pokémon card** posts into the channel — on both clients, over SignalR — with artwork, type chips, abilities, moves, the Pokédex entry and stat bars. Point out the flow on the server terminal: first request → two PokeAPI calls → `INSERT INTO PokemonCards`; post the same Pokémon again → no PokeAPI call, straight from SQLite.
* Ctrl+, → *Pokémon cards* (a settings section the extension registered): switch to **pixel sprite**, tick **shiny by default**. Post another card — it honours both. Kill the client, start it again: the values came back from the server (`GET /api/settings`), not from a local file. Click the ✨ button on any card to flip shiny for just that card.
* Talking point: `MessageDto` now carries an opaque `Attachment { Kind, PayloadJson }`. The core renders it through `IAttachmentRegistry`; the `"pokemon"` kind, the card view model and its XAML all live in `Extensions/Pokemon/`. Delete that folder and the two lines in `App.xaml.cs` / `Program.cs` and the build is still green — the `ArchitectureTests` guarantee the core never referenced it.

## 2. Ctrl+click through the source generators (3 min)

* `ChannelListItemViewModel.Preview` → `[ObservableProperty] public partial string Preview { get; set; }` — F12 twice to jump into the **generated** implementation. No backing field. Ever.
* `ChannelListItemViewModel.UnreadCount` → hand-written property using the C# 14 **`field` keyword** with clamping — custom logic, still no declared backing field.
* `ChatViewModel.SendMessageCommand` → generated `AsyncRelayCommand` from `[RelayCommand]`, cancellation-aware, concurrency-gated.
* `ChatActivitySimulator` on the server — a `BackgroundService` feeding the *same* `MessageService.SendAsUserAsync` pipeline your own sends use; nothing about a simulated message is special.
* `Contracts/ColtonStackJsonContext.cs` → every DTO serialized by **source-generated** System.Text.Json. Same context on client and server.
* Server side: `AuditService` / `WebhookDispatchService` → `[LoggerMessage]` compiled logging delegates.
* `Views/MainWindow.xaml.cs` — the whole file is a constructor. Enter-to-send is a `KeyBinding`, auto-scroll is a reusable attached behavior, and the composition root assigns the DataContext. Nothing in the view to test, mock, or debug. The custom title bar (icon, palette, caption buttons) is `WindowChrome` + one `WindowCaptionCommands` behavior — still no code-behind.
* `ChatViewModel` constructor — every dependency is an **interface** (`IColtonStackApiClient`, `IChatConnection`, `ICommandRegistry`, `IAttachmentRegistry`). Then open `ChatViewModelTests`: the whole network is two `Substitute.For<>()` lines, and the tests run on a thread-pool thread because the view model has no `Dispatcher` — an `ArchitectureTests` rule now bans `System.Windows` from the ViewModels namespace outright.
* `MessageSearch` and `SlashCommandInput` — the two concerns that would have been "regions" in a god-class chat view model are separate sealed classes the chat view model *composes*, each with its own tests. Both use the `field` keyword for their validated properties.
* `Extensions/Pokemon/PokemonExtension.cs` (client) and `Extensions/Pokemon/PokemonExtension.cs` (server) — one feature, two small classes: settings, a typed `HttpClient` with the standard resilience handler, a schema contributor (its own SQLite table), endpoints, a slash command with autocomplete, a settings section, an attachment renderer, and a XAML dictionary. Zero core files edited.
* `UiThreadMessenger` — composition over inheritance: a decorator wraps the messenger once at the thread boundary, so SignalR events land on the UI thread and **no view model contains a `Dispatcher`, an event handler, or any threading code at all**.
* `Server/Data/*Row.cs` + `WebhookEndpoints` — **Dapper.Contrib**: all CRUD (inserts, lookups, deletes, updates like the profile save, the seeder) is derived from five tiny row classes, zero SQL. Hand-written SQL survives in exactly three read queries that earn it — two joins/aggregates and the audit page.
* `Behaviors/` — every "needs the actual control" feature is a small attached behavior, not code-behind: `ComposerActions` (selection wrapping + caret insertion, one **inherited** `Target` set on the composer root), `MessageTextFormatter` (markup → `Inlines`), `TitleBarTheme` (dark title bar via a **`[LibraryImport]` source-generated** P/Invoke — even interop has no runtime magic).

## 3. Resilience you can watch (3 min)

* In a client's status bar, flip the **chaos** toggle. The server now fails ~40% of API requests with 503.
* Send messages rapidly from client A. The status bar's retry counter ticks up — **retry, exponential backoff, jitter** — and messages still land (watch them arrive in B). Users see latency, not errors.
* Keep failing requests until the **circuit breaker opens**: status text announces it, requests fail fast instead of hanging, and after 15 s it half-opens, probes, and closes.
* Toggle chaos off — "Circuit closed — server recovered".
* Kill the server (Ctrl+C in terminal 1). Client status goes **Reconnecting…** then red. Restart the server; the client **heals itself**: reconnects, re-joins the channel group, and messages flow again — including anything posted while it was reconnecting.

Talking point: none of this code lives in the view models — `ColtonStackApiClient` is a plain typed `HttpClient` wrapper; the pipeline is attached once in `ResilientHttpClient.AddColtonStackHttpClient<T>()`, and the Pokémon extension's `PokemonApi` gets the identical pipeline by calling the same helper. Flip chaos on and post a card: the retries show up in the status bar exactly like a plain message.

## 4. Webhooks + audit: smart services, dumb models (2 min)

```bash
curl -X POST http://localhost:5080/api/webhooks -H "Content-Type: application/json" \
  -d '{"url":"http://localhost:5090/webhook","secret":"demo-secret"}'

curl -X POST http://localhost:5080/api/channels/1/messages -H "Content-Type: application/json" \
  -d '{"text":"look at the sink"}'
```

The sink terminal prints the delivery with **signature: VALID** (HMAC-SHA256, constant-time verified). Fun variant: `curl -X POST http://localhost:5090/chaos/true` to make the sink flaky, send again — the server retries with backoff and the sink shows the same message arriving twice, then succeeding.

Then:

```bash
curl http://localhost:5080/api/audit?limit=3
```

Every message and channel creation is in the audit trail — written by `IAuditService`, an injected service. **The model is a 7-property record that cannot audit, save, delete or serialize itself.** That's the point.

## 5. The old world, for contrast (2 min)

Open `src/ColtonStack.Client/Legacy/`:

* `LegacyEntityBase` — the god base class: reflection INPC, `EnableAuditing = true` magic, soft delete, dirty tracking, `RaiseAllChanged()`, and a `Save()` that builds SQL by reflecting over attributes.
* `LegacyMessageViewModel` — `RaisePropertyChanged("AutherName")`: the **typo compiles**. Show the comment: it notifies nothing, at runtime, forever.
* Point at the `#pragma warning disable RS0030` at the top: this is the only file in the solution allowed to use `Type.GetProperties` et al. — because it *is* the thing being banned.
* In the modern files, try adding `var x = Task.Result;` or `Thread.Sleep(100)` anywhere — the build **fails** via `BannedSymbols.txt`.

## 6. Close

* `dotnet build ColtonStack.slnx` — 0 warnings with analyzers at full enforcement, warnings-as-errors, banned APIs.
* Everything on the slide — MVVM Toolkit, Dapper, MEDI, Generic Host, Resilience — is doing exactly one job each, visibly, with no magic.
