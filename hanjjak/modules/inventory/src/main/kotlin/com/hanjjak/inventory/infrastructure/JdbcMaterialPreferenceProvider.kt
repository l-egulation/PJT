package com.hanjjak.inventory.infrastructure

import com.hanjjak.inventory.application.MaterialPreferenceProvider
import com.hanjjak.inventory.domain.MaterialType
import org.springframework.jdbc.core.simple.JdbcClient
import java.util.UUID

class JdbcMaterialPreferenceProvider(private val jdbc: JdbcClient) : MaterialPreferenceProvider {
    override fun primaryMaterial(accountId: UUID): MaterialType = jdbc.sql("select material_type from material_preference where account_id=:account")
        .param("account", accountId)
        .query(String::class.java)
        .optional()
        .map(MaterialType::valueOf)
        .orElseThrow { IllegalArgumentException("MATERIAL_SELECTION_REQUIRED") }
}
