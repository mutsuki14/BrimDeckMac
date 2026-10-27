using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BrimDeck.Core;

namespace BrimDeck.Mac.Platform;

// API keys stay on this Mac. The encryption key is a random 32 bytes kept in the login keychain;
// the ciphertext is secrets.dat. Where the keychain cannot be used, the key file is mode 0600.
public sealed class SecretStore : IProviderSecrets
{
    private readonly string _directory;
    private readonly object _gate = new();
    private readonly bool _preferKeychain;

    public SecretStore(string directory, bool? preferKeychain = null)
    {
        _directory = directory;
        _preferKeychain = preferKeychain ?? OperatingSystem.IsMacOS();
    }

    private string DataPath => Path.Combine(_directory, "secrets.dat");
    private string KeyPath => Path.Combine(_directory, "secrets.key");

    public string Read(Guid instanceId)
    {
        lock (_gate)
        {
            var values = Load();
            return values.TryGetValue(instanceId, out var secret) ? secret : "";
        }
    }

    public void Write(Guid instanceId, string secret)
    {
        lock (_gate)
        {
            var values = Load();
            if (secret.Length == 0) values.Remove(instanceId);
            else values[instanceId] = secret;
            Save(values);
        }
    }

    public void Delete(Guid instanceId) => Write(instanceId, "");

    private Dictionary<Guid, string> Load()
    {
        if (!File.Exists(DataPath)) return [];
        var payload = File.ReadAllBytes(DataPath);
        if (payload.Length < 1 + 12 + 16 || payload[0] != 1) return [];
        var key = LoadKey();
        var nonce = payload.AsSpan(1, 12);
        var tag = payload.AsSpan(13, 16);
        var cipher = payload.AsSpan(29);
        var plain = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
            return JsonSerializer.Deserialize<Dictionary<Guid, string>>(plain) ?? [];
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException) { return []; }
        finally { CryptographicOperations.ZeroMemory(plain); CryptographicOperations.ZeroMemory(key); }
    }

    private void Save(Dictionary<Guid, string> values)
    {
        Directory.CreateDirectory(_directory);
        var key = LoadKey(create: true);
        var plain = JsonSerializer.SerializeToUtf8Bytes(values);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plain, cipher, tag);
            var payload = new byte[1 + nonce.Length + tag.Length + cipher.Length];
            payload[0] = 1;
            nonce.CopyTo(payload.AsSpan(1));
            tag.CopyTo(payload.AsSpan(13));
            cipher.CopyTo(payload.AsSpan(29));
            var temp = DataPath + ".tmp";
            File.WriteAllBytes(temp, payload);
            File.Move(temp, DataPath, true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private byte[] LoadKey(bool create = false)
    {
        if (_preferKeychain && MacKeychain.Read() is { Length: 32 } stored) return stored;
        if (File.Exists(KeyPath))
        {
            var file = File.ReadAllBytes(KeyPath);
            if (file.Length == 32) return file;
        }
        if (!create) return new byte[32];
        var key = RandomNumberGenerator.GetBytes(32);
        if (_preferKeychain && MacKeychain.Write(key)) return key;
        Directory.CreateDirectory(_directory);
        File.WriteAllBytes(KeyPath, key);
        TryRestrict(KeyPath);
        return key;
    }

    private static void TryRestrict(string path)
    {
        try { if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException) { }
    }
}

static class MacKeychain
{
    private const string Service = "BrimDeck";
    private const string Account = "secrets-key";

    public static byte[]? Read()
    {
        if (!OperatingSystem.IsMacOS()) return null;
        try
        {
            int status = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)Service.Length, Service, (uint)Account.Length, Account, out var length, out var data, out var item);
            if (status != 0 || data == IntPtr.Zero || length != 32) return null;
            try
            {
                var key = new byte[32];
                System.Runtime.InteropServices.Marshal.Copy(data, key, 0, 32);
                return key;
            }
            finally { SecKeychainItemFreeContent(IntPtr.Zero, data); if (item != IntPtr.Zero) System.Runtime.InteropServices.Marshal.Release(item); }
        }
        catch (DllNotFoundException) { return null; }
    }

    public static bool Write(byte[] key)
    {
        if (!OperatingSystem.IsMacOS() || key.Length != 32) return false;
        try
        {
            if (SecKeychainFindGenericPassword(IntPtr.Zero, (uint)Service.Length, Service, (uint)Account.Length, Account, out _, out var data, out var item) == 0)
            {
                if (data != IntPtr.Zero) SecKeychainItemFreeContent(IntPtr.Zero, data);
                if (item != IntPtr.Zero) { SecKeychainItemDelete(item); System.Runtime.InteropServices.Marshal.Release(item); }
            }
            return SecKeychainAddGenericPassword(IntPtr.Zero, (uint)Service.Length, Service, (uint)Account.Length, Account, (uint)key.Length, key, out _) == 0;
        }
        catch (DllNotFoundException) { return false; }
    }

    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainFindGenericPassword(IntPtr keychain, uint serviceLength, string service, uint accountLength, string account, out uint passwordLength, out IntPtr password, out IntPtr item);

    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainAddGenericPassword(IntPtr keychain, uint serviceLength, string service, uint accountLength, string account, uint passwordLength, byte[] password, out IntPtr item);

    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemFreeContent(IntPtr attributeList, IntPtr data);

    [System.Runtime.InteropServices.DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemDelete(IntPtr item);
}
