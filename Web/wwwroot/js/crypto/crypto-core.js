/**
 * Core encryption/decryption module
 * Uses Web Crypto API for AES-256-GCM
 * Uses Web Worker for Argon2id key derivation
 */

import { CryptoConstants } from "./crypto-constants.js";

let argon2Worker = null;

function getArgon2Worker() {
  if (!argon2Worker) {
    argon2Worker = new Worker("/js/crypto/argon2-worker.js");
  }
  return argon2Worker;
}

// Send password + salt to the Web Worker, get back derived key bytes, convert to CryptoKey

async function deriveKey(password, salt) {
  const argon2Worker = getArgon2Worker();

  return new Promise((resolve, reject) => {
    argon2Worker.addEventListener(
      "message",
      (event) => {
        const { success, hash, error } = event.data;
        if (success) {
          const keyBytes = new Uint8Array(hash);
          crypto.subtle
            .importKey(
              "raw",
              keyBytes,
              { name: CryptoConstants.ALGORITHM },
              false,
              CryptoConstants.KEY_USAGE,
            )
            .then((cryptoKey) => {
              resolve(cryptoKey);
            })
            .catch((error) => {
              reject(error);
            });
        } else {
          reject(new Error("Argon2id failed: " + error));
        }
      },
      { once: true },
    );
    // Handle worker crash/error
    argon2Worker.addEventListener(
      "error",
      (event) => {
        reject(new Error("Worker error " + event.message));
      },
      { once: true },
    );
    // Send work to the Argon2Worker
    argon2Worker.postMessage({
      password: password,
      salt: salt,
      config: {
        iterations: CryptoConstants.ITERATIONS,
        memory: CryptoConstants.MEMORY,
        parallelism: CryptoConstants.PARALLELISM,
        keyLength: CryptoConstants.KEY_SIZE_BYTES,
      },
    });
  });
}

export async function encrypt(plaintext, password, onProgress = null) {
  // Generate a Random Salt
  const salt = crypto.getRandomValues(
    new Uint8Array(CryptoConstants.SALT_SIZE),
  );

  if (onProgress) onProgress(10, "Generating the salt...");

  // Deriving the key
  if (onProgress) onProgress(15, "Deriving key...");
  const key = await deriveKey(password, salt);

  // Generating the IV
  const randomIV = crypto.getRandomValues(
    new Uint8Array(CryptoConstants.IV_SIZE),
  );

  // Encrypting with AES-GCM
  if (onProgress) onProgress(50, "Encrypting...");

  const ciphertext = await crypto.subtle.encrypt(
    {
      name: CryptoConstants.ALGORITHM,
      iv: randomIV,
      tagLength: CryptoConstants.TAG_SIZE_BYTES,
    },
    key,
    plaintext,
  );

  if (onProgress) onProgress(90, "Providing output");

  // Encryption result
  const encryptedResult = new Uint8Array(
    CryptoConstants.SALT_SIZE + CryptoConstants.IV_SIZE + ciphertext.byteLength,
  );
  encryptedResult.set(salt, 0);
  encryptedResult.set(randomIV, CryptoConstants.SALT_SIZE);
  encryptedResult.set(
    new Uint8Array(ciphertext),
    CryptoConstants.SALT_SIZE + CryptoConstants.IV_SIZE,
  );

  if (onProgress) onProgress(100, "Done!");

  return encryptedResult.buffer;
}

export async function decrypt(encrypted, password, onProgress = null) {
  const encryptedData = new Uint8Array(encrypted);

  // Checking the Encrypted Data size
  if (
    encryptedData.length <
    CryptoConstants.SALT_SIZE +
      CryptoConstants.IV_SIZE +
      CryptoConstants.TAG_SIZE_BYTES
  ) {
    throw new Error("Invalid Entry! Data is corrupted or tampered with.");
  }

  // Getting the Encrypted SALT
  const salt = encryptedData.slice(0, CryptoConstants.SALT_SIZE);
  if (onProgress) onProgress(10, "Extracted salt...");

  // Getting the Encrypted IV
  const IV = encryptedData.slice(
    CryptoConstants.SALT_SIZE,
    CryptoConstants.SALT_SIZE + CryptoConstants.IV_SIZE,
  );

  // Getting the Encrypted Ciphertext
  const ciphertext = encryptedData.slice(
    CryptoConstants.SALT_SIZE + CryptoConstants.IV_SIZE,
  );

  // Deriving the key from the password
  if (onProgress) onProgress(15, "Deriving key...");
  const key = await deriveKey(password, salt);

  if (onProgress) onProgress(50, "Decrypting...");
  // Decrypting the data
  try {
    const plaintext = await crypto.subtle.decrypt(
      {
        name: CryptoConstants.ALGORITHM,
        iv: IV,
        tagLength: CryptoConstants.TAG_SIZE_BYTES,
      },
      key,
      ciphertext,
    );

    if (onProgress) onProgress(100, "Done! :tada:");
    return plaintext;
  } catch (error) {
    throw new Error("Invalid Tag! Wrong password or file is tampered with.");
  }
}
