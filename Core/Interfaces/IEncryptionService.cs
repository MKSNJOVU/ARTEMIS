namespace Artemis.Core.Interfaces;

public interface IEncryptionService
{
    /// <summary>
    /// Uses AES-256-GCM with Argon2id derived key to encrypt plaintext. 
    /// </summary>
    /// <param name="plaintext"></param>
    /// <param name="password"></param>
    /// <returns>byte[]</returns>
    Task EncryptAsync(Stream sourceFile, Stream destinationFile, char[] password);

    /// <summary>
    /// Decrypts data. Throws if password is wrong or data is corrupted.
    /// </summary>
    /// <param name="encryptedData"></param>
    /// <param name="password"></param>
    /// <returns>original plaintext bytes</returns>
    Task DecryptAsync(Stream sourceFile, Stream destinationFile, char[] password);
}