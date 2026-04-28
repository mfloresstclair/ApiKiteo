using System.Security.Cryptography;
using System.Text;

namespace ApiKiteo.API.Infrastructure.Cryptography;

/// <summary>
/// Desencripta una cadena cifrada con AES-CBC + IV prepend (16 bytes).
/// Equivalente exacto de la función Python DecryptString(cipherText, key):
///   raw = base64.b64decode(cipherText)
///   iv  = raw[:16]
///   cipher = raw[16:]
///   aes.decrypt(cipher) con PKCS7 unpad
/// </summary>
public static class AesDecryptor
{
    /// <summary>
    /// Desencripta <paramref name="cipherBase64"/> usando <paramref name="key"/> (UTF-8).
    /// </summary>
    /// <exception cref="CryptographicException">Si la clave o el texto son inválidos.</exception>
    public static string Decrypt(string cipherBase64, string key)
    {
        var raw    = Convert.FromBase64String(cipherBase64);
        var iv     = raw[..16];
        var cipher = raw[16..];
        var keyBytes = Encoding.UTF8.GetBytes(key);

        using var aes = Aes.Create();
        aes.Mode    = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key     = keyBytes;
        aes.IV      = iv;

        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        return Encoding.UTF8.GetString(decrypted);
    }
}
