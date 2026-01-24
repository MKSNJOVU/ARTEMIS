import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import java.io.InputStream
import java.io.OutputStream
import java.security.KeyStore
import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey

object CryptoManager  {
    const val Algorithm = KeyProperties.KEY_ALGORITHM_AES
    const val BLOCKMODE= KeyProperties.BLOCK_MODE_GCM
    const val PADDING = KeyProperties.ENCRYPTION_PADDING_NONE

    fun getKey(): SecretKey {
        val keyStore = KeyStore.getInstance("AndroidKeyStore")
        keyStore.load (null)

        if(keyStore.containsAlias("secret_key"))
            return keyStore.getKey("secret_key",null) as SecretKey
        else{
            val keyGenerator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES,
                "AndroidKeyStore")
            val keyParameter = KeyGenParameterSpec.Builder("secret_key",
                KeyProperties.PURPOSE_DECRYPT or
                KeyProperties.PURPOSE_ENCRYPT)
                .setBlockModes(BLOCKMODE)
                .setEncryptionPaddings(PADDING)
                .build()

            keyGenerator.init(keyParameter)

            return  keyGenerator.generateKey()
    }
}
    fun encrypt(inputStream: InputStream, outputStream: OutputStream){

        val cipher = Cipher.ENCRYPT_MODE
        val iv = ByArr
    }
}