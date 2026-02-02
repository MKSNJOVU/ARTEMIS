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
    argon2Worker.onmessage = function (event) {
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
    };
    // Handle worker crash/error
    argon2Worker.onerror = function (event) {
      reject(new Error("Worker error " + event.message));
    };
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
