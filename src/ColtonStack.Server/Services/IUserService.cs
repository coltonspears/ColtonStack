using ColtonStack.Contracts;

namespace ColtonStack.Server.Services;

/// <summary>
/// Workspace members. One place resolves "who is the current user" instead of three call sites
/// each scanning the Users table — the hub, the message pipeline and the profile endpoint all
/// ask here.
/// </summary>
public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>The single-user demo's "me" row.</summary>
    Task<UserDto> GetSelfAsync(CancellationToken cancellationToken);

    /// <summary>Any member by id, or null.</summary>
    Task<UserDto?> FindAsync(long userId, CancellationToken cancellationToken);

    /// <summary>Updates the current user's profile (validated by the caller) and audits it.</summary>
    Task<UserDto> UpdateSelfAsync(UpdateProfileRequest request, CancellationToken cancellationToken);
}
