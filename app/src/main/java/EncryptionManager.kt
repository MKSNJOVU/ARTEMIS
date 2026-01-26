import android.os.Build
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import androidx.annotation.RequiresApi
import org.bouncycastle.crypto.params.Argon2Parameters
import java.io.InputStream
import java.io.OutputStream
import java.security.KeyStore
import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.PBEKeySpec
import javax.crypto.spec.SecretKeySpec


object EncryptionManager {
    const val Algorithm = KeyProperties.KEY_ALGORITHM_AES
    const val BLOCKMODE = KeyProperties.BLOCK_MODE_GCM
    const val PADDING = KeyProperties.ENCRYPTION_PADDING_NONE
    const val KEYLENGTH = 256
    const val ITERATIONS = 3
    const val MEMORY = 1024000 // ~1 GiB
    const val PARALLELISM = 4

    // Get or Create the SecretKey
    @RequiresApi(Build.VERSION_CODES.P)
    fun getKey(): SecretKey {
        val keyStore = KeyStore.getInstance("AndroidKeyStore")
        keyStore.load(null)

        if (keyStore.containsAlias("secret_key"))
            return keyStore.getKey("secret_key", null) as SecretKey
        else {
            val keyGenerator = KeyGenerator.getInstance(
                KeyProperties.KEY_ALGORITHM_AES,
                "AndroidKeyStore"
            )
            val keyParameter = KeyGenParameterSpec.Builder(
                "secret_key",
                KeyProperties.PURPOSE_DECRYPT or
                        KeyProperties.PURPOSE_ENCRYPT
            )
                .setBlockModes(BLOCKMODE)
                .setEncryptionPaddings(PADDING)
                .setIsStrongBoxBacked(true)
                .build()

            keyGenerator.init(keyParameter)

            return keyGenerator.generateKey()
        }
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
        val secretKeyFactory = SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256")
        val keySpec = PBEKeySpec(password, salt, ITERATIONS, KEYLENGTH)
        val generateSecret = secretKeyFactory.generateSecret(keySpec)
        val secretByteArray = generateSecret.encoded
        val secretKeySpec = SecretKeySpec(secretByteArray,"AES")

        val builder = Argon2Parameters.Builder()
        builder.withIterations(ITERATIONS)
        builder.withMemoryAsKB(MEMORY)
        builder.withParallelism(PARALLELISM)
        builder.withSalt(salt)
        builder.withSecret(pepper)
        builder.withVersion(1) // Argon2i
        builder.withType(Argon2Parameters.ARGON2_i)
        val parameters = builder.build()

        //return secretKeySpec
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
