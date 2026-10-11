namespace TcgStockBot.Library.Enums;

/// <summary>
/// What an account may do. Everyone uses the same bot; only an admin manages the other accounts.
/// </summary>
public enum UserRole
{
    /// <summary>Uses the app.</summary>
    User,

    /// <summary>Uses the app and manages accounts (the first account is an admin).</summary>
    Admin,
}
