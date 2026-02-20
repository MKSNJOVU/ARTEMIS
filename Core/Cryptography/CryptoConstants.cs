namespace Artemis.Core.Cryptography;

public static class CryptoConstants
{
    #region AES-GCM Constants
    public const int KeySizeBytes = 32;
    public const int SaltSize = 16;
    public const int IvSize = 12;
    public const int TagSize = 16;
    #endregion

    #region Argon2id Constants
    public const int Iterations = 3;
    public const int MemorySize = 65536;
    public const int Parallelism = 1;
    #endregion
}