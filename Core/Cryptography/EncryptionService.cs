using Artemis.Core.Interfaces;
using System.Security.Cryptography;
namespace Artemis.Core.Cryptography;

public class EncryptionService : IEncryptionService
{
    #region Private Fields
    private readonly IKeyDerivationService _keyDerivationService;
    #endregion

    #region Constructor
    public EncryptionService(IKeyDerivationService keyDerivationService)
    {
        _keyDerivationService = keyDerivationService;
    }
    #endregion

    #region Methods
    public async Task<byte[]> EncryptAsync(byte[] plaintext, string password)
    {
        // Generate the randomSalt
        var randomSalt = new byte[CryptoConstants.SALTSIZE];
        RandomNumberGenerator.Fill(randomSalt);

        // Derive the AES key from password + salt
        var key = await _keyDerivationService.DeriveKeyAsync(password, randomSalt);
        // Generate a random IV 
        var randomIV = new byte[CryptoConstants.IVSIZE];
        RandomNumberGenerator.Fill(randomIV);

        // AES-GCM Encryption
        var ciphertext = new byte[plaintext.Length];
        var authenticationTag = new byte[CryptoConstants.TAGSIZE];

        using var aes = new AesGcm(key);

        /* TODO encrypt with aes.Encrypt() and assemble the output */

        return null;
    }

    public async Task<byte[]> DecryptAsync(byte[] encryptedData, string password)
    {
        throw new NotImplementedException();
    }
    #endregion
}