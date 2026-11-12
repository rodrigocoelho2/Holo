using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Validation;
using TcgStockBot.Sdk;
using TcgStockBot.Sdk.Models;

namespace TcgStockBot.Website.Model.Users;

/// <summary>The signed-in account, as the dashboard sees it.</summary>
public sealed class MeModel
{
    /// <summary>Gets or sets the account id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the user name.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the role.</summary>
    public UserRole Role { get; set; }

    /// <summary>Gets or sets a value indicating whether anyone may create an account (Dashboard__AllowRegistration).</summary>
    public bool AllowRegistration { get; set; }

    /// <summary>Gets or sets a value indicating whether Discord sign-in is set up on this Holo.</summary>
    public bool DiscordEnabled { get; set; }

    /// <summary>Gets or sets a value indicating whether this account has a Discord account linked.</summary>
    public bool DiscordLinked { get; set; }

    /// <summary>Gets or sets the linked Discord account's name.</summary>
    public string? DiscordName { get; set; }

    /// <summary>Gets or sets a value indicating whether the account has a password (one made with Discord may not).</summary>
    public bool HasPassword { get; set; } = true;
}

/// <summary>A new password for the signed-in account.</summary>
public sealed class PasswordChangeRequest
{
    /// <summary>Gets or sets the current password.</summary>
    public string? CurrentPassword { get; set; }

    /// <summary>Gets or sets the new password.</summary>
    public string? NewPassword { get; set; }
}

/// <summary>
/// Account operations for the dashboard: the signed-in account, its password and (admins) the other accounts.
/// </summary>
/// <param name="apiClient">The API client.</param>
public sealed class UserModel(TcgStockBotClient apiClient) : WebsiteBaseModel(apiClient)
{
    /// <summary>The signed-in account.</summary>
    /// <param name="userId">The account id from the cookie.</param>
    /// <param name="allowRegistration">Whether registration is open.</param>
    /// <param name="discordEnabled">Whether Discord sign-in is set up.</param>
    /// <returns>The account.</returns>
    public Task<AjaxDataResult<MeModel>> MeAsync(string userId, bool allowRegistration, bool discordEnabled) => CallAsync(async () =>
    {
        var user = await ApiClient.UserGetAsync(userId);
        return new MeModel
        {
            Id = user.Id,
            UserName = user.UserName,
            Role = user.Role,
            AllowRegistration = allowRegistration,
            DiscordEnabled = discordEnabled,
            DiscordLinked = user.DiscordLinked,
            DiscordName = user.DiscordName,
            HasPassword = user.HasPassword,
        };
    });

    /// <summary>Unlinks Discord from the signed-in account (refused while it's the only way in).</summary>
    /// <param name="userId">The account id from the cookie.</param>
    /// <returns>Done, or the problem.</returns>
    public Task<AjaxDataResult<bool>> DiscordUnlinkAsync(string userId) => CallAsync(async () => { await ApiClient.UserDiscordLinkAsync(new UserDiscordLinkRequest { Id = userId }); });

    /// <summary>Changes the signed-in account's password.</summary>
    /// <param name="userId">The account id from the cookie.</param>
    /// <param name="item">Current and new password.</param>
    /// <returns>The account (with its new security stamp).</returns>
    public Task<AjaxDataResult<UserDTO>> ChangePasswordAsync(string userId, PasswordChangeRequest item) =>
        CallAsync(() => ApiClient.UserPasswordAsync(new UserPasswordRequest { Id = userId, CurrentPassword = item.CurrentPassword, NewPassword = item.NewPassword }));

    /// <summary>Every account (admins only), without security stamps.</summary>
    /// <returns>The accounts.</returns>
    public Task<AjaxDataResult<List<UserDTO>>> ListAsync() => CallAsync(async () =>
    {
        var users = await ApiClient.UserListAsync();
        users.ForEach(static user => user.SecurityStamp = string.Empty);
        return users;
    });

    /// <summary>Creates an account for someone else (admins only).</summary>
    /// <param name="item">User name and password.</param>
    /// <returns>The account, without its security stamp.</returns>
    public Task<AjaxDataResult<UserDTO>> CreateAsync(UserCreateRequest item) => CallAsync(async () =>
    {
        var user = await ApiClient.UserCreateAsync(item);
        user.SecurityStamp = string.Empty;
        return user;
    });

    /// <summary>Deletes another account (admins only).</summary>
    /// <param name="userId">The signed-in account (cannot delete itself here).</param>
    /// <param name="identifier">The account to delete.</param>
    /// <returns>Success.</returns>
    public async Task<AjaxDataResult<bool>> DeleteAsync(string userId, string identifier)
    {
        if (string.Equals(userId, identifier, StringComparison.Ordinal))
        {
            return new AjaxDataResult<bool> { IsValid = false, ValidationErrors = [new ApiValidationError(string.Empty, "You can't delete the account you are signed in with.")] };
        }

        return await CallAsync(() => ApiClient.UserDeleteAsync(identifier ?? string.Empty));
    }
}
