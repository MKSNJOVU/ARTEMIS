using System.Text;
using Artemis.Core.Interfaces;
using Konscious.Security.Cryptography;
using System.Security.Cryptography;

namespace Artemis.Core.Cryptography;

public class KeyDerivationService : IKeyDerivationService
{
    #region Methods
    public async Task<byte[]> DeriveKeyAsync(char[] password, byte[] salt)
    {
        var passwordSnapshot = new char[password.Length];
        Array.Copy(password, passwordSnapshot, password.Length);

        byte[]? passwordBytes = null;
        try
        {
            var passwordByteCount = Encoding.UTF8.GetByteCount(passwordSnapshot);
            passwordBytes = new byte[passwordByteCount];
            Encoding.UTF8.GetBytes(passwordSnapshot, 0, passwordSnapshot.Length, passwordBytes, 0);

            return await Task.Run(() =>
            {
                try
                {
                    using var argon2 = new Argon2id(passwordBytes);

                    argon2.Salt = salt;
                    argon2.DegreeOfParallelism = CryptoConstants.Parallelism;
                    argon2.MemorySize = CryptoConstants.MemorySize;
                    argon2.Iterations = CryptoConstants.Iterations;

                    return argon2.GetBytes(CryptoConstants.KeySizeBytes);
                }
                finally
                {
                    // EXCELLENCE: Clear the sensitive byte array as soon as Argon2 is done with it, 
                    // even if the derivation itself is still running or fails.
                    if (passwordBytes is not null) CryptographicOperations.ZeroMemory(passwordBytes);
                }
            });
        }
        finally
        {
            // Guaranteed cleanup for the character snapshot
            Array.Clear(passwordSnapshot, 0, passwordSnapshot.Length);

            // Safety: Clear the byte array if it hasn't been cleared inside the Task (e.g., if Task.Run failed to start)
            if (passwordBytes is not null) CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }
    #endregion
}