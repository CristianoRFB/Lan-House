using System.Security.Cryptography;
using System.Text;
using System.IO;
using Adrenalina.Application;

namespace Adrenalina.Client;

public sealed class ClientCredentialStore
{
    private readonly string _path;

    public ClientCredentialStore(string? path = null)
    {
        _path = path ?? Path.Combine(AdrenalinaPaths.GetClientSettingsRoot(), "machine-credential.dpapi");
    }

    public string? Load(string credentialId)
    {
        if (string.IsNullOrWhiteSpace(credentialId) || !File.Exists(_path))
        {
            return null;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(_path);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, Encoding.UTF8.GetBytes(credentialId), DataProtectionScope.CurrentUser);
            var secret = Encoding.UTF8.GetString(plainBytes);
            return secret.Length is >= 16 and <= 200 ? secret : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Save(string credentialId, string secret)
    {
        if (string.IsNullOrWhiteSpace(credentialId) || string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Credencial inválida.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(credentialId), DataProtectionScope.CurrentUser);
        var temporaryPath = _path + ".tmp";
        File.WriteAllBytes(temporaryPath, protectedBytes);
        File.Move(temporaryPath, _path, overwrite: true);
    }

    public void Revoke()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
