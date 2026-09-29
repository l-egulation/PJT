package com.hanjjak.admin.application

data class AdminGitlabProfile(
    val id: Long,
    val username: String,
    val name: String,
    val state: String,
    val locked: Boolean,
)

fun interface AdminGitlabProvider {
    fun exchange(code: String, redirectUri: String, codeVerifier: String): AdminGitlabProfile
}
