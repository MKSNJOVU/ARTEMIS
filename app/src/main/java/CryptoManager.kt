import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import java.security.KeyStore
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey

object CryptoManager  {
    const val Algorithm = KeyProperties.KEY_ALGORITHM_AES
    const val BLOCKMODE= KeyProperties.BLOCK_MODE_CBC
    const val PADDING = KeyProperties.ENCRYPTION_PADDING_PKCS7

    fun getKey(): SecretKey {
        val keyStore = KeyStore.getInstance("AndroidKeyStore")
        keyStore.load (null)

        if(keyStore.containsAlias("secret_key"))
            return keyStore.getKey("secret_key",null) as SecretKey
        else{
            val keyGenerator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES,
                "AndroidKeyStore")

           val keyGenParameterSpec = KeyGenParameterSpec.Builder("secret_key",
               KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
               .setBlockModes(BLOCKMODE)
               .setEncryptionPaddings(PADDING)
               .build()

            keyGenerator.init(keyGenParameterSpec)
            return  keyGenerator.generateKey()
    }
}
}