package com.hanjjak.marketworker

import org.springframework.boot.autoconfigure.SpringBootApplication
import org.springframework.boot.runApplication

@SpringBootApplication
class MarketWorkerApplication

fun main(args: Array<String>) = runApplication<MarketWorkerApplication>(*args).let { Unit }
