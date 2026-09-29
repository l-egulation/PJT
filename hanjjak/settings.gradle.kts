pluginManagement {
    repositories {
        gradlePluginPortal()
        mavenCentral()
    }
}

rootProject.name = "hanjjak"

include(
    ":apps:game-api",
    ":apps:market-worker",
    ":apps:event-consumers",
    ":modules:cosmetics",
    ":apps:market-benchmark",
    ":apps:balance-lab",
    ":packages:sim-core",
    ":modules:account",
    ":modules:admin",
    ":modules:chat",
    ":modules:inventory",
    ":modules:wallet",
    ":modules:mail",
    ":modules:market",
    ":modules:stage",
    ":modules:progression",
    ":modules:equipment",
    ":modules:skills",
    ":modules:gems",
    ":modules:events",
    ":modules:battle",
    ":modules:raid",
)
