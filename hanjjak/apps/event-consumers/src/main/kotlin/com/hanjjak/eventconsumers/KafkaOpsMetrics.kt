package com.hanjjak.eventconsumers

import io.micrometer.core.instrument.MeterRegistry
import io.micrometer.core.instrument.Timer
import org.springframework.beans.factory.ObjectProvider
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration
import org.springframework.jdbc.core.simple.JdbcClient
import java.time.Duration

@Configuration
class KafkaOpsMetricsConfiguration {
    @Bean
    fun kafkaOpsMetrics(registry: MeterRegistry, jdbc: ObjectProvider<JdbcClient>) = KafkaOpsMetrics(registry, jdbc.ifAvailable)
}
class KafkaOpsMetrics(private val registry: MeterRegistry, private val jdbc: JdbcClient? = null) {
    fun outboxClaimed(count: Int) { counter("outbox.events.claimed").increment(count.toDouble()); updateOutbox("claimed", count) }
    fun outboxSent() { counter("outbox.events.sent").increment(); updateOutbox("sent", 1) }
    fun outboxRetry() { counter("outbox.events.retry").increment(); updateOutbox("retry", 1) }
    fun outboxFailed() { counter("outbox.events.failed").increment(); updateOutbox("failed", 1) }

    fun consumerBatch(consumer: String, size: Int) {
        counter("kafka.consumer.batches", consumer).increment()
        registry.summary("kafka.consumer.batch.size", "consumer", consumer).record(size.toDouble())
        updateConsumer(consumer, batches = 1, batchSize = size)
    }

    fun consumerEvents(consumer: String, count: Int) { counter("kafka.consumer.events", consumer).increment(count.toDouble()); updateConsumer(consumer, events = count) }
    fun consumerDuplicates(consumer: String, count: Int) { counter("kafka.consumer.duplicates", consumer).increment(count.toDouble()); updateConsumer(consumer, duplicates = count) }
    fun consumerFailed(consumer: String) { counter("kafka.consumer.failures", consumer).increment(); updateConsumer(consumer, failures = 1) }
    fun consumerLatency(consumer: String, duration: Duration) { timer("kafka.consumer.processing", consumer).record(duration); updateConsumer(consumer, processingMillis = duration.toMillis()) }

    private fun updateOutbox(metric: String, amount: Int) {
        jdbc?.sql("insert into kafka_ops_metric(metric_name,value) values (:name,:amount) on conflict (metric_name) do update set value=kafka_ops_metric.value + excluded.value, updated_at=now()")
            ?.params(mapOf("name" to "outbox.$metric", "amount" to amount))?.update()
    }

    private fun updateConsumer(consumer: String, events: Int = 0, duplicates: Int = 0, failures: Int = 0, batches: Int = 0, batchSize: Int = 0, processingMillis: Long = 0) {
        jdbc?.sql("""
            insert into kafka_ops_consumer_metric(consumer_name,events,duplicates,failures,batches,batch_size,processing_millis)
            values (:consumer,:events,:duplicates,:failures,:batches,:batchSize,:processingMillis)
            on conflict (consumer_name) do update set
              events=kafka_ops_consumer_metric.events + excluded.events,
              duplicates=kafka_ops_consumer_metric.duplicates + excluded.duplicates,
              failures=kafka_ops_consumer_metric.failures + excluded.failures,
              batches=kafka_ops_consumer_metric.batches + excluded.batches,
              batch_size=kafka_ops_consumer_metric.batch_size + excluded.batch_size,
              processing_millis=kafka_ops_consumer_metric.processing_millis + excluded.processing_millis,
              updated_at=now()
        """.trimIndent())?.params(mapOf("consumer" to consumer, "events" to events, "duplicates" to duplicates, "failures" to failures, "batches" to batches, "batchSize" to batchSize, "processingMillis" to processingMillis))?.update()
    }

    private fun counter(name: String, consumer: String? = null) = registry.counter(name, *(consumer?.let { arrayOf("consumer", it) } ?: emptyArray()))
    private fun timer(name: String, consumer: String) = Timer.builder(name).tag("consumer", consumer).register(registry)
}
