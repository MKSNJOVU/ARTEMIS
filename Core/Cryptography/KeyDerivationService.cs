using Artemis.Core.Interfaces;
using Konscious.Security.Cryptography;

namespace Artemis.Core.Cryptography;

public class KeyDerivationService : IKeyDerivationService
{
    #region Methods
    public async Task<byte[]> DeriveKeyAsync(byte[] password, byte[] salt)
    {

        return await Task.Run(() =>
        {

            using var argon2 = new Argon2id(password);
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

    #endregion
}