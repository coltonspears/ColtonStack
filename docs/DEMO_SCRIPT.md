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

Talking point: open `ChatViewModel` and `ChannelListViewModel` — neither references the other. The only shared thing is immutable message records on the messenger.

## 2. Ctrl+click through the source generators (3 min)

* `ChannelListItemViewModel.Preview` → `[ObservableProperty] public partial string Preview { get; set; }` — F12 twice to jump into the **generated** implementation. No backing field. Ever.
* `ChannelListItemViewModel.UnreadCount` → hand-written property using the C# 14 **`field` keyword** with clamping — custom logic, still no declared backing field.
* `ChatViewModel.SendMessageCommand` → generated `AsyncRelayCommand` from `[RelayCommand]`, cancellation-aware, concurrency-gated.
* `ChatActivitySimulator` on the server — a `BackgroundService` feeding the *same* `MessageService.SendAsUserAsync` pipeline your own sends use; nothing about a simulated message is special.
* `Contracts/ColtonStackJsonContext.cs` → every DTO serialized by **source-generated** System.Text.Json. Same context on client and server.
* Server side: `AuditService` / `WebhookDispatchService` → `[LoggerMessage]` compiled logging delegates.
* `Views/MainWindow.xaml.cs` — the whole file is a constructor. Enter-to-send is a `KeyBinding`, auto-scroll is a reusable attached behavior, and the composition root assigns the DataContext. Nothing in the view to test, mock, or debug.
* `UiThreadMessenger` — composition over inheritance: a decorator wraps the messenger once at the thread boundary, so SignalR events land on the UI thread and **no view model contains a `Dispatcher`, an event handler, or any threading code at all**.
* `Server/Data/*Row.cs` + `WebhookEndpoints` — **Dapper.Contrib**: all CRUD (inserts, lookups, deletes, the seeder) is derived from five tiny row classes, zero SQL. Hand-written SQL survives in exactly three read queries that earn it — two joins/aggregates and the audit page.

## 3. Resilience you can watch (3 min)

* In a client's status bar, flip the **chaos** toggle. The server now fails ~40% of API requests with 503.
* Send messages rapidly from client A. The status bar's retry counter ticks up — **retry, exponential backoff, jitter** — and messages still land (watch them arrive in B). Users see latency, not errors.
* Keep failing requests until the **circuit breaker opens**: status text announces it, requests fail fast instead of hanging, and after 15 s it half-opens, probes, and closes.
* Toggle chaos off — "Circuit closed — server recovered".
* Kill the server (Ctrl+C in terminal 1). Client status goes **Reconnecting…** then red. Restart the server; the client **heals itself**: reconnects, re-joins the channel group, and messages flow again — including anything posted while it was reconnecting.

Talking point: none of this code lives in the view models — `ColtonStackApiClient` is a plain typed `HttpClient` wrapper; the pipeline is attached at the composition root in `App.xaml.cs`.

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
