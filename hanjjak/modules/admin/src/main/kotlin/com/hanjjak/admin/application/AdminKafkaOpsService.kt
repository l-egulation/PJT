package com.hanjjak.admin.application

import org.springframework.jdbc.core.simple.JdbcClient
import org.springframework.stereotype.Service

@Service
class AdminKafkaOpsService(private val jdbc: JdbcClient) {
    data class ConsumerOps(val name: String, val events: Long, val duplicates: Long, val failures: Long, val batches: Long, val averageBatchSize: Double, val processingSeconds: Double)
    data class KafkaOps(val outboxClaimed: Long, val outboxSent: Long, val outboxRetry: Long, val outboxFailed: Long, val consumers: List<ConsumerOps>)

    fun snapshot(): KafkaOps {
        val outbox = jdbc.sql("select coalesce((select value from kafka_ops_metric where metric_name='outbox.claimed'),0) claimed, coalesce((select value from kafka_ops_metric where metric_name='outbox.sent'),0) sent, coalesce((select value from kafka_ops_metric where metric_name='outbox.retry'),0) retries, coalesce((select value from kafka_ops_metric where metric_name='outbox.failed'),0) failed")
            .query { row, _ -> longArrayOf(row.getLong("claimed"), row.getLong("sent"), row.getLong("retries"), row.getLong("failed")) }.single()
        val consumers = jdbc.sql("select consumer_name, events, duplicates, failures, batches, case when batches=0 then 0 else batch_size::double precision / batches end average_batch_size, processing_millis::double precision / 1000 processing_seconds from kafka_ops_consumer_metric order by consumer_name")
            .query { row, _ -> ConsumerOps(row.getString("consumer_name"), row.getLong("events"), row.getLong("duplicates"), row.getLong("failures"), row.getLong("batches"), row.getDouble("average_batch_size"), row.getDouble("processing_seconds")) }.list()
        return KafkaOps(outbox[0], outbox[1], outbox[2], outbox[3], consumers)
    }
}
