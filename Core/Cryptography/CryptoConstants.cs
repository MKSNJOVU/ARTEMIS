
namespace Artemis.Core.Cryptography;

public static class CryptoConstants
{
    #region AES-GCM Constants
    public const int KEYSIZEBYTES = 32;
    public const int SaltSize = 16;
    public const int IVSiIVze = 12;
    public const int TagSize = 16;
    #endregion

    #region Argon2id Constants
    public const int ITERATIONS = 3;
    public const int MEMORYSIZE = 65536;
    public const int PARALLELISM = 1;
    #endregion


}