using Artemis.Core.Interfaces;
using System.Security.Cryptography;
namespace Artemis.Core.Cryptography;

public class EncryptionService : IEncryptionService
{
    #region Private Fields
    private readonly IKeyDerivationService _keyDerivationService;
    #endregion

    #region Private Constants
    private const int Offset = 0;
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
        var randomSalt = new byte[CryptoConstants.SaltSize];
        RandomNumberGenerator.Fill(randomSalt);

        // Derive the AES key from password + salt
        var key = await _keyDerivationService.DeriveKeyAsync(password, randomSalt);
        // Generate a random IV 
        var randomIV = new byte[CryptoConstants.IvSize];
        RandomNumberGenerator.Fill(randomIV);

        // AES-GCM Encryption
        var ciphertext = new byte[plaintext.Length];
        var authenticationTag = new byte[CryptoConstants.TagSize];

        try
        {
            using (var aes = new AesGcm(key, CryptoConstants.TagSize))
            {
                aes.Encrypt(randomIV, plaintext, ciphertext, authenticationTag);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }


        // Assemble the output
        var result = new byte[CryptoConstants.SaltSize + CryptoConstants.IvSize + ciphertext.Length + CryptoConstants.TagSize];

        Buffer.BlockCopy(randomSalt, Offset, result, Offset, randomSalt.Length);
        Buffer.BlockCopy(randomIV, Offset, result, randomSalt.Length, randomIV.Length);
        Buffer.BlockCopy(ciphertext, Offset, result, randomSalt.Length + randomIV.Length, ciphertext.Length);
        Buffer.BlockCopy(authenticationTag, Offset, result, randomSalt.Length + randomIV.Length + ciphertext.Length, authenticationTag.Length);


        return result;
    }

    public async Task<byte[]> DecryptAsync(byte[] encryptedData, string password)
    {
        // Validate the minimum size of the encrypted data
        var minimumSize = CryptoConstants.SaltSize + CryptoConstants.IvSize + CryptoConstants.TagSize;

        if (encryptedData.Length < minimumSize)
            throw new ArgumentException("Invalid encrypted data: input is too short!");

        // Extract the Salt
        var extractedSalt = new byte[CryptoConstants.SaltSize];
        Buffer.BlockCopy(encryptedData, Offset, extractedSalt, Offset, extractedSalt.Length);

        // Extract the IV (nonce)
        var extractedIV = new byte[CryptoConstants.IvSize];
        Buffer.BlockCopy(encryptedData, extractedSalt.Length, extractedIV, Offset, extractedIV.Length);

        // Extract the ciphertext
        var ciphertextLength = encryptedData.Length - CryptoConstants.SaltSize - CryptoConstants.IvSize - CryptoConstants.TagSize;
        var extractedCiphertext = new byte[ciphertextLength];
        Buffer.BlockCopy(encryptedData, extractedSalt.Length + extractedIV.Length, extractedCiphertext, Offset, ciphertextLength);

        // Extract the Authentication Tag
        var extractedAuthTag = new byte[CryptoConstants.TagSize];
        Buffer.BlockCopy(encryptedData, encryptedData.Length - extractedAuthTag.Length, extractedAuthTag, Offset, extractedAuthTag.Length);

        // Derive the key
        var key = await _keyDerivationService.DeriveKeyAsync(password, extractedSalt);

        // Decrypt the data with AES-GCM
        var plaintext = new byte[ciphertextLength];

        try
        {
            using (var aes = new AesGcm(key, extractedAuthTag.Length))
            {
                aes.Decrypt(extractedIV, extractedCiphertext, extractedAuthTag, plaintext);
            }
        }
        catch (CryptographicException)
        {

            throw new CryptographicException("Decryption failed. Wrong password or corrupted/tampered data!");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        return plaintext;
    }
    #endregion
}