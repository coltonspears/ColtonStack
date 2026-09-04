using ColtonStack.Client.ViewModels;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// PersonViewModel is an immutable projection of UserDto — no base class, no notifications,
/// just constructor parameters and computed properties. These tests prove it at compile time.
/// </summary>
public sealed class PersonViewModelTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var user = new ColtonStack.Contracts.UserDto(1, "Devon Park", "#2EB67D", IsSelf: false);
        var vm = new PersonViewModel(user);

        Assert.Equal(1, vm.Id);
        Assert.Equal("Devon Park", vm.DisplayName);
        Assert.Equal("#2EB67D", vm.AvatarColor);
        Assert.False(vm.IsSelf);
    }

    [Fact]
    public void Constructor_IsSelf_Marked()
    {
        var user = new ColtonStack.Contracts.UserDto(2, "Colton", "#E01E5A", IsSelf: true);
        var vm = new PersonViewModel(user);

        Assert.True(vm.IsSelf);
    }

    [Fact]
    public void Initials_FromDisplayName()
    {
        var vm = new PersonViewModel(new ColtonStack.Contracts.UserDto(1, "Riley Fox", "#36C5F0", IsSelf: false));
        Assert.Equal("RF", vm.Initials);
    }

    [Fact]
    public void ViewModel_IsNotObservableObject()
    {
        Assert.False(typeof(System.ComponentModel.INotifyPropertyChanged).IsAssignableFrom(typeof(PersonViewModel)));
    }
}