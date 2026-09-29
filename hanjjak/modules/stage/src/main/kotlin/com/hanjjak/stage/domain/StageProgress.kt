package com.hanjjak.stage.domain

import java.time.Instant
import java.util.UUID

data class StageProgress(
    val accountId: UUID,
    val stageId: String,
    val unlocked: Boolean,
    val firstClearedAt: Instant?,
    val clearCount: Long,
    val contentVersion: String,
)

object StageProgression {
    fun initial(accountId: UUID, contentVersion: String): StageProgress =
        StageProgress(accountId, StageId(1, 1).key, true, null, 0, contentVersion)

    fun clear(progress: StageProgress, clearedAt: Instant): StageProgress = progress.copy(
        firstClearedAt = progress.firstClearedAt ?: clearedAt,
        clearCount = progress.clearCount + 1,
    )
}
