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
        keyLength: CryptoConstants.KEY_SIZE,
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
      tagLength: CryptoConstants.TAG_SIZE,
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
