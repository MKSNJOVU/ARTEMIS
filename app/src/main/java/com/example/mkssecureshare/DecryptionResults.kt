package com.example.mkssecureshare

sealed class DecryptionResults {

    object SuccessfulDecryption: DecryptionResults(){

    }

    data class FailedDecryption ( val reason: String): DecryptionResults()

}
