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
    public async Task EncryptAsync(Stream source, Stream destination, string password)
    {
        // Generate the randomSalt and randomIV
        var randomSalt = new byte[CryptoConstants.SaltSize];
        RandomNumberGenerator.Fill(randomSalt);

        await destination.WriteAsync(randomSalt);

        // Derive the AES key from password + salt
        var key = await _keyDerivationService.DeriveKeyAsync(password, randomSalt);

        try
        {
            using (var aes = new AesGcm(key, CryptoConstants.TagSize))
            {
                //Write the SALT to the Destination Stream
                var buffer = new byte[CryptoConstants.ChunkSizeBytes];

                // Looping through the chunks
                int bytesRead = 0;
                while ((bytesRead = await source.ReadAsync(buffer)) > 0)
                {
                    // Generate a randomIV
                    var randomIV = new byte[CryptoConstants.IvSize];
                    RandomNumberGenerator.Fill(randomIV);

                    // Write the randomIV to the Destination Stream
                    await destination.WriteAsync(randomIV);

                    // AES-GCM Encryption
                    var authenticationTag = new byte[CryptoConstants.TagSize];



                    var plainTextSpan = new ReadOnlySpan<byte>(buffer, 0, bytesRead);
                    var cipherText = new byte[bytesRead];

                    aes.Encrypt(randomIV, plainTextSpan, cipherText, authenticationTag);

                    // Write the authentication tag and ciphertext to the Destination Stream
                    await destination.WriteAsync(cipherText);
                    await destination.WriteAsync(authenticationTag);
                }
            }
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public async Task DecryptAsync(Stream source, Stream destination, string password)
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
        var plainText = new byte[ciphertextLength];

        try
        {
            using (var aes = new AesGcm(key, extractedAuthTag.Length))
            {
                aes.Decrypt(extractedIV, extractedCiphertext, extractedAuthTag, plainText);
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

        return plainText;
    }
    #endregion
}