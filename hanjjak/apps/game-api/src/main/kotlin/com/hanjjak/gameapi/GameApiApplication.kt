package com.hanjjak.gameapi

import org.flywaydb.core.Flyway
import org.springframework.boot.autoconfigure.SpringBootApplication
import org.springframework.boot.runApplication
import org.springframework.boot.WebApplicationType
import org.springframework.boot.builder.SpringApplicationBuilder
import kotlin.system.exitProcess

@SpringBootApplication(scanBasePackages = ["com.hanjjak"])
class GameApiApplication

fun main(args: Array<String>) {
    if (args.firstOrNull() == "migrate") {
        Flyway.configure()
            .dataSource(
                System.getenv("DB_URL") ?: "jdbc:postgresql://localhost:5432/hanjjak",
                System.getenv("DB_USER") ?: "hanjjak",
                System.getenv("DB_PASSWORD") ?: "local-only",
            )
            .locations("classpath:db/migration")
            .load()
            .migrate()
        return
    }

    if (args.firstOrNull() == "backfill-progression-rewards") {
        var exitCode = 0
        val output: Any = try {
            val options = ProgressionBackfillCommand.parse(args.drop(1))
            val context = SpringApplicationBuilder(GameApiApplication::class.java)
                .web(WebApplicationType.NONE)
                .logStartupInfo(false)
                .properties(*ProgressionBackfillCommand.applicationProperties().map { (key, value) -> "$key=$value" }.toTypedArray())
                .run(*args)
            try {
                val service = context.getBean(com.hanjjak.battle.application.ProgressionRebalanceBackfillService::class.java)
                val report = if (options.dryRun) service.preview(options.batchSize)
                else if (options.untilComplete) service.applyUntilComplete(options.batchSize)
                else service.apply(options.batchSize)
                if (report.failedAccountIds.isNotEmpty()) exitCode = 1
                report
            } finally {
                context.close()
            }
        } catch (exception: RuntimeException) {
            exitCode = 1
            ProgressionBackfillCommand.failurePayload(exception)
        }
        print(com.fasterxml.jackson.databind.ObjectMapper().writeValueAsString(output))
        if (exitCode != 0) exitProcess(exitCode)
        return
    }

    runApplication<GameApiApplication>(*args)
}

internal data class ProgressionBackfillOptions(
    val dryRun: Boolean,
    val batchSize: Int,
    val untilComplete: Boolean = false,
)

internal object ProgressionBackfillCommand {
    fun parse(args: List<String>): ProgressionBackfillOptions {
        val dryRun = "--dry-run" in args
        val apply = "--apply" in args
        val untilComplete = "--until-complete" in args
        require(dryRun xor apply) { "BACKFILL_MODE_REQUIRED" }
        require(!untilComplete || apply) { "BACKFILL_MODE_REQUIRED" }
        val batchOption = args.firstOrNull { it.startsWith("--batch-size=") }
        val batchSize = batchOption?.substringAfter("=")?.toIntOrNull()
            ?: if (batchOption == null) 100 else throw IllegalArgumentException("INVALID_BATCH_SIZE")
        require(batchSize > 0) { "INVALID_BATCH_SIZE" }
        return ProgressionBackfillOptions(dryRun, batchSize, untilComplete)
    }

    fun applicationProperties(): Map<String, String> = mapOf(
        "spring.main.web-application-type" to "none",
        "spring.task.scheduling.enabled" to "false",
    )

    fun failurePayload(exception: RuntimeException): Map<String, String> = mapOf(
        "errorCode" to (exception.message ?: "BACKFILL_FAILED"),
    )
}