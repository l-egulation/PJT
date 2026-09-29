plugins {
    kotlin("jvm")
    kotlin("plugin.spring")
    id("org.springframework.boot")
    id("io.spring.dependency-management")
}

dependencies {
    implementation(project(":modules:account"))
    implementation(project(":modules:admin"))
    implementation(project(":modules:chat"))
    implementation(project(":modules:stage"))
    implementation(project(":modules:battle"))
    implementation(project(":modules:cosmetics"))
    implementation(project(":modules:inventory"))
    implementation(project(":modules:wallet"))
    implementation(project(":modules:progression"))
    implementation(project(":modules:equipment"))
    implementation(project(":modules:skills"))
    implementation(project(":modules:gems"))
    implementation(project(":modules:mail"))
    implementation(project(":modules:market"))
    implementation(project(":modules:events"))
    implementation(project(":modules:raid"))
    implementation(project(":packages:sim-core"))
    implementation("org.springframework.boot:spring-boot-starter-actuator")
    implementation("org.springframework.boot:spring-boot-starter-data-jdbc")
    implementation("org.springframework.boot:spring-boot-starter-security")
    implementation("org.springframework.boot:spring-boot-starter-web")
    implementation("org.springframework.boot:spring-boot-starter-validation")
    implementation("org.springframework.session:spring-session-jdbc")
    implementation("org.flywaydb:flyway-core")
    implementation("org.flywaydb:flyway-database-postgresql")
    runtimeOnly("org.postgresql:postgresql")
    testImplementation("org.springframework.boot:spring-boot-starter-test")
    testImplementation("org.springframework.security:spring-security-test")
    testImplementation("org.testcontainers:junit-jupiter")
    testImplementation("org.testcontainers:postgresql")
    testImplementation(kotlin("test"))
}

tasks.named<org.springframework.boot.gradle.tasks.bundling.BootJar>("bootJar") {
    archiveFileName.set("game-api.jar")
}

tasks.named<Test>("test") {
    maxParallelForks = if (System.getenv("CI") == "true") 2 else 1
}


sourceSets.main {
    resources.srcDir(rootProject.file("packages/game-content/versions/v1"))
    resources.srcDir(rootProject.file("packages/game-content/versions"))
}
