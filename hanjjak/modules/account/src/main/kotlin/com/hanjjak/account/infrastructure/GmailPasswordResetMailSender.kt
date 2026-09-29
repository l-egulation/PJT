package com.hanjjak.account.infrastructure

import com.hanjjak.account.application.PasswordResetMailSender
import com.hanjjak.account.application.PasswordResetProperties
import org.springframework.mail.SimpleMailMessage
import org.springframework.mail.javamail.JavaMailSender
import org.springframework.scheduling.annotation.Async
import org.springframework.stereotype.Component
import java.net.URI

@Component
class GmailPasswordResetMailSender(
    private val mailSender: JavaMailSender,
    private val properties: PasswordResetProperties,
) : PasswordResetMailSender {
    @Async("passwordResetMailExecutor")
    override fun send(to: String, resetLink: URI) {
        require(properties.mail.from.isNotBlank()) { "Gmail sender is not configured" }
        val message = SimpleMailMessage().apply {
            from = properties.mail.from
            setTo(to)
            subject = properties.mail.subject
            text = """
                한짝 비밀번호 재설정 요청을 받았습니다.

                아래 링크에서 비밀번호를 변경해 주세요.
                $resetLink

                이 링크는 ${properties.tokenLifetime.toMinutes()}분 동안 한 번만 사용할 수 있습니다.
                요청하지 않았다면 이 메일을 무시해 주세요.
            """.trimIndent()
        }
        mailSender.send(message)
    }
}
