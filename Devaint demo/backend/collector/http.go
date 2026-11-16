package main

import (
	"crypto/subtle"
	"encoding/json"
	"errors"
	"io"
	"net/http"
	"os"
	"strconv"
	"strings"
	"time"
)

type server struct {
	store   *store
	admin   string
	origins map[string]bool
	slots   chan struct{}
	now     func() time.Time
}

func newServer(s *store, admin string, origins []string) *server {
	allow := map[string]bool{}
	for _, o := range origins {
		if o = strings.TrimSpace(o); o != "" {
			allow[o] = true
		}
	}
	return &server{store: s, admin: admin, origins: allow, slots: make(chan struct{}, 4), now: time.Now}
}
func (s *server) handler() http.Handler {
	m := http.NewServeMux()
	m.HandleFunc("GET /healthz", func(w http.ResponseWriter, r *http.Request) {
		if s.store.db.PingContext(r.Context()) != nil {
			apiError(w, 503, "database_unavailable")
			return
		}
		reply(w, 200, map[string]any{"status": "ok"})
	})
	m.HandleFunc("POST /v1/sessions", s.register)
	m.HandleFunc("PUT /v1/sessions/{session}/chunks/{segment}/{index}", s.upload)
	m.HandleFunc("PUT /v1/sessions/{session}/status", s.status)
	m.HandleFunc("GET /admin/players", s.auth(s.players))
	m.HandleFunc("GET /admin/players/{player}/sessions", s.auth(s.sessions))
	m.HandleFunc("DELETE /admin/players/{player}", s.auth(s.deletePlayer))
	m.HandleFunc("GET /admin/sessions/{session}", s.auth(s.session))
	m.HandleFunc("GET /admin/sessions/{session}/events", s.auth(s.events))
	m.HandleFunc("GET /admin/sessions/{session}/chunks/{segment}/{index}", s.auth(s.download))
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Cache-Control", "no-store")
		w.Header().Set("X-Content-Type-Options", "nosniff")
		w.Header().Add("Vary", "Origin")
		if origin := r.Header.Get("Origin"); origin != "" {
			if !s.origins[origin] {
				apiError(w, 403, "origin_not_allowed")
				return
			}
			w.Header().Set("Access-Control-Allow-Origin", origin)
			w.Header().Set("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS")
			w.Header().Set("Access-Control-Allow-Headers", "Content-Type, X-Player-ID, X-Replay-SHA256, Authorization")
			w.Header().Set("Access-Control-Max-Age", "600")
			w.Header().Set("Access-Control-Expose-Headers", "Retry-After")
		}
		if r.Method == "OPTIONS" {
			w.WriteHeader(204)
			return
		}
		if strings.HasPrefix(r.URL.Path, "/v1/") {
			select {
			case s.slots <- struct{}{}:
				defer func() { <-s.slots }()
			default:
				w.Header().Set("Retry-After", "5")
				apiError(w, 429, "busy")
				return
			}
		}
		m.ServeHTTP(w, r)
	})
}
func reply(w http.ResponseWriter, status int, value any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(value)
}
func apiError(w http.ResponseWriter, status int, code string) {
	reply(w, status, map[string]string{"error": code})
}
func fail(w http.ResponseWriter, err error) {
	switch {
	case errors.Is(err, errFormat):
		apiError(w, 400, "invalid_request")
	case errors.Is(err, errConflict):
		apiError(w, 409, errConflict.Error())
	case errors.Is(err, errNotFound):
		apiError(w, 404, errNotFound.Error())
	case errors.Is(err, errExpired):
		apiError(w, 410, errExpired.Error())
	case errors.Is(err, errQuota):
		w.Header().Set("Retry-After", "3600")
		apiError(w, 429, errQuota.Error())
	default:
		apiError(w, 500, "storage_error")
	}
}
func jsonBody(w http.ResponseWriter, r *http.Request, target any) bool {
	data, err := io.ReadAll(http.MaxBytesReader(w, r.Body, 8192))
	if err != nil {
		apiError(w, 413, "body_too_large")
		return false
	}
	if strictJSON(data, target) != nil {
		apiError(w, 400, "invalid_json")
		return false
	}
	return true
}
func pathID(w http.ResponseWriter, r *http.Request, key string) (string, bool) {
	id, ok := normalID(r.PathValue(key))
	if !ok {
		apiError(w, 400, "invalid_id")
	}
	return id, ok
}
func playerHeader(w http.ResponseWriter, r *http.Request) (string, bool) {
	id, ok := normalID(r.Header.Get("X-Player-ID"))
	if !ok {
		apiError(w, 400, "invalid_player_id")
	}
	return id, ok
}
func chunkIndex(w http.ResponseWriter, r *http.Request) (int, bool) {
	i, err := strconv.Atoi(r.PathValue("index"))
	if err != nil || i < 0 || i > 100000 {
		apiError(w, 400, "invalid_chunk_index")
		return 0, false
	}
	return i, true
}

func (s *server) register(w http.ResponseWriter, r *http.Request) {
	var in sessionInput
	if !jsonBody(w, r, &in) {
		return
	}
	if err := s.store.register(r.Context(), in, s.now()); err != nil {
		fail(w, err)
		return
	}
	reply(w, 200, map[string]string{"state": "registered"})
}
func (s *server) upload(w http.ResponseWriter, r *http.Request) {
	player, ok := playerHeader(w, r)
	if !ok {
		return
	}
	session, ok := pathID(w, r, "session")
	if !ok {
		return
	}
	segment, ok := pathID(w, r, "segment")
	if !ok {
		return
	}
	index, ok := chunkIndex(w, r)
	if !ok {
		return
	}
	expected := r.Header.Get("X-Replay-SHA256")
	if !digestPattern.MatchString(expected) {
		apiError(w, 400, "invalid_sha256")
		return
	}
	if r.Header.Get("Content-Encoding") != "" {
		apiError(w, 415, "send_gzip_as_octet_stream")
		return
	}
	data, err := io.ReadAll(http.MaxBytesReader(w, r.Body, maxUpload))
	if err != nil {
		apiError(w, 413, "body_too_large")
		return
	}
	if hashBytes(data) != expected {
		apiError(w, 400, "sha256_mismatch")
		return
	}
	parsed, err := decodeReplay(data)
	if err != nil {
		apiError(w, 400, "invalid_replay")
		return
	}
	if parsed.Header.Session != session || parsed.Header.Segment != segment || parsed.Header.Chunk != index {
		apiError(w, 409, "header_path_conflict")
		return
	}
	duplicate, err := s.store.upload(r.Context(), player, parsed, data, s.now())
	if err != nil {
		fail(w, err)
		return
	}
	reply(w, 200, map[string]any{"state": "committed", "sha256": expected, "duplicate": duplicate})
}
func (s *server) status(w http.ResponseWriter, r *http.Request) {
	player, ok := playerHeader(w, r)
	if !ok {
		return
	}
	id, ok := pathID(w, r, "session")
	if !ok {
		return
	}
	var in sessionStatus
	if !jsonBody(w, r, &in) {
		return
	}
	if err := s.store.status(r.Context(), id, player, in, s.now()); err != nil {
		fail(w, err)
		return
	}
	reply(w, 200, map[string]string{"state": "updated"})
}
func (s *server) auth(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		want := "Bearer " + s.admin
		got := r.Header.Get("Authorization")
		if len(s.admin) < 24 || subtle.ConstantTimeCompare([]byte(want), []byte(got)) != 1 {
			apiError(w, 401, "admin_required")
			return
		}
		next(w, r)
	}
}
func limit(r *http.Request, max int) int {
	i, err := strconv.Atoi(r.URL.Query().Get("limit"))
	if err != nil || i <= 0 || i > max {
		return max
	}
	return i
}
func (s *server) players(w http.ResponseWriter, r *http.Request) {
	v, err := s.store.players(r.Context(), limit(r, 200))
	if err != nil {
		fail(w, err)
		return
	}
	reply(w, 200, v)
}
func (s *server) sessions(w http.ResponseWriter, r *http.Request) {
	id, ok := pathID(w, r, "player")
	if !ok {
		return
	}
	ids, err := s.store.sessions(r.Context(), id, limit(r, 200))
	if err != nil {
		fail(w, err)
		return
	}
	out := []*sessionView{}
	for _, id := range ids {
		v, e := s.store.getSession(r.Context(), id)
		if e != nil {
			fail(w, e)
			return
		}
		v.Chunks = nil
		out = append(out, v)
	}
	reply(w, 200, out)
}
func (s *server) session(w http.ResponseWriter, r *http.Request) {
	id, ok := pathID(w, r, "session")
	if !ok {
		return
	}
	v, err := s.store.getSession(r.Context(), id)
	if err != nil {
		fail(w, err)
		return
	}
	if !v.Expires.After(s.now()) {
		fail(w, errExpired)
		return
	}
	reply(w, 200, v)
}
func (s *server) events(w http.ResponseWriter, r *http.Request) {
	id, ok := pathID(w, r, "session")
	if !ok {
		return
	}
	v, err := s.store.getSession(r.Context(), id)
	if err != nil {
		fail(w, err)
		return
	}
	if !v.Expires.After(s.now()) {
		fail(w, errExpired)
		return
	}
	after := int64(0)
	if text := r.URL.Query().Get("after_seq"); text != "" {
		after, err = strconv.ParseInt(text, 10, 64)
		if err != nil || after < 0 {
			apiError(w, 400, "invalid_cursor")
			return
		}
	}
	data, err := s.store.events(r.Context(), id, after, limit(r, 1000), r.URL.Query().Get("snapshots") == "1")
	if err != nil {
		fail(w, err)
		return
	}
	next := after
	if len(data) > 0 {
		next = data[len(data)-1].Seq
	}
	reply(w, 200, map[string]any{"events": data, "next_after_seq": next})
}
func (s *server) download(w http.ResponseWriter, r *http.Request) {
	id, ok := pathID(w, r, "session")
	if !ok {
		return
	}
	segment, ok := pathID(w, r, "segment")
	if !ok {
		return
	}
	index, ok := chunkIndex(w, r)
	if !ok {
		return
	}
	v, err := s.store.getSession(r.Context(), id)
	if err != nil {
		fail(w, err)
		return
	}
	if !v.Expires.After(s.now()) {
		fail(w, errExpired)
		return
	}
	found := false
	for _, c := range v.Chunks {
		if c.Segment == segment && c.Index == index {
			found = true
			break
		}
	}
	if !found {
		fail(w, errNotFound)
		return
	}
	f, err := os.Open(s.store.chunkPath(id, segment, index))
	if err != nil {
		fail(w, errNotFound)
		return
	}
	defer f.Close()
	info, err := f.Stat()
	if err != nil {
		fail(w, err)
		return
	}
	w.Header().Set("Content-Type", "application/octet-stream")
	w.Header().Set("Content-Disposition", "attachment; filename=\"recording.replay.gz\"")
	http.ServeContent(w, r, "recording.replay.gz", info.ModTime(), f)
}
func (s *server) deletePlayer(w http.ResponseWriter, r *http.Request) {
	id, ok := pathID(w, r, "player")
	if !ok {
		return
	}
	if err := s.store.deletePlayer(r.Context(), id); err != nil {
		fail(w, err)
		return
	}
	w.WriteHeader(204)
}
