namespace Artemis.Core.Models;

/// <summary>
/// Carries encrypted data alongside its metadata, including the original file name and size
/// </summary>
/// <param name="EncryptedData"></param>
/// <param name="OriginalFileName"></param>
/// <param name="OriginalSize"></param>
public record EncryptionResult(
    byte[] EncryptedData,
    string OriginalFileName,
    long OriginalSize
);