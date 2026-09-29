package com.hanjjak.gameapi.feedback

import org.junit.jupiter.api.Assertions.assertArrayEquals
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Test

class QaBugReportImageTest {
    @Test
    fun `accepts an optional supported image data url`() {
        assertNull(decodeQaReportImage(null))
        val decoded = decodeQaReportImage("data:image/png;base64,AQID")
        assertEquals("image/png", decoded?.first)
        assertArrayEquals(byteArrayOf(1, 2, 3), decoded?.second)
    }

    @Test
    fun `rejects unsupported or malformed images`() {
        assertThrows(IllegalArgumentException::class.java) { decodeQaReportImage("data:image/gif;base64,AQID") }
        assertThrows(IllegalArgumentException::class.java) { decodeQaReportImage("data:image/png;base64,not-base64") }
    }
}
