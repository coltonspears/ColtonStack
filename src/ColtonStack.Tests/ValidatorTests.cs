using ColtonStack.Contracts;
using FluentValidation.TestHelper;
using Xunit;

namespace ColtonStack.Tests;

/// <summary>
/// FluentValidation validators live in the shared Contracts project — these tests prove
/// the rules work identically whether called from the client ViewModel or the server endpoint.
/// </summary>
public sealed class UpdateProfileRequestValidatorTests
{
    private readonly UpdateProfileRequestValidator _validator = new();

    [Fact]
    public void ValidRequest_Passes()
    {
        var request = new UpdateProfileRequest("Colton", "#E01E5A");
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyDisplayName_Fails()
    {
        var request = new UpdateProfileRequest("", "#E01E5A");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void TooLongDisplayName_Fails()
    {
        var request = new UpdateProfileRequest(new string('a', 41), "#E01E5A");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void InvalidHexColor_Fails()
    {
        var request = new UpdateProfileRequest("Colton", "not-a-color");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.AvatarColor);
    }

    [Fact]
    public void ShortHexColor_Fails()
    {
        var request = new UpdateProfileRequest("Colton", "#FFF"); // must be #RRGGBB
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.AvatarColor);
    }

    [Fact]
    public void EmptyAvatarColor_Fails()
    {
        var request = new UpdateProfileRequest("Colton", "");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.AvatarColor);
    }
}

public sealed class SendMessageRequestValidatorTests
{
    private readonly SendMessageRequestValidator _validator = new();

    [Fact]
    public void ValidMessage_Passes()
    {
        var request = new SendMessageRequest("Hello world");
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyText_Fails()
    {
        var request = new SendMessageRequest("");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Text);
    }

    [Fact]
    public void WhitespaceText_Fails()
    {
        var request = new SendMessageRequest("   ");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Text);
    }
}

public sealed class CreateChannelRequestValidatorTests
{
    private readonly CreateChannelRequestValidator _validator = new();

    [Fact]
    public void ValidName_Passes()
    {
        var request = new CreateChannelRequest("general", "Chat");
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyName_Fails()
    {
        var request = new CreateChannelRequest("", "Chat");
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}