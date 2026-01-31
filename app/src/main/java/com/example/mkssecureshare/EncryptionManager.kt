package com.example.mkssecureshare

import android.security.keystore.KeyProperties
import org.bouncycastle.crypto.params.Argon2Parameters
import java.io.InputStream
import java.io.OutputStream
import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import com.example.mkssecureshare.KeyStoreManager

object EncryptionManager {
    // KeyStore config
    const val ALGORITHM = KeyProperties.KEY_ALGORITHM_AES
    const val BLOCKMODE = KeyProperties.BLOCK_MODE_GCM
    const val PADDING = KeyProperties.ENCRYPTION_PADDING_NONE

    // Cipher Config
    private const val TRANSFORMATION = "AES/GCM/NoPadding"

    // Exception TAG
    const val TAG = "EncryptionManager"


    fun encrypt(inputStream: InputStream, outputStream: OutputStream, mode: KeyMode, byteArray: ByteArray, password: CharArray? = null) {
        // Creating a random IV
        val secureRandom = SecureRandom()
        val iv = ByteArray(IV_SIZE)
        secureRandom.nextBytes(iv)

        // Creating Cipher encrypt mode and AES GCM Parameters
        val cipherInstance = Cipher.getInstance(TRANSFORMATION)
        val cipherMode = Cipher.ENCRYPT_MODE
        val cipherParameter = GCMParameterSpec(TAG_SIZE, iv)
        cipherInstance.init(cipherMode, KeyStoreManager.getKey(), cipherParameter)

        // Writing to OutputStream
        if(mode = KeyMode.PASSWORD){
            PasswordKeyManager()
        }

    }
}