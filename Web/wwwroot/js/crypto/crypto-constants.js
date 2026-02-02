/**
 * Cryptographic constants – must remain consistent with Kotlin CryptoConstants.kt.
 *
 * NOTE ON UNITS:
 * - Kotlin typically specifies sizes in bits (e.g., KEY_SIZE = 256).
 * - This JS module uses both bits and bytes; unit-specific names are provided
 *   (e.g., KEY_SIZE_BITS / KEY_SIZE_BYTES) to avoid ambiguity.
 * Any mismatch in the effective values will break interoperability with the Android app.
 */
export const CryptoConstants = Object.freeze({
  // AES-GCM Configuration
  // AES key size: 256 bits = 32 bytes. Kotlin uses 256 (bits).
  KEY_SIZE_BITS: 256,
  KEY_SIZE_BYTES: 32,
  // Backwards-compatible alias (bytes); prefer KEY_SIZE_BYTES in new code.
  KEY_SIZE: 32,

  SALT_SIZE: 16, // bytes
  IV_SIZE: 12,   // bytes

  // GCM tag length: 128 bits = 16 bytes. Web Crypto expects bits.
  TAG_SIZE_BITS: 128,
  TAG_SIZE_BYTES: 16,
  // Backwards-compatible alias (bits); prefer TAG_SIZE_BITS in new code.
  TAG_SIZE: 128,

  // Argon2 Configuration
  ITERATIONS: 3,
  MEMORY: 65536,
  PARALLELISM: 1,
  TYPE: 2,

  // Hybrid Buffering Memory Threshold
  MEMORY_THRESHOLD: 10 * 1024 * 1024,

  // Algorithm names for Web Crypto API
  ALGORITHM: "AES-GCM",
  KEY_USAGE: ["encrypt", "decrypt"],
});
