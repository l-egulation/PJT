package com.hanjjak.account.domain

import java.time.Instant
import java.util.UUID

data class Account(
    val id: UUID,
    val email: String,
    val passwordHash: String?,
    val loginEmail: String?,
    val stateVersion: Long,
    val createdAt: Instant,
)

data class Character(
    val id: UUID,
    val accountId: UUID,
    val nickname: String,
    val level: Int,
    val experience: Long,
    val rice: Long,
)
