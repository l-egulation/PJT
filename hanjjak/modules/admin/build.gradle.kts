plugins {
    kotlin("jvm")
    kotlin("plugin.spring")
    id("io.spring.dependency-management")
}
apply(plugin = "org.springframework.boot")
tasks.named("bootJar") { enabled = false }
tasks.named("jar") { enabled = true }
dependencies {
    implementation(project(":modules:chat"))
    implementation(project(":modules:events"))
    implementation(project(":modules:market"))
    implementation(project(":modules:cosmetics"))
    implementation(project(":modules:equipment"))
    implementation(project(":modules:gems"))
    implementation(project(":modules:inventory"))
    implementation(project(":modules:progression"))
    implementation(project(":modules:stage"))
    implementation("org.springframework.boot:spring-boot-starter-data-jdbc")
    implementation("org.springframework.boot:spring-boot-starter-web")
    testImplementation("com.h2database:h2")
    testImplementation(kotlin("test"))
    testImplementation("org.springframework.boot:spring-boot-starter-test")
    testImplementation("org.springframework:spring-test")
}
