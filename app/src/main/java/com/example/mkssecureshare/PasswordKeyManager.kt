package com.example.mkssecureshare

import org.bouncycastle.crypto.generators.Argon2BytesGenerator
import org.bouncycastle.crypto.params.Argon2Parameters
import java.security.SecureRandom
import javax.crypto.SecretKey
import javax.crypto.spec.SecretKeySpec

class PasswordKeyManager {

companion object{
    /**
     * Generate a salt for passwords.
     * Returns a SecureRandom salt.
     * */

    fun generateSalt(): ByteArray {
        val salt = ByteArray(16)
        val secureRandom = SecureRandom()
        secureRandom.nextBytes(salt)
        return salt
    }

   /**Derive the Key from User provided password*/

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
}

}