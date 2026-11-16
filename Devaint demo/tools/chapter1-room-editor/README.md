# Chapter 1 Combat Rooms

Open `index.html` directly in a browser. There are twelve room types; terrain starts
locked while monster species and positions remain editable with per-room local autosave.
Rooms 1–6 use compact continuous hill terrain. Rooms 7–12 are rising terraces, descending
terraces, a wide basin, a D-shaped corridor, a switchback corridor and a three-floor
atrium. Rooms 1–9 measure 32×18 tiles; rooms 10–12 measure 32×27 tiles.

Room 10 has a direction selector. Mirroring changes terrain, monsters and both
ports together. Both orientations remain one type, and the runtime seed chooses
the orientation. Exporting a left-facing draft normalizes the monster coordinates
back to the canonical right-facing template before syncing.

Each template declares entry/exit side and foot height. The generator keeps all
room interiors intact and connects their ports using exterior adapters. Macro
columns are 58 tiles apart: 32 room tiles, two 8-tile adapters and a 10-tile connector.
Branch rows are 48 tiles apart, joined by an uninterrupted wall-climb shaft with
no intermediate platforms or decks. Only connected doors open through the enclosing earth walls.
The same-side D entry wraps above the room when needed; a mirrored exit wraps below.
Exploration reveals these actual corridor segments, not their enclosing rectangle.

Ten distinct types appear in a run: seven on the main route and three in optional
branches. The shop and maintenance branches appear in the middle, the elite branch
later, and the boss remains last. The start room has one solid floor. Facilities
retain provisional gallery interiors supplied by the generator.

Wood platforms remain one-way with safe lower landing surfaces. New multi-floor
rooms have combat on their lower floors, not an extra catch floor. Terrain and wood
keep at least one tile of separation. A clear route may reverse direction on a new
floor, but never requires retracing an already cleared combat surface.

After editing a room's monsters, use **JSON 저장** for that room and run:

```powershell
node tools/chapter1-room-editor/sync-unity.cjs C:\path\to\exported-room.json
```

Several exported room files can be passed together. The command updates
`unity/Assets/ForestCombatV2/Resources/Chapter1Rooms.json`, retaining the
already-synced monsters in rooms not supplied in that invocation. It rejects
changed terrain or monsters placed off a surface. Use `--check` to verify the
catalog, or `--defaults` to intentionally restore every room's preset monsters.
The browser's local storage is only a working copy; exporting and syncing is
required before a team member or Unity will see later monster edits.

Run `node tools/chapter1-room-editor/verify-presets.js` to check room layouts,
and `node tools/chapter1-room-editor/verify-sync.cjs` to check monster exports.
Unity editor tests in `Chapter1ProceduralMapTests` compare generated room terrain
and spawns with this catalog, and check passage continuity and map discovery.

`Chapter1AuthoredRouteTests` drives the real player through all six new routes, including both D orientations. `ForestCombatWorld.proceduralSeed` defaults to -1 (fresh run); a non-negative value reproduces a test seed.

Compact drafts use separate `compact-v1` browser storage keys. Old local drafts are left intact; they are not silently resized. Exporting an old 48-tile draft into the new catalog is rejected. Monster edits saved within the compact geometry remain persistent.
