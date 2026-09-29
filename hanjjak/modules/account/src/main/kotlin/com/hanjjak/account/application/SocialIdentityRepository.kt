package com.hanjjak.account.application

import java.time.Instant
import java.util.UUID

interface SocialIdentityRepository {
    fun findAccountId(provider: SocialProvider, subject: String): UUID?
    fun link(accountId: UUID, provider: SocialProvider, subject: String, email: String?, now: Instant)
    fun touch(provider: SocialProvider, subject: String, email: String?, now: Instant)
    fun list(accountId: UUID): List<LinkedSocialIdentity>
    fun unlink(accountId: UUID, provider: SocialProvider): Boolean
    fun hasPassword(accountId: UUID): Boolean
}
