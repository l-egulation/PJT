package com.hanjjak.marketbenchmark

import java.nio.charset.StandardCharsets
import java.sql.Connection
import java.sql.DriverManager
import java.util.UUID
import java.util.concurrent.Callable
import java.util.concurrent.Executors

private const val BENCHMARK_EMAIL_DOMAIN = "benchmark.local"

internal enum class PreparedAccountRole(val value: String) { SELLER("seller"), BUYER("buyer") }

internal data class PreparedAccount(val accountId: UUID, val email: String)

internal fun preparedAccountEmail(prefix: String, role: PreparedAccountRole, index: Int): String =
    "$prefix-${role.value}-${index.toString().padStart(4, '0')}@$BENCHMARK_EMAIL_DOMAIN"

internal fun prepareAccountFixture(config: Config) {
    config.validatePreparedAccounts()
    val materialType = config.itemId.toMaterialType()
    val sellerCount = config.sellers
    val buyerCount = config.preparedAccountCount - sellerCount
    val firstSellerEmail = preparedAccountEmail(config.preparedAccountPrefix, PreparedAccountRole.SELLER, 1)

    if (preparedAccountCount(config) == config.preparedAccountCount) {
        println("prepared account fixture reused prefix=${config.preparedAccountPrefix} count=${config.preparedAccountCount}")
        return
    }

    val seedSession = ApiSession(config.apiBaseUrl)
    val seedAccountId = seedSession.signupOrLogin(firstSellerEmail, config.password, "부하판매1", materialType)
    val passwordHash = DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
        connection.prepareStatement("select password_hash from account where id=?::uuid").use { statement ->
            statement.setObject(1, seedAccountId)
            statement.executeQuery().use { rows ->
                check(rows.next()) { "prepared account password hash missing" }
                rows.getString(1)
            }
        }
    }

    DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
        connection.autoCommit = false
        val accounts = buildList {
            add(PreparedAccount(seedAccountId, firstSellerEmail))
            (2..sellerCount).forEach { index ->
                val email = preparedAccountEmail(config.preparedAccountPrefix, PreparedAccountRole.SELLER, index)
                add(PreparedAccount(stableUuid("account:$email"), email))
            }
            (1..buyerCount).forEach { index ->
                val email = preparedAccountEmail(config.preparedAccountPrefix, PreparedAccountRole.BUYER, index)
                add(PreparedAccount(stableUuid("account:$email"), email))
            }
        }
        insertPreparedAccounts(connection, accounts, passwordHash, materialType)
        connection.commit()
    }

    val actual = preparedAccountCount(config)
    check(actual == config.preparedAccountCount) { "prepared account fixture incomplete expected=${config.preparedAccountCount} actual=$actual" }
    println("prepared account fixture created prefix=${config.preparedAccountPrefix} count=$actual sellers=$sellerCount buyers=$buyerCount")
}

internal fun loginPreparedAccounts(config: Config, role: PreparedAccountRole, count: Int): List<ApiSession> {
    config.validatePreparedAccounts()
    val pool = Executors.newFixedThreadPool(config.preparedLoginConcurrency)
    return try {
        (1..count).map { index ->
            Callable {
                val email = preparedAccountEmail(config.preparedAccountPrefix, role, index)
                ApiSession(config.apiBaseUrl).also { session -> session.accountId = session.login(email, config.password) }
            }
        }.let(pool::invokeAll).map { it.get() }
    } finally {
        pool.shutdownNow()
    }
}

internal fun Config.validatePreparedAccounts() {
    require(preparedAccountCount > 0) { "--prepared-account-count must be positive" }
    require(preparedAccountCount >= sellers + buyers) { "--prepared-account-count must cover sellers and buyers" }
    require(preparedLoginConcurrency in 1..256) { "--prepared-login-concurrency must be between 1 and 256" }
    require(preparedAccountPrefix.matches(Regex("[a-z0-9-]{1,48}"))) { "--prepared-account-prefix must use lowercase letters, digits, or hyphens" }
}

private fun preparedAccountCount(config: Config): Int = DriverManager.getConnection(config.dbUrl, config.dbUser, config.dbPassword).use { connection ->
    connection.prepareStatement("select count(*) from account where email like ? or email like ?").use { statement ->
        statement.setString(1, "${config.preparedAccountPrefix}-seller-%@$BENCHMARK_EMAIL_DOMAIN")
        statement.setString(2, "${config.preparedAccountPrefix}-buyer-%@$BENCHMARK_EMAIL_DOMAIN")
        statement.executeQuery().use { rows -> rows.next(); rows.getInt(1) }
    }
}

private fun insertPreparedAccounts(
    connection: Connection,
    accounts: List<PreparedAccount>,
    passwordHash: String,
    materialType: String,
) {
    connection.prepareStatement(
        "insert into account(id,email,password_hash,login_email,state_version,created_at) values (?::uuid,?,?,?,1,now()) on conflict do nothing",
    ).use { statement ->
        accounts.forEach { account ->
            statement.setObject(1, account.accountId)
            statement.setString(2, account.email)
            statement.setString(3, passwordHash)
            statement.setString(4, account.email)
            statement.addBatch()
        }
        statement.executeBatch()
    }
    connection.prepareStatement(
        "insert into character(id,account_id,nickname,level,experience,rice) values (?::uuid,?::uuid,?,1,0,0) on conflict(account_id) do nothing",
    ).use { statement ->
        accounts.forEachIndexed { index, account ->
            statement.setObject(1, stableUuid("character:${account.email}"))
            statement.setObject(2, account.accountId)
            statement.setString(3, "부하계정${index + 1}")
            statement.addBatch()
        }
        statement.executeBatch()
    }
    connection.prepareStatement(
        "insert into material_preference(account_id,material_type,selected_at) values (?::uuid,?,now()) on conflict(account_id) do nothing",
    ).use { statement ->
        accounts.forEach { account ->
            statement.setObject(1, account.accountId)
            statement.setString(2, materialType)
            statement.addBatch()
        }
        statement.executeBatch()
    }
}

private fun stableUuid(value: String): UUID = UUID.nameUUIDFromBytes(value.toByteArray(StandardCharsets.UTF_8))
