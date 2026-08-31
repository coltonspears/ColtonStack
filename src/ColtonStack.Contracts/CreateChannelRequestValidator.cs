using FluentValidation;

namespace ColtonStack.Contracts;

/// <summary>
/// FluentValidation rules for <see cref="CreateChannelRequest"/>.
/// Ensures channel names are non-empty.
/// </summary>
public sealed class CreateChannelRequestValidator : AbstractValidator<CreateChannelRequest>
{
    public CreateChannelRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Channel name is required.")
            .MaximumLength(80).WithMessage("Channel name must be 80 characters or fewer.");
    }
}