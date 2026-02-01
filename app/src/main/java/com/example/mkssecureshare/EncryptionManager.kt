package com.example.mkssecureshare

import android.security.keystore.KeyProperties
import android.util.Log
import java.io.ByteArrayOutputStream
import java.io.File
import java.io.FileInputStream
import java.io.FileOutputStream
import java.io.InputStream
import java.io.OutputStream
import java.nio.Buffer
import java.security.SecureRandom
import javax.crypto.AEADBadTagException
import javax.crypto.Cipher
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import kotlin.Result.Companion.failure

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
                password: CharArray? = null) {

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

               if (password == null){
                   throw IllegalArgumentException("Password required!")
               }else{

                   val salt = PasswordKeyManager.generateSalt()
                   outputStream.write(salt)
                   PasswordKeyManager.deriveKeyFromPassword(password, salt)
               }
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

        while(readInputStreamBytes > - 1){
            val encryptedChunk = cipher.update(inputChunks,0,readInputStreamBytes)
            if (encryptedChunk != null && encryptedChunk.isNotEmpty()){
                outputStream.write(encryptedChunk)
            }

            readInputStreamBytes = inputStream.read(inputChunks)
        }
       outputStream.write(cipher.doFinal())
    }
    fun readExactly(inputStream: InputStream, buffer: ByteArray): Boolean{
        var offset = 0
        while (offset < buffer.size){
            val bytesRead = inputStream.read(buffer, offset, buffer.size - offset)
            if (bytesRead == -1)
                return false
            offset += bytesRead
        }
        return true
    }

    fun decrypt( inputStream: InputStream, outputStream: OutputStream,
                 mode: KeyMode, password: CharArray?, tempDir: File): DecryptionResults {

        // Initialize Cipher in DecryptMode
        val cipher = Cipher.getInstance(TRANSFORMATION)
        val cipherMode = Cipher.DECRYPT_MODE
        val decryptionSalt = ByteArray(SALT_SIZE)
        val decryptionIV = ByteArray(IV_SIZE)
        // Reading InputStream
        val secretKey: SecretKey = when (mode){ KeyMode.PASSWORD ->{
            if (password == null){
               return DecryptionResults.FailedDecryption("Password was not provided!")
            }
            if (!readExactly(inputStream, decryptionSalt)){
                return DecryptionResults.FailedDecryption("Unexpected EOF reading Salt.")
            }
            if (!readExactly(inputStream, decryptionIV)){
                return DecryptionResults.FailedDecryption("Unexpected EOF reading IV.")
            }
            PasswordKeyManager.deriveKeyFromPassword(password, decryptionSalt)
        }
            KeyMode.KEYSTORE ->{
                if (!readExactly(inputStream, decryptionIV)){
                    return DecryptionResults.FailedDecryption("Unexpected EOF reading IV.")
                }
                KeyStoreManager.getKey()
            }
        }
        val cipherParameter = GCMParameterSpec(TAG_SIZE, decryptionIV)

        cipher.init(cipherMode,secretKey,cipherParameter)

        // Hybrid buffering setup
        var memoryBuffer: ByteArrayOutputStream? = ByteArrayOutputStream()
        var tempFile: File? = null
        var tempFileStream: FileOutputStream? = null
        var totalBytesWritten = 0L
        var usingTempFile = false

   try {
       val outputChunks = ByteArray(8192)
       var readOutputStreamBytes = inputStream.read(outputChunks)
       while(readOutputStreamBytes > - 1){
           val decryptedChunk = cipher.update(outputChunks,0,readOutputStreamBytes)
           if (decryptedChunk != null && decryptedChunk.isNotEmpty()){
               totalBytesWritten += decryptedChunk.size
               // Switch to temp file if threshold exceeded
               if (!usingTempFile && totalBytesWritten > MEMORY_THRESHOLD) {
                   tempFile = File.createTempFile("decrypt_", ".tmp", tempDir)
                   tempFileStream = FileOutputStream(tempFile)
                   memoryBuffer?.writeTo(tempFileStream)
                   memoryBuffer?.close()
                   memoryBuffer = null
                   usingTempFile = true
               }

               if (usingTempFile){
                   tempFileStream?.write(decryptedChunk)
               }else{
                   memoryBuffer?.write(decryptedChunk)
               }
           }
           readOutputStreamBytes = inputStream.read(outputChunks)
       }

       // Verify auth tag
       val finalChunk = cipher.doFinal()

       // Auth verified - write to real output
       if (usingTempFile){
           if (finalChunk != null && finalChunk.isNotEmpty()){
               tempFileStream?.write(finalChunk)
           }
           tempFileStream?.close()
           tempFileStream = null

           val file = requireNotNull(tempFile) { "Temporary file is null when reading decrypted output" }
           FileInputStream(file).use { fis ->
               fis.copyTo(outputStream)
           }
       } else{
           if (finalChunk != null && finalChunk.isNotEmpty()){
               memoryBuffer?.write(finalChunk)
           }
           memoryBuffer?.writeTo(outputStream)
       }
       return DecryptionResults.SuccessfulDecryption


   }catch (e: AEADBadTagException){
       Log.d(TAG,"Invalid Authentication Tag: ${e.message}")
       return DecryptionResults.FailedDecryption("Authentication failed. File may be corrupted or tampered with.")
   } finally{
       memoryBuffer?.close()
       tempFileStream?.close()
       tempFile?.delete()
   }

   }
}