plugins {
    kotlin("jvm")
    application
    id("io.spring.dependency-management")
}
dependencies {
    implementation(platform("org.springframework.boot:spring-boot-dependencies:3.5.16"))
    implementation(project(":packages:sim-core"))
    implementation(project(":modules:progression"))
    implementation(project(":modules:equipment"))
    implementation(project(":modules:inventory"))
    implementation(project(":modules:gems"))
    implementation("com.fasterxml.jackson.core:jackson-databind")
    implementation("com.fasterxml.jackson.module:jackson-module-kotlin")
    testImplementation(kotlin("test"))
}
sourceSets.main {
    resources.srcDir(rootProject.file("packages/game-content/versions"))
}
application { mainClass.set("com.hanjjak.balancelab.MainKt") }
tasks.named<JavaExec>("run") { workingDir(rootProject.projectDir) }
tasks.withType<Test>().configureEach { workingDir(rootProject.projectDir) }


tasks.register<JavaExec>("dungeonBalance") {
    group = "verification"
    description = "Derives and verifies candidate gem dungeon boss values."
    classpath = sourceSets.main.get().runtimeClasspath
    mainClass.set("com.hanjjak.balancelab.DungeonBalanceKt")
    workingDir(rootProject.projectDir)
}

tasks.register<JavaExec>("raidBalance") {
    group = "verification"
    description = "Derives and verifies daily seal raid boss and grade values."
    classpath = sourceSets.main.get().runtimeClasspath
    mainClass.set("com.hanjjak.balancelab.RaidBalanceKt")
    workingDir(rootProject.projectDir)
}
