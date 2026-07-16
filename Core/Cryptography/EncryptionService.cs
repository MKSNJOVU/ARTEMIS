using Artemis.Core.Interfaces;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;
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
    public async Task EncryptAsync(Stream source, Stream destination, byte[] password, string associatedData)
    {
        // Generate and Write the Global Salt (Header)
        byte[] randomSalt = new byte[CryptoConstants.SaltSize];
        RandomNumberGenerator.Fill(randomSalt);
        await destination.WriteAsync(randomSalt);

        byte[] extBytes = Encoding.UTF8.GetBytes(associatedData);
        byte[] extLengthBytes = BitConverter.GetBytes(extBytes.Length);

        await destination.WriteAsync(extLengthBytes);
        await destination.WriteAsync(extBytes);

        //  Derive the AES key
        byte[]? key = await _keyDerivationService.DeriveKeyAsync(password, randomSalt);

        try
        {
            using (AesGcm aes = new AesGcm(key, CryptoConstants.TagSize))
            {
                //MEMORY OPTIMIZATION: Allocate/Rent buffers ONCE outside the loop!
                // We use ArrayPool for the large 64KB buffers to save the Garbage Collector
                byte[] plainTextBuffer = ArrayPool<byte>.Shared.Rent(CryptoConstants.ChunkSizeBytes);
                byte[] cipherTextBuffer = ArrayPool<byte>.Shared.Rent(CryptoConstants.ChunkSizeBytes);

                // Fixed-size small buffers
                byte[] ivBuffer = new byte[CryptoConstants.IvSize];
                byte[] tagBuffer = new byte[CryptoConstants.TagSize];
                byte[] lengthBuffer = new byte[sizeof(int)]; // 4 bytes to hold the chunk length

                byte[] fileExtensionBytes = Encoding.UTF8.GetBytes(associatedData);
                byte[] aadBytes = new byte[sizeof(int) + fileExtensionBytes.Length];

                Buffer.BlockCopy(fileExtensionBytes, 0, aadBytes, sizeof(int), fileExtensionBytes.Length);

                int bytesRead = 0;
                int chunkIndex = 0; // 4. INTEGRITY: Keep track of which chunk we are on

                // Loop through the source file
                while ((bytesRead = await source.ReadAsync(plainTextBuffer.AsMemory(0, CryptoConstants.ChunkSizeBytes))) > 0)
                {
                    // -- PREPARE THE DATA --

                    // Generate a fresh IV for this chunk
                    RandomNumberGenerator.Fill(ivBuffer);

                    // Convert our chunkIndex into bytes for the AAD (Reordering Shield)
                    byte[] currentIndexBytes = BitConverter.GetBytes(chunkIndex);
                    Buffer.BlockCopy(currentIndexBytes, 0, aadBytes, 0, sizeof(int));

                    // Slice our rented buffers to the exact size of the data we just read
                    ReadOnlySpan<byte> plainTextSpan = new ReadOnlySpan<byte>(plainTextBuffer, 0, bytesRead);
                    Span<byte> cipherTextSpan = new Span<byte>(cipherTextBuffer, 0, bytesRead);

                    // -- ENCRYPT --

                    // Pass the AAD in so the chunk's position is cryptographically locked!
                    aes.Encrypt(ivBuffer, plainTextSpan, cipherTextSpan, tagBuffer, associatedData: aadBytes);

                    // -- WRITE TO DESTINATION --

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


    public async Task DecryptAsync(Stream source, Stream destination, byte[] password)
    {
        // Prepare and read the Global Salt and File Extension(Header)
        byte[]? extractedSALT = await ExtractGlobalSALT(source);

        int extensionLength = await ExtractExtensionLength(source);

        byte[] fileExtensionBytes = new byte[extensionLength];
        await source.ReadExactlyAsync(fileExtensionBytes);

        byte[] aadBytes = new byte[sizeof(int) + fileExtensionBytes.Length];

        // Derive the AES key (Only happens once!)
        byte[] key = await _keyDerivationService.DeriveKeyAsync(password, extractedSALT);

        // MEMORY OPTIMIZATION: Rent/Allocate buffers ONCE
        byte[] plainTextBuffer = ArrayPool<byte>.Shared.Rent(CryptoConstants.ChunkSizeBytes);
        byte[] cipherTextBuffer = ArrayPool<byte>.Shared.Rent(CryptoConstants.ChunkSizeBytes);
        try
        {
            using AesGcm aes = new AesGcm(key, CryptoConstants.TagSize);

            byte[] ivBuffer = new byte[CryptoConstants.IvSize];
            byte[] tagBuffer = new byte[CryptoConstants.TagSize];
            byte[] lengthBuffer = new byte[sizeof(int)];

            Buffer.BlockCopy(fileExtensionBytes, 0, aadBytes, sizeof(int), fileExtensionBytes.Length);

            int chunkIndex = 0; // INTEGRITY: Keep track of which chunk we are on for AAD


            int lengthBytesRead = 0;

            while ((lengthBytesRead = await source.ReadAsync(lengthBuffer)) > 0)
            {
                // If we read some bytes but not exactly 4, the file is corrupted/cut off
                if (lengthBytesRead != sizeof(int))
                    throw new CryptographicException("Error: The file has been corrupted");

                // Convert those 4 bytes into an actual integer so we know how much Ciphertext to read!
                int currentCiphertextLength = BitConverter.ToInt32(lengthBuffer);

                // READ THE EXACT CHUNK COMPONENTS IN ORDER
                await source.ReadExactlyAsync(ivBuffer);

                await source.ReadExactlyAsync(new Memory<byte>(cipherTextBuffer, 0, currentCiphertextLength));

                await source.ReadExactlyAsync(tagBuffer);

                // PREPARE THE SPANS & AAD

                byte[] currentIndexBytes = BitConverter.GetBytes(chunkIndex);
                Buffer.BlockCopy(currentIndexBytes, 0, aadBytes, 0, sizeof(int));

                ReadOnlySpan<byte> cipherText = new(cipherTextBuffer, 0, currentCiphertextLength);

                Span<byte> plainText = new(plainTextBuffer, 0, currentCiphertextLength);

                //  DECRYPT
                aes.Decrypt(ivBuffer, cipherText, tagBuffer, plainText, associatedData: aadBytes);

                // WRITE TO DESTINATION
                await destination.WriteAsync(new ReadOnlyMemory<byte>(plainTextBuffer, 0, currentCiphertextLength));

                // Increment the index for the next loop!
                chunkIndex++;
            }
        }
        catch (CryptographicException)
        {
            throw new CryptographicException("Decryption failed. Wrong password or corrupted/tampered data!");
        }
        finally
        {
            //  CLEANUP
            CryptographicOperations.ZeroMemory(key);

            ArrayPool<byte>.Shared.Return(plainTextBuffer, clearArray: true);
            ArrayPool<byte>.Shared.Return(cipherTextBuffer, clearArray: true);
        }
    }

    private async Task<byte[]> ExtractGlobalSALT(Stream source)
    {
        byte[]? extractedSALT = new byte[CryptoConstants.SaltSize];
        await source.ReadExactlyAsync(extractedSALT);

        return extractedSALT;
    }

    private async Task<int> ExtractExtensionLength(Stream source)
    {
        byte[]? lengthBuffer = new byte[sizeof(int)];
        await source.ReadExactlyAsync(lengthBuffer);


        int extensionLength = BitConverter.ToInt32(lengthBuffer);

        return extensionLength <= 0 || extensionLength > 256
            ? throw new CryptographicException("Invalid file header or corrupted extension length.")
            : extensionLength;
    }
    #endregion
}