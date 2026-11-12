using System.Text.RegularExpressions;
using TcgStockBot.Library.Enums;
using TcgStockBot.Library.Services;
using TcgStockBot.Library.Validation;
using TcgStockBot.WebApi.Extensions.Globalization;
using TcgStockBot.WebApi.Features.Users.Models.DTOs;
using TcgStockBot.WebApi.Features.Users.Models.Entities;
using TcgStockBot.WebApi.Features.Users.Repositories;

namespace TcgStockBot.WebApi.Features.Users.Services;

/// <summary>
/// The dashboard accounts: create, sign in, change password, delete. Passwords are PBKDF2 hashes.
/// </summary>
/// <param name="userRepository">users.json.</param>
/// <param name="globalizationHelper">Messages.</param>
/// <param name="timeProvider">The clock.</param>
internal sealed partial class UserService(
    IUserRepository userRepository,
    IGlobalizationHelper globalizationHelper,
    TimeProvider timeProvider) : IUserService
{
    /// <summary>Shortest password accepted.</summary>
    internal const int MinPasswordLength = 8;

    private const int MaxPasswordLength = 200;
    private const string FieldUserName = "UserName";
    private const string FieldPassword = "Password";
    private const string FieldCurrentPassword = "CurrentPassword";
    private const string FieldNewPassword = "NewPassword";
    private const string FieldIdentifier = "Identifier";
    private const string FieldDiscordId = "DiscordId";

    // Spends the same time on an unknown user name as on a wrong password.
    private static readonly string DummyHash = PasswordHasher.Hash("not-a-real-password");

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserStatusDTO>> StatusAsync() =>
        ServiceApiResponse<UserStatusDTO>.Success(new UserStatusDTO { UserCount = (await _userRepository.ListAsync()).Count });

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<UserDTO>>> ListAsync() =>
        ServiceApiResponse<List<UserDTO>>.Success((await _userRepository.ListAsync())
            .OrderBy(static user => user.UserName, StringComparer.OrdinalIgnoreCase).Select(ToDTO).ToList());

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> GetAsync(string identifier)
    {
        var user = (await _userRepository.ListAsync()).FirstOrDefault(user => user.Id == (identifier ?? string.Empty).Trim());
        return user is null
            ? ServiceApiResponse<UserDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), identifier ?? string.Empty))
            : ServiceApiResponse<UserDTO>.Success(ToDTO(user));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> CreateAsync(UserCreateRequest item)
    {
        var userName = (item.UserName ?? string.Empty).Trim();
        var password = item.Password ?? string.Empty;

        await _lock.WaitAsync();
        try
        {
            var users = await _userRepository.ListAsync();
            var validationErrors = new List<ApiValidationError>();

            if (!UserNamePattern().IsMatch(userName))
            {
                validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldUserName, nameof(TranslationKeys.UserNameInvalid)));
            }
            else if (users.Any(user => user.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase)))
            {
                validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldUserName, nameof(TranslationKeys.AlreadyExists), userName));
            }

            validationErrors.AddRange(await ValidatePasswordAsync(FieldPassword, password));
            if (validationErrors.Count > 0)
            {
                return (await ValidationProblemsAsync<UserDTO>()).AddErrorRange(validationErrors);
            }

            var user = new User
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = userName,
                PasswordHash = PasswordHasher.Hash(password),
                Role = users.Count == 0 ? UserRole.Admin : UserRole.User,
                CreatedAt = _timeProvider.GetUtcNow(),
                SecurityStamp = NewStamp(),
            };
            users.Add(user);
            await _userRepository.SaveAsync(users);
            return ServiceApiResponse<UserDTO>.Success(ToDTO(user));
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> SignInAsync(UserSignInRequest item)
    {
        var userName = (item.UserName ?? string.Empty).Trim();
        var users = await _userRepository.ListAsync();
        var user = users.FirstOrDefault(user => user.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));

        var valid = PasswordHasher.Verify(item.Password ?? string.Empty, user?.PasswordHash ?? DummyHash) && user is not null;
        if (!valid)
        {
            var message = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.WrongCredentials));
            return ServiceApiResponse<UserDTO>.ValidationProblems(message).AddError(FieldPassword, message);
        }

        await _lock.WaitAsync();
        try
        {
            users = await _userRepository.ListAsync();
            var saved = users.FirstOrDefault(other => other.Id == user!.Id);
            if (saved is not null)
            {
                saved.LastSignInAt = _timeProvider.GetUtcNow();
                await _userRepository.SaveAsync(users);
                user = saved;
            }
        }
        finally
        {
            _lock.Release();
        }

        return ServiceApiResponse<UserDTO>.Success(ToDTO(user!));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> DiscordSignInAsync(UserDiscordSignInRequest item)
    {
        var discordId = (item.DiscordId ?? string.Empty).Trim();
        if (!UserRules.IsDiscordId(discordId))
        {
            return ServiceApiResponse<UserDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.DiscordNotLinked)));
        }

        await _lock.WaitAsync();
        try
        {
            var users = await _userRepository.ListAsync();
            var user = users.FirstOrDefault(user => user.DiscordId == discordId);
            if (user is null)
            {
                return ServiceApiResponse<UserDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.DiscordNotLinked)));
            }

            user.LastSignInAt = _timeProvider.GetUtcNow();
            await _userRepository.SaveAsync(users);
            return ServiceApiResponse<UserDTO>.Success(ToDTO(user));
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> DiscordCreateAsync(UserDiscordCreateRequest item)
    {
        var userName = (item.UserName ?? string.Empty).Trim();
        var discordId = (item.DiscordId ?? string.Empty).Trim();

        await _lock.WaitAsync();
        try
        {
            var users = await _userRepository.ListAsync();
            var validationErrors = new List<ApiValidationError>();
            if (!UserNamePattern().IsMatch(userName))
            {
                validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldUserName, nameof(TranslationKeys.UserNameInvalid)));
            }
            else if (users.Any(user => user.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase)))
            {
                validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldUserName, nameof(TranslationKeys.AlreadyExists), userName));
            }

            if (!UserRules.IsDiscordId(discordId))
            {
                validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldDiscordId, nameof(TranslationKeys.DiscordInvalid)));
            }

            if (validationErrors.Count > 0)
            {
                return (await ValidationProblemsAsync<UserDTO>()).AddErrorRange(validationErrors);
            }

            if (users.Any(user => user.DiscordId == discordId))
            {
                return await DiscordTakenAsync();
            }

            // No password: the empty hash never verifies, so only Discord opens the account until one is set.
            var now = _timeProvider.GetUtcNow();
            var created = new User
            {
                Id = Guid.NewGuid().ToString("N"),
                UserName = userName,
                PasswordHash = string.Empty,
                Role = users.Count == 0 ? UserRole.Admin : UserRole.User,
                CreatedAt = now,
                LastSignInAt = now,
                SecurityStamp = NewStamp(),
                DiscordId = discordId,
                DiscordName = UserRules.DiscordName(item.DiscordName),
            };
            users.Add(created);
            await _userRepository.SaveAsync(users);
            return ServiceApiResponse<UserDTO>.Success(ToDTO(created));
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> LinkDiscordAsync(UserDiscordLinkRequest item)
    {
        var discordId = string.IsNullOrWhiteSpace(item.DiscordId) ? null : item.DiscordId.Trim();
        if (discordId is not null && !UserRules.IsDiscordId(discordId))
        {
            var error = await _globalizationHelper.ValidationErrorAsync(FieldDiscordId, nameof(TranslationKeys.DiscordInvalid));
            return ServiceApiResponse<UserDTO>.ValidationProblems(error.Message).AddError(FieldDiscordId, error.Message);
        }

        await _lock.WaitAsync();
        try
        {
            var users = await _userRepository.ListAsync();
            var user = users.FirstOrDefault(user => user.Id == (item.Id ?? string.Empty).Trim());
            if (user is null)
            {
                return ServiceApiResponse<UserDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), item.Id ?? string.Empty));
            }

            if (discordId is null && string.IsNullOrEmpty(user.PasswordHash))
            {
                var message = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.DiscordNeedsPassword));
                return ServiceApiResponse<UserDTO>.ValidationProblems(message).AddError(FieldDiscordId, message);
            }

            if (discordId is not null && users.Any(other => other.DiscordId == discordId && other.Id != user.Id))
            {
                return await DiscordTakenAsync();
            }

            user.DiscordId = discordId;
            user.DiscordName = discordId is null ? null : UserRules.DiscordName(item.DiscordName);
            await _userRepository.SaveAsync(users);
            return ServiceApiResponse<UserDTO>.Success(ToDTO(user));
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<UserDTO>> ChangePasswordAsync(UserPasswordRequest item)
    {
        await _lock.WaitAsync();
        try
        {
            var users = await _userRepository.ListAsync();
            var user = users.FirstOrDefault(user => user.Id == (item.Id ?? string.Empty).Trim());
            if (user is null)
            {
                return ServiceApiResponse<UserDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), item.Id ?? string.Empty));
            }

            var validationErrors = new List<ApiValidationError>();
            // An account made with Discord has no password yet: its first one needs no current password.
            if (!string.IsNullOrEmpty(user.PasswordHash) && !PasswordHasher.Verify(item.CurrentPassword ?? string.Empty, user.PasswordHash))
            {
                validationErrors.Add(await _globalizationHelper.ValidationErrorAsync(FieldCurrentPassword, nameof(TranslationKeys.PasswordWrong)));
            }

            validationErrors.AddRange(await ValidatePasswordAsync(FieldNewPassword, item.NewPassword ?? string.Empty));
            if (validationErrors.Count > 0)
            {
                return (await ValidationProblemsAsync<UserDTO>()).AddErrorRange(validationErrors);
            }

            user.PasswordHash = PasswordHasher.Hash(item.NewPassword!);
            user.SecurityStamp = NewStamp();
            await _userRepository.SaveAsync(users);
            return ServiceApiResponse<UserDTO>.Success(ToDTO(user));
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DeleteAsync(string identifier)
    {
        await _lock.WaitAsync();
        try
        {
            var users = await _userRepository.ListAsync();
            var user = users.FirstOrDefault(user => user.Id == (identifier ?? string.Empty).Trim());
            if (user is null)
            {
                return ServiceApiResponse<string>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.NoRecordsFound), identifier ?? string.Empty));
            }

            if (user.Role == UserRole.Admin && users.Count(static other => other.Role == UserRole.Admin) == 1)
            {
                var message = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.LastAdmin));
                return ServiceApiResponse<string>.ValidationProblems(message).AddError(FieldIdentifier, message);
            }

            users.Remove(user);
            await _userRepository.SaveAsync(users);
            return ServiceApiResponse<string>.Success();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<ApiValidationError>> ValidatePasswordAsync(string field, string password)
    {
        var errors = new List<ApiValidationError>();
        if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
        {
            errors.Add(await _globalizationHelper.ValidationErrorAsync(field, nameof(TranslationKeys.PasswordTooShort), MinPasswordLength));
        }

        return errors;
    }

    private async Task<ServiceApiResponse<UserDTO>> DiscordTakenAsync()
    {
        var message = await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.DiscordTaken));
        return ServiceApiResponse<UserDTO>.ValidationProblems(message).AddError(FieldDiscordId, message);
    }

    private async Task<ServiceApiResponse<T>> ValidationProblemsAsync<T>() =>
        ServiceApiResponse<T>.ValidationProblems(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.ValidationProblems)));

    private static string NewStamp() => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));

    private static UserDTO ToDTO(User user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        Role = user.Role,
        CreatedAt = user.CreatedAt,
        LastSignInAt = user.LastSignInAt,
        SecurityStamp = user.SecurityStamp,
        DiscordLinked = !string.IsNullOrEmpty(user.DiscordId),
        DiscordName = string.IsNullOrEmpty(user.DiscordId) ? null : user.DiscordName,
        HasPassword = !string.IsNullOrEmpty(user.PasswordHash),
    };

    [GeneratedRegex("^[A-Za-z0-9._-]{3,32}$")]
    private static partial Regex UserNamePattern();
}
