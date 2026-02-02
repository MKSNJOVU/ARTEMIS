/**
 * Core encryption/decryption module
 * Uses Web Crypto API for AES-256-GCM
 * Uses Web Worker for Argon2id key derivation
 */

import { CryptoConstants } from "./crypto-constants";

let argon2Worker = null;
