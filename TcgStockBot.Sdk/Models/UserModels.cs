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
