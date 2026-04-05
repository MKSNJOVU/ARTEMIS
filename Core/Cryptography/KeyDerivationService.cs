using Artemis.Core.Interfaces;
using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace Artemis.Core.Cryptography;

public class KeyDerivationService : IKeyDerivationService
{
    #region Methods
    public async Task<byte[]> DeriveKeyAsync(string password, byte[] salt)
    {

        byte[]? passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {

            return await Task.Run(() =>
            {

                using var argon2 = new Argon2id(passwordBytes);
                {
                    argon2.Salt = salt;
                    argon2.DegreeOfParallelism = CryptoConstants.Parallelism;
                    argon2.MemorySize = CryptoConstants.MemorySize;
                    argon2.Iterations = CryptoConstants.Iterations;
                }
                ;

                return argon2.GetBytes(CryptoConstants.KeySizeBytes);
            });
        }
        finally
        {
            // Zero out the UTF-8 byte array from RAM immediately.
            // Guaranteed to wipe the memory even if the background thread crashes or is cancelled.
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    #endregion
}