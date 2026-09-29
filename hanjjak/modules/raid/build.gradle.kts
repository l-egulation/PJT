plugins {
    kotlin("jvm")
    kotlin("plugin.spring")
    id("io.spring.dependency-management")
}

apply(plugin = "org.springframework.boot")
tasks.named("bootJar") { enabled = false }
tasks.named("jar") { enabled = true }

dependencies {
    implementation(project(":packages:sim-core"))
    implementation(project(":modules:events"))
    implementation("com.fasterxml.jackson.core:jackson-databind")
    implementation("org.springframework.boot:spring-boot-starter-jdbc")
    implementation("org.springframework.boot:spring-boot-starter-web")
    testImplementation("org.flywaydb:flyway-core")
    testImplementation("org.flywaydb:flyway-database-postgresql")
    testImplementation("org.testcontainers:junit-jupiter")
    testImplementation("org.testcontainers:postgresql")
    testRuntimeOnly("org.postgresql:postgresql")
    testImplementation(kotlin("test"))
}
