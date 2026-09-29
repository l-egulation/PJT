plugins {
    kotlin("jvm")
    kotlin("plugin.spring")
    id("io.spring.dependency-management")
}
apply(plugin = "org.springframework.boot")
tasks.named("bootJar") { enabled = false }
tasks.named("jar") { enabled = true }
dependencies {
    implementation(project(":modules:inventory"))
    implementation(project(":packages:sim-core"))
    implementation(project(":modules:wallet"))
    implementation("com.fasterxml.jackson.core:jackson-databind")
    implementation("org.springframework.boot:spring-boot-starter-jdbc")
    implementation("org.springframework.boot:spring-boot-starter-web")
    testImplementation(kotlin("test"))
}
sourceSets.main {
    resources.srcDir(rootProject.file("packages/game-content/versions/v1"))
}
