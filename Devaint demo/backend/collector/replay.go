package main

import (
	"bytes"
	"compress/gzip"
	"encoding/binary"
	"encoding/hex"
	"encoding/json"
	"errors"
	"io"
	"math"
	"regexp"
	"strings"
	"unicode/utf8"
)

const maxUpload = 4 << 20
const maxExpanded = 16 << 20

var errFormat = errors.New("invalid_replay")
var safeVersion = regexp.MustCompile(`^[A-Za-z0-9._-]{1,128}$`)
var digestPattern = regexp.MustCompile(`^[a-f0-9]{64}$`)

type replayHeader struct {
	Session  string `json:"session"`
	Segment  string `json:"segment"`
	Run      string `json:"run"`
	Build    string `json:"build"`
	Content  string `json:"content"`
	UI       string `json:"ui"`
	Profile  string `json:"profile"`
	Previous string `json:"previous"`
	Schema   int    `json:"schema"`
	Chunk    int    `json:"chunk"`
	First    int64  `json:"first"`
	Start    int64  `json:"start"`
}

type extractedEvent struct {
	Seq      int64           `json:"seq"`
	Time     int64           `json:"time_ms"`
	Tick     int64           `json:"sim_tick"`
	Kind     string          `json:"kind"`
	Snapshot bool            `json:"snapshot"`
	Payload  json.RawMessage `json:"payload"`
}

type parsedReplay struct {
	Header       replayHeader
	LastSeq, End int64
	Records      int
	Scene        string
	Events       []extractedEvent
}

func normalID(s string) (string, bool) {
	if len(s) == 36 {
		if s[8] != '-' || s[13] != '-' || s[18] != '-' || s[23] != '-' {
			return "", false
		}
		s = strings.ReplaceAll(s, "-", "")
	}
	s = strings.ToLower(s)
	if len(s) != 32 || s == strings.Repeat("0", 32) {
		return "", false
	}
	_, err := hex.DecodeString(s)
	return s, err == nil
}

func strictJSON(data []byte, target any) error {
	// Reject duplicate keys at the top level as well as unknown fields/trailing JSON.
	scan := json.NewDecoder(bytes.NewReader(data))
	first, err := scan.Token()
	if err != nil || first != json.Delim('{') {
		return errFormat
	}
	seen := map[string]bool{}
	for scan.More() {
		key, err := scan.Token()
		if err != nil {
			return errFormat
		}
		name, ok := key.(string)
		if !ok || seen[name] {
			return errFormat
		}
		seen[name] = true
		var value json.RawMessage
		if scan.Decode(&value) != nil {
			return errFormat
		}
	}
	if _, err := scan.Token(); err != nil {
		return errFormat
	}
	d := json.NewDecoder(bytes.NewReader(data))
	d.DisallowUnknownFields()
	if d.Decode(target) != nil {
		return errFormat
	}
	if d.Decode(new(any)) != io.EOF {
		return errFormat
	}
	return nil
}

type wire struct {
	data []byte
	at   int
}

func (r *wire) u() (uint64, error) {
	if r.at >= len(r.data) {
		return 0, errFormat
	}
	v, n := binary.Uvarint(r.data[r.at:])
	if n <= 0 || (n > 1 && r.data[r.at+n-1] == 0) {
		return 0, errFormat
	}
	r.at += n
	return v, nil
}
func (r *wire) signed() (int64, error) { u, err := r.u(); return int64(u>>1) ^ -int64(u&1), err }
func (r *wire) slice(n uint64, max uint64) ([]byte, error) {
	if n > max || n > uint64(len(r.data)-r.at) {
		return nil, errFormat
	}
	out := r.data[r.at : r.at+int(n)]
	r.at += int(n)
	return out, nil
}

func decodeReplay(packed []byte) (*parsedReplay, error) {
	if len(packed) == 0 || len(packed) > maxUpload {
		return nil, errFormat
	}
	compressed := bytes.NewReader(packed)
	z, err := gzip.NewReader(compressed)
	if err != nil {
		return nil, errFormat
	}
	z.Multistream(false)
	raw, err := io.ReadAll(io.LimitReader(z, maxExpanded+1))
	closeErr := z.Close()
	if err != nil || closeErr != nil || len(raw) > maxExpanded || compressed.Len() != 0 || len(raw) < 14 || string(raw[:8]) != "DVRPLY01" || binary.LittleEndian.Uint16(raw[8:]) != 1 {
		return nil, errFormat
	}
	n := binary.LittleEndian.Uint32(raw[10:])
	if n == 0 || n > 65536 || int(n) > len(raw)-14 {
		return nil, errFormat
	}
	out := &parsedReplay{}
	if strictJSON(raw[14:14+int(n)], &out.Header) != nil {
		return nil, errFormat
	}
	h := out.Header
	if id, ok := normalID(h.Session); !ok || id != h.Session {
		return nil, errFormat
	}
	if id, ok := normalID(h.Segment); !ok || id != h.Segment {
		return nil, errFormat
	}
	if h.Run != "" {
		if id, ok := normalID(h.Run); !ok || id != h.Run {
			return nil, errFormat
		}
	}
	if h.Schema != 1 || h.Profile != "compact-v1" || h.Chunk < 0 || h.Chunk > 100000 || h.First < 1 || h.First > math.MaxInt64-65537 || h.Start < 0 || h.Start > 7*24*3600000 || !safeVersion.MatchString(h.Build) || !safeVersion.MatchString(h.Content) || !safeVersion.MatchString(h.UI) {
		return nil, errFormat
	}
	if (h.Chunk == 0 && h.Previous != "") || (h.Chunk != 0 && !digestPattern.MatchString(h.Previous)) {
		return nil, errFormat
	}
	r := wire{data: raw[14+int(n):]}
	seq, elapsed := h.First-1, h.Start
	dictionary := map[int64]string{}
	entityState := map[int64][]int64{}
	snapshot, footer := false, false
	analysisBytes := 0
	for r.at < len(r.data) {
		length, err := r.u()
		if err != nil || length == 0 {
			return nil, errFormat
		}
		data, err := r.slice(length, 256<<10)
		if err != nil {
			return nil, errFormat
		}
		p := wire{data: data}
		kind, e1 := p.u()
		delta, e2 := p.u()
		dt, e3 := p.u()
		tick, e4 := p.signed()
		count, e5 := p.u()
		if e1 != nil || e2 != nil || e3 != nil || e4 != nil || e5 != nil || kind < 1 || kind > 22 || delta != 1 || dt > uint64(7*24*3600000-elapsed) || seq == math.MaxInt64 || tick < -1 || count > 64 {
			return nil, errFormat
		}
		seq++
		elapsed += int64(dt)
		values := make([]int64, int(count))
		for i := range values {
			v, err := p.signed()
			if err != nil {
				return nil, errFormat
			}
			values[i] = v
		}
		if out.Records == 0 && kind != 2 {
			return nil, errFormat
		}
		if kind == 2 {
			if snapshot {
				return nil, errFormat
			}
			snapshot = true
			entityState = map[int64][]int64{}
		}
		if kind == 3 {
			if !snapshot {
				return nil, errFormat
			}
			snapshot = false
		}
		if kind == 4 {
			if len(values) < 2 || values[0] < 0 || values[1] < 0 || values[1] > 2047 {
				return nil, errFormat
			}
			old, ok := entityState[values[0]]
			if !ok && values[1] != 2047 {
				return nil, errFormat
			}
			expanded := make([]int64, 12)
			copy(expanded, old)
			expanded[0] = values[0]
			cursor := 2
			for i := 1; i < 12; i++ {
				if values[1]&(1<<uint(i-1)) != 0 {
					if cursor >= len(values) {
						return nil, errFormat
					}
					expanded[i] = values[cursor]
					cursor++
				}
			}
			if cursor != len(values) {
				return nil, errFormat
			}
			entityState[expanded[0]] = expanded
			values = expanded
		}
		if kind == 1 {
			if len(values) != 1 || values[0] <= 0 || len(dictionary) >= 8192 {
				return nil, errFormat
			}
			if _, exists := dictionary[values[0]]; exists {
				return nil, errFormat
			}
			n, err := p.u()
			if err != nil {
				return nil, errFormat
			}
			text, err := p.slice(n, 4096)
			if err != nil || !utf8.Valid(text) {
				return nil, errFormat
			}
			dictionary[values[0]] = string(text)
		}
		if p.at != len(p.data) {
			return nil, errFormat
		}
		expected := []int{0, 1, 0, 0, 12, 2, 5, 3, 2, 7, 9, 15, 8, 3, 5, 2, 2, 7, 2, 9, 9, 7, 2}
		if len(values) != expected[kind] {
			return nil, errFormat
		}
		if kind == 5 {
			delete(entityState, values[0])
		}
		ref := func(id int64) bool { _, ok := dictionary[id]; return id == 0 || (id > 0 && ok) }
		refs := []int{}
		switch kind {
		case 4:
			refs = []int{7, 9}
			for _, i := range []int{4, 5, 6, 8} {
				if !finiteBits(values[i]) {
					return nil, errFormat
				}
			}
		case 8, 17, 18:
			refs = []int{0}
		case 15:
			refs = []int{0, 1}
		case 7:
			if values[0] <= 2 || values[0] >= 10 {
				refs = []int{1}
			}
		case 10:
			refs = []int{6, 8}
		case 11:
			refs = []int{9, 10}
		case 14:
			refs = []int{0}
		case 19:
			refs = []int{1}
		case 20:
			refs = []int{1, 2, 3}
		case 21:
			if values[0] == 1 {
				refs = []int{1, 2, 4, 5}
			} else if values[0] == 2 {
				refs = []int{1, 2}
			}
		}
		for _, i := range refs {
			if !ref(values[i]) {
				return nil, errFormat
			}
		}
		if kind == 6 && (values[1] < -1 || values[1] > 4096 || values[2] < -1 || values[2] > 4096) {
			return nil, errFormat
		}
		if kind == 15 {
			out.Scene = dictionary[values[0]]
		}
		if kind == 16 {
			if snapshot || values[0] != seq-1 || values[1] != int64(out.Records) || r.at != len(r.data) {
				return nil, errFormat
			}
			footer = true
		}
		if name := eventName(kind, values); name != "" {
			payload := map[string]any{"values": values}
			resolved := map[int]string{}
			for _, i := range refs {
				if values[i] > 0 {
					resolved[i] = dictionary[values[i]]
				}
			}
			payload["resolved"] = resolved
			encoded, _ := json.Marshal(payload)
			analysisBytes += len(encoded)
			if len(out.Events) >= 16384 || analysisBytes > 8<<20 {
				return nil, errFormat
			}
			out.Events = append(out.Events, extractedEvent{Seq: seq, Time: elapsed, Tick: tick, Kind: name, Snapshot: snapshot, Payload: encoded})
		}
		out.Records++
		if out.Records > 65537 {
			return nil, errFormat
		}
	}
	if !footer || snapshot {
		return nil, errFormat
	}
	out.LastSeq = seq
	out.End = elapsed
	return out, nil
}

func finiteBits(v int64) bool {
	if v < math.MinInt32 || v > math.MaxInt32 {
		return false
	}
	f := math.Float32frombits(uint32(int32(v)))
	return !math.IsNaN(float64(f)) && !math.IsInf(float64(f), 0)
}
func eventName(kind uint64, v []int64) string {
	switch kind {
	case 8:
		return "ui_activate"
	case 10:
		if v[0] == 1 {
			return "player_command"
		}
	case 11:
		if v[0] == 1 || v[1] == 1 {
			return "player_hit"
		}
	case 14:
		return "run_state"
	case 15:
		return "scene_state"
	case 18:
		return "screen_state"
	case 20:
		return "weapon_state"
	case 21:
		if v[0] == 0 {
			return "growth_state"
		}
	case 22:
		return "economy_state"
	case 13:
		return "capture_gap"
	}
	return ""
}
