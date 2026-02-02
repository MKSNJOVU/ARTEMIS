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

```javascript
/**
 * Core encryption/decryption module
 * Uses Web Crypto API for AES-256-GCM
 * Uses Web Worker for Argon2id key derivation
 */

import { CryptoConstants } from './crypto-constants.js';

// Create worker instance
let argon2Worker = null;

function getArgon2Worker() {
    if (!argon2Worker) {
        argon2Worker = new Worker('/js/crypto/argon2-worker.js');
    }
    return argon2Worker;
}

/**
 * Derive AES-256 key from password using Argon2id
 * @param {string} password - User's password
 * @param {Uint8Array} salt - 16-byte salt
 * @returns {Promise<CryptoKey>} - AES-GCM key for Web Crypto API
 */
export async function deriveKey(password, salt) {
    return new Promise((resolve, reject) => {
        const worker = getArgon2Worker();

        worker.onmessage = async (event) => {
            if (event.data.success) {
                // Import the raw key bytes into Web Crypto API
                try {
                    const cryptoKey = await crypto.subtle.importKey(
                        'raw',
                        event.data.hash,
                        { name: CryptoConstants.ALGORITHM },
                        false,  // not extractable
                        CryptoConstants.KEY_USAGE
                    );
                    resolve(cryptoKey);
                } catch (err) {
                    reject(new Error('Failed to import key: ' + err.message));
                }
            } else {
                reject(new Error('Argon2 failed: ' + event.data.error));
            }
        };

        worker.onerror = (error) => {
            reject(new Error('Worker error: ' + error.message));
        };

        // Send work to the worker
        worker.postMessage({
            password: password,
            salt: salt,
            config: {
                iterations: CryptoConstants.ARGON2_ITERATIONS,
                memory: CryptoConstants.ARGON2_MEMORY,
                parallelism: CryptoConstants.ARGON2_PARALLELISM,
                keyLength: CryptoConstants.KEY_SIZE_BYTES
            }
        });
    });
}

/**
 * Encrypt data with AES-256-GCM
 * Output format: [Salt: 16][IV: 12][Ciphertext][AuthTag: 16]
 *
 * @param {ArrayBuffer} plaintext - Data to encrypt
 * @param {string} password - User's password
 * @param {Function} onProgress - Optional progress callback (0-100)
 * @returns {Promise<ArrayBuffer>} - Encrypted blob
 */
export async function encrypt(plaintext, password, onProgress = null) {
    // 1. Generate random salt (16 bytes)
    const salt = crypto.getRandomValues(new Uint8Array(CryptoConstants.SALT_SIZE));

    if (onProgress) onProgress(10, 'Generating salt...');

    // 2. Derive key using Argon2id (this takes ~2-3 seconds)
    if (onProgress) onProgress(15, 'Deriving key (this takes a moment)...');
    const key = await deriveKey(password, salt);

    if (onProgress) onProgress(50, 'Key derived, encrypting...');

    // 3. Generate random IV (12 bytes)
    const iv = crypto.getRandomValues(new Uint8Array(CryptoConstants.IV_SIZE));

    // 4. Encrypt using AES-256-GCM
    const ciphertext = await crypto.subtle.encrypt(
        {
            name: CryptoConstants.ALGORITHM,
            iv: iv,
            tagLength: CryptoConstants.TAG_SIZE_BITS
        },
        key,
        plaintext
    );

    if (onProgress) onProgress(90, 'Assembling output...');

    // 5. Assemble output: salt + iv + ciphertext (which includes auth tag)
    const result = new Uint8Array(
        CryptoConstants.SALT_SIZE +
        CryptoConstants.IV_SIZE +
        ciphertext.byteLength
    );

    result.set(salt, 0);
    result.set(iv, CryptoConstants.SALT_SIZE);
    result.set(new Uint8Array(ciphertext), CryptoConstants.SALT_SIZE + CryptoConstants.IV_SIZE);

    if (onProgress) onProgress(100, 'Done!');

    return result.buffer;
}

/**
 * Decrypt data with AES-256-GCM
 * Input format: [Salt: 16][IV: 12][Ciphertext][AuthTag: 16]
 *
 * @param {ArrayBuffer} encrypted - Encrypted blob
 * @param {string} password - User's password
 * @param {Function} onProgress - Optional progress callback (0-100)
 * @returns {Promise<ArrayBuffer>} - Decrypted plaintext
 * @throws {Error} - If authentication fails (wrong password or tampered data)
 */
export async function decrypt(encrypted, password, onProgress = null) {
    const data = new Uint8Array(encrypted);

    // Minimum size: salt(16) + iv(12) + tag(16) = 44 bytes
    if (data.length < 44) {
        throw new Error('Invalid encrypted data: too short');
    }

    // 1. Extract salt (first 16 bytes)
    const salt = data.slice(0, CryptoConstants.SALT_SIZE);

    if (onProgress) onProgress(10, 'Extracted salt...');

    // 2. Extract IV (next 12 bytes)
    const iv = data.slice(
        CryptoConstants.SALT_SIZE,
        CryptoConstants.SALT_SIZE + CryptoConstants.IV_SIZE
    );

    // 3. Extract ciphertext + auth tag (remainder)
    const ciphertext = data.slice(CryptoConstants.SALT_SIZE + CryptoConstants.IV_SIZE);

    if (onProgress) onProgress(15, 'Deriving key (this takes a moment)...');

    // 4. Derive key using Argon2id with extracted salt
    const key = await deriveKey(password, salt);

    if (onProgress) onProgress(50, 'Key derived, decrypting...');

    // 5. Decrypt using AES-256-GCM
    try {
        const plaintext = await crypto.subtle.decrypt(
            {
                name: CryptoConstants.ALGORITHM,
                iv: iv,
                tagLength: CryptoConstants.TAG_SIZE_BITS
            },
            key,
            ciphertext
        );

        if (onProgress) onProgress(100, 'Done!');

        return plaintext;
    } catch (error) {
        // Web Crypto throws generic error on auth failure
        throw new Error('Decryption failed. Wrong password or corrupted file.');
    }
}
```

---

## Step 1.5: file-handler.js

**Path:** `wwwroot/js/crypto/file-handler.js`

```javascript
/**
 * File handling utilities for browser
 */

/**
 * Read a File object into an ArrayBuffer
 * @param {File} file - File from input element
 * @param {Function} onProgress - Progress callback (0-100)
 * @returns {Promise<ArrayBuffer>}
 */
export function readFile(file, onProgress = null) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();

        reader.onload = () => resolve(reader.result);
        reader.onerror = () => reject(new Error('Failed to read file'));

        if (onProgress) {
            reader.onprogress = (event) => {
                if (event.lengthComputable) {
                    const percent = Math.round((event.loaded / event.total) * 100);
                    onProgress(percent, 'Reading file...');
                }
            };
        }

        reader.readAsArrayBuffer(file);
    });
}

/**
 * Trigger download of a blob
 * @param {ArrayBuffer} data - File data
 * @param {string} filename - Suggested filename
 * @param {string} mimeType - MIME type (default: application/octet-stream)
 */
export function downloadBlob(data, filename, mimeType = 'application/octet-stream') {
    const blob = new Blob([data], { type: mimeType });
    const url = URL.createObjectURL(blob);

    const link = document.createElement('a');
    link.href = url;
    link.download = filename;

    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);

    // Clean up
    setTimeout(() => URL.revokeObjectURL(url), 100);
}

/**
 * Get filename with .enc extension for encrypted files
 * @param {string} originalName - Original filename
 * @returns {string} - Filename with .enc extension
 */
export function getEncryptedFilename(originalName) {
    return originalName + '.enc';
}

/**
 * Get original filename by removing .enc extension
 * @param {string} encryptedName - Encrypted filename
 * @returns {string} - Original filename
 */
export function getDecryptedFilename(encryptedName) {
    if (encryptedName.endsWith('.enc')) {
        return encryptedName.slice(0, -4);
    }
    return 'decrypted_' + encryptedName;
}
```

---

# Phase 2: UI Layer

## Step 2.1: EncryptController.cs

**Path:** `Controllers/EncryptController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;

namespace MKSSecureShare.Web.Controllers;

public class EncryptController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
```

---

## Step 2.2: DecryptController.cs

**Path:** `Controllers/DecryptController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;

namespace MKSSecureShare.Web.Controllers;

public class DecryptController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
```

---

## Step 2.3: Views/Encrypt/Index.cshtml

**Path:** `Views/Encrypt/Index.cshtml`

```html
@{
    ViewData["Title"] = "Encrypt File";
}

<div class="crypto-container">
    <h1>Encrypt a File</h1>
    <p class="security-note">
        All encryption happens in your browser. Your password never leaves this device.
    </p>

    <form id="encrypt-form" class="crypto-form">
        <div class="form-group">
            <label for="file-input">Select File</label>
            <input type="file" id="file-input" required />
            <div id="file-info" class="file-info"></div>
        </div>

        <div class="form-group">
            <label for="password">Password</label>
            <input type="password" id="password" required minlength="1"
                   autocomplete="new-password" />
        </div>

        <div class="form-group">
            <label for="password-confirm">Confirm Password</label>
            <input type="password" id="password-confirm" required />
            <div id="password-error" class="error-text"></div>
        </div>

        <div id="progress-container" class="progress-container" hidden>
            <div class="progress-bar">
                <div id="progress-fill" class="progress-fill"></div>
            </div>
            <div id="progress-text" class="progress-text">Ready</div>
        </div>

        <div class="form-actions">
            <button type="submit" id="encrypt-btn" class="btn-primary">
                Encrypt
            </button>
        </div>
    </form>

    <div id="error-container" class="error-container" hidden>
        <p id="error-message"></p>
    </div>
</div>

@section Scripts {
    <script type="module" src="~/js/ui/encrypt-ui.js"></script>
}
```

---

## Step 2.4: Views/Decrypt/Index.cshtml

**Path:** `Views/Decrypt/Index.cshtml`

```html
@{
    ViewData["Title"] = "Decrypt File";
}

<div class="crypto-container">
    <h1>Decrypt a File</h1>
    <p class="security-note">
        Decryption happens entirely in your browser. Your password is never sent anywhere.
    </p>

    <form id="decrypt-form" class="crypto-form">
        <div class="form-group">
            <label for="encrypted-file">Encrypted File (.enc)</label>
            <input type="file" id="encrypted-file" accept=".enc" required />
            <div id="file-info" class="file-info"></div>
        </div>

        <div class="form-group">
            <label for="password">Password</label>
            <input type="password" id="password" required
                   autocomplete="current-password" />
        </div>

        <div id="progress-container" class="progress-container" hidden>
            <div class="progress-bar">
                <div id="progress-fill" class="progress-fill"></div>
            </div>
            <div id="progress-text" class="progress-text">Ready</div>
        </div>

        <div class="form-actions">
            <button type="submit" id="decrypt-btn" class="btn-primary">
                Decrypt
            </button>
        </div>
    </form>

    <div id="error-container" class="error-container" hidden>
        <p id="error-message"></p>
    </div>
</div>

@section Scripts {
    <script type="module" src="~/js/ui/decrypt-ui.js"></script>
}
```

---

## Step 2.5: encrypt-ui.js

**Path:** `wwwroot/js/ui/encrypt-ui.js`

```javascript
/**
 * Encrypt page UI handler
 */

import { encrypt } from '../crypto/crypto-core.js';
import { readFile, downloadBlob, getEncryptedFilename } from '../crypto/file-handler.js';

const form = document.getElementById('encrypt-form');
const fileInput = document.getElementById('file-input');
const fileInfo = document.getElementById('file-info');
const password = document.getElementById('password');
const passwordConfirm = document.getElementById('password-confirm');
const passwordError = document.getElementById('password-error');
const progressContainer = document.getElementById('progress-container');
const progressFill = document.getElementById('progress-fill');
const progressText = document.getElementById('progress-text');
const encryptBtn = document.getElementById('encrypt-btn');
const errorContainer = document.getElementById('error-container');
const errorMessage = document.getElementById('error-message');

let selectedFile = null;

// File selection handler
fileInput.addEventListener('change', (e) => {
    selectedFile = e.target.files[0];
    if (selectedFile) {
        const sizeMB = (selectedFile.size / (1024 * 1024)).toFixed(2);
        fileInfo.textContent = `${selectedFile.name} (${sizeMB} MB)`;
    } else {
        fileInfo.textContent = '';
    }
});

// Password confirmation validation
passwordConfirm.addEventListener('input', () => {
    if (password.value !== passwordConfirm.value) {
        passwordError.textContent = 'Passwords do not match';
    } else {
        passwordError.textContent = '';
    }
});

// Progress callback
function updateProgress(percent, message) {
    progressFill.style.width = percent + '%';
    progressText.textContent = message;
}

// Show error
function showError(message) {
    errorContainer.hidden = false;
    errorMessage.textContent = message;
}

// Hide error
function hideError() {
    errorContainer.hidden = true;
}

// Form submit handler
form.addEventListener('submit', async (e) => {
    e.preventDefault();
    hideError();

    // Validate passwords match
    if (password.value !== passwordConfirm.value) {
        showError('Passwords do not match');
        return;
    }

    if (!selectedFile) {
        showError('Please select a file');
        return;
    }

    // Disable button, show progress
    encryptBtn.disabled = true;
    progressContainer.hidden = false;

    try {
        // Read file
        updateProgress(5, 'Reading file...');
        const plaintext = await readFile(selectedFile);

        // Encrypt
        const encrypted = await encrypt(plaintext, password.value, updateProgress);

        // Trigger download
        const encryptedFilename = getEncryptedFilename(selectedFile.name);
        downloadBlob(encrypted, encryptedFilename);

        updateProgress(100, 'Encryption complete! Download started.');

    } catch (error) {
        showError('Encryption failed: ' + error.message);
        console.error(error);
    } finally {
        encryptBtn.disabled = false;
    }
});
```

---

## Step 2.6: decrypt-ui.js

**Path:** `wwwroot/js/ui/decrypt-ui.js`

```javascript
/**
 * Decrypt page UI handler
 */

import { decrypt } from '../crypto/crypto-core.js';
import { readFile, downloadBlob, getDecryptedFilename } from '../crypto/file-handler.js';

const form = document.getElementById('decrypt-form');
const fileInput = document.getElementById('encrypted-file');
const fileInfo = document.getElementById('file-info');
const password = document.getElementById('password');
const progressContainer = document.getElementById('progress-container');
const progressFill = document.getElementById('progress-fill');
const progressText = document.getElementById('progress-text');
const decryptBtn = document.getElementById('decrypt-btn');
const errorContainer = document.getElementById('error-container');
const errorMessage = document.getElementById('error-message');

let selectedFile = null;

// File selection handler
fileInput.addEventListener('change', (e) => {
    selectedFile = e.target.files[0];
    if (selectedFile) {
        const sizeMB = (selectedFile.size / (1024 * 1024)).toFixed(2);
        fileInfo.textContent = `${selectedFile.name} (${sizeMB} MB)`;
    } else {
        fileInfo.textContent = '';
    }
});

// Progress callback
function updateProgress(percent, message) {
    progressFill.style.width = percent + '%';
    progressText.textContent = message;
}

// Show error
function showError(message) {
    errorContainer.hidden = false;
    errorMessage.textContent = message;
}

// Hide error
function hideError() {
    errorContainer.hidden = true;
}

// Form submit handler
form.addEventListener('submit', async (e) => {
    e.preventDefault();
    hideError();

    if (!selectedFile) {
        showError('Please select an encrypted file');
        return;
    }

    if (!password.value) {
        showError('Please enter the password');
        return;
    }

    // Disable button, show progress
    decryptBtn.disabled = true;
    progressContainer.hidden = false;

    try {
        // Read encrypted file
        updateProgress(5, 'Reading file...');
        const encrypted = await readFile(selectedFile);

        // Decrypt
        const plaintext = await decrypt(encrypted, password.value, updateProgress);

        // Trigger download
        const decryptedFilename = getDecryptedFilename(selectedFile.name);
        downloadBlob(plaintext, decryptedFilename);

        updateProgress(100, 'Decryption complete! Download started.');

    } catch (error) {
        showError(error.message);
        console.error(error);
    } finally {
        decryptBtn.disabled = false;
    }
});
```

---

## Step 2.7: Update _Layout.cshtml

**Path:** `Views/Shared/_Layout.cshtml`

Add navigation links:

```html
<header nav ul section:
<ul>
    <li><a asp-area="" asp-controller="Home" asp-action="Index">Home</a></li>
    <li><a asp-area="" asp-controller="Encrypt" asp-action="Index">Encrypt</a></li>
    <li><a asp-area="" asp-controller="Decrypt" asp-action="Index">Decrypt</a></li>
    <li><a asp-area="" asp-controller="Home" asp-action="Privacy">Privacy</a></li>
</ul>
```

---

## Step 2.8: Update site.css

**Path:** `wwwroot/css/site.css`

Add these styles:

```css
/* Crypto Form Styles */
.crypto-container {
    max-width: 600px;
    margin: 2rem auto;
    padding: 0 1rem;
}

.security-note {
    background: #e8f5e9;
    border-left: 4px solid #4caf50;
    padding: 1rem;
    margin-bottom: 2rem;
    font-size: 0.9rem;
}

.crypto-form {
    background: #f5f5f5;
    padding: 2rem;
    border-radius: 8px;
}

.form-group {
    margin-bottom: 1.5rem;
}

.form-group label {
    display: block;
    margin-bottom: 0.5rem;
    font-weight: 600;
}

.form-group input[type="file"],
.form-group input[type="password"],
.form-group input[type="text"] {
    width: 100%;
    padding: 0.75rem;
    border: 1px solid #ccc;
    border-radius: 4px;
    font-size: 1rem;
}

.file-info {
    margin-top: 0.5rem;
    font-size: 0.875rem;
    color: #666;
}

.error-text {
    color: #d32f2f;
    font-size: 0.875rem;
    margin-top: 0.25rem;
}

.progress-container {
    margin: 1.5rem 0;
}

.progress-bar {
    height: 8px;
    background: #ddd;
    border-radius: 4px;
    overflow: hidden;
}

.progress-fill {
    height: 100%;
    background: #2196f3;
    width: 0%;
    transition: width 0.3s ease;
}

.progress-text {
    margin-top: 0.5rem;
    font-size: 0.875rem;
    color: #666;
}

.form-actions {
    margin-top: 1.5rem;
}

.btn-primary {
    background: #1976d2;
    color: white;
    border: none;
    padding: 0.75rem 2rem;
    font-size: 1rem;
    border-radius: 4px;
    cursor: pointer;
}

.btn-primary:hover {
    background: #1565c0;
}

.btn-primary:disabled {
    background: #90caf9;
    cursor: not-allowed;
}

.error-container {
    background: #ffebee;
    border-left: 4px solid #f44336;
    padding: 1rem;
    margin-top: 1rem;
    color: #c62828;
}
```

---

## Step 2.9: Update Program.cs

**Path:** `Program.cs`

Add security headers:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Security headers middleware
app.Use(async (context, next) =>
{
    // Allow WASM for Argon2 and Web Workers
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval'; " +
        "worker-src 'self' blob:; " +
        "style-src 'self' 'unsafe-inline';");

    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
```

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
