using FluentValidation;

namespace ColtonStack.Contracts;

/// <summary>
/// FluentValidation rules for <see cref="UpdateProfileRequest"/>.
/// Shared between client (SettingsViewModel) and server (UserEndpoints) so validation
/// logic lives in one place, not duplicated in a ViewModel and an endpoint.
/// </summary>
public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .Length(1, 40).WithMessage("Display name must be 1–40 characters.");

        RuleFor(x => x.AvatarColor)
            .NotEmpty().WithMessage("Avatar color is required.")
            .Must(BeValidHexColor).WithMessage("Avatar color must look like #RRGGBB.");
    }

    private static bool BeValidHexColor(string color) =>
        color is { Length: 7 } && color[0] == '#' && color[1..].All(Uri.IsHexDigit);
}