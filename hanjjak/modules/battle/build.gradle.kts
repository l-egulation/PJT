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
    implementation(project(":modules:account"))
    implementation(project(":modules:stage"))
    implementation(project(":modules:inventory"))
    implementation(project(":modules:progression"))
    implementation(project(":modules:events"))
    implementation("org.springframework.boot:spring-boot-starter-jdbc")
    implementation("org.springframework.boot:spring-boot-starter-web")
    implementation(project(":modules:skills"))
    implementation(project(":modules:wallet"))
    testImplementation(kotlin("test"))
}
