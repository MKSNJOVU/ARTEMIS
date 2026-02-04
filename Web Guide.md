# MKSSecureShare Web - Zero-Knowledge Encryption Implementation Guide

## Overview

Implement a browser-based zero-knowledge file encryption web app that is fully interoperable with the existing Android/Kotlin MKSSecureShare app. Like Signal, the server **never** has access to passwords or plaintext.

**Architecture:**
- **Browser**: JavaScript handles all encryption/decryption (Web Crypto API + argon2-browser)
- **C# Backend**: ASP.NET Core MVC serves UI and stores encrypted blobs only
- **Interoperability**: Byte-identical file format with Android app

---

## File Format (Must Match Kotlin Exactly)

```
┌───────────────────────────────────────────────────────────────┐
│ Offset 0-15:  Salt (16 bytes, random, for Argon2)            │
│ Offset 16-27: IV (12 bytes, random, for AES-GCM)             │
│ Offset 28-N:  Ciphertext (variable length)                   │
│ Last 16:      Authentication Tag (128 bits, appended by GCM) │
└───────────────────────────────────────────────────────────────┘

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

## Directory Structure to Create

```
MKSSecureShare.Web/
├── Controllers/
│   ├── HomeController.cs          (existing)
│   ├── EncryptController.cs       (new)
│   └── DecryptController.cs       (new)
├── Models/
│   └── ErrorViewModel.cs          (existing)
├── Views/
│   ├── Home/                      (existing)
│   ├── Encrypt/
│   │   └── Index.cshtml           (new)
│   ├── Decrypt/
│   │   └── Index.cshtml           (new)
│   └── Shared/
│       ├── _Layout.cshtml         (modify)
│       └── Error.cshtml           (existing)
├── wwwroot/
│   ├── css/
│   │   └── site.css               (modify)
│   └── js/
│       ├── crypto/
│       │   ├── crypto-constants.js (new)
│       │   ├── crypto-core.js      (new)
│       │   ├── argon2-worker.js    (new)
│       │   └── file-handler.js     (new)
│       ├── ui/
│       │   ├── encrypt-ui.js       (new)
│       │   └── decrypt-ui.js       (new)
│       └── lib/
│           └── argon2-browser.min.js (download)
└── Program.cs                     (modify)
```

---

# Phase 1: JavaScript Crypto Module

## Step 1.1: Get argon2-browser Library

Download from: https://github.com/nicbarker/argon2-browser

Or use CDN in your HTML:
```html
<script src="https://cdn.jsdelivr.net/npm/argon2-browser@1.18.0/dist/argon2-bundled.min.js"></script>
```

For local hosting, download and save to:
`wwwroot/js/lib/argon2-browser.min.js`

---

## Step 1.2: crypto-constants.js

**Path:** `wwwroot/js/crypto/crypto-constants.js`

```javascript
/**
 * Cryptographic constants - MUST match Kotlin CryptoConstants.kt exactly
 * Any mismatch will break interoperability with Android app
 */
export const CryptoConstants = Object.freeze({
    // AES-GCM Configuration
    KEY_SIZE_BITS: 256,
    KEY_SIZE_BYTES: 32,
    SALT_SIZE: 16,           // bytes
    IV_SIZE: 12,             // bytes (96 bits, standard for GCM)
    TAG_SIZE_BITS: 128,      // bits
    TAG_SIZE_BYTES: 16,      // bytes

    // Argon2id Configuration (MUST match Kotlin PasswordKeyManager)
    ARGON2_ITERATIONS: 3,    // time cost
    ARGON2_MEMORY: 65536,    // memory cost in KB (64 MB)
    ARGON2_PARALLELISM: 1,   // lanes
    ARGON2_TYPE: 2,          // 0=Argon2d, 1=Argon2i, 2=Argon2id

    // Algorithm names for Web Crypto API
    ALGORITHM: 'AES-GCM',
    KEY_USAGE: ['encrypt', 'decrypt']
});
```

---

## Step 1.3: argon2-worker.js

**Path:** `wwwroot/js/crypto/argon2-worker.js`

This runs Argon2 in a Web Worker to prevent UI freezing (64MB memory takes ~2-3 seconds).

```javascript
/**
 * Web Worker for Argon2id key derivation
 * Runs off main thread to keep UI responsive
 */

// Import argon2-browser (adjust path as needed)
importScripts('/js/lib/argon2-browser.min.js');

self.onmessage = async function(event) {
    const { password, salt, config } = event.data;

    try {
        const result = await argon2.hash({
            pass: password,
            salt: salt,
            time: config.iterations,      // 3
            mem: config.memory,           // 65536 KB
            parallelism: config.parallelism, // 1
            hashLen: config.keyLength,    // 32 bytes for AES-256
            type: argon2.ArgonType.Argon2id
        });

        // Send back the raw hash bytes
        self.postMessage({
            success: true,
            hash: result.hash  // Uint8Array
        });
    } catch (error) {
        self.postMessage({
            success: false,
            error: error.message
        });
    }
};
```

---

## Step 1.4: crypto-core.js

**Path:** `wwwroot/js/crypto/crypto-core.js`

This is the heart of your encryption system. You need to implement 3 functions that mirror your Kotlin `EncryptionManager`.

**Documentation to study:**
- Web Crypto API: https://developer.mozilla.org/en-US/docs/Web/API/SubtleCrypto
- `crypto.subtle.encrypt()`: https://developer.mozilla.org/en-US/docs/Web/API/SubtleCrypto/encrypt
- `crypto.subtle.decrypt()`: https://developer.mozilla.org/en-US/docs/Web/API/SubtleCrypto/decrypt
- `crypto.subtle.importKey()`: https://developer.mozilla.org/en-US/docs/Web/API/SubtleCrypto/importKey
- `crypto.getRandomValues()`: https://developer.mozilla.org/en-US/docs/Web/API/Crypto/getRandomValues
- Web Workers communication: https://developer.mozilla.org/en-US/docs/Web/API/Worker/postMessage

---

### Setup: Module imports and worker management

**Concepts to implement:**
1. Import your `CryptoConstants` from the constants file
2. Create a module-level variable to hold the worker instance (starts as `null`)
3. Create a helper function `getArgon2Worker()` that:
   - Returns existing worker if already created
   - Creates new `Worker('/js/crypto/argon2-worker.js')` if not
   - This is the "lazy singleton" pattern

**Research:** How does JavaScript `import` work with ES modules?

---

### Function 1: `deriveKey(password, salt)`

```
Function: deriveKey(password, salt)

Purpose: Send password + salt to the Web Worker, receive derived key bytes,
         convert raw bytes to a CryptoKey object for use with Web Crypto API.

Logic:
1. Get worker instance using getArgon2Worker()
2. Return a new Promise (worker communication is async)
3. INSIDE the Promise:
   a. Set up worker.onmessage handler
      - Destructure response: { success, hash, error }
      - If success: convert hash to Uint8Array, call crypto.subtle.importKey()
      - importKey() returns a Promise → .then() call resolve(), .catch() call reject()
      - If not success: call reject() with error message
   b. Set up worker.onerror handler
      - Call reject() with error message
   c. Call worker.postMessage() with password, salt, and config object
      - Config contains: iterations, memory, parallelism, keyLength from constants

Concepts to look up:
- Promise constructor pattern: new Promise((resolve, reject) => { ... })
- Worker.onmessage event handler
- Worker.onerror event handler
- Worker.postMessage() for sending data to worker
- crypto.subtle.importKey() - converts raw bytes to CryptoKey
- Variable scope: resolve/reject only exist inside Promise callback
```

**Documentation:**
- https://developer.mozilla.org/en-US/docs/Web/API/SubtleCrypto/importKey
- https://developer.mozilla.org/en-US/docs/Web/API/Worker/postMessage
- https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Promise

**Code Shell:**
```javascript
export async function deriveKey(password, salt) {
    const worker = getArgon2Worker();

    return new Promise((resolve, reject) => {
        // ─────────────────────────────────────────────
        // Step 3a: Handle successful response from worker
        // ─────────────────────────────────────────────
        worker.onmessage = function(event) {
            const { success, hash, error } = event.data;

            if (success) {
                const keyBytes = new Uint8Array(hash);

                crypto.subtle.importKey(
                    'raw',
                    keyBytes,
                    { name: /* TODO: algorithm from constants */ },
                    false,
                    /* TODO: key usage array from constants */
                )
                .then(function(cryptoKey) {
                    resolve(cryptoKey);
                })
                .catch(function(err) {
                    reject(new Error('Failed to import key: ' + err.message));
                });
            } else {
                reject(new Error('Argon2 failed: ' + error));
            }
        };

        // ─────────────────────────────────────────────
        // Step 3b: Handle worker crash/error
        // ─────────────────────────────────────────────
        worker.onerror = function(event) {
            reject(new Error('Worker error: ' + event.message));
        };

        // ─────────────────────────────────────────────
        // Step 3c: Send work to the worker
        // ─────────────────────────────────────────────
        worker.postMessage({
            password: password,
            salt: salt,
            config: {
                iterations: /* TODO */,
                memory: /* TODO */,
                parallelism: /* TODO */,
                keyLength: /* TODO */
            }
        });
    });
}
```

---

### Function 2: `encrypt(plaintext, password, onProgress)`

```
Function: encrypt(plaintext, password, onProgress = null)

Purpose: Encrypt an ArrayBuffer using AES-256-GCM with password-derived key.
         Output format: [Salt: 16 bytes][IV: 12 bytes][Ciphertext][AuthTag: 16 bytes]

         Compare to your Kotlin EncryptionManager.encrypt() - the logic is nearly identical.

Logic:
1. Generate random salt (16 bytes) using crypto.getRandomValues()
2. Derive AES key from password + salt using deriveKey() (await it)
3. Generate random IV (12 bytes) using crypto.getRandomValues()
4. Encrypt plaintext using crypto.subtle.encrypt() with AES-GCM parameters
   - Note: GCM automatically appends auth tag to ciphertext
5. Assemble output buffer: concatenate salt + iv + ciphertext
   - Create Uint8Array of total size
   - Use .set(array, offset) to copy each piece at correct position
6. Return result.buffer (ArrayBuffer)

Progress reporting (if onProgress provided):
- 10%: "Generating salt..."
- 15%: "Deriving key..."
- 50%: "Encrypting..."
- 90%: "Assembling output..."
- 100%: "Done!"

Concepts to look up:
- crypto.getRandomValues() - cryptographically secure random bytes
- crypto.subtle.encrypt() - Web Crypto encryption
- Uint8Array.set(array, offset) - copy array at position
- ArrayBuffer vs Uint8Array - buffer is raw memory, Uint8Array is a view
```

**Documentation:**
- https://developer.mozilla.org/en-US/docs/Web/API/SubtleCrypto/encrypt
- https://developer.mozilla.org/en-US/docs/Web/API/Crypto/getRandomValues
- https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Uint8Array/set

**Code Shell:**
```javascript
export async function encrypt(plaintext, password, onProgress = null) {
    // ─────────────────────────────────────────────
    // Step 1: Generate random salt
    // ─────────────────────────────────────────────
    const salt = crypto.getRandomValues(new Uint8Array(/* TODO: SALT_SIZE */));

    if (onProgress) onProgress(10, 'Generating salt...');

    // ─────────────────────────────────────────────
    // Step 2: Derive key from password + salt
    // ─────────────────────────────────────────────
    if (onProgress) onProgress(15, 'Deriving key...');
    const key = await deriveKey(password, salt);

    // ─────────────────────────────────────────────
    // Step 3: Generate random IV
    // ─────────────────────────────────────────────
    const iv = crypto.getRandomValues(new Uint8Array(/* TODO: IV_SIZE */));

    if (onProgress) onProgress(50, 'Encrypting...');

    // ─────────────────────────────────────────────
    // Step 4: Encrypt using AES-GCM
    // ─────────────────────────────────────────────
    const ciphertext = await crypto.subtle.encrypt(
        {
            name: /* TODO: ALGORITHM */,
            iv: iv,
            tagLength: /* TODO: TAG_SIZE */
        },
        key,
        plaintext
    );

    if (onProgress) onProgress(90, 'Assembling output...');

    // ─────────────────────────────────────────────
    // Step 5: Assemble output [salt][iv][ciphertext]
    // ─────────────────────────────────────────────
    const result = new Uint8Array(
        /* TODO: SALT_SIZE + IV_SIZE + ciphertext.byteLength */
    );

    result.set(salt, 0);
    result.set(iv, /* TODO: offset after salt */);
    result.set(new Uint8Array(ciphertext), /* TODO: offset after salt + iv */);

    if (onProgress) onProgress(100, 'Done!');

    return result.buffer;
}
```

---

### Function 3: `decrypt(encrypted, password, onProgress)`

```
Function: decrypt(encrypted, password, onProgress = null)

Purpose: Decrypt data in format [Salt][IV][Ciphertext+AuthTag]
         Returns the original plaintext as ArrayBuffer.

         Compare to your Kotlin EncryptionManager.decrypt() - the logic is nearly identical.

Logic:
1. Convert encrypted ArrayBuffer to Uint8Array (needed for slicing)
2. Validate minimum size: salt(16) + iv(12) + tag(16) = 44 bytes minimum
   - If too short, throw Error
3. Extract salt: slice bytes 0 to SALT_SIZE
4. Extract IV: slice bytes SALT_SIZE to (SALT_SIZE + IV_SIZE)
5. Extract ciphertext: slice bytes from (SALT_SIZE + IV_SIZE) to end
6. Derive key using deriveKey(password, salt) - same salt that was used to encrypt
7. Decrypt using crypto.subtle.decrypt() with AES-GCM parameters
   - MUST wrap in try/catch
   - If auth tag fails (wrong password), crypto.subtle.decrypt() throws
   - Catch and throw user-friendly error message

Progress reporting (if onProgress provided):
- 10%: "Extracted salt..."
- 15%: "Deriving key..."
- 50%: "Decrypting..."
- 100%: "Done!"

Concepts to look up:
- Uint8Array.slice(start, end) - extracts portion of array
- crypto.subtle.decrypt() - Web Crypto decryption
- try/catch for handling decryption failures
- GCM auth tag verification happens automatically in decrypt()
```

**Documentation:**
- https://developer.mozilla.org/en-US/docs/Web/API/SubtleCrypto/decrypt
- https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Uint8Array/slice

**Code Shell:**
```javascript
export async function decrypt(encrypted, password, onProgress = null) {
    // ─────────────────────────────────────────────
    // Step 1: Convert to Uint8Array for slicing
    // ─────────────────────────────────────────────
    const data = new Uint8Array(encrypted);

    // ─────────────────────────────────────────────
    // Step 2: Validate minimum size
    // ─────────────────────────────────────────────
    if (data.length < 44) {
        throw new Error('Invalid encrypted data: too short');
    }

    // ─────────────────────────────────────────────
    // Step 3: Extract salt (first 16 bytes)
    // ─────────────────────────────────────────────
    const salt = data.slice(0, /* TODO: SALT_SIZE */);

    if (onProgress) onProgress(10, 'Extracted salt...');

    // ─────────────────────────────────────────────
    // Step 4: Extract IV (next 12 bytes)
    // ─────────────────────────────────────────────
    const iv = data.slice(
        /* TODO: start after salt */,
        /* TODO: end after salt + iv */
    );

    // ─────────────────────────────────────────────
    // Step 5: Extract ciphertext + auth tag (remainder)
    // ─────────────────────────────────────────────
    const ciphertext = data.slice(/* TODO: start after salt + iv */);

    if (onProgress) onProgress(15, 'Deriving key...');

    // ─────────────────────────────────────────────
    // Step 6: Derive key using extracted salt
    // ─────────────────────────────────────────────
    const key = await deriveKey(password, salt);

    if (onProgress) onProgress(50, 'Decrypting...');

    // ─────────────────────────────────────────────
    // Step 7: Decrypt with try/catch for auth failure
    // ─────────────────────────────────────────────
    try {
        const plaintext = await crypto.subtle.decrypt(
            {
                name: /* TODO: ALGORITHM */,
                iv: iv,
                tagLength: /* TODO: TAG_SIZE */
            },
            key,
            ciphertext
        );

        if (onProgress) onProgress(100, 'Done!');

        return plaintext;
    } catch (error) {
        // Auth tag verification failed = wrong password or tampered data
        throw new Error('Decryption failed. Wrong password or corrupted file.');
    }
}
```

---

### Export your functions

Remember to `export` the functions that other modules need:
- `deriveKey` - needed by encrypt/decrypt
- `encrypt` - needed by UI
- `decrypt` - needed by UI

---

## Step 1.5: file-handler.js

**Path:** `wwwroot/js/crypto/file-handler.js`

This module handles reading files from user input and triggering downloads.

---

### Function 1: `readFile(file, onProgress)`

```
Function: readFile(file, onProgress = null)

Purpose: Read a File object (from <input type="file">) into an ArrayBuffer.
         Uses FileReader API wrapped in a Promise for async/await compatibility.

Logic:
1. Return a new Promise
2. Inside Promise: create a FileReader instance
3. Set up reader.onload handler
   - When file is read, resolve() with reader.result
4. Set up reader.onerror handler
   - If read fails, reject() with an Error
5. If onProgress provided, set up reader.onprogress handler
   - Calculate percentage: (event.loaded / event.total) * 100
   - Call onProgress(percent, 'Reading file...')
6. Call reader.readAsArrayBuffer(file) to start reading

Concepts to look up:
- FileReader API and its event handlers
- FileReader.readAsArrayBuffer() method
- Promise constructor pattern
- ProgressEvent for tracking load progress
```

**Documentation:**
- https://developer.mozilla.org/en-US/docs/Web/API/FileReader
- https://developer.mozilla.org/en-US/docs/Web/API/FileReader/readAsArrayBuffer

**Code Shell:**
```javascript
export function readFile(file, onProgress = null) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();

        reader.onload = function() {
            // TODO: resolve with reader.result
        };

        reader.onerror = function() {
            // TODO: reject with an Error
        };

        if (onProgress) {
            reader.onprogress = function(event) {
                if (event.lengthComputable) {
                    // TODO: calculate percent, call onProgress
                }
            };
        }

        // TODO: start reading the file
    });
}
```

---

### Function 2: `downloadBlob(data, filename, mimeType)`

```
Function: downloadBlob(data, filename, mimeType = 'application/octet-stream')

Purpose: Trigger a file download in the browser from an ArrayBuffer.
         Creates a temporary link, clicks it, then cleans up.

Logic:
1. Create a Blob from the data with the given mimeType
2. Create an object URL for the blob using URL.createObjectURL()
3. Create an <a> element
4. Set href to the object URL
5. Set download attribute to the filename
6. Append link to document body
7. Programmatically click the link
8. Remove link from document body
9. Revoke the object URL after a short delay (memory cleanup)

Concepts to look up:
- Blob constructor
- URL.createObjectURL() and URL.revokeObjectURL()
- Creating elements with document.createElement()
- HTMLAnchorElement download attribute
- setTimeout for delayed cleanup
```

**Documentation:**
- https://developer.mozilla.org/en-US/docs/Web/API/Blob
- https://developer.mozilla.org/en-US/docs/Web/API/URL/createObjectURL

**Code Shell:**
```javascript
export function downloadBlob(data, filename, mimeType = 'application/octet-stream') {
    // Step 1: Create Blob
    const blob = new Blob([data], { type: mimeType });

    // Step 2: Create object URL
    const url = URL.createObjectURL(blob);

    // Steps 3-5: Create and configure link
    const link = document.createElement('a');
    // TODO: set link.href
    // TODO: set link.download

    // Steps 6-7: Add to DOM and click
    // TODO: appendChild, click()

    // Step 8: Remove from DOM
    // TODO: removeChild

    // Step 9: Cleanup after delay
    setTimeout(function() {
        // TODO: revoke the URL
    }, 100);
}
```

---

### Function 3: `getEncryptedFilename(originalName)`

```
Function: getEncryptedFilename(originalName)

Purpose: Add .enc extension to a filename.

Logic:
1. Concatenate ".enc" to the original name
2. Return the result

Example: "document.pdf" → "document.pdf.enc"
```

---

### Function 4: `getDecryptedFilename(encryptedName)`

```
Function: getDecryptedFilename(encryptedName)

Purpose: Remove .enc extension from filename, or add prefix if no .enc

Logic:
1. Check if filename ends with ".enc"
2. If yes: return filename without the last 4 characters
3. If no: return "decrypted_" + filename

Concepts to look up:
- String.endsWith() method
- String.slice() with negative index

Example: "document.pdf.enc" → "document.pdf"
Example: "unknown_file" → "decrypted_unknown_file"
```

**Code Shell:**
```javascript
export function getDecryptedFilename(encryptedName) {
    if (encryptedName.endsWith('.enc')) {
        // TODO: return without last 4 characters
    }
    // TODO: return with prefix
}
```

---

# Phase 2: UI Layer

## Step 2.1: EncryptController.cs

**Path:** `Controllers/EncryptController.cs`

**Research:** ASP.NET Core MVC Controllers
- https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/actions

**What to implement:**
- A simple controller class that inherits from `Controller`
- One action method `Index()` that returns `View()`
- Namespace should match your project: `MKSSecureShare.Web.Controllers`

**This is identical to your existing HomeController** - use it as a reference.

---

## Step 2.2: DecryptController.cs

**Path:** `Controllers/DecryptController.cs`

Same pattern as EncryptController - create a controller with an `Index()` action.

---

## Step 2.3: Views/Encrypt/Index.cshtml

**Path:** `Views/Encrypt/Index.cshtml`

**Research:** Razor syntax and HTML forms
- https://learn.microsoft.com/en-us/aspnet/core/mvc/views/razor

**Elements you need to create:**

1. **Page title** - Set `ViewData["Title"]` in a Razor code block

2. **Security notice** - A paragraph explaining that encryption happens in the browser

3. **Form with id="encrypt-form"** containing:
   - File input (`<input type="file" id="file-input">`)
   - A div with `id="file-info"` to show selected filename
   - Password input (`<input type="password" id="password">`)
   - Password confirmation input (`id="password-confirm"`)
   - A div with `id="password-error"` for validation messages
   - Progress bar container (hidden by default) with:
     - `id="progress-container"`
     - Inner div `id="progress-fill"` for the animated bar
     - Text div `id="progress-text"` for status messages
   - Submit button `id="encrypt-btn"`

4. **Error container** - Hidden div `id="error-container"` with `id="error-message"` inside

5. **Scripts section** - Load your encrypt-ui.js as a module:
```html
@section Scripts {
    <script type="module" src="~/js/ui/encrypt-ui.js"></script>
}
```

**Research:** What does `type="module"` do for script tags?

---

## Step 2.4: Views/Decrypt/Index.cshtml

**Path:** `Views/Decrypt/Index.cshtml`

Similar to Encrypt view but simpler:
- File input for `.enc` files (use `accept=".enc"` attribute)
- Single password field (no confirmation needed)
- Progress bar and error containers

**Research:** What does the `accept` attribute do on file inputs?

---

## Step 2.5: encrypt-ui.js

**Path:** `wwwroot/js/ui/encrypt-ui.js`

```
File: encrypt-ui.js

Purpose: Wire up the encrypt form to the crypto functions.
         Handles user interaction: file selection, password validation,
         progress display, and triggering the download.

Imports needed:
- encrypt from crypto-core.js
- readFile, downloadBlob, getEncryptedFilename from file-handler.js

Module structure:
1. Import statements
2. Get references to DOM elements (getElementById)
3. Module-level variable for selected file
4. Event handlers
5. Helper functions

DOM Elements to reference:
- form (id="encrypt-form")
- fileInput (id="file-input")
- fileInfo (id="file-info")
- password (id="password")
- passwordConfirm (id="password-confirm")
- passwordError (id="password-error")
- progressContainer (id="progress-container")
- progressFill (id="progress-fill")
- progressText (id="progress-text")
- encryptBtn (id="encrypt-btn")
- errorContainer (id="error-container")
- errorMessage (id="error-message")
```

---

### Event Handler 1: File Input Change

```
Event: fileInput 'change'

Purpose: When user selects a file, store it and display info.

Logic:
1. Get selected file from e.target.files[0]
2. Store in module-level variable (let selectedFile)
3. If file exists:
   - Calculate size in MB: file.size / (1024 * 1024)
   - Display filename and size in fileInfo element
4. If no file: clear fileInfo text

Concepts to look up:
- File object properties: name, size
- Number.toFixed() for decimal places
- Template literals: `${variable}`
- Element.textContent property
```

---

### Event Handler 2: Password Confirm Input

```
Event: passwordConfirm 'input'

Purpose: Real-time validation that passwords match.

Logic:
1. Compare password.value to passwordConfirm.value
2. If they don't match: show error text in passwordError
3. If they match: clear passwordError text

Concepts to look up:
- Input element .value property
- Comparing strings in JavaScript
```

---

### Event Handler 3: Form Submit

```
Event: form 'submit' (async handler)

Purpose: Handle the encryption process when user clicks Encrypt.

Logic:
1. Call e.preventDefault() - stop normal form submission
2. Call hideError() - clear any previous errors
3. Validate:
   - If passwords don't match: showError() and return
   - If no file selected: showError() and return
4. Disable the encrypt button (encryptBtn.disabled = true)
5. Show progress container (progressContainer.hidden = false)
6. Try block:
   a. Update progress (5%, 'Reading file...')
   b. Read file: const plaintext = await readFile(selectedFile)
   c. Encrypt: const encrypted = await encrypt(plaintext, password.value, updateProgress)
   d. Generate filename: getEncryptedFilename(selectedFile.name)
   e. Trigger download: downloadBlob(encrypted, filename)
   f. Update progress (100%, 'Complete!')
7. Catch block:
   - Call showError() with error message
   - console.error(error) for debugging
8. Finally block:
   - Re-enable button (encryptBtn.disabled = false)

Concepts to look up:
- async function and await keyword
- try/catch/finally pattern
- Event.preventDefault()
- HTMLButtonElement.disabled property
- HTMLElement.hidden property
```

---

### Helper Functions

```
Function: updateProgress(percent, message)
Purpose: Update the progress bar and status text.
Logic:
- Set progressFill.style.width = percent + '%'
- Set progressText.textContent = message

Function: showError(message)
Purpose: Display error message to user.
Logic:
- Set errorContainer.hidden = false
- Set errorMessage.textContent = message

Function: hideError()
Purpose: Hide the error container.
Logic:
- Set errorContainer.hidden = true
```

**Documentation:**
- https://developer.mozilla.org/en-US/docs/Web/API/Document/getElementById
- https://developer.mozilla.org/en-US/docs/Web/API/EventTarget/addEventListener
- https://developer.mozilla.org/en-US/docs/Web/API/HTMLFormElement/submit_event

---

## Step 2.6: decrypt-ui.js

**Path:** `wwwroot/js/ui/decrypt-ui.js`

```
File: decrypt-ui.js

Purpose: Wire up the decrypt form to the crypto functions.
         Similar to encrypt-ui.js but simpler (no password confirmation).

Imports needed:
- decrypt from crypto-core.js
- readFile, downloadBlob, getDecryptedFilename from file-handler.js

DOM Elements to reference:
- form (id="decrypt-form")
- fileInput (id="encrypted-file")
- fileInfo (id="file-info")
- password (id="password")
- progressContainer, progressFill, progressText
- decryptBtn (id="decrypt-btn")
- errorContainer, errorMessage
```

---

### Event Handler 1: File Input Change

```
Event: fileInput 'change'

Purpose: When user selects encrypted file, store it and display info.

Logic: Same as encrypt-ui.js
1. Store file from e.target.files[0]
2. Display filename and size
```

---

### Event Handler 2: Form Submit

```
Event: form 'submit' (async handler)

Purpose: Handle the decryption process.

Logic:
1. e.preventDefault()
2. hideError()
3. Validate:
   - If no file selected: showError() and return
   - If no password: showError() and return
4. Disable button, show progress
5. Try block:
   a. Read file: const encrypted = await readFile(selectedFile)
   b. Decrypt: const plaintext = await decrypt(encrypted, password.value, updateProgress)
   c. Generate filename: getDecryptedFilename(selectedFile.name)
   d. Download: downloadBlob(plaintext, filename)
6. Catch block:
   - showError(error.message) - will show "Wrong password or corrupted file"
7. Finally block:
   - Re-enable button
```

---

### Helper Functions

Same as encrypt-ui.js:
- updateProgress(percent, message)
- showError(message)
- hideError()

---

## Step 2.7: Update _Layout.cshtml

**Path:** `Views/Shared/_Layout.cshtml`

**What to add:** Navigation links to Encrypt and Decrypt pages

**Research:** ASP.NET Core Tag Helpers for links
- `asp-controller` attribute
- `asp-action` attribute

Add to your existing navigation:
- Link to Encrypt controller, Index action
- Link to Decrypt controller, Index action

---

## Step 2.8: Update site.css

**Path:** `wwwroot/css/site.css`

**CSS concepts to implement:**

1. **Container styling** (`.crypto-container`)
   - Max width, centered with auto margins, padding

2. **Security note** (`.security-note`)
   - Light green background, left border accent, padding

3. **Form styling** (`.crypto-form`, `.form-group`)
   - Background color, padding, border-radius
   - Label styling (block display, margin, font-weight)
   - Input styling (full width, padding, border, border-radius)

4. **Progress bar** (`.progress-container`, `.progress-bar`, `.progress-fill`)
   - Fixed height bar with background
   - Inner fill div that changes width (use CSS `transition` for animation)

5. **Button styling** (`.btn-primary`)
   - Background color, text color, padding, border-radius
   - `:hover` state (darker background)
   - `:disabled` state (lighter background, different cursor)

6. **Error styling** (`.error-container`, `.error-text`)
   - Red/pink background for container
   - Red text color

**Research:**
- CSS `transition` property for smooth animations
- CSS `:hover` and `:disabled` pseudo-classes

---

## Step 2.9: Update Program.cs

**Path:** `Program.cs`

**Purpose:** Add security headers so the browser allows WASM and Web Workers

**Research:**
- Content Security Policy (CSP): https://developer.mozilla.org/en-US/docs/Web/HTTP/CSP
- ASP.NET Core Middleware: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware

**What to add:** Middleware that sets response headers

**Headers needed:**
1. `Content-Security-Policy` - Must allow:
   - `'self'` for default sources
   - `'wasm-unsafe-eval'` for script-src (Argon2 WASM)
   - `'self' blob:` for worker-src (Web Workers)
   - `'unsafe-inline'` for style-src (or use nonces)

2. `X-Content-Type-Options: nosniff`
3. `X-Frame-Options: DENY`
4. `Referrer-Policy: strict-origin-when-cross-origin`

**Middleware pattern:**
```csharp
app.Use(async (context, next) =>
{
    // Add headers here using context.Response.Headers.Append()
    await next();
});
```

**Important:** Add this middleware BEFORE `app.UseStaticFiles()`

---

# Phase 3: Testing

## Verification Checklist

### Local Testing
- [ ] Run `dotnet run` and navigate to https://localhost:5001
- [ ] Go to Encrypt page, select a small text file
- [ ] Enter password, confirm password, click Encrypt
- [ ] Verify .enc file downloads
- [ ] Go to Decrypt page, upload the .enc file
- [ ] Enter same password, click Decrypt
- [ ] Verify original file downloads with correct content

### Interoperability Testing
- [ ] Encrypt a file on Android app with password "test123"
- [ ] Transfer .enc file to computer
- [ ] Decrypt in browser with same password - should work
- [ ] Encrypt a file in browser with password "test123"
- [ ] Transfer .enc file to Android
- [ ] Decrypt in Android app with same password - should work

### Error Handling
- [ ] Try decrypting with wrong password - should show error
- [ ] Try decrypting a non-encrypted file - should show error
- [ ] Try with mismatched passwords on encrypt - should show error

---

# Reference: Kotlin Source Files

When debugging interoperability issues, compare against these Kotlin files:

| File | Path |
|------|------|
| Constants | `Kotlin/app/src/main/java/com/example/mkssecureshare/CryptoConstants.kt` |
| Encryption | `Kotlin/app/src/main/java/com/example/mkssecureshare/EncryptionManager.kt` |
| Key Derivation | `Kotlin/app/src/main/java/com/example/mkssecureshare/PasswordKeyManager.kt` |
