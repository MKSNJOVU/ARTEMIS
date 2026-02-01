# SecureShare Implementation Plan

## Overview

Build an Android file encryption app using **AES-GCM** (Galois/Counter Mode) authenticated encryption with streaming support for large files.

---

## Technical Specifications

### Encryption

- **Algorithm**: AES-256-GCM
- **IV Size**: 12 bytes (96 bits) - GCM standard
- **Auth Tag**: 128 bits - appended automatically by GCM
- **Streaming**: Process files in chunks (8KB buffers)

### File Format

```
[IV: 12 bytes][Ciphertext][Auth Tag: 16 bytes]
```

### Key Management

**Mode 1: Android KeyStore (Local)**

- Hardware-backed (Titan M2, StrongBox)
- Key alias: `secret_key`
- Files only decryptable on originating device

**Mode 2: Password-Derived (Portable)**

- Algorithm: Argon2id (via Bouncy Castle)
- Memory: 64 MB
- Iterations: 3
- Parallelism: 1
- Salt: 16 bytes, stored with encrypted file
- Enables cross-device sharing

### Dependencies

```kotlin
// In app/build.gradle.kts
implementation("org.bouncycastle:bcprov-jdk18on:1.83")
```

---

## Phase 1: Core Encryption Logic

**File**: `EncryptionManager.kt`

### 1.1 Constants

```
Concepts to look up:
- KeyProperties.KEY_ALGORITHM_AES
- KeyProperties.BLOCK_MODE_GCM
- KeyProperties.ENCRYPTION_PADDING_NONE (GCM handles padding)
- Cipher transformation string format
```

### 1.2 KeyStore Key Management

```
Function: getOrCreateKeyStoreKey()

Concepts to look up:
- AndroidKeyStore provider
- KeyGenerator initialization
- KeyGenParameterSpec.Builder
- setBlockModes(), setEncryptionPaddings()
- setIsStrongBoxBacked() for Titan M2
```

### 1.3 Password Key Derivation (Argon2id)

```
Function: deriveKeyFromPassword(password: CharArray, salt: ByteArray): SecretKey

Concepts to look up:
- Argon2BytesGenerator (Bouncy Castle)
- Argon2Parameters.Builder(Argon2Parameters.ARGON2_id)
- Parameters: memory (64MB), iterations (3), parallelism (1)
- SecureRandom for salt generation
- SecretKeySpec to wrap derived bytes as AES key
```

### 1.4 Encrypt Function

```
Function: encrypt(
    inputStream: InputStream,
    outputStream: OutputStream,
    mode: KeyMode,
    password: CharArray? = null
)

Logic:
1. Get key (KeyStore or derive from password)
2. Initialize Cipher in ENCRYPT_MODE
3. Write IV to output (cipher.iv)
4. If password mode: write salt before IV
5. Read input in 8KB chunks
6. cipher.update() for each chunk, write to output
7. cipher.doFinal() for last chunk (includes auth tag)

Concepts to look up:
- Cipher.getInstance() with GCM transformation
- GCMParameterSpec
- CipherOutputStream (alternative approach)
- Buffer/chunk processing
```

### 1.5 Decrypt Function

```
Function: decrypt(
    inputStream: InputStream,
    outputStream: OutputStream,
    mode: KeyMode,
    password: CharArray? = null
)

Logic:
1. If password mode: read salt first
2. Read IV from input
3. Get key (KeyStore or derive from password+salt)
4. Initialize Cipher in DECRYPT_MODE with IV
5. Read/decrypt in chunks
6. doFinal() verifies auth tag automatically

Concepts to look up:
- GCMParameterSpec(tagLength, iv)
- AEADBadTagException (authentication failure)
- CipherInputStream (alternative approach)
```

---

## Phase 2: File Operations

**File**: `MainActivity.kt` (or separate `FileProcessor.kt`)

### 2.1 File Selection

```
Concepts to look up:
- registerForActivityResult()
- ActivityResultContracts.OpenDocument()
- Intent MIME types
- ContentResolver
```

### 2.2 Stream Handling

```
Function: processFile(uri: Uri, encrypt: Boolean)

Concepts to look up:
- contentResolver.openInputStream(uri)
- File(filesDir, "filename.enc")
- FileOutputStream
- use {} block for auto-close
```

### 2.3 User Choice Dialog

```
After encryption, prompt:
- "Keep original file"
- "Delete original file"

Concepts to look up:
- AlertDialog.Builder
- DocumentsContract.deleteDocument()
```

---

## Phase 3: UI/UX

**Files**: `activity_main.xml`, `MainActivity.kt`

### 3.1 Layout Elements

- Encrypt button
- Decrypt button
- Status TextView (feedback)
- RadioGroup for mode selection (KeyStore / Password)
- Password input field (visible when Password mode selected)

### 3.2 Click Handlers

```
Concepts to look up:
- findViewById<Button>()
- setOnClickListener {}
- Intent(Intent.ACTION_OPEN_DOCUMENT)
```

---

## Phase 4: FileProvider (Sharing)

### 4.1 Provider Paths XML

**Create**: `res/xml/provider_paths.xml`

```
Concepts to look up:
- <files-path> element
- path="." for internal files root
```

### 4.2 Manifest Entry

**Edit**: `AndroidManifest.xml`

```
Concepts to look up:
- <provider> element
- android:authorities="${applicationId}.provider"
- android:grantUriPermissions="true"
- <meta-data> for paths resource
```

### 4.3 Share Function

```
Function: shareFile(file: File)

Concepts to look up:
- FileProvider.getUriForFile()
- Intent.ACTION_SEND
- Intent.FLAG_GRANT_READ_URI_PERMISSION
- Intent.createChooser()
```

---

## Phase 5: Testing & Verification

### Manual Tests

1. Encrypt small file → decrypt → compare to original
2. Encrypt large file (100MB+) → verify no memory crash
3. KeyStore mode: encrypted file unreadable on other devices
4. Password mode: decrypt on different device with same password
5. Wrong password → verify AEADBadTagException

### Verification Commands

```bash
# Build debug APK
./gradlew assembleDebug

# Run unit tests
./gradlew test

# Run on device
./gradlew installDebug
```

---

## Session Workflow

Each coding session:

1. **I present**: Current task + concepts to look up
2. **You research**: Reference Kotlin/Android/Crypto documentation
3. **You code**: Write implementation by hand
4. **If stuck**:
   - I give hints
   - I ask for your suggestion
   - I show example block (you adapt)
5. **We verify**: Test the piece before moving on

---

## Future Scope (Not This Session)

- PostgreSQL metadata storage
- Biometric key derivation
- Retention policies (auto-purge: 24h, 3mo, 2yr)
- Progress indicator for large files

---

## Amendments

### Phase 1 Amendments

#### File Structure

Separated concerns into multiple files:

```
com/example/mkssecureshare/
├── CryptoConstants.kt      # Top-level constants (KEY_SIZE, IV_SIZE, SALT_SIZE, etc.)
├── KeyMode.kt              # Enum: KEYSTORE, PASSWORD
├── KeyStoreManager.kt      # Hardware-backed key management with StrongBox fallback
├── PasswordKeyManager.kt   # Argon2id key derivation with sensitive data clearing
├── DecryptionResults.kt    # Sealed class for decrypt success/failure handling
├── EncryptionManager.kt    # Core encrypt/decrypt functions
└── MainActivity.kt         # UI entry point (Phase 2)
```

#### File Format Update

**PASSWORD mode** prepends salt before IV:
```
[Salt: 16 bytes][IV: 12 bytes][Ciphertext][Auth Tag: 16 bytes]
```

#### 1.2 KeyStore Key Management - Amendments

- Added `ProviderException` catch for StrongBox fallback (API 24 compatibility)
- Separated into `KeyStoreManager.kt` with companion object
- `generateNewKey(useStrongBox: Boolean)` helper function

#### 1.3 Password Key Derivation - Amendments

- Separated into `PasswordKeyManager.kt`
- **Security**: Sensitive data clearing in `finally` block
  - `password.fill('\u0000')` - zeros the CharArray
  - `argonByteArray.fill(0)` - zeros the derived key bytes

#### 1.4 Encrypt Function - Amendments

- Password parameter made nullable: `password: CharArray? = null`
- Added validation: throws `IllegalArgumentException` if PASSWORD mode without password
- Added null-check for `encryptedChunk` before writing

#### 1.5 Decrypt Function - Amendments

**Signature change:**
```kotlin
fun decrypt(
    inputStream: InputStream,
    outputStream: OutputStream,
    mode: KeyMode,
    password: CharArray?,
    tempDir: File
): DecryptionResults
```

**New helper function - `readExactly()`:**
- Reads exact byte count into buffer
- Handles partial reads (InputStream.read() not guaranteed to fill buffer)
- Returns `false` on early EOF

**Hybrid buffering (security enhancement):**
- Problem: GCM auth tag only verified at `doFinal()`, but plaintext written during loop
- Solution: Buffer decrypted output, only write to real output after auth verification
- Memory buffer for files < 10MB (`MEMORY_THRESHOLD`)
- Temp file for files >= 10MB
- Temp file cleanup in `finally` block

**Result type:**
- Returns `DecryptionResults` sealed class instead of `Unit`
- `SuccessfulDecryption` object for success
- `FailedDecryption(reason: String)` data class for failures

#### New Types Added

**KeyMode.kt:**
```kotlin
enum class KeyMode {
    KEYSTORE,
    PASSWORD
}
```

**DecryptionResults.kt:**
```kotlin
sealed class DecryptionResults {
    object SuccessfulDecryption : DecryptionResults()
    data class FailedDecryption(val reason: String) : DecryptionResults()
}
```

**CryptoConstants.kt:**
- `MEMORY_THRESHOLD = 10 * 1024 * 1024` (10MB for hybrid buffering)
