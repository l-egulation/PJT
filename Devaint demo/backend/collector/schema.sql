PRAGMA journal_mode=WAL;
PRAGMA foreign_keys=ON;
PRAGMA busy_timeout=5000;
CREATE TABLE IF NOT EXISTS schema_version(version INTEGER PRIMARY KEY);
INSERT OR IGNORE INTO schema_version VALUES(1);
CREATE TABLE IF NOT EXISTS players(id TEXT PRIMARY KEY, created_at INTEGER NOT NULL);
CREATE TABLE IF NOT EXISTS sessions(
 id TEXT PRIMARY KEY, player_id TEXT NOT NULL REFERENCES players(id) ON DELETE CASCADE,
 build_id TEXT NOT NULL, platform TEXT NOT NULL, started_at INTEGER NOT NULL,
 updated_at INTEGER NOT NULL, expires_at INTEGER NOT NULL, consent_version INTEGER NOT NULL,
 last_ms INTEGER NOT NULL DEFAULT 0, last_seq INTEGER NOT NULL DEFAULT 0,
 ended INTEGER NOT NULL DEFAULT 0, truncated INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS sessions_player ON sessions(player_id,started_at);
CREATE INDEX IF NOT EXISTS sessions_expiry ON sessions(expires_at);
CREATE TABLE IF NOT EXISTS segments(
 session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE, id TEXT NOT NULL,
 run_id TEXT NOT NULL, scene TEXT NOT NULL, content_hash TEXT NOT NULL, ui_version TEXT NOT NULL,
 PRIMARY KEY(session_id,id)
);
CREATE TABLE IF NOT EXISTS chunks(
 session_id TEXT NOT NULL, segment_id TEXT NOT NULL, chunk_index INTEGER NOT NULL,
 sha256 TEXT NOT NULL, previous_sha256 TEXT NOT NULL, bytes INTEGER NOT NULL,
 analysis_bytes INTEGER NOT NULL, start_ms INTEGER NOT NULL, end_ms INTEGER NOT NULL,
 first_seq INTEGER NOT NULL, last_seq INTEGER NOT NULL, records INTEGER NOT NULL,
 received_at INTEGER NOT NULL,
 PRIMARY KEY(session_id,segment_id,chunk_index),
 FOREIGN KEY(session_id,segment_id) REFERENCES segments(session_id,id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS chunks_timeline ON chunks(session_id,first_seq);
CREATE TABLE IF NOT EXISTS events(
 session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE, seq INTEGER NOT NULL,
 segment_id TEXT NOT NULL, chunk_index INTEGER NOT NULL, elapsed_ms INTEGER NOT NULL,
 sim_tick INTEGER NOT NULL, kind TEXT NOT NULL, is_snapshot INTEGER NOT NULL,
 payload TEXT NOT NULL CHECK(json_valid(payload)), PRIMARY KEY(session_id,seq)
);
CREATE INDEX IF NOT EXISTS events_kind ON events(kind,session_id,elapsed_ms);
