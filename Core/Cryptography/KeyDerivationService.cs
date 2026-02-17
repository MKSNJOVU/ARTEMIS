using System.Text;
using Artemis.Core.Interfaces;
using Artemis.Core.Cryptography;
using Konscious.Security.Cryptography;

namespace Artemis.Core.Cryptography;

public class KeyDerivationService : IKeyDerivationService
{
    #region Methods
    public async Task<byte[]> DeriveKey(string password, byte[] salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        return await Task.Run(() =>
        {
            using var argon2 = new Argon2id(passwordBytes);

            argon2.Salt = salt;
            argon2.DegreeOfParallelism = CryptoConstants.PARALLELISM;
            argon2.MemorySize = CryptoConstants.MEMORYSIZE;
            argon2.Iterations = CryptoConstants.ITERATIONS;

            return argon2.GetBytes(CryptoConstants.KEYSIZEBYTES);
        });
    }
    #endregion
}