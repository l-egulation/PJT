package com.hanjjak.stage.application

import com.hanjjak.stage.domain.BossType
import com.hanjjak.stage.domain.StageDefinition
import com.hanjjak.stage.domain.StageId

interface StageCatalog {
    fun all(): List<StageDefinition>
    fun all(balanceVersion: String): List<StageDefinition> = all()
    fun require(stageKey: String): StageDefinition = all().firstOrNull { it.id.key == stageKey }
        ?: throw IllegalArgumentException("STAGE_NOT_FOUND")
    fun require(balanceVersion: String, stageKey: String): StageDefinition = all(balanceVersion).firstOrNull { it.id.key == stageKey }
        ?: throw IllegalArgumentException("STAGE_NOT_FOUND")

    /**
     * 자동 진행이 넘어갈 수 있는 마지막 스테이지의 글로벌 인덱스. 수치만 있고 그림이 아직
     * 없는 챕터는 열지 않으므로, 배경이 붙은 스테이지까지만 센다. 그림이 콘텐츠에 들어오는
     * 순간 별도 수정 없이 다음 챕터가 열린다. 프레젠테이션이 하나도 없는 축소 카탈로그
     * (테스트용)에서는 전체 마지막 스테이지로 물러선다.
     */
    fun lastPlayableIndex(balanceVersion: String): Int {
        val stages = all(balanceVersion)
        return stages.filter { it.backgroundId != null }.maxOfOrNull { it.id.globalIndex }
            ?: stages.maxOfOrNull { it.id.globalIndex }
            ?: 0
    }
}

class InMemoryStageCatalog(private val definitions: List<StageDefinition>) : StageCatalog {
    override fun all(): List<StageDefinition> = definitions

    companion object {
        fun minimumVerticalSlice(): InMemoryStageCatalog = InMemoryStageCatalog(
            listOf(StageDefinition(StageId(1, 1), 1, 0, 250, 23, 1000, 46, BossType.STANDARD)),
        )
    }
}
