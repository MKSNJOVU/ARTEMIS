namespace Artemis.Core.Interfaces;

public interface IEncryptionService
{
    /// <summary>
    /// Uses AES-256-GCM with Argon2id derived key to encrypt plaintext. 
    /// </summary>
    /// <param name="plaintext"></param>
    /// <param name="password"></param>
    /// <returns>byte[]</returns>
    Task<byte[]> EncryptAsync(byte[] plaintext, char[] password);

    /// <summary>
    /// Decrypts data. Throws if password is wrong or data is corrupted.
    /// </summary>
    /// <param name="encryptedData"></param>
    /// <param name="password"></param>
    /// <returns>original plaintext bytes</returns>
    Task<byte[]> DecryptAsync(byte[] encryptedData, char[] password);
}