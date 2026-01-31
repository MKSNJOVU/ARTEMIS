package com.example.mkssecureshare

import android.os.Build
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Log
import org.bouncycastle.crypto.generators.Argon2BytesGenerator
import org.bouncycastle.crypto.params.Argon2Parameters
import java.io.InputStream
import java.io.OutputStream
import java.security.KeyStore
import java.security.ProviderException
import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

object EncryptionManager {
    // KeyStore config

    const val ALGORITHM = KeyProperties.KEY_ALGORITHM_AES
    const val BLOCKMODE = KeyProperties.BLOCK_MODE_GCM
    const val PADDING = KeyProperties.ENCRYPTION_PADDING_NONE
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
    // Cipher Config
    private const val TRANSFORMATION = "AES/GCM/NoPadding"

    // Exception TAG
    private  const val TAG = "EncryptionManager"

    // Helper function for devices with or without StrongBox
    private fun generateNewKey(useStrongBox: Boolean): SecretKey {
        val keyGenerator = KeyGenerator.getInstance(ALGORITHM,
            PROVIDER
        )

        val builder = KeyGenParameterSpec.Builder(
            ALIAS,
            KeyProperties.PURPOSE_DECRYPT or
                    KeyProperties.PURPOSE_ENCRYPT
        )
            .setBlockModes(BLOCKMODE)
            .setEncryptionPaddings(PADDING)
            .setKeySize(KEY_SIZE)
            if (useStrongBox && Build.VERSION.SDK_INT >= Build.VERSION_CODES.P){
                builder.setIsStrongBoxBacked(true)
            }
            keyGenerator.init(builder.build())
            return keyGenerator.generateKey()

    }
    // Get the Key
    fun getKey(): SecretKey {
        val keyStore = KeyStore.getInstance(PROVIDER)
        keyStore.load(null)

        if (keyStore.containsAlias(ALIAS))
            return keyStore.getKey(ALIAS, null) as SecretKey

        if(Build.VERSION.SDK_INT >= Build.VERSION_CODES.P){
            try {
                return generateNewKey( true)
            } catch (e: ProviderException){
               Log.d(TAG,"StrongBox not available, using KeyStore: ${e.message}")
            }
        }
            return generateNewKey(false)

    }

    // Generate a salt for passwords
    fun generateSalt(): ByteArray {
        val salt = ByteArray(16)
        val secureRandom = SecureRandom()
        secureRandom.nextBytes(salt)
        return salt
    }

    // Derive the Key from User provided password
    fun deriveKeyFromPassword(password: CharArray, salt: ByteArray): SecretKey {
        val argonByteArray = ByteArray(KEY_SIZE/8)

        try{val builder = Argon2Parameters.Builder(TYPE)
            .withIterations(ITERATIONS)
            .withMemoryAsKB(MEMORY)
            .withParallelism(PARALLELISM)
            .withSalt(salt)
        val argonParameters = builder.build() // build Argon from parameters
        val argon = Argon2BytesGenerator()
        argon.init(argonParameters) // initialize Argon2 from Parameters

        argon.generateBytes(password,argonByteArray)
        val secretKey = SecretKeySpec(argonByteArray, "AES")

        return secretKey}
        finally {
            password.fill('\u0000')
            argonByteArray.fill(0)
        }
    }

    fun encrypt(inputStream: InputStream, outputStream: OutputStream, byteArray: ByteArray) {
        // Creating a random IV
        val secureRandom = SecureRandom()
        val iv = ByteArray(IV_SIZE)
        secureRandom.nextBytes(iv)

        // Creating Cipher encrypt mode and AES GCM Parameters
        val cipherInstance = Cipher.getInstance(TRANSFORMATION)
        val cipherMode = Cipher.ENCRYPT_MODE
        val cipherParameter = GCMParameterSpec(TAG_SIZE, iv)
        cipherInstance.init(cipherMode, getKey(), cipherParameter)

        // Writing to OutputStream


    }
}