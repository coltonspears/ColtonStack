namespace ColtonStack.Client.Extensions;

/// <summary>
/// One feature's client-side bootstrap. An extension is a plain class — no base class, no
/// attributes, no reflection discovery — handed an explicit context by the composition root.
/// Extensions are listed in one place (<see cref="App.BuildHost"/>), so "what is installed"
/// is a compile-checked line of code, not a runtime scan.
///
/// In the full product each extension lives in its own assembly and can ship independently
/// of the core app; here the two in-box extensions (core panes, audit) demonstrate the same
/// contract the shell itself follows.
/// </summary>
public interface IClientStartup
{
    /// <summary>Registers services, panes and resources for this extension. Runs before the DI container is built.</summary>
    void Configure(IClientStartupContext context);
}