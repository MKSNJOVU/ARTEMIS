using Artemis.Core.Interfaces;
using System.Buffers;
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
        // 1. Generate and Write the Global Salt (Header)
        var randomSalt = new byte[CryptoConstants.SaltSize];
        RandomNumberGenerator.Fill(randomSalt);
        await destination.WriteAsync(randomSalt);

        // 2. Derive the AES key
        var key = await _keyDerivationService.DeriveKeyAsync(password, randomSalt);

        try
        {
            using (var aes = new AesGcm(key, CryptoConstants.TagSize))
            {
                // 3. MEMORY OPTIMIZATION: Allocate/Rent buffers ONCE outside the loop!
                // We use ArrayPool for the large 64KB buffers to save the Garbage Collector
                byte[] plainTextBuffer = ArrayPool<byte>.Shared.Rent(CryptoConstants.ChunkSizeBytes);
                byte[] cipherTextBuffer = ArrayPool<byte>.Shared.Rent(CryptoConstants.ChunkSizeBytes);

                // Fixed-size small buffers
                byte[] ivBuffer = new byte[CryptoConstants.IvSize];
                byte[] tagBuffer = new byte[CryptoConstants.TagSize];
                byte[] lengthBuffer = new byte[sizeof(int)]; // 4 bytes to hold the chunk length

                int bytesRead = 0;
                int chunkIndex = 0; // 4. INTEGRITY: Keep track of which chunk we are on

                // 5. Loop through the source file
                while ((bytesRead = await source.ReadAsync(plainTextBuffer, 0, CryptoConstants.ChunkSizeBytes)) > 0)
                {
                    // -- PREPARE THE DATA --

                    // Generate a fresh IV for this chunk
                    RandomNumberGenerator.Fill(ivBuffer);

                    // Convert our chunkIndex into bytes for the AAD (Reordering Shield)
                    byte[] aadBytes = BitConverter.GetBytes(chunkIndex);

                    // Slice our rented buffers to the exact size of the data we just read
                    var plainTextSpan = new ReadOnlySpan<byte>(plainTextBuffer, 0, bytesRead);
                    var cipherTextSpan = new Span<byte>(cipherTextBuffer, 0, bytesRead);

                    // -- ENCRYPT --

                    // Pass the AAD in so the chunk's position is cryptographically locked!
                    aes.Encrypt(ivBuffer, plainTextSpan, cipherTextSpan, tagBuffer, associatedData: aadBytes);

                    // -- WRITE TO DESTINATION (V2 Format) --

                    // A. Write the Chunk Length Prefix (4 bytes)
                    BitConverter.TryWriteBytes(lengthBuffer, bytesRead);
                    await destination.WriteAsync(lengthBuffer);

                    // B. Write the IV (12 bytes)
                    await destination.WriteAsync(ivBuffer);

                    // C. Write the Ciphertext (variable size, up to 64KB)
                    // We use ReadOnlyMemory to write from a rented array safely
                    await destination.WriteAsync(new ReadOnlyMemory<byte>(cipherTextBuffer, 0, bytesRead));

                    // D. Write the Authentication Tag (16 bytes)
                    await destination.WriteAsync(tagBuffer);

                    // Increment the index for the next loop!
                    chunkIndex++;
                }

                // 6. Cleanup: Return the large buffers to the system pool
                ArrayPool<byte>.Shared.Return(plainTextBuffer, clearArray: true);
                ArrayPool<byte>.Shared.Return(cipherTextBuffer, clearArray: true);
            }
        }
        finally
        {
            // Always securely wipe the AES key
            CryptographicOperations.ZeroMemory(key);
        }
    }


    public async Task DecryptAsync(Stream source, Stream destination, string password)
    {
        // 1. Prepare and read the Global Salt (Header)
        var randomSalt = new byte[CryptoConstants.SaltSize];
        RandomNumberGenerator.Fill(randomSalt);
        await source.ReadExactlyAsync(randomSalt);

        // 2. Derive the AES key (Only happens once!)
        var key = await _keyDerivationService.DeriveKeyAsync(password, randomSalt);

        try
        {
            using (var aes = new AesGcm(key, CryptoConstants.TagSize))
            {
                // 3. MEMORY OPTIMIZATION: Rent/Allocate buffers ONCE
                // [YOUR TURN: Rent the plainTextBuffer and cipherTextBuffer using ArrayPool<byte>.Shared.Rent]
                // [YOUR TURN: Create the small fixed-size buffers: ivBuffer, tagBuffer, lengthBuffer]

                int chunkIndex = 0; // INTEGRITY: Keep track of which chunk we are on for AAD

                // 4. THE RADAR: Read the 4-byte chunk length.
                // If it returns 0, we cleanly hit the end of the file!
                int lengthBytesRead = 0;
                while ((lengthBytesRead = await source.ReadAsync(lengthBuffer)) > 0)
                {
                    // If we read some bytes but not exactly 4, the file is corrupted/cut off
                    if (lengthBytesRead != sizeof(int))
                        throw new CryptographicException("Corrupted file: Missing chunk length header.");

                    // Convert those 4 bytes into an actual integer so we know how much Ciphertext to read!
                    int currentCiphertextLength = BitConverter.ToInt32(lengthBuffer);

                    // 5. READ THE EXACT CHUNK COMPONENTS IN ORDER
                    // [YOUR TURN: Use ReadExactlyAsync to read the IV into your ivBuffer]
                    // [YOUR TURN: Use ReadExactlyAsync to read the Ciphertext into your cipherTextBuffer. Hint: Use currentCiphertextLength]
                    // [YOUR TURN: Use ReadExactlyAsync to read the Tag into your tagBuffer]

                    // 6. PREPARE THE SPANS & AAD
                    // [YOUR TURN: Convert chunkIndex to an aadBytes array using BitConverter]
                    // [YOUR TURN: Create a ReadOnlySpan for the Ciphertext based on currentCiphertextLength]
                    // [YOUR TURN: Create a Span for the Plaintext based on currentCiphertextLength]

                    // 7. DECRYPT
                    // [YOUR TURN: Call aes.Decrypt. Don't forget to pass the aadBytes!]

                    // 8. WRITE TO DESTINATION
                    // [YOUR TURN: Write the decrypted Plaintext span to the destination stream]

                    // Increment the index for the next loop!
                    chunkIndex++;
                }
            }
        }
        catch (CryptographicException)
        {
            throw new CryptographicException("Decryption failed. Wrong password or corrupted/tampered data!");
        }
        finally
        {
            // 9. CLEANUP
            // [YOUR TURN: Securely wipe the AES key]
            // [YOUR TURN: Return the large plainTextBuffer and cipherTextBuffer to the ArrayPool]
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

                // Create a Decrypt buffer
                var cipherTextBufffer = new byte[CryptoConstants.ChunkSizeBytes + CryptoConstants.TagSize];

                while ((ivBytesRead = await source.ReadAsync(extractedIV)) > 0)
                {
                    while (ivBytesRead < CryptoConstants.IvSize)
                    {
                        int bytesRead = await source.ReadAsync(extractedIV.AsMemory(ivBytesRead, CryptoConstants.IvSize - ivBytesRead));
                        if (bytesRead == 0)
                            throw new CryptographicException("Corrupted file!");
                        ivBytesRead += bytesRead;
                    }

                    // AES-GCM Decryption
                    int chunkBytesRead = await source.ReadAsync(cipherTextBufffer);
                    int cipherTextLength = chunkBytesRead - CryptoConstants.TagSize;

                    var plainText = new byte[cipherTextLength];
                    var decryptedCipherText = new ReadOnlySpan<byte>(cipherTextBufffer, 0, cipherTextLength);
                    var decryptedTag = new ReadOnlySpan<byte>(cipherTextBufffer, chunkBytesRead - CryptoConstants.TagSize, CryptoConstants.TagSize);

                    aes.Decrypt(extractedIV, decryptedCipherText, decryptedTag, plainText);

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