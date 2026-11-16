package main

import (
	"context"
	"encoding/json"
	"strings"
	"testing"
	"time"
)

func TestUUID7TimestampUsesRFCByteOrderAndAcceptsLegacy(t *testing.T) {
	for _, id := range []string{"017f22e279b07cc398c4dc0c0c07398f", "017F22E2-79B0-7CC3-98C4-DC0C0C07398F"} {
		stamp, ok := uuid7Time(id)
		if !ok || stamp.UnixMilli() != 1645557742000 {
			t.Fatalf("RFC vector %s: %v %v", id, stamp, ok)
		}
	}
	for _, id := range []string{testSegment, "not-an-id", "017f22e279b07cc3f8c4dc0c0c07398f", "ffffffffffff7cc398c4dc0c0c07398f"} {
		if _, ok := uuid7Time(id); ok {
			t.Fatal("invented timestamp", id)
		}
	}
}
func TestUUID7SessionAndSegmentsKeepRecordingOrderAndReadableTimes(t *testing.T) {
	s, api := testSetup(t)
	ctx := context.Background()
	in := registerInput(api.now())
	in.Session = "019a6700000070008000000000000001"
	if err := s.register(ctx, in, api.now()); err != nil {
		t.Fatal(err)
	}
	segment1 := "019a6700000070008000000000000002"
	segment2 := "019a6700000170008000000000000000"
	h := replayHeader{Session: in.Session, Segment: segment1, Build: in.Build, Content: "fixture", UI: "fixture", Profile: "compact-v1", Schema: 1, First: 1, Start: 1000}
	first := fixtureHeader(h)
	h.Segment = segment2
	h.First = 8
	h.Start = 2000
	second := fixtureHeader(h)
	// Arrival order must not become playback order.
	for _, data := range [][]byte{second, first} {
		p, err := decodeReplay(data)
		if err != nil {
			t.Fatal(err)
		}
		if _, err = s.upload(ctx, testPlayer, p, data, api.now()); err != nil {
			t.Fatal(err)
		}
	}
	view, err := s.getSession(ctx, in.Session)
	if err != nil || len(view.Chunks) != 2 {
		t.Fatal(view, err)
	}
	if view.Chunks[0].Segment != segment1 || view.Chunks[1].Segment != segment2 || !view.Coverage {
		t.Fatal("recording order lost", view)
	}
	if view.Chunks[0].SegmentCreated == nil || view.Chunks[1].SegmentCreated == nil || !view.Chunks[1].SegmentCreated.After(*view.Chunks[0].SegmentCreated) {
		t.Fatal("missing UUID times", view.Chunks)
	}
	start, err := time.Parse(time.RFC3339Nano, in.Started)
	if err != nil {
		t.Fatal(err)
	}
	if !view.Chunks[0].RecordedFrom.Equal(start.Add(time.Second)) || !view.Chunks[1].RecordedTo.Equal(start.Add(2*time.Second)) {
		t.Fatal("absolute recording times", view.Chunks)
	}
	payload, err := json.Marshal(view)
	if err != nil {
		t.Fatal(err)
	}
	if !strings.Contains(string(payload), `"segment_created_at"`) || !strings.Contains(string(payload), `"recorded_from"`) {
		t.Fatal(string(payload))
	}
}
func TestLegacySegmentStillHasAbsoluteRecordingTimeWithoutFakeUUIDTime(t *testing.T) {
	s, api := testSetup(t)
	registerHTTP(t, api)
	if w := uploadHTTP(t, api, fixture(0, 1, "")); w.Code != 200 {
		t.Fatal(w.Code, w.Body.String())
	}
	view, err := s.getSession(context.Background(), testSession)
	if err != nil || len(view.Chunks) != 1 {
		t.Fatal(view, err)
	}
	if view.Chunks[0].SegmentCreated != nil || !view.Chunks[0].RecordedFrom.Equal(view.Started) {
		t.Fatal(view.Chunks)
	}
	payload, err := json.Marshal(view)
	if err != nil || strings.Contains(string(payload), `"segment_created_at"`) {
		t.Fatal(string(payload), err)
	}
}
