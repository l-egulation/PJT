package main

import (
	"strconv"
	"time"
)

// UUIDv7 timestamp is a client clock hint. Do not use it as authentication or TTL input.
func uuid7Time(value string) (time.Time, bool) {
	id, ok := normalID(value)
	if !ok || id[12] != '7' || id[16] < '8' || id[16] > 'b' {
		return time.Time{}, false
	}
	ms, err := strconv.ParseInt(id[:12], 16, 64)
	// Keep dates representable in RFC3339/JSON, even for attacker-supplied UUIDs.
	if err != nil || ms > 253402300799999 {
		return time.Time{}, false
	}
	return time.UnixMilli(ms).UTC(), true
}
