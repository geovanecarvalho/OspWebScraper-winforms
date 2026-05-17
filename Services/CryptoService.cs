using System.Security.Cryptography;
using System.Text;

namespace OSPVivoScraper.Services;

public static class CryptoService
{
    /// <summary>
    /// Criptografa usando DPAPI (somente Windows - mesma conta de usuário)
    /// </summary>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;
        
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        
        return Convert.ToBase64String(encryptedBytes);
    }
    
    /// <summary>
    /// Descriptografa usando DPAPI
    /// </summary>
    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return string.Empty;
        
        try
        {
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            byte[] decryptedBytes = ProtectedData.Unprotect(cipherBytes, null, DataProtectionScope.CurrentUser);
            
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch
        {
            return string.Empty;
        }
    }
}