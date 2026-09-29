package com.hanjjak.account.application

import java.util.UUID

interface AuthenticationSessionLifecycle {
    fun closeForPasswordReset(accountId: UUID)
}

interface GameSessionLifecycle {
    fun replaceForLogin(accountId: UUID)
    fun closeForLogout(accountId: UUID)
    fun closeForPasswordReset(accountId: UUID)
}

interface OfflineRewardLifecycle {
    fun startForSession(accountId: UUID, gameSessionId: UUID) {}
    fun touchForSession(accountId: UUID, gameSessionId: UUID, heartbeatAt: java.time.Instant) {}
    fun markDisconnectedForAccount(accountId: UUID, disconnectedAt: java.time.Instant) {}
    fun cancelForSession(accountId: UUID, gameSessionId: UUID) {}
    fun cancelForAccount(accountId: UUID) {}
    fun freezeForSession(accountId: UUID, gameSessionId: UUID, reason: String) {}
    fun freezeForAccount(accountId: UUID, reason: String) {}
}
