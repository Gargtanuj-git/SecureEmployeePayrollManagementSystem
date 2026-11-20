using System;
using System.Security.Cryptography;
using System.Text;

namespace SecureEmployeePayrollSystem.Services;

public static class SecurityTokenHelper
{
    public static string GenerateSecureToken(int byteLength = 32)
    {
        byte[] bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    public static string HashToken(string rawToken)
    {
        if (string.IsNullOrEmpty(rawToken)) return string.Empty;
        byte[] inputBytes = Encoding.UTF8.GetBytes(rawToken);
        byte[] hash = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool VerifyToken(string rawToken, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        string computedHash = HashToken(rawToken);
        byte[] computedBytes = Encoding.UTF8.GetBytes(computedHash);
        byte[] storedBytes = Encoding.UTF8.GetBytes(storedHash.ToLowerInvariant());

        if (computedBytes.Length != storedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }
}
