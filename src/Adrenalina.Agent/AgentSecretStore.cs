using System.Security.Cryptography;
using System.Text;

namespace Adrenalina.Agent;

internal static class AgentSecretStore
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Adrenalina", "Agent", "machine-secret.dpapi");

    public static void Save(string secret)
    {
        if (secret.Length is < 16 or > 200)
        {
            throw new ArgumentException("Segredo da estação inválido.");
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), null, DataProtectionScope.LocalMachine);
        File.WriteAllBytes(Path, encrypted);
    }

    public static void ProvisionFromClientFile(string clientCredentialPath, string credentialId)
    {
        if (string.IsNullOrWhiteSpace(clientCredentialPath) || string.IsNullOrWhiteSpace(credentialId))
        {
            throw new ArgumentException("Arquivo e identificador da credencial do Client são obrigatórios.");
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(clientCredentialPath);
            var plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                Encoding.UTF8.GetBytes(credentialId),
                DataProtectionScope.CurrentUser);
            Save(Encoding.UTF8.GetString(plainBytes));
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("Não foi possível abrir a credencial DPAPI do Client com o usuário atual.", exception);
        }
    }

    public static string? Load()
    {
        if (!File.Exists(Path))
        {
            return null;
        }

        try
        {
            var plain = ProtectedData.Unprotect(File.ReadAllBytes(Path), null, DataProtectionScope.LocalMachine);
            var secret = Encoding.UTF8.GetString(plain);
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
}
