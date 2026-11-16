package main

import (
	"bytes"
	"compress/gzip"
	"context"
	"encoding/binary"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http/httptest"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"testing"
	"time"
)

const testPlayer = "11111111111141118111111111111111"
const testSession = "22222222222242228222222222222222"
const testSegment = "33333333333343338333333333333333"
const testAdmin = "test-only-administrator-token-not-for-deployment"

func putVar(b *bytes.Buffer, v uint64) {
	var temp [10]byte
	n := binary.PutUvarint(temp[:], v)
	b.Write(temp[:n])
}
func putSigned(b *bytes.Buffer, v int64) { putVar(b, uint64(v<<1)^uint64(v>>63)) }
func fixture(index int, first int64, previous string) []byte {
	h := replayHeader{Session: testSession, Segment: testSegment, Build: "fixture", Content: "fixture", UI: "fixture", Profile: "compact-v1", Schema: 1, Chunk: index, First: first, Start: int64(index) * 1000, Previous: previous}
	return fixtureHeader(h)
}
func fixtureHeader(h replayHeader) []byte {
	encoded, _ := json.Marshal(h)
	raw := bytes.NewBufferString("DVRPLY01")
	_ = binary.Write(raw, binary.LittleEndian, uint16(1))
	_ = binary.Write(raw, binary.LittleEndian, uint32(len(encoded)))
	raw.Write(encoded)
	write := func(kind uint64, values []int64, text string) {
		var body bytes.Buffer
		putVar(&body, kind)
		putVar(&body, 1)
		putVar(&body, 0)
		putSigned(&body, -1)
		putVar(&body, uint64(len(values)))
		for _, v := range values {
			putSigned(&body, v)
		}
		if kind == 1 {
			putVar(&body, uint64(len(text)))
			body.WriteString(text)
		}
		putVar(raw, uint64(body.Len()))
		raw.Write(body.Bytes())
	}
	write(2, nil, "")
	write(1, []int64{1}, "gui.BuildMenu.button.0")
	write(1, []int64{2}, "Assets/ForestCombatV2/Scenes/Chapter1Title.unity")
	write(15, []int64{2, 0}, "")
	write(3, nil, "")
	write(8, []int64{1, 0}, "")
	write(16, []int64{h.First + 5, 6}, "")
	var packed bytes.Buffer
	z := gzip.NewWriter(&packed)
	_, _ = z.Write(raw.Bytes())
	_ = z.Close()
	return packed.Bytes()
}

func testSetup(t *testing.T) (*store, *server) {
	t.Helper()
	s, err := openStore(t.TempDir(), 1<<30, 64<<20, 30*24*time.Hour)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = s.close() })
	api := newServer(s, testAdmin, []string{"https://game.example"})
	api.now = func() time.Time { return time.Date(2026, 11, 9, 0, 0, 0, 0, time.UTC) }
	return s, api
}
func registerInput(now time.Time) sessionInput {
	return sessionInput{Player: testPlayer, Session: testSession, Build: "fixture", Platform: "web", Started: now.Format(time.RFC3339Nano), Consent: 1}
}
func call(t *testing.T, api *server, method, path string, body []byte, headers map[string]string) *httptest.ResponseRecorder {
	t.Helper()
	r := httptest.NewRequest(method, path, bytes.NewReader(body))
	for k, v := range headers {
		r.Header.Set(k, v)
	}
	w := httptest.NewRecorder()
	api.handler().ServeHTTP(w, r)
	return w
}
func registerHTTP(t *testing.T, api *server) {
	t.Helper()
	b, _ := json.Marshal(registerInput(api.now()))
	if w := call(t, api, "POST", "/v1/sessions", b, nil); w.Code != 200 {
		t.Fatalf("register: %d %s", w.Code, w.Body.String())
	}
}
func uploadHTTP(t *testing.T, api *server, packed []byte) *httptest.ResponseRecorder {
	t.Helper()
	return call(t, api, "PUT", "/v1/sessions/"+testSession+"/chunks/"+testSegment+"/0", packed, map[string]string{"X-Player-ID": testPlayer, "X-Replay-SHA256": hashBytes(packed)})
}

func TestUploadRoundTripDeduplicatesAndSeparatesSnapshots(t *testing.T) {
	s, api := testSetup(t)
	registerHTTP(t, api)
	data := fixture(0, 1, "")
	for i := 0; i < 2; i++ {
		w := uploadHTTP(t, api, data)
		if w.Code != 200 {
			t.Fatalf("upload: %d %s", w.Code, w.Body.String())
		}
		var body struct {
			State, SHA256 string
			Duplicate     bool
		}
		if json.Unmarshal(w.Body.Bytes(), &body) != nil || body.State != "committed" || body.SHA256 != hashBytes(data) || body.Duplicate != (i == 1) {
			t.Fatalf("bad receipt: %s", w.Body.String())
		}
	}
	if w := uploadHTTP(t, api, fixture(0, 2, "")); w.Code != 409 {
		t.Fatal("different bytes overwrote committed chunk", w.Code)
	}
	events, err := s.events(context.Background(), testSession, 0, 100, false)
	if err != nil || len(events) != 1 || events[0].Kind != "ui_activate" {
		t.Fatalf("events: %+v %v", events, err)
	}
	if !bytes.Contains(events[0].Payload, []byte("gui.BuildMenu.button.0")) {
		t.Fatal("dictionary ID not resolved")
	}
	admin := map[string]string{"Authorization": "Bearer " + testAdmin}
	w := call(t, api, "GET", "/admin/sessions/"+testSession+"/chunks/"+testSegment+"/0", nil, admin)
	if w.Code != 200 || !bytes.Equal(w.Body.Bytes(), data) {
		t.Fatal("download mismatch")
	}
	view, err := s.getSession(context.Background(), testSession)
	if err != nil || view.State != "unknown_end" || len(view.Chunks) != 1 {
		t.Fatalf("view: %+v %v", view, err)
	}
	status := sessionStatus{LastSeq: 7, Ended: true}
	if err = s.status(context.Background(), testSession, testPlayer, status, api.now()); err != nil {
		t.Fatal(err)
	}
	view, err = s.getSession(context.Background(), testSession)
	if err != nil || view.State != "complete" {
		t.Fatalf("complete: %+v %v", view, err)
	}
}
func TestReplayValidationRejectsCorruptionTrailingAndUnsupportedData(t *testing.T) {
	data := fixture(0, 1, "")
	if _, err := decodeReplay(data); err != nil {
		t.Fatal(err)
	}
	for _, bad := range [][]byte{nil, {}, data[:len(data)/2], append(append([]byte{}, data...), 0), append(append([]byte{}, data...), data...)} {
		if _, err := decodeReplay(bad); err == nil {
			t.Fatal("invalid gzip accepted")
		}
	}
	copy := append([]byte{}, data...)
	copy[len(copy)/2] ^= 0x7f
	if _, err := decodeReplay(copy); err == nil {
		t.Fatal("corruption accepted")
	}
	var large bytes.Buffer
	z := gzip.NewWriter(&large)
	_, _ = io.CopyN(z, zeroReader{}, maxExpanded+1)
	_ = z.Close()
	if _, err := decodeReplay(large.Bytes()); err == nil {
		t.Fatal("expansion limit bypassed")
	}
	if _, err := decodeReplay(fixture(1, 1, "")); err == nil {
		t.Fatal("missing previous hash accepted")
	}
}

type zeroReader struct{}

func (zeroReader) Read(p []byte) (int, error) { clear(p); return len(p), nil }

func TestAPIRejectsHashPathIdentityAndInvalidJSON(t *testing.T) {
	_, api := testSetup(t)
	registerHTTP(t, api)
	data := fixture(0, 1, "")
	w := call(t, api, "PUT", "/v1/sessions/"+testSession+"/chunks/"+testSegment+"/0", data, map[string]string{"X-Player-ID": testPlayer, "X-Replay-SHA256": string(bytes.Repeat([]byte{'0'}, 64))})
	if w.Code != 400 {
		t.Fatalf("hash: %d", w.Code)
	}
	w = call(t, api, "PUT", "/v1/sessions/"+testSession+"/chunks/"+testSegment+"/1", data, map[string]string{"X-Player-ID": testPlayer, "X-Replay-SHA256": hashBytes(data)})
	if w.Code != 409 {
		t.Fatalf("path: %d", w.Code)
	}
	w = call(t, api, "PUT", "/v1/sessions/"+testSession+"/chunks/"+testSegment+"/0", data, map[string]string{"X-Player-ID": testSegment, "X-Replay-SHA256": hashBytes(data)})
	if w.Code != 409 {
		t.Fatalf("owner: %d", w.Code)
	}
	for _, body := range []string{`{"player_id":"a","player_id":"b"}`, `{} {}`, `{"unknown":true}`, `null`} {
		if w = call(t, api, "POST", "/v1/sessions", []byte(body), nil); w.Code != 400 {
			t.Fatalf("JSON: %s %d", body, w.Code)
		}
	}
	if _, ok := normalID("../escape"); ok {
		t.Fatal("unsafe ID accepted")
	}
}

func TestAdminAndCORS(t *testing.T) {
	_, api := testSetup(t)
	if w := call(t, api, "GET", "/admin/players", nil, nil); w.Code != 401 {
		t.Fatal("public admin")
	}
	if w := call(t, api, "DELETE", "/admin/players/"+testPlayer, nil, map[string]string{"Authorization": "Bearer invalid"}); w.Code != 401 {
		t.Fatal("delete bypass")
	}
	if w := call(t, api, "OPTIONS", "/v1/sessions", nil, map[string]string{"Origin": "https://game.example"}); w.Code != 204 || w.Header().Get("Access-Control-Allow-Origin") != "https://game.example" {
		t.Fatal("allowed origin")
	}
	if w := call(t, api, "OPTIONS", "/v1/sessions", nil, map[string]string{"Origin": "https://evil.example"}); w.Code != 403 {
		t.Fatal("unexpected CORS")
	}
}

func TestCollectionRequestsHaveNoPerIPRateLimit(t *testing.T) {
	_, api := testSetup(t)
	data := fixture(0, 1, "")
	// testSetup fixes the clock; every request uses httptest's same remote IP.
	// Registration, chunk retries and status updates must all work beyond the former 120/minute threshold.
	for i := 0; i < 80; i++ {
		registerHTTP(t, api)
		if w := uploadHTTP(t, api, data); w.Code != 200 {
			t.Fatalf("chunk retry %d: %d %s", i, w.Code, w.Body.String())
		}
		w := call(t, api, "PUT", "/v1/sessions/"+testSession+"/status",
			[]byte(`{"last_ms":1,"last_seq":1,"ended":false,"truncated":false}`),
			map[string]string{"X-Player-ID": testPlayer})
		if w.Code != 200 {
			t.Fatalf("status update %d: %d %s", i, w.Code, w.Body.String())
		}
	}
}

func TestCollectionBusyRetriesAfterSlotIsReleased(t *testing.T) {
	_, api := testSetup(t)
	for i := 0; i < cap(api.slots); i++ {
		api.slots <- struct{}{}
	}
	body, _ := json.Marshal(registerInput(api.now()))
	w := call(t, api, "POST", "/v1/sessions", body, map[string]string{"Origin": "https://game.example"})
	if w.Code != 429 || w.Header().Get("Retry-After") != "5" || strings.TrimSpace(w.Body.String()) != `{"error":"busy"}` {
		t.Fatalf("busy: %d %s", w.Code, w.Body.String())
	}
	<-api.slots
	registerHTTP(t, api)
}
func TestConcurrentUploadQuotaRestartAndDeletion(t *testing.T) {
	s, api := testSetup(t)
	if err := s.register(context.Background(), registerInput(api.now()), api.now()); err != nil {
		t.Fatal(err)
	}
	data := fixture(0, 1, "")
	parsed, _ := decodeReplay(data)
	var wg sync.WaitGroup
	errs := make(chan error, 16)
	for i := 0; i < 16; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			_, err := s.upload(context.Background(), testPlayer, parsed, data, api.now())
			errs <- err
		}()
	}
	wg.Wait()
	close(errs)
	for err := range errs {
		if err != nil {
			t.Fatal(err)
		}
	}
	var n int
	if err := s.db.QueryRow("SELECT COUNT(*) FROM chunks").Scan(&n); err != nil || n != 1 {
		t.Fatal("duplicate rows", n, err)
	}
	s.quota = 1
	second := fixture(1, 8, hashBytes(data))
	p, _ := decodeReplay(second)
	if _, err := s.upload(context.Background(), testPlayer, p, second, api.now()); !errors.Is(err, errQuota) {
		t.Fatal("quota", err)
	}
	path := s.chunkPath(testSession, testSegment, 0)
	if err := os.Remove(path); err != nil {
		t.Fatal(err)
	}
	if duplicate, err := s.upload(context.Background(), testPlayer, parsed, data, api.now()); err != nil || !duplicate {
		t.Fatal("restore duplicate", err)
	}
	if restored, err := os.ReadFile(path); err != nil || !bytes.Equal(restored, data) {
		t.Fatal("missing raw not repaired", err)
	}
	root := s.root
	if err := s.close(); err != nil {
		t.Fatal(err)
	}
	reopened, err := openStore(root, 1<<30, 64<<20, 30*24*time.Hour)
	if err != nil {
		t.Fatal(err)
	}
	defer reopened.close()
	if view, err := reopened.getSession(context.Background(), testSession); err != nil || len(view.Chunks) != 1 {
		t.Fatal("restart", err)
	}
	if err = reopened.deletePlayer(context.Background(), testPlayer); err != nil {
		t.Fatal(err)
	}
	if _, err = reopened.getSession(context.Background(), testSession); !errors.Is(err, errNotFound) {
		t.Fatal("metadata remains", err)
	}
	if _, err = os.Stat(s.chunkPath(testSession, testSegment, 0)); !os.IsNotExist(err) {
		t.Fatal("file remains")
	}
}
func TestChainGapsAndRetentionRemainExplicit(t *testing.T) {
	s, api := testSetup(t)
	ctx := context.Background()
	if err := s.register(ctx, registerInput(api.now()), api.now()); err != nil {
		t.Fatal(err)
	}
	second := fixture(1, 8, string(bytes.Repeat([]byte{'a'}, 64)))
	p, _ := decodeReplay(second)
	if _, err := s.upload(ctx, testPlayer, p, second, api.now()); err != nil {
		t.Fatal(err)
	}
	if err := s.status(ctx, testSession, testPlayer, sessionStatus{LastSeq: 14, Ended: true}, api.now()); err != nil {
		t.Fatal(err)
	}
	view, err := s.getSession(ctx, testSession)
	if err != nil || view.State != "partial" {
		t.Fatal("gap marked complete")
	}
	orphan := filepath.Join(s.root, "chunks", "orphan.tmp")
	if err = os.WriteFile(orphan, []byte("interrupted"), 0600); err != nil {
		t.Fatal(err)
	}
	_ = os.Chtimes(orphan, api.now().Add(-2*time.Hour), api.now().Add(-2*time.Hour))
	if err = s.cleanup(ctx, api.now().Add(31*24*time.Hour)); err != nil {
		t.Fatal(err)
	}
	if _, err = s.getSession(ctx, testSession); !errors.Is(err, errNotFound) {
		t.Fatal("retention did not remove session")
	}
	if _, err = os.Stat(orphan); !os.IsNotExist(err) {
		t.Fatal("orphan retained")
	}
}

func TestOptionalProductionClientFixtures(t *testing.T) {
	dir := os.Getenv("REPLAY_FIXTURE_DIR")
	if dir == "" {
		t.Skip("set REPLAY_FIXTURE_DIR to validate generated Unity/C# ready files")
	}
	count := 0
	err := filepath.WalkDir(dir, func(path string, d os.DirEntry, err error) error {
		if err != nil {
			return err
		}
		if d.IsDir() || !strings.HasSuffix(path, ".replay.gz.ready") {
			return nil
		}
		b, err := os.ReadFile(path)
		if err != nil {
			return err
		}
		_, err = decodeReplay(b)
		if err != nil {
			return fmt.Errorf("fixture %s: %w", path, err)
		}
		count++
		return nil
	})
	if err != nil || count == 0 {
		t.Fatal("client fixtures", count, err)
	}
	t.Logf("Validated %d actual C#-encoded files", count)
}
