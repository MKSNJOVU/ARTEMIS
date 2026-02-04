/**
 * Web Worker for Argon2id key derivation
 * Runs off main thread to keep UI responsive
 */

// Import argon2-browser
importScripts("/js/lib/argon2-browser.min.js");

self.onmessage = async function (event) {
  const { password, salt, config } = event.data;

  try {
    const result = await argon2.hash({
      pass: password,
      salt: salt,
      time: config.iterations,
      memory: config.memory,
      parallelism: config.parallelism,
      hashLen: config.keyLength,
      type: argon2.ArgonType.Argon2id,
    });

    self.postMessage({
      success: true,
      hash: result.hash,
    });
  } catch (error) {
    self.postMessage({
      success: false,
      error: error.message,
    });
  }
};
