package com.example.mkssecureshare

import android.security.keystore.KeyProperties
import java.io.InputStream
import java.io.OutputStream
import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

object EncryptionManager {
    // KeyStore config
    const val ALGORITHM = KeyProperties.KEY_ALGORITHM_AES
    const val BLOCKMODE = KeyProperties.BLOCK_MODE_GCM
    const val PADDING = KeyProperties.ENCRYPTION_PADDING_NONE

    // Cipher Config
    private const val TRANSFORMATION = "AES/GCM/NoPadding"

    // Exception TAG
    const val TAG = "EncryptionManager"

    fun encrypt(inputStream: InputStream, outputStream: OutputStream,
                mode: KeyMode,
                password: CharArray) {

        // Creating a random IV
        val secureRandom = SecureRandom()
        val iv = ByteArray(IV_SIZE)
        secureRandom.nextBytes(iv)

        // Creating Cipher encrypt mode and AES GCM Parameters
        val cipher = Cipher.getInstance(TRANSFORMATION)
        val cipherMode = Cipher.ENCRYPT_MODE
        val cipherParameter = GCMParameterSpec(TAG_SIZE, iv)

        // Writing to OutputStream
       val secretKey: SecretKey = when (mode){ KeyMode.PASSWORD ->{
           val salt =  PasswordKeyManager.generateSalt()
            outputStream.write(salt)
            PasswordKeyManager.deriveKeyFromPassword(password, salt)

        }
            KeyMode.KEYSTORE ->{
               KeyStoreManager.getKey()
            }
        }

        cipher.init(cipherMode, secretKey, cipherParameter)

        outputStream.write(cipher.iv)

        // Reading input
        val inputChunks = ByteArray(8192)
        var readInputStreamBytes = inputStream.read(inputChunks)

        while(readInputStreamBytes > -1){
            val encryptedChunk = cipher.update(inputChunks,0,readInputStreamBytes)
            outputStream.write(encryptedChunk)
            readInputStreamBytes = inputStream.read(inputChunks)
        }
       outputStream.write(cipher.doFinal())
    }
}