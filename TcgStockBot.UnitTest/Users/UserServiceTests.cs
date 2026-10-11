using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Users.Models.DTOs;
using TcgStockBot.WebApi.Features.Users.Repositories;
using TcgStockBot.WebApi.Features.Users.Services;

namespace TcgStockBot.UnitTest.Users;

/// <summary>
/// Accounts: the first one is an admin, names are unique, passwords are hashed, sign-in never says which part was
/// wrong, a new password ends other sessions, and the last admin stays.
/// </summary>
public sealed class UserServiceTests : IDisposable
{
    private readonly TestDataContext _data = new();
    private readonly UserService _service;

    public UserServiceTests() => _service = new UserService(new UserRepository(_data.Context), new GlobalizationHelper(), TimeProvider.System);

    public void Dispose() => _data.Dispose();

    private Task<ServiceApiResponse<UserDTO>> Create(string name, string password = "a-long-password") =>
        _service.CreateAsync(new UserCreateRequest { UserName = name, Password = password });

    [Fact]
    public async Task FirstAccountIsAdmin_TheNextAreUsers_AndPasswordsAreNotStoredInClear()
    {
        var first = (await Create("rodas")).Result!;
        var second = (await Create("guest")).Result!;

        Assert.Equal(UserRole.Admin, first.Role);
        Assert.Equal(UserRole.User, second.Role);
        Assert.Equal(2, (await _service.StatusAsync()).Result!.UserCount);
        Assert.DoesNotContain("a-long-password", File.ReadAllText(_data.Context.UsersPath));
    }

    [Fact]
    public async Task Create_RejectsDuplicateBadNameAndShortPassword()
    {
        await Create("rodas");

        Assert.Equal(["UserName"], (await Create("RODAS")).Problems!.Errors.Select(static error => error.Field));
        Assert.Equal(["UserName"], (await Create("a b")).Problems!.Errors.Select(static error => error.Field));
        Assert.Equal(["Password"], (await Create("other", "short")).Problems!.Errors.Select(static error => error.Field));
    }

    [Fact]
    public async Task SignIn_WorksWithAnyCase_AndFailsTheSameWayForUnknownUserOrWrongPassword()
    {
        await Create("Rodas");

        var ok = await _service.SignInAsync(new UserSignInRequest { UserName = "rodas", Password = "a-long-password" });
        var wrongPassword = await _service.SignInAsync(new UserSignInRequest { UserName = "rodas", Password = "nope-nope-nope" });
        var unknown = await _service.SignInAsync(new UserSignInRequest { UserName = "ghost", Password = "a-long-password" });

        Assert.True(ok.IsSuccess);
        Assert.NotNull(ok.Result!.LastSignInAt);
        Assert.Equal(wrongPassword.Problems!.Title, unknown.Problems!.Title);
    }

    [Fact]
    public async Task ChangePassword_NeedsTheCurrentOne_AndChangesTheStamp()
    {
        var user = (await Create("rodas")).Result!;

        var wrong = await _service.ChangePasswordAsync(new UserPasswordRequest { Id = user.Id, CurrentPassword = "nope-nope", NewPassword = "another-password" });
        Assert.Equal(["CurrentPassword"], wrong.Problems!.Errors.Select(static error => error.Field));

        var changed = (await _service.ChangePasswordAsync(new UserPasswordRequest { Id = user.Id, CurrentPassword = "a-long-password", NewPassword = "another-password" })).Result!;
        Assert.NotEqual(user.SecurityStamp, changed.SecurityStamp);
        Assert.True((await _service.SignInAsync(new UserSignInRequest { UserName = "rodas", Password = "another-password" })).IsSuccess);
    }

    [Fact]
    public async Task Delete_KeepsTheLastAdmin()
    {
        var admin = (await Create("rodas")).Result!;
        var guest = (await Create("guest")).Result!;

        Assert.Equal(ServiceApiResponseStatus.ValidationProblems, (await _service.DeleteAsync(admin.Id)).Status);
        Assert.True((await _service.DeleteAsync(guest.Id)).IsSuccess);
        Assert.Equal(ServiceApiResponseStatus.NotFound, (await _service.DeleteAsync(guest.Id)).Status);
    }

    [Fact]
    public void Hash_IsSaltedAndVerifies()
    {
        var a = PasswordHasher.Hash("secret-password");
        var b = PasswordHasher.Hash("secret-password");

        Assert.NotEqual(a, b);
        Assert.True(PasswordHasher.Verify("secret-password", a));
        Assert.False(PasswordHasher.Verify("Secret-password", a));
        Assert.False(PasswordHasher.Verify("secret-password", "garbage"));
    }
}
