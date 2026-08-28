namespace ColtonStack.Server.Infrastructure;

/// <summary>Static first-run seed data for the demo workspace.</summary>
public static class SeedData
{
    public static readonly (long Id, string Name, string Color, bool IsSelf)[] Users =
    [
        (1, "Colton", "#E01E5A", true),
        (2, "Maya Chen", "#2EB67D", false),
        (3, "Devon Park", "#ECB22E", false),
        (4, "Riley Fox", "#36C5F0", false),
        (5, "Priya Nair", "#E8912D", false),
    ];

    public static readonly (long Id, string Name, string Topic)[] Channels =
    [
        (1, "general", "Company-wide announcements and watercooler chat"),
        (2, "dev-talk", "Bugs, builds and benchmarks"),
        (3, "design", "Pixels, prototypes and critiques"),
        (4, "incidents", "Something is on fire — check here first"),
        (5, "random", "Anything goes"),
    ];

    /// <summary>(channelId, authorName, minutesAgo, text) — seeded history reads like a real workspace.</summary>
    public static readonly (long Channel, string Author, int MinutesAgo, string Text)[] Messages =
    [
        (1, "Maya Chen", 3 * 24 * 60 + 320, "Morning everyone! Reminder: the platform demo is Thursday at 2."),
        (1, "Devon Park", 3 * 24 * 60 + 315, "Put it on my calendar. Will there be snacks?"),
        (1, "Colton", 3 * 24 * 60 + 310, "There will be snacks."),
        (1, "Priya Nair", 3 * 24 * 60 + 90, "Demo slides are in the shared drive, title slide is looking sharp"),
        (1, "Maya Chen", 2 * 24 * 60 + 400, "Office closed Monday for maintenance — the badge readers get upgraded."),
        (1, "Riley Fox", 2 * 24 * 60 + 120, "the coffee machine too??"),
        (1, "Colton", 2 * 24 * 60 + 118, "The coffee machine is non-negotiable. It stays."),
        (1, "Riley Fox", 2 * 24 * 60 + 117, "crisis averted"),
        (1, "Devon Park", 26 * 60, "Welcome to all the new folks starting today! Say hi in this thread."),
        (1, "Priya Nair", 24 * 60, "Hi! Priya from the platform team, excited to be here"),
        (1, "Maya Chen", 300, "Don't forget Thursday's demo — 2pm, main conference room."),
        (2, "Devon Park", 3 * 24 * 60 + 500, "Anyone else seeing flaky tests on the CI queue? Three green reruns in a row."),
        (2, "Colton", 3 * 24 * 60 + 480, "Yeah, it's the ordering test again. I'll pin it to a single runner."),
        (2, "Riley Fox", 3 * 24 * 60 + 200, "TIL Dapper parameterizes everything by default. No more string-concat SQL from 2019, please."),
        (2, "Maya Chen", 2 * 24 * 60 + 700, "Perf pass on the message query landed — channel list went from 340ms to 6ms on the sample DB."),
        (2, "Devon Park", 2 * 24 * 60 + 690, "6ms?! What was it doing before, resolving properties through reflection one at a time?"),
        (2, "Colton", 2 * 24 * 60 + 688, "...you don't want to know. Yes. Exactly that."),
        (2, "Riley Fox", 2 * 24 * 60 + 80, "Reminder: async all the way down. If you see .Result in a code review, reject it on sight."),
        (2, "Priya Nair", 50 * 60, "Who owns the retry policy? The backoff looks exponential but the jitter seems missing."),
        (2, "Colton", 48 * 60, "Good catch — that predates the resilience pipelines. Fixed on the current branch."),
        (2, "Maya Chen", 240, "Benchmarks for the new serializers are in: source-gen JSON beats reflection JSON ~2x on cold start."),
        (3, "Devon Park", 4 * 24 * 60 + 60, "New dark theme tokens are up. Sidebar is #19171D, accent is the classic aubergine pink."),
        (3, "Riley Fox", 4 * 24 * 60 + 30, "Can we talk about the 4px corner radius on avatars? We voted on 50%. This is a democracy."),
        (3, "Maya Chen", 2 * 24 * 60 + 500, "Unread badge styling updated — it's now #E01E5A with white text, passes contrast AA."),
        (3, "Priya Nair", 300, "The typing indicator animation is 3 dots now, not 2. You're welcome."),
        (4, "Colton", 5 * 24 * 60 + 90, "Deploy 2024.31 is rolling back — connection pool exhaustion under load. Root cause thread to follow."),
        (4, "Devon Park", 5 * 24 * 60 + 60, "Confirmed pooled connections were never disposed. Classic."),
        (4, "Colton", 5 * 24 * 60 + 15, "Fixed and redeployed as 2024.32. Postmortem doc linked in the runbook."),
        (4, "Riley Fox", 30 * 60, "Heads up: load test at 4pm today. Expect some 503s from the staging API — that's the point."),
        (4, "Maya Chen", 20, "Retrying through the resilience pipeline like a champ. Retries: 3. Jitter: on. Blood pressure: normal."),
        (5, "Devon Park", 6 * 24 * 60 + 400, "correct horse battery staple is now my entire personality"),
        (5, "Riley Fox", 6 * 24 * 60 + 100, "My keyboard shortcut muscle memory has fully migrated to the new IDE. Day 4: no Alt+F4 incidents."),
        (5, "Priya Nair", 3 * 24 * 60 + 55, "A pigeon landed on my balcony and I'm 90% sure it's the same one from last week."),
        (5, "Maya Chen", 3 * 24 * 60 + 50, "the pigeon has a standup now?"),
        (5, "Priya Nair", 3 * 24 * 60 + 48, "it blocks my sprint, so yes"),
        (5, "Colton", 45, "Friday trivia is back. Teams of 4, theme is '2000s tech'. Deliverables: trauma."),
    ];
}
