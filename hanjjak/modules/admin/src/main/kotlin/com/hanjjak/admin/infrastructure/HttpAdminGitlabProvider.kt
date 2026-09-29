package com.hanjjak.admin.infrastructure

import com.fasterxml.jackson.databind.JsonNode
import com.hanjjak.admin.api.AdminProperties
import com.hanjjak.admin.application.AdminGitlabProfile
import com.hanjjak.admin.application.AdminGitlabProvider
import org.springframework.http.MediaType
import org.springframework.stereotype.Component
import org.springframework.util.LinkedMultiValueMap
import org.springframework.web.client.RestClient

@Component
class HttpAdminGitlabProvider(
    private val properties: AdminProperties,
    restClientBuilder: RestClient.Builder,
) : AdminGitlabProvider {
    private val restClient = restClientBuilder.build()

    override fun exchange(code: String, redirectUri: String, codeVerifier: String): AdminGitlabProfile {
        val configuration = properties.gitlab
        val form = LinkedMultiValueMap<String, String>().apply {
            add("grant_type", "authorization_code")
            add("client_id", configuration.clientId)
            add("client_secret", configuration.clientSecret)
            add("redirect_uri", redirectUri)
            add("code", code)
            add("code_verifier", codeVerifier)
        }
        val token = runCatching {
            restClient.post()
                .uri(configuration.tokenUri)
                .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                .accept(MediaType.APPLICATION_JSON)
                .body(form)
                .retrieve()
                .body(JsonNode::class.java)
        }.getOrElse { throw IllegalArgumentException("ADMIN_GITLAB_RESPONSE_INVALID") }
            ?: throw IllegalArgumentException("ADMIN_GITLAB_RESPONSE_INVALID")
        val accessToken = token.path("access_token").asText().takeIf(String::isNotBlank)
            ?: throw IllegalArgumentException("ADMIN_GITLAB_RESPONSE_INVALID")
        val user = runCatching {
            restClient.get()
                .uri(configuration.userUri)
                .headers { it.setBearerAuth(accessToken) }
                .accept(MediaType.APPLICATION_JSON)
                .retrieve()
                .body(JsonNode::class.java)
        }.getOrElse { throw IllegalArgumentException("ADMIN_GITLAB_RESPONSE_INVALID") }
            ?: throw IllegalArgumentException("ADMIN_GITLAB_RESPONSE_INVALID")

        return AdminGitlabProfile(
            id = user.path("id").asLong().takeIf { it > 0 }
                ?: throw IllegalArgumentException("ADMIN_GITLAB_RESPONSE_INVALID"),
            username = user.path("username").asText().takeIf(String::isNotBlank)
                ?: throw IllegalArgumentException("ADMIN_GITLAB_RESPONSE_INVALID"),
            name = user.path("name").asText().takeIf(String::isNotBlank) ?: user.path("username").asText(),
            state = user.path("state").asText().takeIf(String::isNotBlank)
                ?: throw IllegalArgumentException("ADMIN_GITLAB_RESPONSE_INVALID"),
            locked = user.path("locked").asBoolean(false),
        )
    }
}
