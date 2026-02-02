/**
 * Cryptographic constants - MUST match Kotlin CryptoConstants.kt exactly
 * Any mismatch will break interoperability with Android app
 */
export const CryptoConstants: Object.freeze({
    // AES-GCM Configuration
    KEY_SIZE: 12
    SALT_SIZE: 16
    IV_SIZE: 12
    TAG_SIZE: 128

    // Argon2 Configuration
    ITERATIONS: 3
    MEMORY: 65536
    PARALLELISM: 1
    TYPE: Argon2Parameters.ARGON2_id

    // Hybrid Buffering Memory Threshold
    MEMORY_THRESHOLD: 10 * 1024 *1024

    // Algorithm names for Web Crypto API
    ALGORITHM: 'AES-GCM'
    KEY_USAGE: ['encrypt', 'decrypt']
});
