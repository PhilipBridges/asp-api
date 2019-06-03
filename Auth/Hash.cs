using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using System;
using System.Security.Cryptography;
using System.Text;

public class Hash
{
    public static string Create(string plainTextPassword)
    {
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(plainTextPassword);

        return hashedPassword;
    }

    public static bool Validate(string userSubmittedPassword, string hashedPassword)
    {
        bool validPassword = BCrypt.Net.BCrypt.Verify(userSubmittedPassword, hashedPassword);

        return validPassword;
    }
        
}

public class Salt
{
    public static string Create()
    {
        byte[] randomBytes = new byte[128 / 8];
        using (var generator = RandomNumberGenerator.Create())
        {
            generator.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
    }
}

