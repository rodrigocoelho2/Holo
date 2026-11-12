using TcgStockBot.Library.Enums;

namespace TcgStockBot.WebApi.Features.Users.Models.Entities;

/// <summary>
/// An account of the dashboard (users.json). The password is only kept as a salted PBKDF2 hash.
/// </summary>
public sealed class User
{
    /// <summary>Gets or sets the stable identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the user name (unique, case-insensitive).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the password hash (<see cref="Services.PasswordHasher"/> format).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Gets or sets the role.</summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>Gets or sets when the account was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the last successful sign-in.</summary>
    public DateTimeOffset? LastSignInAt { get; set; }

    /// <summary>Gets or sets a random value that changes with the password, so other sessions end.</summary>
    public string SecurityStamp { get; set; } = string.Empty;

    /// <summary>Gets or sets the linked Discord account's id (null: none). An account made with Discord has no password hash until one is set.</summary>
    public string? DiscordId { get; set; }

    /// <summary>Gets or sets the linked Discord account's name when it was linked.</summary>
    public string? DiscordName { get; set; }
}
