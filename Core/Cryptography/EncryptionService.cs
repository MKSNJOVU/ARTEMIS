using Artemis.Core.Interfaces;
using System.Security.Cryptography;
namespace Artemis.Core.Cryptography;

public class EncryptionService : IEncryptionService
{
    #region Private Fields
    private readonly IKeyDerivationService _keyDerivationService;
    #endregion

    #region Private Constants
    const int _offset = 0;
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

        try
        {
            using (var aes = new AesGcm(key, CryptoConstants.TAGSIZE))
            {
                aes.Encrypt(randomIV, plaintext, ciphertext, authenticationTag);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }


        // Assemble the output
        var result = new byte[CryptoConstants.SALTSIZE + CryptoConstants.IVSIZE + ciphertext.Length + CryptoConstants.TAGSIZE];

        Buffer.BlockCopy(randomSalt, _offset, result, _offset, randomSalt.Length);
        Buffer.BlockCopy(randomIV, _offset, result, randomSalt.Length, randomIV.Length);
        Buffer.BlockCopy(ciphertext, _offset, result, randomSalt.Length + randomIV.Length, ciphertext.Length);
        Buffer.BlockCopy(authenticationTag, _offset, result, randomSalt.Length + randomIV.Length + ciphertext.Length, authenticationTag.Length);


        return result;
    }

    public async Task<byte[]> DecryptAsync(byte[] encryptedData, string password)
    {
        // Validate the minimum size of the encrypted data
        var minimumSize = CryptoConstants.SALTSIZE + CryptoConstants.IVSIZE + CryptoConstants.TAGSIZE;

        if (encryptedData.Length < minimumSize)
            throw new ArgumentException("Invalid encrypted data: input is too short!");

        // Extract the Salt
        var extractedSalt = new byte[CryptoConstants.SALTSIZE];
        Buffer.BlockCopy(encryptedData, _offset, extractedSalt, _offset, extractedSalt.Length);

        // Extract the IV (nonce)
        var extractedIV = new byte[CryptoConstants.IVSIZE];
        Buffer.BlockCopy(encryptedData, extractedSalt.Length, extractedIV, _offset, extractedIV.Length);

        // Extract the ciphertext
        var ciphertextLength = encryptedData.Length - CryptoConstants.SALTSIZE - CryptoConstants.IVSIZE - CryptoConstants.TAGSIZE;
        var extractedCiphertext = new byte[ciphertextLength];
        Buffer.BlockCopy(encryptedData, extractedSalt.Length + extractedIV.Length, extractedCiphertext, _offset, ciphertextLength);

        // Extract the Authentication Tag
        var extractedAuthTag = new byte[CryptoConstants.TAGSIZE];
        Buffer.BlockCopy(encryptedData, encryptedData.Length - extractedAuthTag.Length, extractedAuthTag, _offset, extractedAuthTag.Length);

        // Derive the key
        var key = await _keyDerivationService.DeriveKeyAsync(password, extractedSalt);

        // Decrypt the data with AES-GCM
        var plaintext = new byte[ciphertextLength];

        try
        {
            using (var aes = new AesGcm(key, extractedAuthTag.Length))

                aes.Decrypt(extractedIV, extractedCiphertext, extractedAuthTag, plaintext);
        }
        catch (CryptographicException)
        {

            throw new CryptographicException($"Decryption failed. Wrong password or corrupted/tampered data!");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        return plaintext;
    }
    #endregion
}