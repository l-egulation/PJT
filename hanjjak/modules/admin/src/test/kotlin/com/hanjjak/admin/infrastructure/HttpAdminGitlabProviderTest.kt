package com.hanjjak.admin.infrastructure

import com.hanjjak.admin.api.AdminProperties
import org.springframework.http.HttpMethod
import org.springframework.http.MediaType
import org.springframework.test.web.client.MockRestServiceServer
import org.springframework.test.web.client.response.MockRestResponseCreators.withSuccess
import org.springframework.web.client.RestClient
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith

class HttpAdminGitlabProviderTest {
    private val properties = AdminProperties(
        gitlab = AdminProperties.Gitlab(
            enabled = true,
            clientId = "client-id",
            clientSecret = "client-secret",
            tokenUri = "https://gitlab.example.com/oauth/token",
            userUri = "https://gitlab.example.com/api/v4/user",
            allowedUsernames = listOf("allowed-admin"),
        ),
    )

    @Test
    fun `reads the authenticated GitLab username with read user access`() {
        val builder = RestClient.builder()
        val server = MockRestServiceServer.bindTo(builder).build()
        server.expect { request ->
            assertEquals(HttpMethod.POST, request.method)
            assertEquals("https://gitlab.example.com/oauth/token", request.uri.toString())
        }.andRespond(withSuccess("""{"access_token":"oauth-access-token"}""", MediaType.APPLICATION_JSON))
        server.expect { request ->
            assertEquals(HttpMethod.GET, request.method)
            assertEquals("https://gitlab.example.com/api/v4/user", request.uri.toString())
            assertEquals("Bearer oauth-access-token", request.headers.getFirst("Authorization"))
        }.andRespond(withSuccess("""{"id":314,"username":"allowed-admin","name":"Allowed Admin","state":"active","locked":false}""", MediaType.APPLICATION_JSON))

        val profile = HttpAdminGitlabProvider(properties, builder).exchange("code", "https://app.example.com/callback", "verifier")

        assertEquals(314, profile.id)
        assertEquals("allowed-admin", profile.username)
        server.verify()
    }

    @Test
    fun `rejects a GitLab response without a stable user id`() {
        val builder = RestClient.builder()
        val server = MockRestServiceServer.bindTo(builder).build()
        server.expect { true }.andRespond(withSuccess("""{"access_token":"oauth-access-token"}""", MediaType.APPLICATION_JSON))
        server.expect { true }.andRespond(withSuccess("""{"username":"allowed-admin","name":"Allowed Admin","state":"active"}""", MediaType.APPLICATION_JSON))

        val error = assertFailsWith<IllegalArgumentException> {
            HttpAdminGitlabProvider(properties, builder).exchange("code", "https://app.example.com/callback", "verifier")
        }

        assertEquals("ADMIN_GITLAB_RESPONSE_INVALID", error.message)
    }
}
