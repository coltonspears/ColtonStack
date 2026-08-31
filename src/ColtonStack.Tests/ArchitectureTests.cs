using ColtonStack.Client.ViewModels;
using ColtonStack.Contracts;
using ColtonStack.Server.Services;
using NetArchTest.Rules;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// Executed architecture. The conventions that keep this codebase free of god classes —
/// layering, sealed view models, no reflection outside the Legacy contrast sample — are
/// enforced here as tests, so they survive team growth and code review lapses.
/// A failing rule fails the build, exactly like a compiler error.
/// </summary>
public sealed class ArchitectureTests
{
    private const string ClientNamespace = "ColtonStack.Client";
    private const string ServerNamespace = "ColtonStack.Server";
    private const string ContractsNamespace = "ColtonStack.Contracts";

    [Fact]
    public void ViewModels_AreSealedAndNeverAbstract()
    {
        // Abstract view model base classes are how god classes are born. Every VM here is
        // a leaf: concrete, sealed, constructed by DI. This rule keeps it that way.
        var result = Types.InAssembly(typeof(MainViewModel).Assembly)
            .That()
            .ResideInNamespace($"{ClientNamespace}.ViewModels")
            .And()
            .AreClasses()
            .Should()
            .NotBeAbstract()
            .And()
            .BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful, DescribeFailures(result));
    }

    [Fact]
    public void ViewModels_NeverDependOnTheServer()
    {
        // The client talks HTTP + SignalR through ColtonStackApiClient and ChatHubClient.
        // A view model referencing ColtonStack.Server would merge the two processes'
        // internals — the first step back toward the monolith.
        var result = Types.InAssembly(typeof(MainViewModel).Assembly)
            .That()
            .ResideInNamespace($"{ClientNamespace}.ViewModels")
            .ShouldNot()
            .HaveDependencyOnAny(ServerNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, DescribeFailures(result));
    }

    [Fact]
    public void ViewModels_NeverTouchReflection()
    {
        // Reflection by string is the legacy failure mode: typos compile and explode later.
        // The Legacy/ contrast sample is the only place reflection may appear — and these
        // tests make sure it never leaks back into real code.
        var result = Types.InAssembly(typeof(MainViewModel).Assembly)
            .That()
            .ResideInNamespace($"{ClientNamespace}.ViewModels")
            .Or()
            .ResideInNamespace($"{ClientNamespace}.Services")
            .ShouldNot()
            .HaveDependencyOn("System.Reflection")
            .GetResult();

        Assert.True(result.IsSuccessful, DescribeFailures(result));
    }

    [Fact]
    public void Legacy_IsQuarantined_NoProductCodeReferencesIt()
    {
        // The legacy folder exists only to be read as a contrast sample. Nothing outside
        // ColtonStack.Client.Legacy may depend on it — if someone "reuses" LegacyEntityBase,
        // this test fails before it ships.
        var result = Types.InAssembly(typeof(MainViewModel).Assembly)
            .That()
            .DoNotResideInNamespace($"{ClientNamespace}.Legacy")
            .ShouldNot()
            .HaveDependencyOn($"{ClientNamespace}.Legacy")
            .GetResult();

        Assert.True(result.IsSuccessful, DescribeFailures(result));
    }

    [Fact]
    public void Contracts_ArePure_DataAndRulesOnly()
    {
        // Contracts is shared by both processes. It must never reach into either side's
        // internals, nor into WPF or ASP.NET — a DTO that drags its host runtime along
        // breaks the isolation the extension model depends on.
        var result = Types.InAssembly(typeof(MessageDto).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                ClientNamespace,
                ServerNamespace,
                "System.Windows",
                "Microsoft.AspNetCore",
                "Microsoft.Extensions.Hosting")
            .GetResult();

        Assert.True(result.IsSuccessful, DescribeFailures(result));
    }

    [Fact]
    public void Server_Services_AreSealedAndConcrete()
    {
        // Dumb records flow through smart services — and service *classes* stay concrete
        // leaves registered in the composition root, not an inheritance hierarchy. (The
        // I*Service interfaces live in the same namespace, so scope the rule to classes.)
        var result = Types.InAssembly(typeof(ChannelService).Assembly)
            .That()
            .ResideInNamespace($"{ServerNamespace}.Services")
            .And()
            .AreClasses()
            .Should()
            .NotBeAbstract()
            .And()
            .BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful, DescribeFailures(result));
    }

    [Fact]
    public void Server_NeverDependsOnTheClient()
    {
        var result = Types.InAssembly(typeof(ChannelService).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(ClientNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful, DescribeFailures(result));
    }

    /// <summary>NetArchTest failures list the offending type names; surface them in the assert message.</summary>
    private static string DescribeFailures(Result result) =>
        result.IsSuccessful
            ? string.Empty
            : "Violations: " + string.Join(", ", result.FailingTypeNames ?? ["<unknown>"]);
}