package com.hanjjak.account.infrastructure

import com.hanjjak.account.application.AccountRepository
import com.hanjjak.account.domain.Account
import com.hanjjak.account.domain.Character
import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.dao.DuplicateKeyException
import org.springframework.stereotype.Repository
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

@Repository
class JdbcAccountRepository(private val jdbc: JdbcClient) : AccountRepository {
    override fun findByEmail(email: String): Account? = jdbc.sql("select * from account where login_email = :email and password_hash is not null and deleted_at is null")
        .param("email", email).query(Account::class.java).optional().orElse(null)


    override fun findCharacter(accountId: UUID): Character? = jdbc.sql("select * from character where account_id = :accountId")
        .param("accountId", accountId).query(Character::class.java).optional().orElse(null)
    override fun findById(id: UUID): Account? = jdbc.sql("select * from account where id = :id and deleted_at is null")
        .param("id", id).query(Account::class.java).optional().orElse(null)

    override fun save(account: Account): Account {
        try {
            jdbc.sql("insert into account(id,email,password_hash,login_email,state_version,created_at) values (:id,:email,:hash,:loginEmail,:version,:created)")
                .params(mapOf("id" to account.id, "email" to account.email, "hash" to account.passwordHash, "loginEmail" to account.loginEmail, "version" to account.stateVersion, "created" to OffsetDateTime.ofInstant(account.createdAt, ZoneOffset.UTC)))
                .update()
        } catch (_: DuplicateKeyException) {
            throw IllegalArgumentException("EMAIL_ALREADY_EXISTS")
        }
        return account
    }

    override fun saveCharacter(character: Character): Character {
        jdbc.sql("insert into character(id,account_id,nickname,level,experience,rice) values (:id,:account,:nickname,:level,:experience,:rice)")
            .params(mapOf("id" to character.id, "account" to character.accountId, "nickname" to character.nickname, "level" to character.level, "experience" to character.experience, "rice" to character.rice))
            .update()
        return character
    }

    override fun updateCharacterNickname(accountId: UUID, nickname: String): Character? = jdbc.sql("update character set nickname = :nickname where account_id = :accountId returning *")
        .param("accountId", accountId)
        .param("nickname", nickname)
        .query(Character::class.java)
        .optional().orElse(null)

    override fun incrementStateVersion(id: UUID): Account? = jdbc.sql("update account set state_version = state_version + 1 where id = :id returning *")
        .param("id", id)
        .query(Account::class.java)
        .optional().orElse(null)
    override fun updatePasswordAndIncrementStateVersion(id: UUID, passwordHash: String): Account? = jdbc.sql(
        "update account set password_hash = :passwordHash, state_version = state_version + 1 where id = :id and deleted_at is null returning *",
    )
        .param("id", id)
        .param("passwordHash", passwordHash)
        .query(Account::class.java)
        .optional().orElse(null)

    override fun softDelete(id: UUID, anonymizedEmail: String, anonymizedNickname: String): Boolean {
        jdbc.sql("update character set nickname = :nickname where account_id = :account")
            .params(mapOf("account" to id, "nickname" to anonymizedNickname))
            .update()
        return jdbc.sql("update account set email = :email, deleted_at = now(), state_version = state_version + 1 where id = :account and deleted_at is null")
            .params(mapOf("account" to id, "email" to anonymizedEmail))
            .update() == 1
    }
}
