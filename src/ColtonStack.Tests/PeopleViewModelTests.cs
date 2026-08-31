using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using ColtonStack.Client.ViewModels;
using ColtonStack.Contracts;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// PeopleViewModel loads members and reacts to ProfileUpdatedMessage through the messenger.
/// These tests exercise the receive path without DI or HTTP — just the messenger and the VM.
/// </summary>
public sealed class PeopleViewModelTests : IDisposable
{
    private readonly IMessenger _messenger = WeakReferenceMessenger.Default;
    private readonly PeopleViewModel _vm;

    public PeopleViewModelTests()
    {
        // PeopleViewModel needs an IMessenger; NullLogger is fine since we don't test logging here.
        _vm = new PeopleViewModel(
            Substitute.For<ColtonStackApiClient>(
                Substitute.For<HttpClient>(),
                NullLogger<ColtonStackApiClient>.Instance),
            _messenger,
            NullLogger<PeopleViewModel>.Instance);
        _messenger.RegisterAll(_vm);
    }

    public void Dispose()
    {
        _messenger.UnregisterAll(_vm);
    }

    [Fact]
    public void InitialState_Empty()
    {
        Assert.Empty(_vm.People);
        Assert.False(_vm.IsLoading);
    }

    [Fact]
    public void Receive_ProfileUpdated_UpdatesExistingPerson()
    {
        // Seed the people list directly (simulating a loaded state)
        var original = new UserDto(1, "Old Name", "#000", IsSelf: false);
        _vm.People.Add(new PersonViewModel(original));

        var updated = new UserDto(1, "New Name", "#FFF", IsSelf: false);
        _messenger.Send(new ProfileUpdatedMessage(updated));

        Assert.Single(_vm.People);
        Assert.Equal("New Name", _vm.People[0].DisplayName);
        Assert.Equal("#FFF", _vm.People[0].AvatarColor);
    }

    [Fact]
    public void Receive_ProfileUpdated_IgnoresUnknownUser()
    {
        _vm.People.Add(new PersonViewModel(new UserDto(1, "Colton", "#E01E5A", IsSelf: false)));

        var stranger = new UserDto(999, "Stranger", "#000", IsSelf: false);
        _messenger.Send(new ProfileUpdatedMessage(stranger));

        Assert.Single(_vm.People);
        Assert.Equal("Colton", _vm.People[0].DisplayName);
    }
}