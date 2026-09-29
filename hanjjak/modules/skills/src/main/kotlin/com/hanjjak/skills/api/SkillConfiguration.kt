package com.hanjjak.skills.api

import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.inventory.application.ItemCatalog
import com.hanjjak.skills.application.SecureSkillRollSource
import com.hanjjak.skills.application.SkillRollSource
import com.hanjjak.skills.application.SkillService
import com.hanjjak.skills.domain.SkillContent
import com.hanjjak.skills.domain.SkillContentLoader
import com.hanjjak.skills.domain.SkillRules
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.jdbc.core.simple.JdbcClient

@Configuration
class SkillConfiguration {
    @Bean
    fun skillContent(mapper: ObjectMapper): SkillContent = SkillContentLoader(mapper).load()

    @Bean
    fun skillRules(content: SkillContent) = SkillRules(content)

    @Bean
    fun skillRollSource(): SkillRollSource = SecureSkillRollSource()

    @Bean
    fun skillService(jdbc: JdbcClient, mapper: ObjectMapper, inventory: com.hanjjak.inventory.application.InventoryReservationService, catalog: ItemCatalog, rules: SkillRules, rollSource: SkillRollSource) = SkillService(jdbc, mapper, inventory, catalog, rules, rollSource)
}
