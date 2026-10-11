namespace TcgStockBot.Website.Model.Account;

/// <summary>
/// Model of the login form.
/// </summary>
public sealed class LoginModel
{
    /// <summary>Gets or sets the local address to go back to after signing in.</summary>
    public string ReturnUrl { get; set; } = "/";

    /// <summary>Gets or sets a value indicating whether the session is kept after the browser closes.</summary>
    public bool Remember { get; set; } = true;

    /// <summary>Gets or sets the message shown above the form (wrong password, too many tries…).</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets how many days "manter sessão" lasts.</summary>
    public int RememberDays { get; set; } = 30;
}
