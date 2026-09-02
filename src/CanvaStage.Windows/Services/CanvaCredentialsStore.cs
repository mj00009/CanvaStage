using System.Security.Cryptography;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CanvaStage.Windows.Services;

public sealed class CanvaCredentialsStore
{
    private readonly string _credentialsPath;

    public CanvaCredentialsStore()
    {
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CanvaStage");
        _credentialsPath = Path.Combine(directory, "canva.credentials.protected");
    }

    public CanvaCredentials? Load()
    {
        try
        {
            if (!File.Exists(_credentialsPath))
            {
                return null;
            }

            byte[] encrypted = Convert.FromBase64String(File.ReadAllText(_credentialsPath));
            byte[] plaintext = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<CanvaCredentials>(plaintext);
        }
        catch (Exception)
        {
            PrivacySafeLog.Write("Protected Canva credentials could not be loaded.");
            return null;
        }
    }

    public void Save(CanvaCredentials credentials)
    {
        string? directory = Path.GetDirectoryName(_credentialsPath);
        if (directory is null)
        {
            throw new InvalidOperationException("Unable to determine the local credentials directory.");
        }

        Directory.CreateDirectory(directory);
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(credentials);
        byte[] encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
        string temporaryPath = _credentialsPath + ".tmp";
        File.WriteAllText(temporaryPath, Convert.ToBase64String(encrypted), Encoding.UTF8);
        File.Move(temporaryPath, _credentialsPath, true);
    }

    public void Clear()
    {
        if (File.Exists(_credentialsPath))
        {
            File.Delete(_credentialsPath);
        }
    }
}

public sealed record CanvaCredentials(
    string ClientId,
    string ClientSecret,
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset? AccessTokenExpiresAt);
