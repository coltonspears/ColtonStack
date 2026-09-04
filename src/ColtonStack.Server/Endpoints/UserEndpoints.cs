using ColtonStack.Contracts;
using ColtonStack.Server.Services;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ColtonStack.Server.Endpoints;

/// <summary>
/// Workspace members and the current user's profile. Thin: validation through the shared
/// <see cref="UpdateProfileRequestValidator"/>, then straight to <see cref="IUserService"/>.
/// </summary>
public static class UserEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/users");

        api.MapGet("/", async Task<IResult> (IUserService users, CancellationToken cancellationToken) =>
            TypedResults.Ok(await users.GetAllAsync(cancellationToken)));

        api.MapPut("/me", async Task<IResult> (
            UpdateProfileRequest request,
            IValidator<UpdateProfileRequest> validator,
            IUserService users,
            CancellationToken cancellationToken) =>
        {
            if (await EndpointValidation.ValidateAsync(validator, request, cancellationToken) is { } invalid)
            {
                return invalid;
            }

            return TypedResults.Ok(await users.UpdateSelfAsync(request, cancellationToken));
        });
    }
}
