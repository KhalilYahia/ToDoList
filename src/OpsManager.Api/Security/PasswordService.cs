using Microsoft.AspNetCore.Identity;
using OpsManager.Service.Abstractions;

namespace OpsManager.Api.Security;

public sealed class PasswordService : IPasswordService
{
    private readonly PasswordHasher<object> _hasher = new();
    private readonly object _marker = new();

    public string HashPassword(string password) => _hasher.HashPassword(_marker, password);

    public bool VerifyPassword(string passwordHash, string providedPassword) =>
        _hasher.VerifyHashedPassword(_marker, passwordHash, providedPassword)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
