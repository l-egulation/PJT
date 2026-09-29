package com.hanjjak.admin.domain

import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class AdminAccessTest {
    @Test
    fun `roles grant only their declared read permissions`() {
        val viewer = AdminOperator(UUID.randomUUID(), "viewer", "Viewer", true, 1, setOf(AdminRole.VIEWER))
        assertTrue(AdminPermission.DASHBOARD_READ in viewer.permissions)
        assertTrue(AdminPermission.ECONOMY_READ in viewer.permissions)
        assertFalse(AdminPermission.AUDIT_READ in viewer.permissions)
        assertFalse(AdminPermission.USER_MANAGE in viewer.permissions)

        val administrator = AdminOperator(UUID.randomUUID(), "admin", "Admin", true, 2, setOf(AdminRole.ADMIN))
        assertTrue(AdminPermission.entries.all { it in administrator.permissions })
        assertTrue(AdminPermission.USER_MANAGE in administrator.permissions)
    }
}
