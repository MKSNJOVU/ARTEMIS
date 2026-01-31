package com.example.mkssecureshare

import android.os.Build
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Log
import com.example.mkssecureshare.EncryptionManager.ALGORITHM
import com.example.mkssecureshare.EncryptionManager.ALIAS
import com.example.mkssecureshare.EncryptionManager.BLOCKMODE
import com.example.mkssecureshare.EncryptionManager.KEY_SIZE
import com.example.mkssecureshare.EncryptionManager.PADDING
import com.example.mkssecureshare.EncryptionManager.PROVIDER
import com.example.mkssecureshare.EncryptionManager.TAG
import java.security.KeyStore
import java.security.ProviderException
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey

class KeyStoreManager {
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
}