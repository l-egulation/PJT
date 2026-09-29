package com.hanjjak.raid.domain

import kotlin.reflect.KClass
import kotlin.test.Test
import kotlin.test.assertFalse

class RaidModuleBoundaryTest {
    @Test
    fun `raid domain public signatures exclude spring jdbc and adapters`() {
        assertNoForbiddenTypeReferences(RaidSession::class, RaidAttemptResult::class)
    }

    private fun assertNoForbiddenTypeReferences(vararg types: KClass<*>) {
        val forbiddenPrefixes = listOf("org.springframework", "java.sql", "javax.sql", "com.hanjjak.raid.api", "com.hanjjak.raid.infrastructure")
        val referencedNames = types.flatMap { type ->
            type.java.declaredFields.map { it.type.name } +
                type.java.declaredMethods.flatMap { method -> listOf(method.returnType.name) + method.parameterTypes.map(Class<*>::getName) }
        }
        forbiddenPrefixes.forEach { prefix ->
            assertFalse(referencedNames.any { it.startsWith(prefix) }, "domain signature references $prefix: $referencedNames")
        }
    }
}
