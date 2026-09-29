plugins {
    kotlin("jvm")
    kotlin("plugin.spring")
    id("io.spring.dependency-management")
}
apply(plugin = "org.springframework.boot")
tasks.named("bootJar") { enabled = false }
tasks.named("jar") { enabled = true }
dependencies {
    implementation(project(":modules:wallet"))
    implementation("org.springframework.boot:spring-boot-starter-web")
    implementation("org.springframework.boot:spring-boot-starter-jdbc")
    testImplementation(kotlin("test"))
}
