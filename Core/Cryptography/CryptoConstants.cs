
namespace Artemis.Core.Cryptography;

public static class CryptoConstants
{
    #region AES-GCM Constants
    const int KeySizeBytes = 32;
    const int SaltSize = 16;
    const int IVSize = 12;
    const int TagSize = 16;
    #endregion

    #region Argon2id Constants
    const int Iterations = 3;
    const int MemorySize = 65536;
    const int Parallelism = 1;
    #endregion
}