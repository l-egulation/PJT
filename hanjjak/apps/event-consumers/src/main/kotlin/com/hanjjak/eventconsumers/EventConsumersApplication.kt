package com.hanjjak.eventconsumers

import org.springframework.boot.autoconfigure.SpringBootApplication
import org.springframework.boot.runApplication
import org.springframework.kafka.annotation.EnableKafka

@EnableKafka
@SpringBootApplication
class EventConsumersApplication

fun main(args: Array<String>) = runApplication<EventConsumersApplication>(*args).let { Unit }
