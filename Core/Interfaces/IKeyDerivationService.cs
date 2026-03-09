namespace Artemis.Core.Interfaces;

public interface IKeyDerivationService
{
    /// <summary>
    /// Derives a 256-bit AES key from a password using Argon2id.
    /// </summary>
    /// <param name="password">User's master password</param>
    /// <param name="salt">Random salt (16 bytes)</param>
    /// <returns>32-byte derived key</returns>
    Task<byte[]> DeriveKeyAsync(char[] password, byte[] salt);
}