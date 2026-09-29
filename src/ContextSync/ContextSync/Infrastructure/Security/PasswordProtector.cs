using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace ContextSync.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public static class PasswordProtector
{
    public static string Protect(string plaintext)
    {
        return Convert.ToBase64String(
            ProtectedData.Protect(Encoding.UTF8.GetBytes(plaintext), null, DataProtectionScope.CurrentUser));
    }

    public static string Unprotect(string protectedBase64)
    {
        return Encoding.UTF8.GetString(
            ProtectedData.Unprotect(Convert.FromBase64String(protectedBase64), null, DataProtectionScope.CurrentUser));
    }
}
