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

        using (var aes = new AesGcm(key, CryptoConstants.TAGSIZE))
        {
            aes.Encrypt(randomIV, plaintext, ciphertext, authenticationTag);
        }


        // Assemble the output
        int offset = 0;

        var result = new byte[CryptoConstants.SALTSIZE + CryptoConstants.IVSIZE + ciphertext.Length + CryptoConstants.TAGSIZE];
        var minimumSize = CryptoConstants.SALTSIZE + CryptoConstants.IVSIZE + CryptoConstants.TAGSIZE;

        Buffer.BlockCopy(randomSalt, offset, result, offset, randomSalt.Length);
        Buffer.BlockCopy(randomIV, offset, result, randomSalt.Length, randomIV.Length);
        Buffer.BlockCopy(ciphertext, offset, result, randomSalt.Length + randomIV.Length, ciphertext.Length);
        Buffer.BlockCopy(authenticationTag, offset, result, randomSalt.Length + randomIV.Length + ciphertext.Length, authenticationTag.Length);


        return result;
    }

    public async Task<byte[]> DecryptAsync(byte[] encryptedData, string password)
    {
        throw new NotImplementedException();

        /* TODO Decrypt and implement

         var minimumSize = CryptoConstants.SALTSIZE + CryptoConstants.IVSIZE + CryptoConstants.TAGSIZE;

  if (encryptedData.Length < minimumSize)
      throw new ArgumentException("Invalid encrypted data: too short."); */

    }
    #endregion
}