plugins {
    kotlin("jvm")
    application
}

dependencies {
    implementation("org.postgresql:postgresql:42.7.8")
    testImplementation(kotlin("test"))
}

application { mainClass.set("com.hanjjak.marketbenchmark.MainKt") }

tasks.named<JavaExec>("run") { workingDir(rootProject.projectDir) }
