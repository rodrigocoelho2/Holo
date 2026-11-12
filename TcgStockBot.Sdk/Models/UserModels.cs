using TcgStockBot.Library.Enums;

namespace TcgStockBot.Sdk.Models;

/// <summary>An account (mirror of the WebApi UserDTO).</summary>
public sealed class UserDTO
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastSignInAt { get; set; }
    public string SecurityStamp { get; set; } = string.Empty;
    public bool DiscordLinked { get; set; }
    public string? DiscordName { get; set; }
    public bool HasPassword { get; set; }
}

/// <summary>How many accounts exist.</summary>
public sealed class UserStatusDTO
{
    public int UserCount { get; set; }
}

/// <summary>Creates an account.</summary>
public sealed class UserCreateRequest
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
}

/// <summary>Checks a user name and password.</summary>
public sealed class UserSignInRequest
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
}

/// <summary>Changes a password.</summary>
public sealed class UserPasswordRequest
{
    public string? Id { get; set; }
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
}

/// <summary>Signs in with Discord: finds the account a (checked) Discord account is linked to.</summary>
public sealed class UserDiscordSignInRequest
{
    public string? DiscordId { get; set; }
}

/// <summary>Creates an account that signs in with Discord (no password until one is set).</summary>
public sealed class UserDiscordCreateRequest
{
    public string? UserName { get; set; }
    public string? DiscordId { get; set; }
    public string? DiscordName { get; set; }
}

/// <summary>Links a Discord account to an account (no DiscordId: unlinks it).</summary>
public sealed class UserDiscordLinkRequest
{
    public string? Id { get; set; }
    public string? DiscordId { get; set; }
    public string? DiscordName { get; set; }
}
