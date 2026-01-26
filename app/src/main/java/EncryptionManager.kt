import android.os.Build
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import org.bouncycastle.crypto.generators.Argon2BytesGenerator
import org.bouncycastle.crypto.params.Argon2Parameters
import java.io.InputStream
import java.io.OutputStream
import java.security.KeyStore
import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec
import android.security.keystore.StrongBoxUnavailableException


object EncryptionManager {
    const val ALGORITHM = KeyProperties.KEY_ALGORITHM_AES
    const val BLOCKMODE = KeyProperties.BLOCK_MODE_GCM
    const val PADDING = KeyProperties.ENCRYPTION_PADDING_NONE

    const val ALIAS = "secret_key"
    const val  PROVIDER = "AndroidKeyStore"
    const val ITERATIONS = 3
    const val MEMORY = 65536 // ~64 MB
    const val PARALLELISM = 1
    const val VERSION = 2



    // Helper function for devices with or without StrongBox
    private fun generateNewKey(useStrongBox: Boolean): SecretKey{
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
            } catch (e: StrongBoxUnavailableException){
                // StrongBox not available, use regular KeyStore
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
        val builder = Argon2Parameters.Builder(Argon2Parameters.ARGON2_id)
        builder.withIterations(ITERATIONS)
        builder.withMemoryAsKB(MEMORY)
        builder.withParallelism(PARALLELISM)
        builder.withSalt(salt)
        builder.withVersion(VERSION) // Argon2_id
        val argonParameters = builder.build() // build Argon from parameters
        val argon = Argon2BytesGenerator()
        argon.init(argonParameters) // initialize Argon2 from Parameters

        val argonByteArray = ByteArray(32)
        argon.generateBytes(password,argonByteArray)
        val secretKey = SecretKeySpec(argonByteArray,"AES")

        return secretKey
    }

    fun encrypt(inputStream: InputStream, outputStream: OutputStream, byteArray: ByteArray) {
        // Creating a random IV
        val secureRandom = SecureRandom()
        val iv = ByteArray(12)
        secureRandom.nextBytes(iv)

        // Creating Cipher encrypt mode and AES GCM Parameters
        val cipherInstance = Cipher.getInstance("AES/GCM/NoPadding")
        val cipherMode = Cipher.ENCRYPT_MODE
        val cipherParameter = GCMParameterSpec(128, iv)
        cipherInstance.init(cipherMode, getKey(), cipherParameter)

        // Writing to OutputStream


    }
}
