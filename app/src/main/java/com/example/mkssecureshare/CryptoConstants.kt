package com.example.mkssecureshare

import org.bouncycastle.crypto.params.Argon2Parameters

// KeyStore Config
const val ALIAS = "secret_key"
const val PROVIDER = "AndroidKeyStore"

// AES-GCM Config
const val KEY_SIZE = 256
const val IV_SIZE = 12
const val TAG_SIZE = 128


// Argon2 Config
const val ITERATIONS = 3
const val MEMORY = 65536 // ~64 MB
const val PARALLELISM = 1
const val TYPE = Argon2Parameters.ARGON2_id