using FluentValidation;

namespace ColtonStack.Contracts;

/// <summary>
/// FluentValidation rules for <see cref="SendMessageRequest"/>.
/// Prevents empty or whitespace-only text from reaching the save pipeline.
/// </summary>
public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Text)
            .NotEmpty().WithMessage("Message text is required.")
            .MaximumLength(10_000).WithMessage("Message text must be 10,000 characters or fewer.");
    }
}