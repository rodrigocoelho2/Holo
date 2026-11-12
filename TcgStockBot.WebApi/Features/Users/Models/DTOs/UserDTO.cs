using TcgStockBot.Library.Enums;

namespace TcgStockBot.WebApi.Features.Users.Models.DTOs;

/// <summary>An account (never the password hash).</summary>
public sealed class UserDTO
{
    /// <summary>Gets or sets the identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the user name.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the role.</summary>
    public UserRole Role { get; set; }

    /// <summary>Gets or sets when the account was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the last successful sign-in.</summary>
    public DateTimeOffset? LastSignInAt { get; set; }

    /// <summary>Gets or sets the security stamp (changes with the password; ends the other sessions).</summary>
    public string SecurityStamp { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether a Discord account is linked (it can sign in to this one).</summary>
    public bool DiscordLinked { get; set; }

    /// <summary>Gets or sets the linked Discord account's name.</summary>
    public string? DiscordName { get; set; }

    /// <summary>Gets or sets a value indicating whether the account has a password (one made with Discord has none until one is set).</summary>
    public bool HasPassword { get; set; }
}

/// <summary>Whether any account exists (the first visit creates the first one).</summary>
public sealed class UserStatusDTO
{
    /// <summary>Gets or sets how many accounts exist.</summary>
    public int UserCount { get; set; }
}

/// <summary>Creates an account.</summary>
public sealed class UserCreateRequest
{
    /// <summary>Gets or sets the user name.</summary>
    public string? UserName { get; set; }

    /// <summary>Gets or sets the password.</summary>
    public string? Password { get; set; }
}

/// <summary>Checks a user name and password.</summary>
public sealed class UserSignInRequest
{
    /// <summary>Gets or sets the user name.</summary>
    public string? UserName { get; set; }

    /// <summary>Gets or sets the password.</summary>
    public string? Password { get; set; }
}

/// <summary>Changes the password of an account.</summary>
public sealed class UserPasswordRequest
{
    /// <summary>Gets or sets the account.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the current password.</summary>
    public string? CurrentPassword { get; set; }

    /// <summary>Gets or sets the new password.</summary>
    public string? NewPassword { get; set; }
}

/// <summary>Signs in with Discord: the Website checked the Discord account, this finds the account it's linked to.</summary>
public sealed class UserDiscordSignInRequest
{
    /// <summary>Gets or sets the Discord account's id.</summary>
    public string? DiscordId { get; set; }
}

/// <summary>Creates an account that signs in with Discord (no password until one is set in Settings).</summary>
public sealed class UserDiscordCreateRequest
{
    /// <summary>Gets or sets the user name.</summary>
    public string? UserName { get; set; }

    /// <summary>Gets or sets the Discord account's id (already checked by the Website).</summary>
    public string? DiscordId { get; set; }

    /// <summary>Gets or sets the Discord account's name.</summary>
    public string? DiscordName { get; set; }
}

/// <summary>Links a Discord account to an account (no DiscordId: unlinks it).</summary>
public sealed class UserDiscordLinkRequest
{
    /// <summary>Gets or sets the account.</summary>
    public string? Id { get; set; }

    /// <summary>Gets or sets the Discord account's id.</summary>
    public string? DiscordId { get; set; }

    /// <summary>Gets or sets the Discord account's name.</summary>
    public string? DiscordName { get; set; }
}
