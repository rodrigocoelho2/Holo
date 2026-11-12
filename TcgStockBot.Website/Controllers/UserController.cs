using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TcgStockBot.Sdk;
using TcgStockBot.Website.Model;
using TcgStockBot.Website.Model.Users;
using TcgStockBot.Website.Security;

namespace TcgStockBot.Website.Controllers;

/// <summary>
/// Account actions of the dashboard: who is signed in, change password and (admins) manage accounts.
/// </summary>
/// <param name="apiClient">The API client.</param>
/// <param name="options">Sign-in settings.</param>
public partial class UserController(TcgStockBotClient apiClient, IOptions<DashboardOptions> options) : WebsiteBaseController(apiClient)
{
    /// <summary>The signed-in account.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    public async Task<IActionResult> MeAsync()
    {
        var model = new UserModel(ApiClient);
        return Json(await model.MeAsync(DashboardAuthentication.UserId(User), options.Value.AllowRegistration, options.Value.DiscordEnabled));
    }

    /// <summary>Sends the browser to Discord to link a Discord account to this account (a plain form post).</summary>
    /// <returns>A redirect to Discord.</returns>
    [HttpPost("/settings/discord")]
    [ValidateAntiForgeryToken]
    public IActionResult DiscordLink()
    {
        if (!options.Value.DiscordEnabled)
        {
            return NotFound();
        }

        var properties = new AuthenticationProperties { RedirectUri = "/sign-in/discord/done" };
        properties.Items[DiscordAuthentication.ModeKey] = "link";
        return Challenge(properties, DiscordAuthentication.Scheme);
    }

    /// <summary>Unlinks Discord (only when the account has a password to sign in with).</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DiscordUnlinkAsync() => Json(await new UserModel(ApiClient).DiscordUnlinkAsync(DashboardAuthentication.UserId(User)));

    /// <summary>Changes the signed-in account's password and keeps this browser signed in.</summary>
    /// <param name="item">Current and new password.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PasswordAsync([FromBody] PasswordChangeRequest item)
    {
        var model = new UserModel(ApiClient);
        var result = await model.ChangePasswordAsync(DashboardAuthentication.UserId(User), item);
        if (result.IsValid && result.Data is not null)
        {
            // The new stamp ends the other sessions; this one gets a fresh cookie.
            var remember = (await HttpContext.AuthenticateAsync()).Properties?.IsPersistent ?? false;
            await DashboardAuthentication.SignInAsync(HttpContext, result.Data, remember, options.Value.RememberDays);
        }

        return Json(new AjaxDataResult<bool> { IsValid = result.IsValid, ValidationErrors = result.ValidationErrors, Data = result.IsValid });
    }

    /// <summary>Every account.</summary>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ListAsync()
    {
        var model = new UserModel(ApiClient);
        return Json(await model.ListAsync());
    }

    /// <summary>Creates an account for someone else.</summary>
    /// <param name="item">User name and password.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateAsync([FromBody] TcgStockBot.Sdk.Models.UserCreateRequest item)
    {
        var model = new UserModel(ApiClient);
        return Json(await model.CreateAsync(item));
    }

    /// <summary>Deletes another account.</summary>
    /// <param name="identifier">The account id.</param>
    /// <returns>JSON <c>AjaxDataResult</c>.</returns>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAsync([FromQuery] string identifier)
    {
        var model = new UserModel(ApiClient);
        return Json(await model.DeleteAsync(DashboardAuthentication.UserId(User), identifier));
    }
}
