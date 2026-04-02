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
        bool minimumSize = source.Length >= (CryptoConstants.SaltSize + CryptoConstants.IvSize + CryptoConstants.TagSize);

        if (!minimumSize)
            throw new ArgumentException("Invalid encrypted data!");

        // Extract the Salt
        var extractedSalt = new byte[CryptoConstants.SaltSize];
        await source.ReadExactlyAsync(extractedSalt);

        // Derive the key
        var key = await _keyDerivationService.DeriveKeyAsync(password, extractedSalt);

        try
        {

            using (var aes = new AesGcm(key, CryptoConstants.TagSize))
            {
                // Extract the IV
                var extractedIV = new byte[CryptoConstants.IvSize];

                // Looping through the chunks
                int ivBytesRead = 0;
                while ((ivBytesRead = await source.ReadAsync(extractedIV)) > 0)
                {
                    if (ivBytesRead is not CryptoConstants.IvSize)
                        throw new CryptographicException("Corrupted file!");

                    // Create a Decrypt buffer
                    var cipherTextBufffer = new byte[CryptoConstants.ChunkSizeBytes + CryptoConstants.TagSize];

                    // AES-GCM Decryption
                    var authenticationTag = new byte[CryptoConstants.TagSize];

                    int chunkBytesRead = await source.ReadAsync(cipherTextBufffer);

                    var plainText = new byte[ivBytesRead];
                    var decryptedCipherText = new ReadOnlySpan<byte>(cipherTextBufffer, 0, CryptoConstants.ChunkSizeBytes);
                    var decryptedTag = new ReadOnlySpan<byte>(cipherTextBufffer, chunkBytesRead - CryptoConstants.TagSize, CryptoConstants.TagSize);


                    aes.Decrypt(extractedIV, decryptedCipherText, authenticationTag, plainText);

                    await destination.WriteAsync(plainText);
                }

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
    }
    #endregion
}