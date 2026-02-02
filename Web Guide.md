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

**Purpose:** Send password + salt to the Web Worker, get back derived key bytes, convert to CryptoKey

**This function must:**
1. Return a Promise (the worker communication is async)
2. Get the worker using your helper function
3. Set up `worker.onmessage` handler to receive the result
4. Set up `worker.onerror` handler for failures
5. Send data to worker with `worker.postMessage()`
6. When worker responds successfully, use `crypto.subtle.importKey()` to convert raw bytes to a CryptoKey

**Key concept - `crypto.subtle.importKey()`:**
```javascript
// Converts raw key bytes into a CryptoKey object
crypto.subtle.importKey(
    'raw',                              // format: we have raw bytes
    keyBytes,                           // the Uint8Array from Argon2
    { name: 'AES-GCM' },               // algorithm
    false,                              // extractable: false for security
    ['encrypt', 'decrypt']              // what operations this key can do
)
```

**Data to send to worker:**
```javascript
{
    password: password,
    salt: salt,
    config: {
        iterations: /* from constants */,
        memory: /* from constants */,
        parallelism: /* from constants */,
        keyLength: /* from constants - KEY_SIZE_BYTES */
    }
}
```

---

### Function 2: `encrypt(plaintext, password, onProgress)`

**Purpose:** Encrypt an ArrayBuffer and return format: `[Salt][IV][Ciphertext+AuthTag]`

**Compare to your Kotlin `EncryptionManager.encrypt()`** - the logic is nearly identical:

**Steps to implement:**
1. Generate random salt (16 bytes) using `crypto.getRandomValues(new Uint8Array(...))`
2. Call `deriveKey()` with password and salt (use `await`)
3. Generate random IV (12 bytes) using `crypto.getRandomValues()`
4. Call `crypto.subtle.encrypt()` with AES-GCM parameters
5. Assemble the output by concatenating salt + iv + ciphertext

**Key concept - `crypto.subtle.encrypt()`:**
```javascript
const ciphertext = await crypto.subtle.encrypt(
    {
        name: 'AES-GCM',
        iv: iv,                         // your 12-byte IV
        tagLength: 128                  // auth tag size in bits
    },
    key,                                // CryptoKey from deriveKey()
    plaintext                           // ArrayBuffer to encrypt
);
// Note: ciphertext includes the auth tag automatically appended
```

**Key concept - Concatenating Uint8Arrays:**
```javascript
// Create result array of correct total size
const result = new Uint8Array(totalSize);
// Copy salt at position 0
result.set(salt, 0);
// Copy iv at position after salt
result.set(iv, saltSize);
// Copy ciphertext at position after salt+iv
result.set(new Uint8Array(ciphertext), saltSize + ivSize);
// Return as ArrayBuffer
return result.buffer;
```

**Progress reporting:** If `onProgress` is provided, call it at key points:
- `onProgress(10, 'Generating salt...')`
- `onProgress(15, 'Deriving key...')`
- `onProgress(50, 'Encrypting...')`
- `onProgress(100, 'Done!')`

---

### Function 3: `decrypt(encrypted, password, onProgress)`

**Purpose:** Decrypt data in format `[Salt][IV][Ciphertext+AuthTag]`

**Compare to your Kotlin `EncryptionManager.decrypt()`**

**Steps to implement:**
1. Convert input to Uint8Array: `const data = new Uint8Array(encrypted)`
2. Validate minimum size (salt + iv + tag = 44 bytes minimum)
3. Extract salt: `data.slice(0, SALT_SIZE)`
4. Extract IV: `data.slice(SALT_SIZE, SALT_SIZE + IV_SIZE)`
5. Extract ciphertext: `data.slice(SALT_SIZE + IV_SIZE)`
6. Derive key using extracted salt
7. Call `crypto.subtle.decrypt()`
8. Handle errors - wrong password throws an exception

**Key concept - `crypto.subtle.decrypt()`:**
```javascript
try {
    const plaintext = await crypto.subtle.decrypt(
        {
            name: 'AES-GCM',
            iv: iv,
            tagLength: 128
        },
        key,
        ciphertext
    );
    return plaintext;
} catch (error) {
    // Auth tag verification failed = wrong password or tampered data
    throw new Error('Decryption failed. Wrong password or corrupted file.');
}
```

**Research:**
- What does `Uint8Array.slice()` do?
- What's the difference between `ArrayBuffer` and `Uint8Array`?

---

### Export your functions

Remember to `export` the functions that other modules need:
- `deriveKey` - needed by encrypt/decrypt
- `encrypt` - needed by UI
- `decrypt` - needed by UI

---

## Step 1.5: file-handler.js

**Path:** `wwwroot/js/crypto/file-handler.js`

This module handles reading files from user input and triggering downloads. You need to implement 4 functions.

### Function 1: `readFile(file, onProgress)`

**Purpose:** Read a File object (from `<input type="file">`) into an ArrayBuffer

**Concepts to research:**
- `FileReader` API - how browsers read files: https://developer.mozilla.org/en-US/docs/Web/API/FileReader
- `readAsArrayBuffer()` method - we need raw bytes, not text
- `Promise` - wrap the async FileReader in a Promise so we can use `await`
- FileReader events: `onload`, `onerror`, `onprogress`

**Function signature:**
```javascript
export function readFile(file, onProgress = null) {
    // Return a Promise that:
    // 1. Creates a FileReader
    // 2. Sets up onload to resolve with reader.result
    // 3. Sets up onerror to reject with an Error
    // 4. If onProgress provided, set up onprogress to report percentage
    // 5. Calls reader.readAsArrayBuffer(file)
}
```

**Hint:** The Promise constructor pattern:
```javascript
return new Promise((resolve, reject) => {
    // resolve(value) when successful
    // reject(error) when failed
});
```

---

### Function 2: `downloadBlob(data, filename, mimeType)`

**Purpose:** Trigger a file download in the browser from an ArrayBuffer

**Concepts to research:**
- `Blob` - binary data container: https://developer.mozilla.org/en-US/docs/Web/API/Blob
- `URL.createObjectURL()` - creates a temporary URL for a Blob
- `URL.revokeObjectURL()` - clean up when done (memory management)
- Creating and clicking an `<a>` element programmatically
- The `download` attribute on anchor elements

**Function signature:**
```javascript
export function downloadBlob(data, filename, mimeType = 'application/octet-stream') {
    // 1. Create a Blob from the data with the given mimeType
    // 2. Create an object URL for the blob
    // 3. Create an <a> element with href=url and download=filename
    // 4. Append to document, click it, remove it
    // 5. Revoke the object URL to free memory (use setTimeout)
}
```

---

### Function 3: `getEncryptedFilename(originalName)`

**Purpose:** Add `.enc` extension to a filename

**This one is simple:** Just concatenate `.enc` to the original name and return it.

---

### Function 4: `getDecryptedFilename(encryptedName)`

**Purpose:** Remove `.enc` extension, or add `decrypted_` prefix if no `.enc`

**Concepts to research:**
- `String.endsWith()` - check if string ends with a suffix
- `String.slice()` - extract part of a string (negative index removes from end)

**Logic:**
```
if filename ends with ".enc":
    return filename without the last 4 characters
else:
    return "decrypted_" + filename
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

**Purpose:** Wire up the encrypt form to your crypto functions

**Documentation to study:**
- `document.getElementById()`: https://developer.mozilla.org/en-US/docs/Web/API/Document/getElementById
- `addEventListener()`: https://developer.mozilla.org/en-US/docs/Web/API/EventTarget/addEventListener
- Form `submit` event: https://developer.mozilla.org/en-US/docs/Web/API/HTMLFormElement/submit_event
- `event.preventDefault()`: Stop form from submitting normally
- File input `change` event and `e.target.files[0]`

**Imports needed:**
```javascript
import { encrypt } from '../crypto/crypto-core.js';
import { readFile, downloadBlob, getEncryptedFilename } from '../crypto/file-handler.js';
```

**Elements to get references to:**
- form, fileInput, fileInfo, password, passwordConfirm, passwordError
- progressContainer, progressFill, progressText
- encryptBtn, errorContainer, errorMessage

**Event handlers to implement:**

1. **File input `change` handler:**
   - Store selected file in a module-level variable
   - Display filename and size in the file-info div
   - Research: How to get file size in MB? (`file.size` is in bytes)

2. **Password confirm `input` handler:**
   - Compare password and passwordConfirm values
   - Show/hide error text if they don't match

3. **Form `submit` handler (async):**
   - Call `e.preventDefault()` to stop normal form submission
   - Validate: passwords match? file selected?
   - Disable button, show progress container
   - Use try/catch/finally:
     - `try`: Read file → Encrypt → Download result
     - `catch`: Show error message
     - `finally`: Re-enable button

**Helper functions to create:**
- `updateProgress(percent, message)` - Update progress bar width and text
- `showError(message)` - Show error container with message
- `hideError()` - Hide error container

**Research:**
- How to set an element's `hidden` attribute?
- How to set CSS width as a percentage via JavaScript?
- How to disable a button via JavaScript?

---

## Step 2.6: decrypt-ui.js

**Path:** `wwwroot/js/ui/decrypt-ui.js`

**Similar to encrypt-ui.js but:**
- Import `decrypt` instead of `encrypt`
- Import `getDecryptedFilename` instead of `getEncryptedFilename`
- No password confirmation field
- Different validation (just check file and password exist)

**The submit handler flow:**
1. Read encrypted file
2. Decrypt with password
3. Download decrypted result

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
