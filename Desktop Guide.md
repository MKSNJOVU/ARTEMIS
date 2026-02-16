# Artemis Desktop - Zero-Knowledge Encryption Implementation Guide

## Overview

Implement a cross-platform desktop zero-knowledge file encryption application that is fully interoperable with the existing Android/Kotlin MKSSecureShare app and the Web app. Like Signal, the application **never** transmits passwords or plaintext — all cryptographic operations happen locally on the user's machine.

**Architecture:**
- **Artemis.Core**: Shared .NET 10 class library — Argon2id key derivation + AES-256-GCM encryption/decryption
- **Artemis.Desktop**: Avalonia UI (.NET 10) with CommunityToolkit.Mvvm — desktop UI for encrypt/decrypt operations
- **Interoperability**: Byte-identical file format with Android app and Web app

**Technology Stack:**
| Component | Technology | Package/Source |
|-----------|-----------|----------------|
| UI Framework | Avalonia 11.3.12 | `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent` |
| MVVM Framework | CommunityToolkit.Mvvm 8.4.0 | `CommunityToolkit.Mvvm` |
| Key Derivation | Argon2id | `Konscious.Security.Cryptography.Argon2` (NuGet, in Core) |
| Encryption | AES-256-GCM | `System.Security.Cryptography.AesGcm` (built-in .NET) |
| Target | .NET 10 | Cross-platform: Windows 11 + Linux |

---

## File Format (Must Match Kotlin/Web Exactly)

```
+---------------------------------------------------------------+
| Offset 0-15:  Salt (16 bytes, random, for Argon2)             |
| Offset 16-27: IV (12 bytes, random, for AES-GCM)             |
| Offset 28-N:  Ciphertext (variable length)                    |
| Last 16:      Authentication Tag (128 bits, appended by GCM)  |
+---------------------------------------------------------------+

Total size = plaintext_length + 44 bytes overhead
```

**Crypto Parameters** (from `Kotlin/app/src/main/java/.../CryptoConstants.kt`):
| Parameter | Value | Notes |
|-----------|-------|-------|
| Algorithm | AES-256-GCM | Authenticated encryption |
| Key Size | 256 bits (32 bytes) | |
| IV Size | 12 bytes (96 bits) | Standard for GCM |
| Salt Size | 16 bytes | For Argon2 |
| Auth Tag | 128 bits (16 bytes) | Integrity verification |
| Argon2 Type | Argon2id | Hybrid (side-channel + GPU resistant) |
| Argon2 Iterations | 3 | Time cost |
| Argon2 Memory | 65536 KB (64 MB) | Memory cost |
| Argon2 Parallelism | 1 | Lanes |

---

## Directory Structure

```
Artemis/
├── Artemis.sln
│
├── Core/                                    (Artemis.Core - shared class library)
│   ├── Artemis.Core.csproj
│   ├── Cryptography/
│   │   ├── CryptoConstants.cs               (Phase 1 - Step 1.1)
│   │   ├── KeyDerivationService.cs          (Phase 1 - Step 1.3)
│   │   └── EncryptionService.cs             (Phase 1 - Step 1.4)
│   ├── Interfaces/
│   │   ├── IKeyDerivationService.cs         (Phase 1 - Step 1.2)
│   │   └── IEncryptionService.cs            (Phase 1 - Step 1.2)
│   └── Models/
│       └── EncryptionResult.cs              (Phase 1 - Step 1.5)
│
├── Desktop/                                 (Artemis.Desktop - Avalonia MVVM)
│   ├── Artemis.Desktop.csproj
│   ├── App.axaml / App.axaml.cs             (modify - Phase 2 - Step 2.5)
│   ├── Program.cs
│   ├── ViewLocator.cs
│   ├── Assets/
│   │   └── avalonia-logo.ico
│   ├── Models/
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs                 (exists)
│   │   ├── MainWindowViewModel.cs           (modify - Phase 2 - Step 2.5)
│   │   ├── EncryptViewModel.cs              (Phase 2 - Step 2.1)
│   │   └── DecryptViewModel.cs              (Phase 2 - Step 2.2)
│   ├── Views/
│   │   ├── MainWindow.axaml / .axaml.cs     (modify - Phase 2 - Step 2.5)
│   │   ├── EncryptView.axaml / .axaml.cs    (Phase 2 - Step 2.3)
│   │   └── DecryptView.axaml / .axaml.cs    (Phase 2 - Step 2.4)
│   └── Services/
│       └── FileDialogService.cs             (Phase 2 - Step 2.6)
│
├── Web/                                     (existing)
└── Kotlin/                                  (existing)
```

---

# Phase 1: Core Cryptography Module (Artemis.Core)

All cryptographic logic lives in the shared `Artemis.Core` library. This is the strategic design: both `Artemis.Desktop` and potentially the `Web` project can consume the same battle-tested crypto code without duplication.

**NuGet packages already installed:**
- `Konscious.Security.Cryptography.Argon2` 1.3.1 — Argon2id key derivation
- `System.Security.Cryptography.AesGcm` — built into .NET 10, no package needed

---

## Step 1.1: CryptoConstants.cs

**Path:** `Core/Cryptography/CryptoConstants.cs`

**Purpose:** A single source of truth for all cryptographic parameters. These values MUST match the Kotlin `CryptoConstants.kt` exactly — any mismatch breaks interoperability.

**Research:**
- C# `static class` and `const` fields: https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members
- Why `const` over `static readonly` for compile-time constants: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/const

**What to implement:**
1. A `public static class` named `CryptoConstants` in namespace `Artemis.Core.Cryptography`
2. `const` fields for every parameter in the table above
3. Group them logically: AES-GCM config, then Argon2id config

**Fields to define:**
```
AES-GCM Configuration:
- KeySizeBytes:     32      (256 bits / 8)
- SaltSize:         16      (bytes)
- IvSize:           12      (bytes, 96 bits — standard for GCM)
- TagSize:          16      (bytes, 128 bits)

Argon2id Configuration:
- Argon2Iterations:  3      (time cost)
- Argon2MemorySize:  65536  (KB = 64 MB)
- Argon2Parallelism: 1      (lanes)
```

**Kotlin reference:** `Kotlin/app/src/main/java/com/example/mkssecureshare/CryptoConstants.kt`

**Code Shell:**
```csharp
namespace Artemis.Core.Cryptography;

/// <summary>
/// Cryptographic constants — MUST match Kotlin CryptoConstants.kt exactly.
/// Any mismatch breaks interoperability with Android and Web apps.
/// </summary>
public static class CryptoConstants
{
    // ─────────────────────────────────────────────
    // AES-256-GCM Configuration
    // ─────────────────────────────────────────────
    public const int KeySizeBytes = /* TODO */;
    public const int SaltSize = /* TODO */;
    public const int IvSize = /* TODO */;
    public const int TagSize = /* TODO */;

    // ─────────────────────────────────────────────
    // Argon2id Configuration
    // ─────────────────────────────────────────────
    public const int Argon2Iterations = /* TODO */;
    public const int Argon2MemorySize = /* TODO */;
    public const int Argon2Parallelism = /* TODO */;
}
```

---

## Step 1.2: Service Interfaces

Define contracts in `Core/Interfaces/` so the Desktop project depends on abstractions, not implementations. This follows the Dependency Inversion Principle and makes testing straightforward.

**Research:**
- C# interfaces: https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/interfaces
- Dependency Inversion Principle: https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/architectural-principles#dependency-inversion

---

### IKeyDerivationService.cs

**Path:** `Core/Interfaces/IKeyDerivationService.cs`

**Purpose:** Contract for deriving an AES-256 key from a password and salt using Argon2id.

**What to define:**
- A single method: `DeriveKey(string password, byte[] salt)` that returns `Task<byte[]>`
- The returned byte array is always 32 bytes (256 bits)
- It is `Task<byte[]>` because Argon2id with 64MB memory is CPU-intensive and should not block the UI thread

**Concepts to look up:**
- `Task<T>` for async return types: https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/
- Why async for CPU-bound work on a UI thread

**Code Shell:**
```csharp
namespace Artemis.Core.Interfaces;

public interface IKeyDerivationService
{
    /// <summary>
    /// Derives a 256-bit AES key from a password using Argon2id.
    /// </summary>
    /// <param name="password">User's master password</param>
    /// <param name="salt">Random salt (16 bytes)</param>
    /// <returns>32-byte derived key</returns>
    Task<byte[]> DeriveKeyAsync(string password, byte[] salt);
}
```

---

### IEncryptionService.cs

**Path:** `Core/Interfaces/IEncryptionService.cs`

**Purpose:** Contract for encrypting and decrypting byte arrays using AES-256-GCM with password-based key derivation.

**What to define:**
Two methods:

```
Method 1: EncryptAsync(byte[] plaintext, string password)
- Takes raw plaintext bytes and the user's password
- Returns byte[] in the format: [Salt][IV][Ciphertext][AuthTag]
- Generates its own random salt and IV internally

Method 2: DecryptAsync(byte[] encryptedData, string password)
- Takes encrypted data in format [Salt][IV][Ciphertext][AuthTag]
- Extracts salt, derives key, decrypts
- Returns original plaintext bytes
- Throws if password is wrong or data is tampered
```

**Concepts to look up:**
- Method signatures with `Task<byte[]>` return type
- XML documentation comments (`///`)

**Code Shell:**
```csharp
namespace Artemis.Core.Interfaces;

public interface IEncryptionService
{
    /// <summary>
    /// Encrypts plaintext using AES-256-GCM with Argon2id-derived key.
    /// Output format: [Salt:16][IV:12][Ciphertext][AuthTag:16]
    /// </summary>
    Task<byte[]> EncryptAsync(byte[] plaintext, string password);

    /// <summary>
    /// Decrypts data in format [Salt][IV][Ciphertext][AuthTag].
    /// Throws if password is wrong or data is corrupted.
    /// </summary>
    Task<byte[]> DecryptAsync(byte[] encryptedData, string password);
}
```

---

## Step 1.3: KeyDerivationService.cs

**Path:** `Core/Cryptography/KeyDerivationService.cs`

```
Class: KeyDerivationService
Implements: IKeyDerivationService

Purpose: Derive a 256-bit AES key from a password + salt using Argon2id.
         Uses Konscious.Security.Cryptography.Argon2id under the hood.
         This mirrors the Kotlin PasswordKeyManager class.

Logic for DeriveKeyAsync(password, salt):
1. Convert the password string to bytes using UTF-8 encoding
   - Use System.Text.Encoding.UTF8.GetBytes(password)
   - MUST be UTF-8 to match Kotlin/Web
2. Create a new Argon2id instance from Konscious library
   - Constructor takes the password bytes
3. Configure the Argon2id instance with constants:
   - Salt = salt parameter
   - DegreeOfParallelism = CryptoConstants.Argon2Parallelism (1)
   - MemorySize = CryptoConstants.Argon2MemorySize (65536)
   - Iterations = CryptoConstants.Argon2Iterations (3)
4. Call GetBytes(CryptoConstants.KeySizeBytes) to produce 32-byte key
5. Return the derived key bytes

Important notes:
- Argon2id is the class from Konscious.Security.Cryptography namespace
- The Argon2id class follows the System.Security.Cryptography.DeriveBytes pattern
- It is IDisposable — use a 'using' statement
- GetBytes() is synchronous but CPU-intensive — wrap in Task.Run() to
  avoid blocking the UI thread
```

**Research:**
- Konscious.Security.Cryptography API: https://github.com/kmaragon/Konscious.Security.Cryptography
- Argon2id usage pattern: https://asecuritysite.com/csharp/csharp_argon2
- `using` statement for IDisposable: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/using
- `Task.Run()` for offloading CPU work: https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.run
- `Encoding.UTF8.GetBytes()`: https://learn.microsoft.com/en-us/dotnet/api/system.text.encoding.getbytes

**Kotlin reference:** `Kotlin/app/src/main/java/com/example/mkssecureshare/PasswordKeyManager.kt`

**Code Shell:**
```csharp
using System.Text;
using Artemis.Core.Interfaces;
using Konscious.Security.Cryptography;

namespace Artemis.Core.Cryptography;

public class KeyDerivationService : IKeyDerivationService
{
    public async Task<byte[]> DeriveKeyAsync(string password, byte[] salt)
    {
        // ─────────────────────────────────────────────
        // Step 1: Convert password to UTF-8 bytes
        // ─────────────────────────────────────────────
        var passwordBytes = /* TODO: Encoding.UTF8... */;

        // ─────────────────────────────────────────────
        // Steps 2-4: Create and configure Argon2id,
        //            then derive key bytes.
        //            Wrapped in Task.Run to stay off UI thread.
        // ─────────────────────────────────────────────
        return await Task.Run(() =>
        {
            using var argon2 = new Argon2id(passwordBytes);

            argon2.Salt = /* TODO */;
            argon2.DegreeOfParallelism = /* TODO: from constants */;
            argon2.MemorySize = /* TODO: from constants */;
            argon2.Iterations = /* TODO: from constants */;

            return argon2.GetBytes(/* TODO: key size from constants */);
        });
    }
}
```

---

## Step 1.4: EncryptionService.cs

**Path:** `Core/Cryptography/EncryptionService.cs`

This is the heart of the system. Compare directly to your Kotlin `EncryptionManager.kt` — the logic is nearly identical.

**Research:**
- `AesGcm` class: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm
- `AesGcm.Encrypt()`: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm.encrypt
- `AesGcm.Decrypt()`: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm.decrypt
- `RandomNumberGenerator.Fill()`: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator.fill
- Cross-platform crypto in .NET: https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography
- `Span<T>` and `byte[]` interop: https://learn.microsoft.com/en-us/dotnet/api/system.span-1
- Authenticated encryption with AES-GCM (Scott Brady): https://www.scottbrady.io/c-sharp/aes-gcm-dotnet

---

### Constructor

```
Constructor: EncryptionService(IKeyDerivationService keyDerivationService)

Purpose: Accept the key derivation service via constructor injection.
         Store it in a private readonly field.

Concepts to look up:
- Constructor injection pattern
- private readonly fields
```

---

### Method 1: EncryptAsync(byte[] plaintext, string password)

```
Method: EncryptAsync(plaintext, password)

Purpose: Encrypt plaintext bytes using AES-256-GCM with an Argon2id-derived key.
         Output format: [Salt:16][IV:12][Ciphertext][AuthTag:16]

Logic:
1. Generate random salt (16 bytes)
   - Create byte array of CryptoConstants.SaltSize
   - Fill with RandomNumberGenerator.Fill(salt)
   - This is the .NET equivalent of SecureRandom in Kotlin

2. Derive AES key from password + salt
   - Call await _keyDerivationService.DeriveKeyAsync(password, salt)

3. Generate random IV (12 bytes)
   - Create byte array of CryptoConstants.IvSize
   - Fill with RandomNumberGenerator.Fill(iv)

4. Encrypt using AesGcm
   - Create byte array for ciphertext (same length as plaintext)
   - Create byte array for auth tag (CryptoConstants.TagSize = 16)
   - Create AesGcm instance with the derived key
     - AesGcm is IDisposable — use 'using'
   - Call aesGcm.Encrypt(iv, plaintext, ciphertext, tag)
     - Note: .NET AesGcm separates ciphertext and tag (unlike Web Crypto
       which appends them). You must handle this difference.

5. Assemble output: [salt][iv][ciphertext][tag]
   - Calculate total size: SaltSize + IvSize + ciphertext.Length + TagSize
   - Create result byte array of total size
   - Use Buffer.BlockCopy() or Array.Copy() to copy each piece at correct offset:
     - salt     at offset 0
     - iv       at offset SaltSize (16)
     - ciphertext at offset SaltSize + IvSize (28)
     - tag      at offset SaltSize + IvSize + ciphertext.Length

6. Return the assembled byte array

Important notes:
- .NET's AesGcm.Encrypt() outputs ciphertext and tag SEPARATELY
- The Kotlin/Web format has the tag APPENDED to the ciphertext
- When assembling: you must place ciphertext THEN tag in sequence
- This means the output is: [salt][iv][ciphertext][tag]
- Total overhead: 16 + 12 + 16 = 44 bytes (matches Kotlin/Web)

Concepts to look up:
- RandomNumberGenerator.Fill() — static method, no instance needed
- AesGcm constructor: new AesGcm(key, tagSizeInBytes)
- AesGcm.Encrypt(nonce, plaintext, ciphertext, tag) — all are byte spans
- Buffer.BlockCopy(src, srcOffset, dst, dstOffset, count) for efficient copying
- Array.Copy() as an alternative
```

**Code Shell:**
```csharp
public async Task<byte[]> EncryptAsync(byte[] plaintext, string password)
{
    // ─────────────────────────────────────────────
    // Step 1: Generate random salt
    // ─────────────────────────────────────────────
    var salt = new byte[CryptoConstants.SaltSize];
    RandomNumberGenerator.Fill(salt);

    // ─────────────────────────────────────────────
    // Step 2: Derive key from password + salt
    // ─────────────────────────────────────────────
    var key = await _keyDerivationService.DeriveKeyAsync(password, salt);

    // ─────────────────────────────────────────────
    // Step 3: Generate random IV
    // ─────────────────────────────────────────────
    var iv = new byte[/* TODO: IV size from constants */];
    RandomNumberGenerator.Fill(iv);

    // ─────────────────────────────────────────────
    // Step 4: Encrypt with AES-GCM
    // ─────────────────────────────────────────────
    var ciphertext = new byte[plaintext.Length];
    var tag = new byte[/* TODO: Tag size from constants */];

    using (var aesGcm = new AesGcm(key, /* TODO: tag size from constants */))
    {
        aesGcm.Encrypt(
            /* TODO: nonce (iv) */,
            /* TODO: plaintext */,
            /* TODO: ciphertext output */,
            /* TODO: tag output */
        );
    }

    // ─────────────────────────────────────────────
    // Step 5: Assemble [salt][iv][ciphertext][tag]
    // ─────────────────────────────────────────────
    var result = new byte[
        /* TODO: total size calculation */
    ];

    Buffer.BlockCopy(salt, 0, result, /* TODO: offset */, salt.Length);
    Buffer.BlockCopy(iv, 0, result, /* TODO: offset */, iv.Length);
    Buffer.BlockCopy(ciphertext, 0, result, /* TODO: offset */, ciphertext.Length);
    Buffer.BlockCopy(tag, 0, result, /* TODO: offset */, tag.Length);

    return result;
}
```

---

### Method 2: DecryptAsync(byte[] encryptedData, string password)

```
Method: DecryptAsync(encryptedData, password)

Purpose: Decrypt data in format [Salt:16][IV:12][Ciphertext][AuthTag:16].
         Returns original plaintext bytes.
         Compare to your Kotlin EncryptionManager.decrypt().

Logic:
1. Validate minimum size
   - Minimum = SaltSize + IvSize + TagSize = 16 + 12 + 16 = 44 bytes
   - If encryptedData.Length < 44, throw ArgumentException
   - An encrypted file with 0 bytes of actual content would be exactly 44 bytes

2. Extract salt (first 16 bytes)
   - Create byte array of SaltSize
   - Copy from encryptedData starting at offset 0

3. Extract IV (next 12 bytes)
   - Create byte array of IvSize
   - Copy from encryptedData starting at offset SaltSize (16)

4. Calculate ciphertext length
   - ciphertextLength = encryptedData.Length - SaltSize - IvSize - TagSize
   - This is everything between the IV and the tag

5. Extract ciphertext (variable length, after IV, before tag)
   - Create byte array of ciphertextLength
   - Copy from encryptedData starting at offset SaltSize + IvSize (28)

6. Extract auth tag (last 16 bytes)
   - Create byte array of TagSize
   - Copy from encryptedData starting at offset (encryptedData.Length - TagSize)

7. Derive key using extracted salt
   - Call await _keyDerivationService.DeriveKeyAsync(password, salt)
   - Same salt that was used during encryption

8. Decrypt with AES-GCM
   - Create byte array for plaintext (same length as ciphertext)
   - Create AesGcm instance with the derived key
   - Call aesGcm.Decrypt(iv, ciphertext, tag, plaintext)
   - MUST wrap in try/catch:
     - If auth tag fails (wrong password or tampered data),
       AesGcm.Decrypt() throws CryptographicException
     - Catch CryptographicException and throw a user-friendly message

9. Return the decrypted plaintext bytes

Important notes:
- .NET's AesGcm.Decrypt() takes the tag as a SEPARATE parameter
- You must split the Kotlin/Web format: extract ciphertext and tag separately
- The tag is always the LAST 16 bytes of the encrypted payload
- CryptographicException means wrong password OR corrupted/tampered data

Concepts to look up:
- Buffer.BlockCopy() for extracting sub-arrays
- AesGcm.Decrypt(nonce, ciphertext, tag, plaintext) — note parameter order
- CryptographicException: https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cryptographicexception
- try/catch pattern for expected exceptions
```

**Code Shell:**
```csharp
public async Task<byte[]> DecryptAsync(byte[] encryptedData, string password)
{
    // ─────────────────────────────────────────────
    // Step 1: Validate minimum size
    // ─────────────────────────────────────────────
    var minimumSize = /* TODO: Salt + IV + Tag */;
    if (encryptedData.Length < minimumSize)
        throw new ArgumentException("Invalid encrypted data: too short.");

    // ─────────────────────────────────────────────
    // Step 2: Extract salt (first 16 bytes)
    // ─────────────────────────────────────────────
    var salt = new byte[CryptoConstants.SaltSize];
    Buffer.BlockCopy(encryptedData, /* TODO: offset */, salt, 0, salt.Length);

    // ─────────────────────────────────────────────
    // Step 3: Extract IV (next 12 bytes)
    // ─────────────────────────────────────────────
    var iv = new byte[CryptoConstants.IvSize];
    Buffer.BlockCopy(encryptedData, /* TODO: offset */, iv, 0, iv.Length);

    // ─────────────────────────────────────────────
    // Step 4: Calculate ciphertext length
    // ─────────────────────────────────────────────
    var ciphertextLength = /* TODO: total - salt - iv - tag */;

    // ─────────────────────────────────────────────
    // Step 5: Extract ciphertext
    // ─────────────────────────────────────────────
    var ciphertext = new byte[ciphertextLength];
    Buffer.BlockCopy(encryptedData, /* TODO: offset */, ciphertext, 0, ciphertextLength);

    // ─────────────────────────────────────────────
    // Step 6: Extract auth tag (last 16 bytes)
    // ─────────────────────────────────────────────
    var tag = new byte[CryptoConstants.TagSize];
    Buffer.BlockCopy(encryptedData, /* TODO: offset */, tag, 0, tag.Length);

    // ─────────────────────────────────────────────
    // Step 7: Derive key using extracted salt
    // ─────────────────────────────────────────────
    var key = await _keyDerivationService.DeriveKeyAsync(password, salt);

    // ─────────────────────────────────────────────
    // Step 8: Decrypt with AES-GCM
    // ─────────────────────────────────────────────
    var plaintext = new byte[ciphertextLength];

    try
    {
        using var aesGcm = new AesGcm(key, /* TODO: tag size */);
        aesGcm.Decrypt(
            /* TODO: nonce (iv) */,
            /* TODO: ciphertext */,
            /* TODO: tag */,
            /* TODO: plaintext output */
        );
    }
    catch (CryptographicException)
    {
        throw new CryptographicException(
            "Decryption failed. Wrong password or corrupted file.");
    }

    return plaintext;
}
```

---

## Step 1.5: EncryptionResult.cs (Optional Helper Model)

**Path:** `Core/Models/EncryptionResult.cs`

**Purpose:** A simple model to carry encryption results with metadata. Useful when the UI needs to know the original filename, file size, or other context alongside the encrypted bytes.

**What to define:**
- `byte[] EncryptedData` — the encrypted output
- `string OriginalFileName` — original file name for later decryption
- `long OriginalSize` — original file size in bytes

**Research:**
- C# records: https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record
- Records vs classes for data carriers

**Code Shell:**
```csharp
namespace Artemis.Core.Models;

/// <summary>
/// Carries encrypted data alongside metadata for UI display.
/// </summary>
public record EncryptionResult(
    byte[] EncryptedData,
    string OriginalFileName,
    long OriginalSize
);
```

---

# Phase 2: Desktop UI (Avalonia MVVM)

The UI layer consumes the `Artemis.Core` cryptographic services through dependency injection. Views are defined in AXAML (Avalonia's XAML dialect — nearly identical to WPF XAML), and ViewModels use CommunityToolkit.Mvvm source generators to eliminate boilerplate.

**Key Avalonia concepts for WPF developers:**
| WPF | Avalonia | Notes |
|-----|----------|-------|
| `.xaml` | `.axaml` | Avalonia XAML file extension |
| `xmlns:local="clr-namespace:..."` | `xmlns:local="using:..."` | Namespace syntax |
| `Window` | `Window` | Same |
| `UserControl` | `UserControl` | Same |
| `{Binding Path}` | `{Binding Path}` | Same, but compiled bindings preferred |
| `x:DataType` (not in WPF) | `x:DataType="vm:MyViewModel"` | Enables compiled bindings |
| `<ContentControl Content="{Binding}">` | Same | Used for view switching |

**Research:**
- Avalonia XAML basics: https://docs.avaloniaui.net/docs/basics/user-interface/introduction-to-xaml
- Avalonia data binding: https://docs.avaloniaui.net/docs/basics/data/data-binding/
- Compiled bindings: https://docs.avaloniaui.net/docs/basics/data/data-binding/compiled-bindings
- CommunityToolkit.Mvvm source generators: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/overview

---

## Step 2.1: EncryptViewModel.cs

**Path:** `Desktop/ViewModels/EncryptViewModel.cs`

```
Class: EncryptViewModel (partial class)
Inherits: ViewModelBase

Purpose: ViewModel for the encrypt view. Handles file selection,
         password input/validation, encryption execution, and progress.

CommunityToolkit.Mvvm source generators used:
- [ObservableProperty] — auto-generates property + INotifyPropertyChanged
- [RelayCommand] — auto-generates ICommand from a method

Properties to define (use [ObservableProperty] on private fields):
- string? selectedFilePath       → generates SelectedFilePath property
- string? selectedFileName       → generates SelectedFileName property
- string? password               → generates Password property
- string? passwordConfirm        → generates PasswordConfirm property
- string? passwordError          → generates PasswordError property
- string? errorMessage           → generates ErrorMessage property
- string? statusMessage          → generates StatusMessage property
- double progressValue           → generates ProgressValue property (0-100)
- bool isEncrypting              → generates IsEncrypting property
- bool showProgress              → generates ShowProgress property

Private fields:
- IEncryptionService (injected via constructor)
- byte[]? _selectedFileBytes (loaded file content)

Commands to define (use [RelayCommand]):
- SelectFileCommand      → opens file dialog
- EncryptCommand         → runs encryption
```

**Research:**
- `[ObservableProperty]` attribute: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty
- `[RelayCommand]` attribute: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/relaycommand
- `partial` classes (required for source generators): https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/partial-classes-and-methods

---

### Command 1: SelectFile

```
Method: async Task SelectFile()
Attribute: [RelayCommand]

Purpose: Open a file picker dialog, let user select a file, read its bytes.

Logic:
1. Create an OpenFileDialog (Avalonia.Controls)
   - Set Title = "Select file to encrypt"
   - Set AllowMultiple = false
2. Get the current top-level window (for dialog parent)
3. Show the dialog, get result (string[]? of paths)
4. If user selected a file:
   a. Store the file path in SelectedFilePath
   b. Store the file name in SelectedFileName
   c. Read all bytes: File.ReadAllBytesAsync(path)
   d. Store bytes in _selectedFileBytes
5. Clear any previous error

Research:
- Avalonia file dialogs: https://docs.avaloniaui.net/docs/concepts/services/storage-provider/file-picker
- File.ReadAllBytesAsync(): https://learn.microsoft.com/en-us/dotnet/api/system.io.file.readallbytesasync
```

---

### Command 2: Encrypt

```
Method: async Task Encrypt()
Attribute: [RelayCommand]

Purpose: Validate inputs, encrypt the file, save the result.

Logic:
1. Validate:
   - If _selectedFileBytes is null → set ErrorMessage, return
   - If Password is null/empty → set ErrorMessage, return
   - If Password != PasswordConfirm → set PasswordError, return
2. Clear errors, set IsEncrypting = true, ShowProgress = true
3. Try:
   a. Set StatusMessage = "Deriving encryption key..."
   b. Set ProgressValue = 15
   c. Call await _encryptionService.EncryptAsync(_selectedFileBytes, Password)
   d. Set ProgressValue = 80, StatusMessage = "Saving encrypted file..."
   e. Generate output filename: SelectedFileName + ".enc"
   f. Open a SaveFileDialog for user to choose save location
      - Set InitialFileName = encrypted filename
   g. If user chose a location:
      - Write bytes: File.WriteAllBytesAsync(path, encryptedBytes)
      - Set ProgressValue = 100, StatusMessage = "Encryption complete!"
   h. If user cancelled: StatusMessage = "Cancelled."
4. Catch Exception:
   - Set ErrorMessage = exception.Message
5. Finally:
   - Set IsEncrypting = false

Research:
- Avalonia SaveFileDialog / StorageProvider
- File.WriteAllBytesAsync()
```

---

### Validation Helper

```
Method: partial void OnPasswordConfirmChanged(string? value)

Purpose: CommunityToolkit.Mvvm generates a partial method hook that is
         called whenever PasswordConfirm changes. Use it for real-time
         password match validation.

Logic:
1. If Password and value are both not null/empty:
   - If they don't match: PasswordError = "Passwords do not match"
   - If they match: PasswordError = null
```

**Research:**
- Partial method hooks in CommunityToolkit.Mvvm: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty#running-code-upon-changes

---

## Step 2.2: DecryptViewModel.cs

**Path:** `Desktop/ViewModels/DecryptViewModel.cs`

```
Class: DecryptViewModel (partial class)
Inherits: ViewModelBase

Purpose: ViewModel for the decrypt view. Simpler than EncryptViewModel
         (no password confirmation needed).

Properties (use [ObservableProperty]):
- string? selectedFilePath
- string? selectedFileName
- string? password
- string? errorMessage
- string? statusMessage
- double progressValue
- bool isDecrypting
- bool showProgress

Private fields:
- IEncryptionService (injected)
- byte[]? _selectedFileBytes

Commands (use [RelayCommand]):
- SelectFileCommand    → open file dialog (filter to .enc files)
- DecryptCommand       → run decryption
```

---

### Command 1: SelectFile

```
Same as EncryptViewModel.SelectFile, but:
- Dialog title: "Select encrypted file"
- Add file filter for .enc files:
  FileTypeFilter with pattern "*.enc"
```

---

### Command 2: Decrypt

```
Method: async Task Decrypt()
Attribute: [RelayCommand]

Purpose: Validate inputs, decrypt the file, save the result.

Logic:
1. Validate:
   - If _selectedFileBytes is null → set ErrorMessage, return
   - If Password is null/empty → set ErrorMessage, return
2. Clear errors, set IsDecrypting = true, ShowProgress = true
3. Try:
   a. StatusMessage = "Deriving decryption key..."
   b. ProgressValue = 15
   c. Call await _encryptionService.DecryptAsync(_selectedFileBytes, Password)
   d. ProgressValue = 80, StatusMessage = "Saving decrypted file..."
   e. Generate output filename:
      - If SelectedFileName ends with ".enc" → remove ".enc"
      - Otherwise → prepend "decrypted_"
   f. Open SaveFileDialog with InitialFileName = decrypted filename
   g. If user chose location: write bytes, set progress 100
   h. If cancelled: set status message
4. Catch CryptographicException:
   - ErrorMessage = "Decryption failed. Wrong password or corrupted file."
5. Catch Exception:
   - ErrorMessage = exception.Message
6. Finally:
   - IsDecrypting = false
```

---

## Step 2.3: EncryptView.axaml

**Path:** `Desktop/Views/EncryptView.axaml`

A UserControl that displays the encryption form. Uses compiled bindings to EncryptViewModel.

**Research:**
- Avalonia UserControl: https://docs.avaloniaui.net/docs/basics/user-interface/controls/creating-controls/user-controls
- Avalonia built-in controls: https://docs.avaloniaui.net/docs/reference/controls/
- TextBox: https://docs.avaloniaui.net/docs/reference/controls/textbox
- Button: https://docs.avaloniaui.net/docs/reference/controls/buttons/button
- ProgressBar: https://docs.avaloniaui.net/docs/reference/controls/progressbar

**AXAML elements to create:**

1. **UserControl root** with compiled binding:
   - `x:DataType="vm:EncryptViewModel"`

2. **Layout** — `StackPanel` or `Grid` with padding and spacing

3. **Security notice** — `TextBlock` or `Border` with info text

4. **File selection section:**
   - `Button` bound to `SelectFileCommand` with text "Select File"
   - `TextBlock` bound to `SelectedFileName` to show chosen file

5. **Password inputs:**
   - `TextBox` with `PasswordChar="*"` bound to `Password`
     (Avalonia uses `RevealPasswordButtonVisible="True"` for show/hide)
   - `TextBox` with `PasswordChar="*"` bound to `PasswordConfirm`
   - `TextBlock` bound to `PasswordError` (red text for mismatch)

6. **Progress section** (visible when `ShowProgress` is true):
   - `ProgressBar` bound to `ProgressValue` (Minimum=0, Maximum=100)
   - `TextBlock` bound to `StatusMessage`

7. **Error display:**
   - `TextBlock` bound to `ErrorMessage` (red, visible when not empty)

8. **Encrypt button:**
   - `Button` bound to `EncryptCommand`
   - Disabled when `IsEncrypting` is true: `IsEnabled="{Binding !IsEncrypting}"`

**Code Shell:**
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Artemis.Desktop.ViewModels"
             x:Class="Artemis.Desktop.Views.EncryptView"
             x:DataType="vm:EncryptViewModel">

    <StackPanel Margin="20" Spacing="15">

        <TextBlock Text="Encrypt a File"
                   FontSize="24" FontWeight="Bold" />

        <!-- TODO: Security notice -->

        <!-- TODO: File selection: Button + filename display -->

        <!-- TODO: Password input -->

        <!-- TODO: Password confirm input -->

        <!-- TODO: Password error text -->

        <!-- TODO: Progress bar + status (visible when ShowProgress) -->

        <!-- TODO: Error message (visible when ErrorMessage not empty) -->

        <!-- TODO: Encrypt button -->

    </StackPanel>
</UserControl>
```

**Code-behind** (`EncryptView.axaml.cs`):
```csharp
using Avalonia.Controls;

namespace Artemis.Desktop.Views;

public partial class EncryptView : UserControl
{
    public EncryptView()
    {
        InitializeComponent();
    }
}
```

---

## Step 2.4: DecryptView.axaml

**Path:** `Desktop/Views/DecryptView.axaml`

Similar to EncryptView but simpler:
- File selection (with .enc hint)
- Single password field (no confirmation)
- Progress bar + status
- Error display
- Decrypt button

**Code Shell:**
```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Artemis.Desktop.ViewModels"
             x:Class="Artemis.Desktop.Views.DecryptView"
             x:DataType="vm:DecryptViewModel">

    <StackPanel Margin="20" Spacing="15">

        <TextBlock Text="Decrypt a File"
                   FontSize="24" FontWeight="Bold" />

        <!-- TODO: File selection: Button + filename display -->

        <!-- TODO: Password input -->

        <!-- TODO: Progress bar + status -->

        <!-- TODO: Error message -->

        <!-- TODO: Decrypt button -->

    </StackPanel>
</UserControl>
```

---

## Step 2.5: MainWindow Navigation

**Path:** Modify `Desktop/ViewModels/MainWindowViewModel.cs`, `Desktop/Views/MainWindow.axaml`, `Desktop/App.axaml.cs`

**Purpose:** Set up navigation between Encrypt and Decrypt views using a `ContentControl` pattern. The MainWindow hosts a navigation bar and swaps content based on the selected view.

**Research:**
- Avalonia navigation patterns: https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/avalonia-ui-and-mvvm
- ContentControl for view switching: https://docs.avaloniaui.net/docs/guides/implementation-guides/how-to-use-the-mvvm-pattern
- ViewLocator (already generated): maps ViewModel → View automatically

---

### MainWindowViewModel Changes

```
Properties to add:
- ViewModelBase currentView          → the active view (Encrypt or Decrypt)
- bool isEncryptSelected             → for nav button highlighting
- bool isDecryptSelected             → for nav button highlighting

Commands to add:
- ShowEncryptCommand    → sets CurrentView = new EncryptViewModel(...)
- ShowDecryptCommand    → sets CurrentView = new DecryptViewModel(...)

Constructor:
- Accept IEncryptionService (injected)
- Default to showing EncryptView on startup
```

---

### MainWindow.axaml Changes

```xml
Layout:
- DockPanel or Grid with two rows
  - Top: Navigation bar (StackPanel with two Buttons: "Encrypt" / "Decrypt")
  - Center: ContentControl bound to CurrentView
    - The ViewLocator automatically resolves the correct View

Structure:
<Window ...>
    <DockPanel>
        <!-- Navigation bar docked to top -->
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal">
            <Button Content="Encrypt" Command="{Binding ShowEncryptCommand}" />
            <Button Content="Decrypt" Command="{Binding ShowDecryptCommand}" />
        </StackPanel>

        <!-- Active view content -->
        <ContentControl Content="{Binding CurrentView}" />
    </DockPanel>
</Window>
```

The `ViewLocator` (already in the project) handles the magic:
- When `CurrentView` is an `EncryptViewModel` → it renders `EncryptView`
- When `CurrentView` is a `DecryptViewModel` → it renders `DecryptView`

---

### App.axaml.cs Changes

**Purpose:** Wire up dependency injection so services are available to ViewModels.

```
Logic:
1. In OnFrameworkInitializationCompleted():
   - Create KeyDerivationService instance
   - Create EncryptionService instance (passing KeyDerivationService)
   - Create MainWindowViewModel (passing EncryptionService)
   - Set as DataContext for MainWindow
```

**Research:**
- For a small app, manual constructor injection is fine
- For larger apps, consider Microsoft.Extensions.DependencyInjection:
  https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection

---

## Step 2.6: FileDialogService.cs (Optional Abstraction)

**Path:** `Desktop/Services/FileDialogService.cs`

**Purpose:** Wraps Avalonia's file dialog APIs so ViewModels don't depend directly on UI types. This makes ViewModels testable.

**Research:**
- Avalonia StorageProvider API: https://docs.avaloniaui.net/docs/concepts/services/storage-provider/file-picker
- IStorageProvider interface

**What to define:**
- `IFileDialogService` interface with:
  - `Task<string?> OpenFileAsync(string title, string[]? filters)`
  - `Task<string?> SaveFileAsync(string title, string suggestedFileName)`
- `FileDialogService` implementation using Avalonia's TopLevel.StorageProvider

This is optional — you can call the file dialogs directly from ViewModels initially and extract this abstraction later when you add tests.

---

# Phase 3: Testing

## Verification Checklist

### Core Crypto Unit Tests

Create a test project: `dotnet new xunit -n Artemis.Tests -o Tests --framework net10.0`

- [ ] **CryptoConstants** — verify all values match Kotlin constants
- [ ] **KeyDerivationService** — derive key from known password+salt, verify output length is 32 bytes
- [ ] **KeyDerivationService** — same password+salt always produces same key (deterministic)
- [ ] **KeyDerivationService** — different salt produces different key
- [ ] **EncryptionService.EncryptAsync** — output is plaintext.Length + 44 bytes
- [ ] **EncryptionService.EncryptAsync** — output starts with 16-byte salt, then 12-byte IV
- [ ] **EncryptionService.DecryptAsync** — round-trip: encrypt then decrypt returns original plaintext
- [ ] **EncryptionService.DecryptAsync** — wrong password throws CryptographicException
- [ ] **EncryptionService.DecryptAsync** — truncated data throws ArgumentException
- [ ] **EncryptionService.DecryptAsync** — tampered ciphertext throws CryptographicException

### Interoperability Testing

- [ ] Encrypt a file on Android app with password "test123"
- [ ] Transfer .enc file to computer
- [ ] Decrypt with Artemis Desktop using same password — should work
- [ ] Encrypt a file with Artemis Desktop with password "test123"
- [ ] Transfer .enc file to Android
- [ ] Decrypt in Android app with same password — should work
- [ ] Repeat with Web app (browser) — all three platforms should interoperate

### Desktop UI Testing

- [ ] Run `dotnet run --project Desktop/Artemis.Desktop.csproj`
- [ ] Verify window opens with Encrypt/Decrypt navigation
- [ ] Select a small text file, enter password, confirm password, click Encrypt
- [ ] Verify .enc file is saved to chosen location
- [ ] Switch to Decrypt, select the .enc file
- [ ] Enter same password, click Decrypt
- [ ] Verify original file content is restored
- [ ] Try decrypting with wrong password — should show error message
- [ ] Try encrypting with mismatched passwords — should show validation error
- [ ] Test on both Linux and Windows 11

### Error Handling

- [ ] Wrong password → user-friendly error, not a stack trace
- [ ] Non-encrypted file fed to decrypt → clear error message
- [ ] Empty password → validation prevents submission
- [ ] No file selected → validation prevents submission
- [ ] Very large file (100MB+) → UI remains responsive during encryption

---

# Reference: Source Files Across Platforms

When debugging interoperability issues, compare implementations across all platforms:

| Component | Kotlin (Android) | Web (Browser) | Desktop (C#) |
|-----------|----------|------|---------|
| Constants | `Kotlin/.../CryptoConstants.kt` | `Web/wwwroot/js/crypto/crypto-constants.js` | `Core/Cryptography/CryptoConstants.cs` |
| Key Derivation | `Kotlin/.../PasswordKeyManager.kt` | `Web/wwwroot/js/crypto/argon2-worker.js` | `Core/Cryptography/KeyDerivationService.cs` |
| Encryption | `Kotlin/.../EncryptionManager.kt` | `Web/wwwroot/js/crypto/crypto-core.js` | `Core/Cryptography/EncryptionService.cs` |
| File Handling | Android FileProvider | `Web/wwwroot/js/crypto/file-handler.js` | Avalonia StorageProvider |

---

# Reference: Key Documentation Links

| Topic | URL |
|-------|-----|
| AesGcm Class | https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.aesgcm |
| Cross-Platform Crypto | https://learn.microsoft.com/en-us/dotnet/standard/security/cross-platform-cryptography |
| Konscious Argon2 | https://github.com/kmaragon/Konscious.Security.Cryptography |
| AES-GCM in .NET (Scott Brady) | https://www.scottbrady.io/c-sharp/aes-gcm-dotnet |
| Avalonia Getting Started | https://docs.avaloniaui.net/docs/get-started/ |
| Avalonia MVVM Pattern | https://docs.avaloniaui.net/docs/concepts/the-mvvm-pattern/ |
| Avalonia Data Binding | https://docs.avaloniaui.net/docs/basics/data/data-binding/ |
| Avalonia File Picker | https://docs.avaloniaui.net/docs/concepts/services/storage-provider/file-picker |
| CommunityToolkit.Mvvm | https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/ |
| [ObservableProperty] | https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty |
| [RelayCommand] | https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/relaycommand |
