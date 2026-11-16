package main

import (
	"context"
	"crypto/sha256"
	"database/sql"
	_ "embed"
	"encoding/hex"
	"encoding/json"
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"sync"
	"time"

	_ "modernc.org/sqlite"
)

//go:embed schema.sql
var schema string
var errConflict = errors.New("identity_or_chunk_conflict")
var errNotFound = errors.New("not_found")
var errQuota = errors.New("storage_quota")
var errExpired = errors.New("session_expired")

type store struct {
	db          *sql.DB
	root        string
	mu          sync.Mutex
	quota       int64
	playerDaily int64
	retention   time.Duration
}
type sessionInput struct {
	Player   string `json:"player_id"`
	Session  string `json:"session_id"`
	Build    string `json:"build_id"`
	Platform string `json:"platform"`
	Started  string `json:"started_at"`
	Consent  int    `json:"consent_version"`
}
type sessionStatus struct {
	LastMS    int64 `json:"last_ms"`
	LastSeq   int64 `json:"last_seq"`
	Ended     bool  `json:"ended"`
	Truncated bool  `json:"truncated"`
}
type sessionView struct {
	ID        string      `json:"session_id"`
	Player    string      `json:"player_id"`
	Build     string      `json:"build_id"`
	Platform  string      `json:"platform"`
	Started   time.Time   `json:"started_at"`
	Updated   time.Time   `json:"updated_at"`
	Expires   time.Time   `json:"expires_at"`
	LastMS    int64       `json:"last_ms"`
	LastSeq   int64       `json:"last_seq"`
	Ended     bool        `json:"ended"`
	Truncated bool        `json:"truncated"`
	State     string      `json:"state"`
	Coverage  bool        `json:"coverage_complete"`
	Chunks    []chunkView `json:"chunks,omitempty"`
}
type chunkView struct {
	Segment        string     `json:"segment_id"`
	Index          int        `json:"chunk_index"`
	Hash           string     `json:"sha256"`
	Previous       string     `json:"previous_sha256"`
	Bytes          int64      `json:"bytes"`
	Start          int64      `json:"start_ms"`
	End            int64      `json:"end_ms"`
	First          int64      `json:"first_seq"`
	Last           int64      `json:"last_seq"`
	Scene          string     `json:"scene"`
	Run            string     `json:"run_id"`
	SegmentCreated *time.Time `json:"segment_created_at,omitempty"`
	RecordedFrom   time.Time  `json:"recorded_from"`
	RecordedTo     time.Time  `json:"recorded_to"`
}

func openStore(root string, quota, playerDaily int64, retention time.Duration) (*store, error) {
	root, err := filepath.Abs(root)
	if err != nil {
		return nil, err
	}
	if err = os.MkdirAll(filepath.Join(root, "chunks"), 0700); err != nil {
		return nil, err
	}
	db, err := sql.Open("sqlite", filepath.Join(root, "metadata.sqlite"))
	if err != nil {
		return nil, err
	}
	db.SetMaxOpenConns(1)
	if _, err = db.Exec(schema); err != nil {
		db.Close()
		return nil, err
	}
	var version int
	if err = db.QueryRow("SELECT MAX(version) FROM schema_version").Scan(&version); err != nil || version != 1 {
		db.Close()
		return nil, errors.New("unsupported database schema")
	}
	return &store{db: db, root: root, quota: quota, playerDaily: playerDaily, retention: retention}, nil
}
func (s *store) close() error { return s.db.Close() }

func (s *store) register(ctx context.Context, in sessionInput, now time.Time) error {
	id, ok := normalID(in.Player)
	if !ok {
		return errFormat
	}
	in.Player = id
	id, ok = normalID(in.Session)
	if !ok {
		return errFormat
	}
	in.Session = id
	start, err := time.Parse(time.RFC3339Nano, in.Started)
	if err != nil || start.After(now.Add(10*time.Minute)) || start.Before(now.Add(-7*24*time.Hour)) || !safeVersion.MatchString(in.Build) || in.Consent != 1 {
		return errFormat
	}
	switch in.Platform {
	case "web", "windows", "linux", "osx", "editor":
	default:
		return errFormat
	}
	s.mu.Lock()
	defer s.mu.Unlock()
	tx, err := s.db.BeginTx(ctx, nil)
	if err != nil {
		return err
	}
	defer tx.Rollback()
	var player, build, platform string
	var started, expires int64
	err = tx.QueryRowContext(ctx, "SELECT player_id,build_id,platform,started_at,expires_at FROM sessions WHERE id=?", in.Session).Scan(&player, &build, &platform, &started, &expires)
	if err == nil {
		if player != in.Player || build != in.Build || platform != in.Platform || started != start.UnixMilli() {
			return errConflict
		}
		if expires <= now.UnixMilli() {
			return errExpired
		}
		return nil
	}
	if !errors.Is(err, sql.ErrNoRows) {
		return err
	}
	var sessionCount int
	if err = tx.QueryRowContext(ctx, "SELECT COUNT(*) FROM sessions").Scan(&sessionCount); err != nil {
		return err
	}
	if sessionCount >= 100000 {
		return errQuota
	}
	if _, err = tx.ExecContext(ctx, "INSERT OR IGNORE INTO players(id,created_at) VALUES(?,?)", in.Player, now.UnixMilli()); err != nil {
		return err
	}
	_, err = tx.ExecContext(ctx, "INSERT INTO sessions(id,player_id,build_id,platform,started_at,updated_at,expires_at,consent_version) VALUES(?,?,?,?,?,?,?,?)", in.Session, in.Player, in.Build, in.Platform, start.UnixMilli(), now.UnixMilli(), start.Add(s.retention).UnixMilli(), in.Consent)
	if err != nil {
		return err
	}
	return tx.Commit()
}

func (s *store) chunkPath(session, segment string, index int) string {
	return filepath.Join(s.root, "chunks", session, segment, fmt.Sprintf("%06d.replay.gz", index))
}
func hashBytes(b []byte) string { h := sha256.Sum256(b); return hex.EncodeToString(h[:]) }
func atomicFile(path string, data []byte) error {
	if err := os.MkdirAll(filepath.Dir(path), 0700); err != nil {
		return err
	}
	f, err := os.CreateTemp(filepath.Dir(path), ".upload-*")
	if err != nil {
		return err
	}
	temp := f.Name()
	defer os.Remove(temp)
	if _, err = f.Write(data); err == nil {
		err = f.Sync()
	}
	closeErr := f.Close()
	if err != nil {
		return err
	}
	if closeErr != nil {
		return closeErr
	}
	if err = os.Rename(temp, path); err != nil {
		return err
	}
	d, err := os.Open(filepath.Dir(path))
	if err != nil {
		return err
	}
	defer d.Close()
	return d.Sync()
}

func (s *store) upload(ctx context.Context, player string, r *parsedReplay, data []byte, now time.Time) (bool, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	h := r.Header
	hash := hashBytes(data)
	tx, err := s.db.BeginTx(ctx, nil)
	if err != nil {
		return false, err
	}
	defer tx.Rollback()
	var owner, build string
	var expires int64
	err = tx.QueryRowContext(ctx, "SELECT player_id,build_id,expires_at FROM sessions WHERE id=?", h.Session).Scan(&owner, &build, &expires)
	if errors.Is(err, sql.ErrNoRows) {
		return false, errNotFound
	}
	if err != nil {
		return false, err
	}
	if owner != player || build != h.Build {
		return false, errConflict
	}
	if expires <= now.UnixMilli() {
		return false, errExpired
	}
	var existing string
	err = tx.QueryRowContext(ctx, "SELECT sha256 FROM chunks WHERE session_id=? AND segment_id=? AND chunk_index=?", h.Session, h.Segment, h.Chunk).Scan(&existing)
	if err == nil {
		if existing != hash {
			return false, errConflict
		}
		path := s.chunkPath(h.Session, h.Segment, h.Chunk)
		if old, e := os.ReadFile(path); e != nil || hashBytes(old) != hash {
			if e = atomicFile(path, data); e != nil {
				return false, e
			}
		}
		return true, nil
	}
	if !errors.Is(err, sql.ErrNoRows) {
		return false, err
	}
	var overlaps int
	if err = tx.QueryRowContext(ctx, "SELECT COUNT(*) FROM chunks WHERE session_id=? AND NOT(last_seq<? OR first_seq>?)", h.Session, h.First, r.LastSeq).Scan(&overlaps); err != nil {
		return false, err
	}
	if overlaps != 0 {
		return false, errConflict
	}
	var run, content, ui string
	err = tx.QueryRowContext(ctx, "SELECT run_id,content_hash,ui_version FROM segments WHERE session_id=? AND id=?", h.Session, h.Segment).Scan(&run, &content, &ui)
	if err == nil && (run != h.Run || content != h.Content || ui != h.UI) {
		return false, errConflict
	}
	if err != nil && !errors.Is(err, sql.ErrNoRows) {
		return false, err
	}
	if _, err = tx.ExecContext(ctx, "INSERT OR IGNORE INTO segments(session_id,id,run_id,scene,content_hash,ui_version) VALUES(?,?,?,?,?,?)", h.Session, h.Segment, h.Run, r.Scene, h.Content, h.UI); err != nil {
		return false, err
	}
	var total, daily int64
	if err = tx.QueryRowContext(ctx, "SELECT COALESCE(SUM(bytes+analysis_bytes),0) FROM chunks").Scan(&total); err != nil {
		return false, err
	}
	day := now.UTC().Truncate(24 * time.Hour).UnixMilli()
	if err = tx.QueryRowContext(ctx, "SELECT COALESCE(SUM(c.bytes),0) FROM chunks c JOIN sessions s ON s.id=c.session_id WHERE s.player_id=? AND c.received_at>=?", player, day).Scan(&daily); err != nil {
		return false, err
	}
	analysis := int64(0)
	for _, e := range r.Events {
		analysis += int64(len(e.Payload)) + 512
	}
	if total+int64(len(data))+analysis > s.quota || daily+int64(len(data)) > s.playerDaily {
		return false, errQuota
	}
	_, err = tx.ExecContext(ctx, "INSERT INTO chunks VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?)", h.Session, h.Segment, h.Chunk, hash, h.Previous, len(data), analysis, h.Start, r.End, h.First, r.LastSeq, r.Records, now.UnixMilli())
	if err != nil {
		return false, err
	}
	stmt, err := tx.PrepareContext(ctx, "INSERT INTO events VALUES(?,?,?,?,?,?,?,?,?)")
	if err != nil {
		return false, err
	}
	defer stmt.Close()
	for _, e := range r.Events {
		if _, err = stmt.ExecContext(ctx, h.Session, e.Seq, h.Segment, h.Chunk, e.Time, e.Tick, e.Kind, e.Snapshot, string(e.Payload)); err != nil {
			return false, err
		}
	}
	if _, err = tx.ExecContext(ctx, "UPDATE sessions SET updated_at=?,last_ms=MAX(last_ms,?),last_seq=MAX(last_seq,?) WHERE id=?", now.UnixMilli(), r.End, r.LastSeq, h.Session); err != nil {
		return false, err
	}
	// The DB commit follows a durable immutable file. A crash can leave an orphan file, never a committed row whose bytes were not written.
	path := s.chunkPath(h.Session, h.Segment, h.Chunk)
	if old, e := os.ReadFile(path); e == nil && hashBytes(old) != hash {
		return false, errConflict
	}
	if err = atomicFile(path, data); err != nil {
		return false, err
	}
	if err = tx.Commit(); err != nil {
		return false, err
	}
	return false, nil
}

func (s *store) status(ctx context.Context, session, player string, in sessionStatus, now time.Time) error {
	if in.LastMS < 0 || in.LastMS > 7*24*3600000 || in.LastSeq < 0 {
		return errFormat
	}
	s.mu.Lock()
	defer s.mu.Unlock()
	r, err := s.db.ExecContext(ctx, "UPDATE sessions SET last_ms=MAX(last_ms,?),last_seq=MAX(last_seq,?),ended=MAX(ended,?),truncated=MAX(truncated,?),updated_at=? WHERE id=? AND player_id=? AND expires_at>?", in.LastMS, in.LastSeq, in.Ended, in.Truncated, now.UnixMilli(), session, player, now.UnixMilli())
	if err != nil {
		return err
	}
	n, _ := r.RowsAffected()
	if n == 0 {
		return errNotFound
	}
	return nil
}

func (s *store) getSession(ctx context.Context, id string) (*sessionView, error) {
	v := &sessionView{}
	var start, updated, expires int64
	err := s.db.QueryRowContext(ctx, "SELECT id,player_id,build_id,platform,started_at,updated_at,expires_at,last_ms,last_seq,ended,truncated FROM sessions WHERE id=?", id).Scan(&v.ID, &v.Player, &v.Build, &v.Platform, &start, &updated, &expires, &v.LastMS, &v.LastSeq, &v.Ended, &v.Truncated)
	if errors.Is(err, sql.ErrNoRows) {
		return nil, errNotFound
	}
	if err != nil {
		return nil, err
	}
	v.Started = time.UnixMilli(start).UTC()
	v.Updated = time.UnixMilli(updated).UTC()
	v.Expires = time.UnixMilli(expires).UTC()
	rows, err := s.db.QueryContext(ctx, "SELECT c.segment_id,c.chunk_index,c.sha256,c.previous_sha256,c.bytes,c.start_ms,c.end_ms,c.first_seq,c.last_seq,g.scene,g.run_id FROM chunks c JOIN segments g ON g.session_id=c.session_id AND g.id=c.segment_id WHERE c.session_id=? ORDER BY first_seq", id)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	next := int64(1)
	lastEnd := int64(0)
	prev := map[string]chunkView{}
	coverage := true
	for rows.Next() {
		var c chunkView
		if err = rows.Scan(&c.Segment, &c.Index, &c.Hash, &c.Previous, &c.Bytes, &c.Start, &c.End, &c.First, &c.Last, &c.Scene, &c.Run); err != nil {
			return nil, err
		}
		if c.First != next || c.Start < lastEnd {
			coverage = false
		}
		next = c.Last + 1
		lastEnd = c.End
		p, ok := prev[c.Segment]
		if (!ok && (c.Index != 0 || c.Previous != "")) || (ok && (c.Index != p.Index+1 || c.Previous != p.Hash)) {
			coverage = false
		}
		if created, ok := uuid7Time(c.Segment); ok {
			c.SegmentCreated = &created
		}
		c.RecordedFrom = v.Started.Add(time.Duration(c.Start) * time.Millisecond)
		c.RecordedTo = v.Started.Add(time.Duration(c.End) * time.Millisecond)
		prev[c.Segment] = c
		v.Chunks = append(v.Chunks, c)
	}
	if err = rows.Err(); err != nil {
		return nil, err
	}
	v.Coverage = coverage && len(v.Chunks) > 0 && next-1 == v.LastSeq
	v.State = "unknown_end"
	if v.Ended {
		v.State = "partial"
		if v.Coverage && !v.Truncated {
			v.State = "complete"
		}
	}
	if v.Truncated {
		v.State = "truncated"
	}
	return v, nil
}

func (s *store) players(ctx context.Context, limit int) ([]map[string]any, error) {
	rows, err := s.db.QueryContext(ctx, "SELECT p.id,COUNT(DISTINCT s.id),COUNT(c.chunk_index),COALESCE(SUM(c.bytes),0),COALESCE(MAX(s.updated_at),p.created_at) FROM players p LEFT JOIN sessions s ON s.player_id=p.id LEFT JOIN chunks c ON c.session_id=s.id GROUP BY p.id ORDER BY MAX(s.updated_at) DESC LIMIT ?", limit)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []map[string]any{}
	for rows.Next() {
		var id string
		var sessions, chunks, bytes, last int64
		if err = rows.Scan(&id, &sessions, &chunks, &bytes, &last); err != nil {
			return nil, err
		}
		out = append(out, map[string]any{"player_id": id, "sessions": sessions, "chunks": chunks, "bytes": bytes, "last_received_at": time.UnixMilli(last).UTC()})
	}
	return out, rows.Err()
}
func (s *store) sessions(ctx context.Context, player string, limit int) ([]string, error) {
	rows, err := s.db.QueryContext(ctx, "SELECT id FROM sessions WHERE player_id=? ORDER BY started_at DESC LIMIT ?", player, limit)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []string{}
	for rows.Next() {
		var id string
		if err = rows.Scan(&id); err != nil {
			return nil, err
		}
		out = append(out, id)
	}
	return out, rows.Err()
}
func (s *store) events(ctx context.Context, id string, after int64, limit int, includeSnapshots bool) ([]extractedEvent, error) {
	rows, err := s.db.QueryContext(ctx, "SELECT seq,elapsed_ms,sim_tick,kind,is_snapshot,payload FROM events WHERE session_id=? AND seq>? AND (? OR is_snapshot=0) ORDER BY seq LIMIT ?", id, after, includeSnapshots, limit)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []extractedEvent{}
	for rows.Next() {
		var e extractedEvent
		var p string
		if err = rows.Scan(&e.Seq, &e.Time, &e.Tick, &e.Kind, &e.Snapshot, &p); err != nil {
			return nil, err
		}
		e.Payload = json.RawMessage(p)
		out = append(out, e)
	}
	return out, rows.Err()
}

func (s *store) deletePlayer(ctx context.Context, id string) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	rows, err := s.db.QueryContext(ctx, "SELECT id FROM sessions WHERE player_id=?", id)
	if err != nil {
		return err
	}
	var sessions []string
	for rows.Next() {
		var session string
		if err = rows.Scan(&session); err != nil {
			rows.Close()
			return err
		}
		sessions = append(sessions, session)
	}
	err = rows.Err()
	rows.Close()
	if err != nil {
		return err
	}
	if _, err = s.db.ExecContext(ctx, "DELETE FROM players WHERE id=?", id); err != nil {
		return err
	}
	for _, session := range sessions {
		if err = os.RemoveAll(filepath.Join(s.root, "chunks", session)); err != nil {
			return err
		}
	}
	return nil
}

func (s *store) cleanup(ctx context.Context, now time.Time) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	rows, err := s.db.QueryContext(ctx, "SELECT id FROM sessions WHERE expires_at<=?", now.UnixMilli())
	if err != nil {
		return err
	}
	var expired []string
	for rows.Next() {
		var id string
		if err = rows.Scan(&id); err != nil {
			rows.Close()
			return err
		}
		expired = append(expired, id)
	}
	err = rows.Err()
	rows.Close()
	if err != nil {
		return err
	}
	if _, err = s.db.ExecContext(ctx, "DELETE FROM sessions WHERE expires_at<=?", now.UnixMilli()); err != nil {
		return err
	}
	for _, id := range expired {
		if err = os.RemoveAll(filepath.Join(s.root, "chunks", id)); err != nil {
			return err
		}
	}
	_, err = s.db.ExecContext(ctx, "DELETE FROM players WHERE NOT EXISTS(SELECT 1 FROM sessions WHERE sessions.player_id=players.id)")
	if err != nil {
		return err
	}
	// Reconcile crash leftovers and interrupted temporary uploads without touching fresh in-flight work.
	return filepath.WalkDir(filepath.Join(s.root, "chunks"), func(path string, d os.DirEntry, e error) error {
		if e != nil {
			return e
		}
		if d.IsDir() {
			return nil
		}
		info, e := d.Info()
		if e != nil {
			return e
		}
		if now.Sub(info.ModTime()) < time.Hour {
			return nil
		}
		rel, e := filepath.Rel(filepath.Join(s.root, "chunks"), path)
		if e != nil {
			return e
		}
		var n int
		// Known files have three relative components. Everything else is an uncommitted temporary file.
		parts := splitPath(rel)
		if len(parts) == 3 {
			var index int
			if _, e = fmt.Sscanf(parts[2], "%06d.replay.gz", &index); e == nil && parts[2] == fmt.Sprintf("%06d.replay.gz", index) {
				e = s.db.QueryRowContext(ctx, "SELECT COUNT(*) FROM chunks WHERE session_id=? AND segment_id=? AND chunk_index=?", parts[0], parts[1], index).Scan(&n)
				if e != nil {
					return e
				}
			}
		}
		if n == 0 {
			return os.Remove(path)
		}
		return nil
	})
}
func splitPath(p string) []string {
	var out []string
	for p != "." && p != "" {
		dir, base := filepath.Split(p)
		if base != "" {
			out = append([]string{base}, out...)
		}
		p = filepath.Clean(dir)
	}
	return out
}
