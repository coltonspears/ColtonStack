using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace ColtonStack.Server.Endpoints;

/// <summary>
/// The one way endpoints reject bad input: run the shared FluentValidation validator, return a
/// 400 with every message joined. Written once so no endpoint hand-rolls its own checks.
/// </summary>
public static class EndpointValidation
{
    /// <summary>Returns a 400 result when <paramref name="request"/> is invalid, otherwise null.</summary>
    public static async Task<IResult?> ValidateAsync<TRequest>(
        IValidator<TRequest> validator,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        return validation.IsValid
            ? null
            : TypedResults.BadRequest(new { error = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)) });
    }
}
