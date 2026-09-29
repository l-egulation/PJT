package com.hanjjak.market.infrastructure

import com.fasterxml.jackson.core.type.TypeReference
import com.fasterxml.jackson.databind.ObjectMapper
import com.hanjjak.market.application.OrderBookRepository
import com.hanjjak.market.domain.*
import org.springframework.jdbc.core.simple.JdbcClient
import java.sql.Array as SqlArray
import java.sql.Timestamp
import java.time.Instant
import java.util.UUID

private val SYSTEM_MARKET_ACCOUNT_ID: UUID = UUID.fromString("00000000-0000-4000-8000-000000000001")
private val SYSTEM_MARKET_CHARACTER_ID: UUID = UUID.fromString("00000000-0000-4000-8000-000000000002")
private const val SYSTEM_MARKET_EMAIL = "system-market@internal.hanjjak"

class JdbcOrderBookRepository(
    private val jdbc: JdbcClient,
    private val mapper: ObjectMapper,
) : OrderBookRepository {
    override fun stateVersion(accountId: UUID): Long = jdbc.sql("select state_version from account where id=:account")
        .param("account", accountId).query(Long::class.java).single()

    override fun command(accountId: UUID, idempotencyKey: UUID): OrderBookRepository.StoredCommand? = jdbc.sql("select command_id,fingerprint,result_json::text from command_record where account_id=:account and idempotency_key=:key")
        .params(mapOf("account" to accountId, "key" to idempotencyKey))
        .query { row, _ -> OrderBookRepository.StoredCommand(row.getObject("command_id", UUID::class.java), row.getString("fingerprint"), row.getString("result_json")) }
        .optional().orElse(null)

    override fun saveCommand(commandId: UUID, accountId: UUID, idempotencyKey: UUID, fingerprint: String, resultJson: String) {
        jdbc.sql("insert into command_record(command_id,account_id,idempotency_key,fingerprint,status,result_json,expires_at) values (:command,:account,:key,:fingerprint,'SUCCEEDED',cast(:result as jsonb),now()+interval '7 days')")
            .params(mapOf("command" to commandId, "account" to accountId, "key" to idempotencyKey, "fingerprint" to fingerprint, "result" to resultJson)).update()
    }

    override fun synchronizeInstruments(instruments: List<MarketInstrument>, now: Instant) {
        instruments.forEach { instrument ->
            jdbc.sql("""
                insert into market_instrument(instrument_id,canonical_key,item_id,display_name,category,attributes,status,revision,created_at,updated_at)
                values (:id,:key,:item,:name,:category,cast(:attributes as jsonb),'ACTIVE',1,:now,:now)
                on conflict(instrument_id) do update set item_id=excluded.item_id,display_name=excluded.display_name,category=excluded.category,
                  attributes=excluded.attributes,status=case when market_instrument.status='INACTIVE' then 'ACTIVE' else market_instrument.status end,updated_at=excluded.updated_at
            """.trimIndent()).params(mapOf(
                "id" to instrument.instrumentId, "key" to instrument.canonicalKey, "item" to instrument.itemId,
                "name" to instrument.displayName, "category" to instrument.category,
                "attributes" to mapper.writeValueAsString(instrument.attributes), "now" to Timestamp.from(now),
            )).update()
        }
        if (instruments.isNotEmpty()) {
            jdbc.sql("update market_instrument set status='INACTIVE',updated_at=:now where status='ACTIVE' and instrument_id not in (:ids)")
                .params(mapOf("now" to Timestamp.from(now), "ids" to instruments.map { it.instrumentId })).update()
        }
    }

    override fun instruments(accountId: UUID): List<MarketInstrumentSummary> = jdbc.sql("""
        select i.*,
          (select max(o.limit_unit_price) from market_order o where o.instrument_id=i.instrument_id and o.side='BUY' and o.status in ('ACTIVE','PARTIALLY_FILLED')) best_bid,
          (select min(o.limit_unit_price) from market_order o where o.instrument_id=i.instrument_id and o.side='SELL' and o.status in ('ACTIVE','PARTIALLY_FILLED')) best_ask,
          (select t.unit_price from market_trade t where t.instrument_id=i.instrument_id order by t.filled_at desc,t.trade_id desc limit 1) last_trade
        from market_instrument i where i.status<>'INACTIVE' order by i.category,i.display_name,i.instrument_id
    """.trimIndent()).query { row, _ ->
        MarketInstrumentSummary(
            row.getObject("instrument_id", UUID::class.java), row.getString("canonical_key"), row.getString("item_id"),
            row.getString("display_name"), row.getString("category"), attributes(row.getString("attributes")),
            MarketInstrumentStatus.valueOf(row.getString("status")), nullableLong(row, "best_bid"), nullableLong(row, "best_ask"),
            nullableLong(row, "last_trade"), row.getLong("revision"),
        )
    }.list()

    override fun instrument(instrumentId: UUID): MarketInstrument? = jdbc.sql("select * from market_instrument where instrument_id=:id")
        .param("id", instrumentId).query(::mapInstrument).optional().orElse(null)

    override fun lockInstrument(instrumentId: UUID): MarketInstrument? = jdbc.sql("select * from market_instrument where instrument_id=:id for update")
        .param("id", instrumentId).query(::mapInstrument).optional().orElse(null)

    override fun incrementRevision(instrumentId: UUID): Long = jdbc.sql("update market_instrument set revision=revision+1,updated_at=now() where instrument_id=:id returning revision")
        .param("id", instrumentId).query(Long::class.java).single()

    override fun lockAccountControl(accountId: UUID, now: Instant): OrderBookRepository.MutationWindow {
        jdbc.sql("insert into market_account_control(account_id,created_at,updated_at) values (:account,:now,:now) on conflict(account_id) do nothing")
            .params(mapOf("account" to accountId, "now" to Timestamp.from(now))).update()
        jdbc.sql("select account_id from market_account_control where account_id=:account for update").param("account", accountId).query(UUID::class.java).single()
        val mutations = jdbc.sql("select count(*) from market_order_mutation_history where account_id=:account and created_at>:since")
            .params(mapOf("account" to accountId, "since" to Timestamp.from(now.minusSeconds(60)))).query(Int::class.java).single()
        val active = jdbc.sql("select count(*) from market_order where account_id=:account and status in ('ACTIVE','PARTIALLY_FILLED')")
            .param("account", accountId).query(Int::class.java).single()
        return OrderBookRepository.MutationWindow(mutations, active)
    }

    override fun appendMutation(mutationId: UUID, accountId: UUID, instrumentId: UUID, orderId: UUID, side: MarketOrderSide, type: String, commandId: UUID, createdAt: Instant) {
        jdbc.sql("insert into market_order_mutation_history(mutation_id,account_id,instrument_id,order_id,side,mutation_type,command_id,created_at) values (:mutation,:account,:instrument,:order,:side,:type,:command,:created)")
            .params(mapOf("mutation" to mutationId, "account" to accountId, "instrument" to instrumentId, "order" to orderId, "side" to side.name, "type" to type, "command" to commandId, "created" to Timestamp.from(createdAt))).update()
    }

    override fun order(orderId: UUID): MarketOrder? = jdbc.sql("select * from market_order where order_id=:order").param("order", orderId).query(::mapOrder).optional().orElse(null)
    override fun lockOrder(orderId: UUID): MarketOrder? = jdbc.sql("select * from market_order where order_id=:order for update").param("order", orderId).query(::mapOrder).optional().orElse(null)

    override fun createOrder(order: MarketOrder) {
        jdbc.sql("""
            insert into market_order(order_id,account_id,instrument_id,side,time_in_force,initial_quantity,filled_quantity,remaining_quantity,limit_unit_price,reserved_rice,status,priority_at,created_at,updated_at,expires_at,display_name_snapshot,instance_ids)
            values (:order,:account,:instrument,:side,:tif,:initial,:filled,:remaining,:price,:reserved,:status,:priority,:created,:updated,:expires,:name,:instances)
        """.trimIndent()).params(orderParams(order)).update()
    }

    override fun updateOrder(order: MarketOrder) {
        jdbc.sql("""
            update market_order set initial_quantity=:initial,filled_quantity=:filled,remaining_quantity=:remaining,limit_unit_price=:price,
              reserved_rice=:reserved,status=:status,priority_at=:priority,updated_at=:updated,expires_at=:expires,instance_ids=:instances where order_id=:order
        """.trimIndent()).params(orderParams(order)).update()
    }

    override fun matchCandidates(instrumentId: UUID, incomingSide: MarketOrderSide, limitUnitPrice: Long, now: Instant): List<MarketOrder> {
        val opposite = if (incomingSide == MarketOrderSide.BUY) MarketOrderSide.SELL else MarketOrderSide.BUY
        val priceClause = if (incomingSide == MarketOrderSide.BUY) "limit_unit_price<=:price" else "limit_unit_price>=:price"
        val ordering = if (opposite == MarketOrderSide.SELL) "limit_unit_price,priority_at,order_id" else "limit_unit_price desc,priority_at,order_id"
        return jdbc.sql("select * from market_order where instrument_id=:instrument and side=:side and status in ('ACTIVE','PARTIALLY_FILLED') and $priceClause and (expires_at is null or expires_at>:now) order by $ordering for update")
            .params(mapOf("instrument" to instrumentId, "side" to opposite.name, "price" to limitUnitPrice, "now" to Timestamp.from(now))).query(::mapOrder).list()
    }

    override fun crossingOwnOrder(instrumentId: UUID, accountId: UUID, incomingSide: MarketOrderSide, limitUnitPrice: Long, now: Instant): Boolean {
        val opposite = if (incomingSide == MarketOrderSide.BUY) MarketOrderSide.SELL else MarketOrderSide.BUY
        val priceClause = if (incomingSide == MarketOrderSide.BUY) "limit_unit_price<=:price" else "limit_unit_price>=:price"
        return jdbc.sql("select exists(select 1 from market_order where instrument_id=:instrument and account_id=:account and side=:side and status in ('ACTIVE','PARTIALLY_FILLED') and $priceClause and expires_at>:now)")
            .params(mapOf("instrument" to instrumentId, "account" to accountId, "side" to opposite.name, "price" to limitUnitPrice, "now" to Timestamp.from(now))).query(Boolean::class.java).single()
    }

    override fun depth(instrumentId: UUID, accountId: UUID, side: MarketOrderSide, levels: Int, now: Instant): List<MarketDepthLevel> {
        val ordering = if (side == MarketOrderSide.BUY) "limit_unit_price desc" else "limit_unit_price"
        val rows = jdbc.sql("""
            select limit_unit_price,sum(remaining_quantity) total_quantity,
              coalesce(sum(remaining_quantity) filter(where account_id=:account),0) my_quantity
            from market_order where instrument_id=:instrument and side=:side and status in ('ACTIVE','PARTIALLY_FILLED') and expires_at>:now
            group by limit_unit_price order by $ordering limit :levels
        """.trimIndent()).params(mapOf("account" to accountId, "instrument" to instrumentId, "side" to side.name, "now" to Timestamp.from(now), "levels" to levels))
            .query { row, _ -> Triple(row.getLong("limit_unit_price"), row.getLong("total_quantity"), row.getLong("my_quantity")) }.list()
        var cumulative = 0L
        return rows.map { (price, total, mine) -> cumulative = Math.addExact(cumulative, total); MarketDepthLevel(price, total, cumulative, mine) }
    }
    override fun priceLevels(instrumentId: UUID, accountId: UUID, side: MarketOrderSide, afterUnitPrice: Long?, limit: Int, now: Instant): List<MarketPriceLevel> {
        val ordering = if (side == MarketOrderSide.BUY) "unit_price desc" else "unit_price"
        val continuation = if (afterUnitPrice == null) "" else if (side == MarketOrderSide.BUY) " and unit_price < :afterPrice" else " and unit_price > :afterPrice"
        val params = mutableMapOf<String, Any>(
            "account" to accountId,
            "instrument" to instrumentId,
            "side" to side.name,
            "now" to Timestamp.from(now),
            "limit" to limit,
        )
        if (afterUnitPrice != null) params["afterPrice"] = afterUnitPrice
        return jdbc.sql("""
            with grouped as (
              select limit_unit_price as unit_price,
                sum(remaining_quantity) as total_quantity,
                coalesce(sum(remaining_quantity) filter (where account_id=:account),0) as my_quantity
              from market_order
              where instrument_id=:instrument and side=:side
                and status in ('ACTIVE','PARTIALLY_FILLED') and expires_at>:now
              group by limit_unit_price
            ), accumulated as (
              select unit_price,total_quantity,my_quantity,
                sum(total_quantity) over (order by $ordering rows between unbounded preceding and current row) as cumulative_quantity,
                sum(total_quantity - my_quantity) over (order by $ordering rows between unbounded preceding and current row) as cumulative_other_quantity
              from grouped
            )
            select unit_price,total_quantity,cumulative_quantity,my_quantity,cumulative_other_quantity
            from accumulated
            where 1=1$continuation
            order by $ordering
            limit :limit
        """.trimIndent()).params(params)
            .query { row, _ ->
                MarketPriceLevel(
                    row.getLong("unit_price"),
                    row.getLong("total_quantity"),
                    row.getLong("cumulative_quantity"),
                    row.getLong("my_quantity"),
                    row.getLong("cumulative_other_quantity"),
                )
            }.list()
    }
    override fun priceLevelCount(instrumentId: UUID, side: MarketOrderSide, now: Instant): Long = jdbc.sql("select count(distinct limit_unit_price) from market_order where instrument_id=:instrument and side=:side and status in ('ACTIVE','PARTIALLY_FILLED') and expires_at>:now")
        .params(mapOf("instrument" to instrumentId, "side" to side.name, "now" to Timestamp.from(now))).query(Long::class.java).single()


    override fun recentTrades(instrumentId: UUID, limit: Int): List<MarketRecentTrade> = jdbc.sql("select trade_id,quantity,unit_price,total_price,filled_at from market_trade where instrument_id=:instrument order by filled_at desc,trade_id desc limit :limit")
        .params(mapOf("instrument" to instrumentId, "limit" to limit)).query { row, _ -> MarketRecentTrade(row.getObject("trade_id", UUID::class.java), row.getLong("quantity"), row.getLong("unit_price"), row.getLong("total_price"), row.getTimestamp("filled_at").toInstant()) }.list()

    override fun candles(instrumentId: UUID, interval: MarketCandleInterval, from: Instant, to: Instant): List<MarketCandle> {
        val bucketSeconds = interval.bucketSeconds
        return jdbc.sql("""
            with bucketed as (
              select filled_at, trade_id, unit_price, quantity, total_price,
                to_timestamp(floor(extract(epoch from filled_at) / :bucket) * :bucket) opened_at
              from market_trade
              where instrument_id=:instrument and filled_at>=:from and filled_at<:to
            )
            select opened_at,
              (array_agg(unit_price order by filled_at,trade_id))[1] open_price,
              max(unit_price) high_price, min(unit_price) low_price,
              (array_agg(unit_price order by filled_at desc,trade_id desc))[1] close_price,
              sum(quantity) quantity, sum(total_price) total_price
            from bucketed
            group by opened_at
            order by opened_at
        """.trimIndent()).params(mapOf("instrument" to instrumentId, "from" to Timestamp.from(from), "to" to Timestamp.from(to), "bucket" to bucketSeconds))
            .query { row, _ -> MarketCandle(row.getTimestamp("opened_at").toInstant(), row.getLong("open_price"), row.getLong("high_price"), row.getLong("low_price"), row.getLong("close_price"), row.getLong("quantity"), row.getLong("total_price")) }.list()
    }

    override fun listOrders(accountId: UUID, scope: MarketOrderScope, side: MarketOrderSide?, instrumentId: UUID?, cursor: OrderBookRepository.OrderCursor?, limit: Int): List<MarketOrderSummary> {
        val where = orderWhere(accountId, scope, side, instrumentId, cursor)
        return jdbc.sql("select o.*,i.item_id,i.display_name from market_order o join market_instrument i using(instrument_id) where ${where.first} order by o.created_at desc,o.order_id desc limit :limit")
            .params(where.second + ("limit" to limit)).query(::mapOrderSummary).list()
    }

    override fun countOrders(accountId: UUID, scope: MarketOrderScope, side: MarketOrderSide?, instrumentId: UUID?): Long {
        val where = orderWhere(accountId, scope, side, instrumentId, null)
        return jdbc.sql("select count(*) from market_order o where ${where.first}").params(where.second).query(Long::class.java).single()
    }

    override fun expiredOrders(now: Instant, limit: Int): List<MarketOrder> = jdbc.sql("select * from market_order where status in ('ACTIVE','PARTIALLY_FILLED') and expires_at<=:now order by expires_at,order_id limit :limit")
        .params(mapOf("now" to Timestamp.from(now), "limit" to limit)).query(::mapOrder).list()
    override fun listAdminOrders(orderId: UUID?, instrumentId: UUID?, cursor: OrderBookRepository.OrderCursor?, limit: Int): List<AdminMarketOrderSummary> {
        val clauses = mutableListOf<String>()
        val params = mutableMapOf<String, Any>("limit" to limit)
        if (orderId != null) {
            clauses += "o.order_id=:order"
            params["order"] = orderId
        }
        if (instrumentId != null) {
            clauses += "o.instrument_id=:instrument"
            params["instrument"] = instrumentId
        }
        if (cursor != null) {
            clauses += "(o.created_at<:cursorAt or (o.created_at=:cursorAt and o.order_id<:cursorId))"
            params["cursorAt"] = Timestamp.from(cursor.createdAt)
            params["cursorId"] = cursor.orderId
        }
        val where = clauses.takeIf { it.isNotEmpty() }?.joinToString(" and ", prefix = " where ").orEmpty()
        return jdbc.sql("select o.*,i.item_id,i.display_name from market_order o join market_instrument i using(instrument_id)$where order by o.created_at desc,o.order_id desc limit :limit")
            .params(params)
            .query { row, _ ->
                val order = mapOrder(row, 0)
                AdminMarketOrderSummary(
                    order.orderId, order.accountId, order.instrumentId, row.getString("item_id"), row.getString("display_name"), order.side,
                    order.initialQuantity, order.remainingQuantity, order.limitUnitPrice, order.status, order.createdAt, order.updatedAt,
                    order.expiresAt, isSystemMarketAccount(order.accountId),
                )
            }
            .list()
    }

    override fun ensureSystemMarketAccount(now: Instant): UUID {
        jdbc.sql("insert into account(id,email,password_hash,login_email,state_version,created_at) values (:account,:email,null,null,1,:created) on conflict(id) do nothing")
            .params(mapOf("account" to SYSTEM_MARKET_ACCOUNT_ID, "email" to SYSTEM_MARKET_EMAIL, "created" to Timestamp.from(now))).update()
        jdbc.sql("insert into character(id,account_id,level,experience,rice,nickname) values (:character,:account,1,0,0,:nickname) on conflict(account_id) do nothing")
            .params(mapOf("character" to SYSTEM_MARKET_CHARACTER_ID, "account" to SYSTEM_MARKET_ACCOUNT_ID, "nickname" to "거래소 시스템")).update()
        jdbc.sql("insert into wallet_balance(account_id,balance,updated_at) values (:account,0,:updated) on conflict(account_id) do nothing")
            .params(mapOf("account" to SYSTEM_MARKET_ACCOUNT_ID, "updated" to Timestamp.from(now))).update()
        return SYSTEM_MARKET_ACCOUNT_ID
    }

    override fun isSystemMarketAccount(accountId: UUID): Boolean = accountId == SYSTEM_MARKET_ACCOUNT_ID

    override fun createTrade(trade: OrderBookRepository.TradeInsert) {
        jdbc.sql("""
            insert into market_trade(trade_id,listing_id,buyer_account_id,seller_account_id,item_id,quantity,unit_price,total_price,fee,settlement_amount,filled_at,maker_order_id,taker_order_id,instrument_id,display_name_snapshot)
            values (:trade,:listing,:buyer,:seller,:item,:quantity,:price,:total,:fee,:settlement,:filled,:maker,:taker,:instrument,:name)
        """.trimIndent()).params(mapOf(
            "trade" to trade.tradeId, "listing" to trade.legacyListingId, "buyer" to trade.buyerAccountId, "seller" to trade.sellerAccountId,
            "item" to trade.itemId, "quantity" to trade.quantity, "price" to trade.unitPrice, "total" to trade.totalPrice,
            "fee" to trade.fee, "settlement" to trade.settlementAmount, "filled" to Timestamp.from(trade.filledAt),
            "maker" to trade.makerOrderId, "taker" to trade.takerOrderId, "instrument" to trade.instrumentId, "name" to trade.displayName,
        )).update()
    }

    override fun listTrades(accountId: UUID?, instrumentIds: List<UUID>, cursor: OrderBookRepository.TradeCursor?, limit: Int): List<MarketTradeView> {
        if (instrumentIds.isEmpty()) return emptyList()
        val accountClause = if (accountId == null) "" else " and (t.buyer_account_id=:account or t.seller_account_id=:account)"
        val cursorClause = if (cursor == null) "" else " and (t.filled_at<:cursorAt or (t.filled_at=:cursorAt and t.trade_id<:cursorId))"
        val params = mutableMapOf<String, Any>("instruments" to instrumentIds, "limit" to limit)
        if (accountId != null) params["account"] = accountId
        if (cursor != null) { params["cursorAt"] = Timestamp.from(cursor.filledAt); params["cursorId"] = cursor.tradeId }
        return jdbc.sql("select t.*,i.display_name from market_trade t join market_instrument i using(instrument_id) where t.instrument_id in (:instruments)$accountClause$cursorClause order by t.filled_at desc,t.trade_id desc limit :limit")
            .params(params).query { row, _ -> mapTrade(row, accountId) }.list()
    }

    override fun countTrades(accountId: UUID?, instrumentIds: List<UUID>): Long {
        if (instrumentIds.isEmpty()) return 0
        val accountClause = if (accountId == null) "" else " and (buyer_account_id=:account or seller_account_id=:account)"
        val params = mutableMapOf<String, Any>("instruments" to instrumentIds)
        if (accountId != null) params["account"] = accountId
        return jdbc.sql("select count(*) from market_trade where instrument_id in (:instruments)$accountClause").params(params).query(Long::class.java).single()
    }

    override fun createDelivery(delivery: MarketDelivery) {
        jdbc.sql("insert into market_delivery(delivery_id,account_id,instrument_id,order_id,trade_id,source,item_id,instance_ids,quantity,display_name_snapshot,created_at,claimed_at,source_account_id) values (:delivery,:account,:instrument,:order,:trade,:source,:item,:instances,:quantity,:name,:created,:claimed,:sourceAccount)")
            .params(mapOf("delivery" to delivery.deliveryId, "account" to delivery.accountId, "instrument" to delivery.instrumentId, "order" to delivery.orderId, "trade" to delivery.tradeId, "source" to delivery.source.name, "item" to delivery.itemId, "instances" to delivery.instanceIds.toTypedArray(), "quantity" to delivery.quantity, "name" to delivery.displayName, "created" to Timestamp.from(delivery.createdAt), "claimed" to delivery.claimedAt?.let(Timestamp::from), "sourceAccount" to delivery.sourceAccountId)).update()
    }

    override fun listDeliveries(accountId: UUID, claimable: Boolean?, cursor: OrderBookRepository.OrderCursor?, limit: Int): List<MarketDelivery> {
        val claimableClause = when (claimable) { true -> " and claimed_at is null"; false -> " and claimed_at is not null"; null -> "" }
        val cursorClause = if (cursor == null) "" else " and (created_at<:cursorAt or (created_at=:cursorAt and delivery_id<:cursorId))"
        val params = mutableMapOf<String, Any>("account" to accountId, "limit" to limit)
        if (cursor != null) { params["cursorAt"] = Timestamp.from(cursor.createdAt); params["cursorId"] = cursor.orderId }
        return jdbc.sql("select * from market_delivery where account_id=:account$claimableClause$cursorClause order by created_at desc,delivery_id desc limit :limit").params(params).query(::mapDelivery).list()
    }

    override fun countDeliveries(accountId: UUID, claimable: Boolean?): Long {
        val clause = when (claimable) { true -> " and claimed_at is null"; false -> " and claimed_at is not null"; null -> "" }
        return jdbc.sql("select count(*) from market_delivery where account_id=:account$clause").param("account", accountId).query(Long::class.java).single()
    }
    override fun lockDelivery(accountId: UUID, deliveryId: UUID): MarketDelivery? = jdbc.sql("select * from market_delivery where account_id=:account and delivery_id=:delivery for update")
        .params(mapOf("account" to accountId, "delivery" to deliveryId)).query(::mapDelivery).optional().orElse(null)
    override fun lockClaimableDeliveries(accountId: UUID): List<MarketDelivery> = jdbc.sql("select * from market_delivery where account_id=:account and claimed_at is null order by created_at,delivery_id for update")
        .param("account", accountId).query(::mapDelivery).list()
    override fun markDeliveriesClaimed(deliveryIds: List<UUID>, claimedAt: Instant) {
        if (deliveryIds.isEmpty()) return
        jdbc.sql("update market_delivery set claimed_at=:claimed where delivery_id in (:ids) and claimed_at is null").params(mapOf("claimed" to Timestamp.from(claimedAt), "ids" to deliveryIds)).update()
    }

    override fun appendUnread(accountId: UUID, stream: MarketUnreadStream, sourceId: UUID, createdAt: Instant): Long {
        jdbc.sql("insert into market_unread_cursor(account_id,stream) values (:account,:stream) on conflict(account_id,stream) do nothing").params(mapOf("account" to accountId, "stream" to stream.name)).update()
        val sequence = jdbc.sql("update market_unread_cursor set latest_sequence=latest_sequence+1 where account_id=:account and stream=:stream returning latest_sequence")
            .params(mapOf("account" to accountId, "stream" to stream.name)).query(Long::class.java).single()
        jdbc.sql("insert into market_unread_event(account_id,stream,sequence,source_id,created_at) values (:account,:stream,:sequence,:source,:created)")
            .params(mapOf("account" to accountId, "stream" to stream.name, "sequence" to sequence, "source" to sourceId, "created" to Timestamp.from(createdAt))).update()
        return sequence
    }

    override fun unreadStates(accountId: UUID): List<MarketUnreadState> {
        MarketUnreadStream.entries.forEach { stream -> jdbc.sql("insert into market_unread_cursor(account_id,stream) values (:account,:stream) on conflict(account_id,stream) do nothing").params(mapOf("account" to accountId, "stream" to stream.name)).update() }
        return jdbc.sql("select stream,latest_sequence,read_sequence from market_unread_cursor where account_id=:account order by stream").param("account", accountId)
            .query { row, _ -> MarketUnreadState(MarketUnreadStream.valueOf(row.getString("stream")), row.getLong("latest_sequence"), row.getLong("read_sequence"), row.getLong("latest_sequence") - row.getLong("read_sequence")) }.list()
    }

    override fun issueReadThrough(accountId: UUID, stream: MarketUnreadStream, sequence: Long) {
        jdbc.sql("update market_unread_cursor set issued_sequence=greatest(issued_sequence,least(latest_sequence,:sequence)) where account_id=:account and stream=:stream")
            .params(mapOf("account" to accountId, "stream" to stream.name, "sequence" to sequence)).update()
    }

    override fun advanceReadCursor(accountId: UUID, stream: MarketUnreadStream, sequence: Long): Long = jdbc.sql("update market_unread_cursor set read_sequence=greatest(read_sequence,:sequence) where account_id=:account and stream=:stream and :sequence<=issued_sequence returning read_sequence")
        .params(mapOf("account" to accountId, "stream" to stream.name, "sequence" to sequence)).query(Long::class.java).optional().orElseThrow { IllegalArgumentException("UNREAD_SEQUENCE_NOT_ISSUED") }

    override fun migrateLegacyMarket(now: Instant): List<Pair<UUID, Long>> {
        val rows = jdbc.sql("select buy_order_id,buyer_account_id,reserved_rice from market_buy_order where status='ACTIVE' order by buyer_account_id,buy_order_id for update")
            .query { row, _ -> Triple(row.getObject("buy_order_id", UUID::class.java), row.getObject("buyer_account_id", UUID::class.java), row.getLong("reserved_rice")) }.list()
        rows.forEach { (orderId, _, _) ->
            jdbc.sql("update market_buy_order set status='CANCELLED',reserved_rice=0,updated_at=:now where buy_order_id=:order")
                .params(mapOf("now" to Timestamp.from(now), "order" to orderId)).update()
        }
        jdbc.sql("""
            insert into market_order(order_id,account_id,instrument_id,side,time_in_force,initial_quantity,filled_quantity,remaining_quantity,limit_unit_price,reserved_rice,status,priority_at,created_at,updated_at,expires_at,display_name_snapshot,legacy_listing_id)
            select l.listing_id,l.seller_account_id,i.instrument_id,'SELL','GTC',l.initial_quantity,l.initial_quantity-l.remaining_quantity,l.remaining_quantity,l.unit_price,0,
              case l.status when 'ACTIVE' then case when l.remaining_quantity<l.initial_quantity then 'PARTIALLY_FILLED' else 'ACTIVE' end else l.status end,
              l.created_at,l.created_at,l.updated_at,l.expires_at,i.display_name,l.listing_id
            from market_listing l join market_instrument i on i.item_id=l.item_id
            where l.status='ACTIVE' and not exists(select 1 from market_order o where o.legacy_listing_id=l.listing_id)
        """.trimIndent()).update()
        jdbc.sql("""
            insert into market_order(order_id,account_id,instrument_id,side,time_in_force,initial_quantity,filled_quantity,remaining_quantity,limit_unit_price,reserved_rice,status,priority_at,created_at,updated_at,expires_at,display_name_snapshot,legacy_buy_order_id)
            select b.buy_order_id,b.buyer_account_id,i.instrument_id,'BUY','GTC',b.initial_quantity,b.initial_quantity-b.remaining_quantity,0,b.max_unit_price,0,
              case when b.status='FILLED' then 'FILLED' else 'CANCELLED' end,b.created_at,b.created_at,:now,b.expires_at,i.display_name,b.buy_order_id
            from market_buy_order b join market_instrument i on i.item_id=b.item_id
            where not exists(select 1 from market_order o where o.legacy_buy_order_id=b.buy_order_id)
        """.trimIndent()).param("now", Timestamp.from(now)).update()
        jdbc.sql("""
            insert into market_delivery(delivery_id,account_id,instrument_id,order_id,trade_id,source,item_id,quantity,display_name_snapshot,created_at,claimed_at,legacy_purchase_delivery_id)
            select d.delivery_id,d.buyer_account_id,i.instrument_id,d.buy_order_id,d.trade_id,'BUY_FILL',d.item_id,d.quantity,i.display_name,d.created_at,d.claimed_at,d.delivery_id
            from market_purchase_delivery d join market_instrument i on i.item_id=d.item_id
            where not exists(select 1 from market_delivery n where n.legacy_purchase_delivery_id=d.delivery_id)
        """.trimIndent()).update()

        return rows.map { it.second to it.third }
    }

    private fun orderWhere(accountId: UUID, scope: MarketOrderScope, side: MarketOrderSide?, instrumentId: UUID?, cursor: OrderBookRepository.OrderCursor?): Pair<String, MutableMap<String, Any>> {
        val statuses = when (scope) {
            MarketOrderScope.ACTIVE -> listOf("ACTIVE", "PARTIALLY_FILLED")
            MarketOrderScope.CLOSED -> listOf("FILLED", "CANCELLED", "EXPIRED")
            MarketOrderScope.RECOVERY_REVIEW -> listOf("RECOVERY_REVIEW")
        }
        val clauses = mutableListOf("o.account_id=:account", "o.status in (:statuses)")
        val params = mutableMapOf<String, Any>("account" to accountId, "statuses" to statuses)
        if (side != null) { clauses += "o.side=:side"; params["side"] = side.name }
        if (instrumentId != null) { clauses += "o.instrument_id=:instrument"; params["instrument"] = instrumentId }
        if (cursor != null) { clauses += "(o.created_at<:cursorAt or (o.created_at=:cursorAt and o.order_id<:cursorId))"; params["cursorAt"] = Timestamp.from(cursor.createdAt); params["cursorId"] = cursor.orderId }
        return clauses.joinToString(" and ") to params
    }

    private fun orderParams(order: MarketOrder): Map<String, Any?> = mapOf(
        "order" to order.orderId, "account" to order.accountId, "instrument" to order.instrumentId, "side" to order.side.name, "tif" to order.timeInForce.name,
        "initial" to order.initialQuantity, "filled" to order.filledQuantity, "remaining" to order.remainingQuantity, "price" to order.limitUnitPrice,
        "reserved" to order.reservedRice, "status" to order.status.name, "priority" to Timestamp.from(order.priorityAt), "created" to Timestamp.from(order.createdAt),
        "updated" to Timestamp.from(order.updatedAt), "expires" to order.expiresAt?.let(Timestamp::from), "name" to order.displayNameSnapshot, "instances" to order.instanceIds.toTypedArray(),
    )

    private fun mapInstrument(row: java.sql.ResultSet, ignored: Int) = MarketInstrument(
        row.getObject("instrument_id", UUID::class.java), row.getString("canonical_key"), row.getString("item_id"), row.getString("display_name"), row.getString("category"),
        attributes(row.getString("attributes")), MarketInstrumentStatus.valueOf(row.getString("status")), row.getLong("revision"),
    )
    private fun mapOrder(row: java.sql.ResultSet, ignored: Int) = MarketOrder(
        row.getObject("order_id", UUID::class.java), row.getObject("account_id", UUID::class.java), row.getObject("instrument_id", UUID::class.java),
        MarketOrderSide.valueOf(row.getString("side")), MarketTimeInForce.valueOf(row.getString("time_in_force")), row.getLong("initial_quantity"),
        row.getLong("filled_quantity"), row.getLong("remaining_quantity"), row.getLong("limit_unit_price"), row.getLong("reserved_rice"),
        MarketOrderStatus.valueOf(row.getString("status")), row.getTimestamp("priority_at").toInstant(), row.getTimestamp("created_at").toInstant(),
        row.getTimestamp("updated_at").toInstant(), row.getTimestamp("expires_at")?.toInstant(), row.getString("display_name_snapshot"), uuidList(row.getArray("instance_ids")), row.getObject("legacy_listing_id", UUID::class.java),
    )
    private fun mapOrderSummary(row: java.sql.ResultSet, ignored: Int): MarketOrderSummary {
        val order = mapOrder(row, ignored)
        return MarketOrderSummary(order.orderId, order.instrumentId, row.getString("item_id"), row.getString("display_name"), order.side, order.timeInForce, order.initialQuantity, order.filledQuantity, order.remainingQuantity, order.limitUnitPrice, order.reservedRice, order.status, order.priorityAt, order.createdAt, order.updatedAt, order.expiresAt)
    }
    private fun mapDelivery(row: java.sql.ResultSet, ignored: Int) = MarketDelivery(
        row.getObject("delivery_id", UUID::class.java), row.getObject("account_id", UUID::class.java), row.getObject("instrument_id", UUID::class.java), row.getObject("order_id", UUID::class.java), row.getObject("trade_id", UUID::class.java),
        MarketDeliverySource.valueOf(row.getString("source")), row.getString("item_id"), uuidList(row.getArray("instance_ids")), row.getLong("quantity"), row.getString("display_name_snapshot"), row.getTimestamp("created_at").toInstant(), row.getTimestamp("claimed_at")?.toInstant(), row.getObject("source_account_id", UUID::class.java),
    )
    private fun mapTrade(row: java.sql.ResultSet, accountId: UUID?): MarketTradeView {
        val buyer = row.getObject("buyer_account_id", UUID::class.java)
        val side = accountId?.let { if (it == buyer) MarketOrderSide.BUY else MarketOrderSide.SELL }
        return MarketTradeView(row.getObject("trade_id", UUID::class.java), row.getObject("instrument_id", UUID::class.java), row.getString("item_id"), row.getString("display_name"), side, row.getLong("quantity"), row.getLong("unit_price"), row.getLong("total_price"), if (accountId == null) null else row.getLong("fee"), if (accountId == null) null else row.getLong("settlement_amount"), row.getTimestamp("filled_at").toInstant())
    }
    private fun attributes(json: String): Map<String, String> = mapper.readValue(json, object : TypeReference<Map<String, String>>() {})
    private fun nullableLong(row: java.sql.ResultSet, name: String): Long? = row.getLong(name).let { if (row.wasNull()) null else it }
    private fun uuidList(array: SqlArray?): List<UUID> = array?.array?.let { (it as Array<*>).filterIsInstance<UUID>() } ?: emptyList()
}
