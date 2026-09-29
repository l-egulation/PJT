package com.hanjjak.account.application

interface SocialProviderClient {
    fun exchange(
        provider: SocialProvider,
        code: String,
        redirectUri: String,
        codeVerifier: String,
        expectedNonce: String,
        state: String,
    ): SocialProfile
}
