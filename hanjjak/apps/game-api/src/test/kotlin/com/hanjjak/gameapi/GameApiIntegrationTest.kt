package com.hanjjak.gameapi

import com.hanjjak.account.application.PasswordResetMailSender
import com.hanjjak.battle.application.BattleStatsProvider
import com.hanjjak.sim.FighterStats
import com.hanjjak.admin.application.AdminGitlabProfile
import com.hanjjak.admin.application.AdminGitlabProvider
import com.hanjjak.skills.application.SkillRollSource
import org.springframework.boot.test.autoconfigure.web.servlet.AutoConfigureMockMvc
import org.springframework.boot.test.context.SpringBootTest
import org.springframework.boot.test.context.TestConfiguration
import org.springframework.context.ApplicationContextInitializer
import org.springframework.context.ConfigurableApplicationContext
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Import
import org.springframework.context.annotation.Primary
import org.springframework.test.context.ContextConfiguration
import org.springframework.test.context.TestContext
import org.springframework.test.context.TestExecutionListeners
import org.springframework.test.context.support.AbstractTestExecutionListener
import org.springframework.test.context.support.TestPropertySourceUtils
import java.net.URI
import java.util.ArrayDeque
import java.util.UUID
import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.LinkedBlockingQueue
import java.util.concurrent.TimeUnit
import javax.sql.DataSource

@Target(AnnotationTarget.CLASS)
@Retention(AnnotationRetention.RUNTIME)
@SpringBootTest(
    webEnvironment = SpringBootTest.WebEnvironment.MOCK,
    properties = [
        "hanjjak.admin.bootstrap.username=bootstrap-admin",
        "hanjjak.admin.bootstrap.password=change-me-now-123",
        "hanjjak.admin.bootstrap.display-name=테스트 운영자",
        "hanjjak.admin.deployment.commit-sha=test-sha",
        "hanjjak.api-security.authentication-rate-limit.max-requests=10000",
        "hanjjak.api-security.password-reset-rate-limit.max-requests=10000",
        "hanjjak.api-security.mutation-rate-limit.max-requests=10000",
        "hanjjak.admin.authentication-rate-limit.max-requests=10000",
        "hanjjak.api-security.legacy-battle-reward-api-enabled=true",
        "hanjjak.admin.gitlab.enabled=true",
        "hanjjak.admin.gitlab.client-id=gitlab-client",
        "hanjjak.admin.gitlab.client-secret=gitlab-secret",
        "hanjjak.admin.gitlab.authorization-uri=https://gitlab.example.com/oauth/authorize",
        "hanjjak.admin.gitlab.token-uri=https://gitlab.example.com/oauth/token",
        "hanjjak.admin.gitlab.user-uri=https://gitlab.example.com/api/v4/user",
        "hanjjak.admin.gitlab.allowed-usernames=allowed-admin,allowed-link-admin",
        "hanjjak.admin.gitlab.public-base-url=http://localhost:5173",
        "hanjjak.admin.gitlab.success-url=http://localhost:5173/admin/",
        "hanjjak.admin.gitlab.failure-url=http://localhost:5173/admin/",
        "hanjjak.offline-reward.test-all-accounts=false",
        "hanjjak.offline-reward.test-account-ids=",
        "hanjjak.offline-reward.test-reward-multiplier=",
        "hanjjak.offline-reward.test-grace-period-seconds=",
        "hanjjak.offline-reward.test-bucket-seconds=",
        "hanjjak.offline-reward.test-accrual-cap-seconds=",
        "hanjjak.gem-dungeon.rotation-period-seconds=3600",
        "hanjjak.gem-dungeon.ticket.regen-seconds=3600",
        "hanjjak.gem-dungeon.ticket.max-stock=3",
        "hanjjak.gem-dungeon.ticket.initial-grant=3",
    ],
)
@AutoConfigureMockMvc
@Import(GameApiIntegrationTestConfiguration::class)
@ContextConfiguration(initializers = [GameApiIntegrationTestInitializer::class])
@TestExecutionListeners(
    listeners = [GameApiIntegrationTestListener::class],
    mergeMode = TestExecutionListeners.MergeMode.MERGE_WITH_DEFAULTS,
)
annotation class GameApiIntegrationTest

class GameApiIntegrationTestInitializer : ApplicationContextInitializer<ConfigurableApplicationContext> {
    override fun initialize(applicationContext: ConfigurableApplicationContext) {
        val database = PostgresTestDatabase.workerConnection
        TestPropertySourceUtils.addInlinedPropertiesToEnvironment(
            applicationContext,
            "spring.datasource.url=${database.jdbcUrl}",
            "spring.datasource.username=${database.username}",
            "spring.datasource.password=${database.password}",
        )
    }
}

@TestConfiguration(proxyBeanMethods = false)
class GameApiIntegrationTestConfiguration {
    @Bean
    @Primary
    fun mutableBattleStatsProvider(): MutableBattleStatsProvider = MutableBattleStatsProvider()

    @Bean
    @Primary
    fun recordingPasswordResetMailSender(): RecordingPasswordResetMailSender = RecordingPasswordResetMailSender()

    @Bean
    @Primary
    fun queueSkillRollSource(): QueueSkillRollSource = QueueSkillRollSource()

    @Bean
    @Primary
    fun stubAdminGitlabProvider(): StubAdminGitlabProvider = StubAdminGitlabProvider()
}

class GameApiIntegrationTestListener : AbstractTestExecutionListener() {
    override fun beforeTestClass(testContext: TestContext) {
        val dataSource = testContext.applicationContext.getBean(DataSource::class.java)
        dataSource.connection.use { connection ->
            val tables = connection.prepareStatement(
                """
                select tablename
                from pg_tables
                where schemaname = current_schema()
                  and tablename not in ('flyway_schema_history', 'admin_operator', 'admin_operator_role')
                order by tablename
                """.trimIndent(),
            ).use { statement ->
                statement.executeQuery().use { rows ->
                    buildList {
                        while (rows.next()) add(rows.getString(1))
                    }
                }
            }
            if (tables.isNotEmpty()) {
                val quotedTables = tables.joinToString(",") { table -> "\"${table.replace("\"", "\"\"")}\"" }
                connection.createStatement().use { statement ->
                    statement.execute("truncate table $quotedTables restart identity cascade")
                }
            }
        }
    }

    override fun beforeTestMethod(testContext: TestContext) {
        testContext.applicationContext.getBean(MutableBattleStatsProvider::class.java).reset()
        testContext.applicationContext.getBean(StubAdminGitlabProvider::class.java).reset()
        testContext.applicationContext.getBean(RecordingPasswordResetMailSender::class.java).reset()
        testContext.applicationContext.getBean(QueueSkillRollSource::class.java).reset()
    }

    override fun afterTestMethod(testContext: TestContext) {
        testContext.applicationContext.getBean(MutableBattleStatsProvider::class.java).reset()
        testContext.applicationContext.getBean(StubAdminGitlabProvider::class.java).reset()
        testContext.applicationContext.getBean(RecordingPasswordResetMailSender::class.java).reset()
        testContext.applicationContext.getBean(QueueSkillRollSource::class.java).reset()
    }
}

class MutableBattleStatsProvider : BattleStatsProvider {
    private val statsByAccount = ConcurrentHashMap<UUID, FighterStats>()

    override fun forAccount(accountId: UUID): FighterStats = statsByAccount[accountId] ?: FighterStats(1, 1, 0)

    fun set(accountId: UUID, stats: FighterStats) {
        statsByAccount[accountId] = stats
    }

    fun reset() = statsByAccount.clear()
}

class RecordingPasswordResetMailSender : PasswordResetMailSender {
    private val links = LinkedBlockingQueue<URI>()

    override fun send(to: String, resetLink: URI) {
        links.put(resetLink)
    }

    fun awaitLink(): URI = requireNotNull(links.poll(5, TimeUnit.SECONDS)) { "Password reset mail was not sent" }

    fun clear() = links.clear()

    fun reset() = clear()
}

class QueueSkillRollSource : SkillRollSource {
    private val queue = ArrayDeque<Int>()
    var draws: Int = 0
        private set

    fun reset() {
        queue.clear()
        draws = 0
    }

    fun enqueue(vararg values: Int) = values.forEach(queue::addLast)

    override fun nextBasisPoint(): Int {
        draws++
        if (queue.isEmpty()) error("TEST_ROLL_QUEUE_EXHAUSTED")
        return queue.removeFirst()
    }
}

class StubAdminGitlabProvider : AdminGitlabProvider {
    var profile = AdminGitlabProfile(314, "allowed-admin", "Allowed Admin", "active", false)

    override fun exchange(code: String, redirectUri: String, codeVerifier: String) = profile

    fun reset() {
        profile = AdminGitlabProfile(314, "allowed-admin", "Allowed Admin", "active", false)
    }
}
