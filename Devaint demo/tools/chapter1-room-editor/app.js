(() => {
  'use strict';

  const TILE = 32;
  const WIDTH = 32;
  const HEIGHT = 18;
  const LEGACY_STORAGE = 'lost-words-chapter1-room-forest-clearing-compact-v1';
  const ART = '../../unity/Assets/';
  const species = [
    { id: '01_field_mouse', name: '들쥐', art: 'ForestCombatV2/Art/Monsters/01_field_mouse.png', width: 2.1 },
    { id: '02_mole', name: '두더지', art: 'ForestCombatV2/Art/Chapter1MonstersV4/02_mole/idle.png', sheet: true, width: 2.8 },
    { id: '05_boar', name: '멧돼지', art: 'ForestCombatV2/Art/Monsters/05_boar.png', width: 3.2 },
    { id: '07_scarecrow', name: '허수아비', art: 'ForestCombatV2/Art/Monsters/07_scarecrow.png', width: 2.4 },
    { id: '08_hay_bale', name: '건초더미', art: 'ForestCombatV2/Art/Monsters/08_hay_bale.png', width: 2.7 },
    { id: '10_water_bucket', name: '물통', art: 'ForestCombatV2/Art/Monsters/10_water_bucket.png', width: 2.2 }
  ];
  const initial = {
  "name": "숲의 이중 공터",
  "width": 32,
  "height": 18,
  "terrain": [
    {
      "x": 0,
      "y": 15,
      "w": 4,
      "h": 3,
      "kind": "earth",
      "id": "compact-surface-01"
    },
    {
      "x": 4,
      "y": 13,
      "w": 5,
      "h": 5,
      "kind": "earth",
      "id": "compact-surface-02"
    },
    {
      "x": 9,
      "y": 11,
      "w": 10,
      "h": 7,
      "kind": "earth",
      "id": "compact-surface-03"
    },
    {
      "x": 19,
      "y": 12,
      "w": 4,
      "h": 6,
      "kind": "earth",
      "id": "compact-surface-04"
    },
    {
      "x": 23,
      "y": 13,
      "w": 5,
      "h": 5,
      "kind": "earth",
      "id": "compact-surface-05"
    },
    {
      "x": 28,
      "y": 15,
      "w": 4,
      "h": 3,
      "kind": "earth",
      "id": "compact-surface-06"
    },
    {
      "x": 5,
      "y": 10,
      "w": 3,
      "h": 0.5,
      "kind": "wood",
      "id": "compact-surface-07"
    }
  ],
  "monsters": [
    {
      "x": 3,
      "y": 15,
      "species": "01_field_mouse",
      "id": "enemy-01"
    },
    {
      "x": 5.5,
      "y": 13,
      "species": "01_field_mouse",
      "id": "enemy-02"
    },
    {
      "x": 8,
      "y": 13,
      "species": "02_mole",
      "id": "enemy-03"
    },
    {
      "x": 10.5,
      "y": 11,
      "species": "01_field_mouse",
      "id": "enemy-04"
    },
    {
      "x": 13,
      "y": 11,
      "species": "05_boar",
      "id": "enemy-05"
    },
    {
      "x": 16,
      "y": 11,
      "species": "08_hay_bale",
      "id": "enemy-06"
    },
    {
      "x": 18,
      "y": 11,
      "species": "07_scarecrow",
      "id": "enemy-07"
    },
    {
      "x": 21,
      "y": 12,
      "species": "01_field_mouse",
      "id": "enemy-08"
    },
    {
      "x": 24.5,
      "y": 13,
      "species": "10_water_bucket",
      "id": "enemy-09"
    },
    {
      "x": 27,
      "y": 13,
      "species": "05_boar",
      "id": "enemy-10"
    }
  ],
  "ports": [
    {
      "side": "left",
      "y": 15,
      "role": "entry"
    },
    {
      "side": "right",
      "y": 15,
      "role": "exit"
    }
  ],
  "id": "forest_double_clearing_01",
  "clearRoute": [
    "compact-surface-01",
    "compact-surface-02",
    "compact-surface-03",
    "compact-surface-04",
    "compact-surface-05",
    "compact-surface-06"
  ],
  "schema": "chapter1-room-draft-v1",
  "tilePixels": 32,
  "terrainRevision": "compact-v1",
  "mirrorable": false,
  "variant": "right"
};
  const rooms = [
  {
    "initial": {
      "name": "숲의 이중 공터",
      "width": 32,
      "height": 18,
      "terrain": [
        {
          "x": 0,
          "y": 15,
          "w": 4,
          "h": 3,
          "kind": "earth",
          "id": "compact-surface-01"
        },
        {
          "x": 4,
          "y": 13,
          "w": 5,
          "h": 5,
          "kind": "earth",
          "id": "compact-surface-02"
        },
        {
          "x": 9,
          "y": 11,
          "w": 10,
          "h": 7,
          "kind": "earth",
          "id": "compact-surface-03"
        },
        {
          "x": 19,
          "y": 12,
          "w": 4,
          "h": 6,
          "kind": "earth",
          "id": "compact-surface-04"
        },
        {
          "x": 23,
          "y": 13,
          "w": 5,
          "h": 5,
          "kind": "earth",
          "id": "compact-surface-05"
        },
        {
          "x": 28,
          "y": 15,
          "w": 4,
          "h": 3,
          "kind": "earth",
          "id": "compact-surface-06"
        },
        {
          "x": 5,
          "y": 10,
          "w": 3,
          "h": 0.5,
          "kind": "wood",
          "id": "compact-surface-07"
        }
      ],
      "monsters": [
        {
          "x": 3,
          "y": 15,
          "species": "01_field_mouse",
          "id": "enemy-01"
        },
        {
          "x": 5.5,
          "y": 13,
          "species": "01_field_mouse",
          "id": "enemy-02"
        },
        {
          "x": 8,
          "y": 13,
          "species": "02_mole",
          "id": "enemy-03"
        },
        {
          "x": 10.5,
          "y": 11,
          "species": "01_field_mouse",
          "id": "enemy-04"
        },
        {
          "x": 13,
          "y": 11,
          "species": "05_boar",
          "id": "enemy-05"
        },
        {
          "x": 16,
          "y": 11,
          "species": "08_hay_bale",
          "id": "enemy-06"
        },
        {
          "x": 18,
          "y": 11,
          "species": "07_scarecrow",
          "id": "enemy-07"
        },
        {
          "x": 21,
          "y": 12,
          "species": "01_field_mouse",
          "id": "enemy-08"
        },
        {
          "x": 24.5,
          "y": 13,
          "species": "10_water_bucket",
          "id": "enemy-09"
        },
        {
          "x": 27,
          "y": 13,
          "species": "05_boar",
          "id": "enemy-10"
        }
      ],
      "ports": [
        {
          "side": "left",
          "y": 15,
          "role": "entry"
        },
        {
          "side": "right",
          "y": 15,
          "role": "exit"
        }
      ],
      "id": "forest_double_clearing_01",
      "clearRoute": [
        "compact-surface-01",
        "compact-surface-02",
        "compact-surface-03",
        "compact-surface-04",
        "compact-surface-05",
        "compact-surface-06"
      ],
      "schema": "chapter1-room-draft-v1",
      "tilePixels": 32,
      "terrainRevision": "compact-v1",
      "mirrorable": false,
      "variant": "right"
    },
    "number": 1,
    "storage": "lost-words-chapter1-room-forest-clearing-compact-v1",
    "description": "이동 구간을 줄인 축소 전투방 · 10마리 순차 배치",
    "route": "입구 → 순차 전투 → 출구",
    "routeBody": "점프 높이와 통행 여유를 유지하며 빈 이동을 줄인 동선입니다.",
    "combat": "숲의 이중 공터",
    "combatBody": "진행 중 새로운 전투면을 통과하며 이미 정리한 면을 왕복하지 않습니다.",
    "clearRoute": [
      "compact-surface-01",
      "compact-surface-02",
      "compact-surface-03",
      "compact-surface-04",
      "compact-surface-05",
      "compact-surface-06"
    ]
  },
  {
    "initial": {
      "id": "root_ravine_02",
      "name": "뿌리 협곡",
      "width": 32,
      "height": 18,
      "terrain": [
        {
          "id": "hill-0",
          "kind": "earth",
          "x": 0,
          "y": 15,
          "w": 4,
          "h": 3
        },
        {
          "id": "hill-1",
          "kind": "earth",
          "x": 4,
          "y": 12,
          "w": 5,
          "h": 6
        },
        {
          "id": "hill-2",
          "kind": "earth",
          "x": 9,
          "y": 13,
          "w": 5,
          "h": 5
        },
        {
          "id": "hill-3",
          "kind": "earth",
          "x": 14,
          "y": 10,
          "w": 7,
          "h": 8
        },
        {
          "id": "hill-4",
          "kind": "earth",
          "x": 21,
          "y": 12,
          "w": 6,
          "h": 6
        },
        {
          "id": "hill-5",
          "kind": "earth",
          "x": 27,
          "y": 15,
          "w": 5,
          "h": 3
        }
      ],
      "ports": [
        {
          "side": "left",
          "y": 15,
          "role": "entry"
        },
        {
          "side": "right",
          "y": 15,
          "role": "exit"
        }
      ],
      "monsters": [
        {
          "id": "enemy-01",
          "species": "01_field_mouse",
          "x": 3,
          "y": 15
        },
        {
          "id": "enemy-02",
          "species": "01_field_mouse",
          "x": 5.5,
          "y": 12
        },
        {
          "id": "enemy-03",
          "species": "02_mole",
          "x": 8,
          "y": 12
        },
        {
          "id": "enemy-04",
          "species": "07_scarecrow",
          "x": 10.5,
          "y": 13
        },
        {
          "id": "enemy-05",
          "species": "01_field_mouse",
          "x": 13,
          "y": 13
        },
        {
          "id": "enemy-06",
          "species": "10_water_bucket",
          "x": 15.5,
          "y": 10
        },
        {
          "id": "enemy-07",
          "species": "08_hay_bale",
          "x": 19,
          "y": 10
        },
        {
          "id": "enemy-08",
          "species": "02_mole",
          "x": 22.5,
          "y": 12
        },
        {
          "id": "enemy-09",
          "species": "05_boar",
          "x": 25.5,
          "y": 12
        },
        {
          "id": "enemy-10",
          "species": "01_field_mouse",
          "x": 29,
          "y": 15
        }
      ],
      "clearRoute": [
        "hill-0",
        "hill-1",
        "hill-2",
        "hill-3",
        "hill-4",
        "hill-5"
      ],
      "schema": "chapter1-room-draft-v1",
      "tilePixels": 32,
      "terrainRevision": "compact-v1",
      "mirrorable": false,
      "variant": "right"
    },
    "number": 2,
    "storage": "lost-words-chapter1-room-root_ravine_02-compact-v1",
    "description": "이동 구간을 줄인 축소 전투방 · 10마리 순차 배치",
    "route": "입구 → 순차 전투 → 출구",
    "routeBody": "점프 높이와 통행 여유를 유지하며 빈 이동을 줄인 동선입니다.",
    "combat": "뿌리 협곡",
    "combatBody": "진행 중 새로운 전투면을 통과하며 이미 정리한 면을 왕복하지 않습니다.",
    "clearRoute": [
      "hill-0",
      "hill-1",
      "hill-2",
      "hill-3",
      "hill-4",
      "hill-5"
    ]
  },
  {
    "initial": {
      "id": "long_timber_bridge_03",
      "name": "긴 숲 언덕",
      "width": 32,
      "height": 18,
      "terrain": [
        {
          "id": "hill-0",
          "kind": "earth",
          "x": 0,
          "y": 15,
          "w": 3,
          "h": 3
        },
        {
          "id": "hill-1",
          "kind": "earth",
          "x": 3,
          "y": 12,
          "w": 11,
          "h": 6
        },
        {
          "id": "hill-2",
          "kind": "earth",
          "x": 14,
          "y": 9,
          "w": 5,
          "h": 9
        },
        {
          "id": "hill-3",
          "kind": "earth",
          "x": 19,
          "y": 12,
          "w": 10,
          "h": 6
        },
        {
          "id": "hill-4",
          "kind": "earth",
          "x": 29,
          "y": 15,
          "w": 3,
          "h": 3
        },
        {
          "id": "ridge-balcony",
          "kind": "wood",
          "x": 5,
          "y": 9,
          "w": 5,
          "h": 0.5
        }
      ],
      "ports": [
        {
          "side": "left",
          "y": 15,
          "role": "entry"
        },
        {
          "side": "right",
          "y": 15,
          "role": "exit"
        }
      ],
      "monsters": [
        {
          "id": "enemy-01",
          "species": "01_field_mouse",
          "x": 2,
          "y": 15
        },
        {
          "id": "enemy-02",
          "species": "07_scarecrow",
          "x": 5,
          "y": 12
        },
        {
          "id": "enemy-03",
          "species": "01_field_mouse",
          "x": 8,
          "y": 12
        },
        {
          "id": "enemy-04",
          "species": "10_water_bucket",
          "x": 11.5,
          "y": 12
        },
        {
          "id": "enemy-05",
          "species": "05_boar",
          "x": 15,
          "y": 9
        },
        {
          "id": "enemy-06",
          "species": "08_hay_bale",
          "x": 18,
          "y": 9
        },
        {
          "id": "enemy-07",
          "species": "02_mole",
          "x": 21,
          "y": 12
        },
        {
          "id": "enemy-08",
          "species": "01_field_mouse",
          "x": 24,
          "y": 12
        },
        {
          "id": "enemy-09",
          "species": "05_boar",
          "x": 27,
          "y": 12
        },
        {
          "id": "enemy-10",
          "species": "07_scarecrow",
          "x": 30,
          "y": 15
        }
      ],
      "clearRoute": [
        "hill-0",
        "hill-1",
        "hill-2",
        "hill-3",
        "hill-4"
      ],
      "schema": "chapter1-room-draft-v1",
      "tilePixels": 32,
      "terrainRevision": "compact-v1",
      "mirrorable": false,
      "variant": "right"
    },
    "number": 3,
    "storage": "lost-words-chapter1-room-long_timber_bridge_03-compact-v1",
    "description": "이동 구간을 줄인 축소 전투방 · 10마리 순차 배치",
    "route": "입구 → 순차 전투 → 출구",
    "routeBody": "점프 높이와 통행 여유를 유지하며 빈 이동을 줄인 동선입니다.",
    "combat": "긴 숲 언덕",
    "combatBody": "진행 중 새로운 전투면을 통과하며 이미 정리한 면을 왕복하지 않습니다.",
    "clearRoute": [
      "hill-0",
      "hill-1",
      "hill-2",
      "hill-3",
      "hill-4"
    ]
  },
  {
    "initial": {
      "id": "three_tier_hills_04",
      "name": "삼단 언덕",
      "width": 32,
      "height": 18,
      "terrain": [
        {
          "id": "hill-0",
          "kind": "earth",
          "x": 0,
          "y": 15,
          "w": 4,
          "h": 3
        },
        {
          "id": "hill-1",
          "kind": "earth",
          "x": 4,
          "y": 13,
          "w": 7,
          "h": 5
        },
        {
          "id": "hill-2",
          "kind": "earth",
          "x": 11,
          "y": 10,
          "w": 7,
          "h": 8
        },
        {
          "id": "hill-3",
          "kind": "earth",
          "x": 18,
          "y": 7,
          "w": 7,
          "h": 11
        },
        {
          "id": "hill-4",
          "kind": "earth",
          "x": 25,
          "y": 10,
          "w": 4,
          "h": 8
        },
        {
          "id": "hill-5",
          "kind": "earth",
          "x": 29,
          "y": 13,
          "w": 3,
          "h": 5
        }
      ],
      "ports": [
        {
          "side": "left",
          "y": 15,
          "role": "entry"
        },
        {
          "side": "right",
          "y": 13,
          "role": "exit"
        }
      ],
      "monsters": [
        {
          "id": "enemy-01",
          "species": "01_field_mouse",
          "x": 3,
          "y": 15
        },
        {
          "id": "enemy-02",
          "species": "01_field_mouse",
          "x": 5.5,
          "y": 13
        },
        {
          "id": "enemy-03",
          "species": "02_mole",
          "x": 9,
          "y": 13
        },
        {
          "id": "enemy-04",
          "species": "08_hay_bale",
          "x": 12.5,
          "y": 10
        },
        {
          "id": "enemy-05",
          "species": "01_field_mouse",
          "x": 16,
          "y": 10
        },
        {
          "id": "enemy-06",
          "species": "07_scarecrow",
          "x": 19.5,
          "y": 7
        },
        {
          "id": "enemy-07",
          "species": "10_water_bucket",
          "x": 23,
          "y": 7
        },
        {
          "id": "enemy-08",
          "species": "01_field_mouse",
          "x": 26,
          "y": 10
        },
        {
          "id": "enemy-09",
          "species": "02_mole",
          "x": 28,
          "y": 10
        },
        {
          "id": "enemy-10",
          "species": "05_boar",
          "x": 30,
          "y": 13
        }
      ],
      "clearRoute": [
        "hill-0",
        "hill-1",
        "hill-2",
        "hill-3",
        "hill-4",
        "hill-5"
      ],
      "schema": "chapter1-room-draft-v1",
      "tilePixels": 32,
      "terrainRevision": "compact-v1",
      "mirrorable": false,
      "variant": "right"
    },
    "number": 4,
    "storage": "lost-words-chapter1-room-three_tier_hills_04-compact-v1",
    "description": "이동 구간을 줄인 축소 전투방 · 10마리 순차 배치",
    "route": "입구 → 순차 전투 → 출구",
    "routeBody": "점프 높이와 통행 여유를 유지하며 빈 이동을 줄인 동선입니다.",
    "combat": "삼단 언덕",
    "combatBody": "진행 중 새로운 전투면을 통과하며 이미 정리한 면을 왕복하지 않습니다.",
    "clearRoute": [
      "hill-0",
      "hill-1",
      "hill-2",
      "hill-3",
      "hill-4",
      "hill-5"
    ]
  },
  {
    "initial": {
      "id": "low_root_tunnel_05",
      "name": "이끼 둔덕",
      "width": 32,
      "height": 18,
      "terrain": [
        {
          "id": "hill-0",
          "kind": "earth",
          "x": 0,
          "y": 12,
          "w": 4,
          "h": 6
        },
        {
          "id": "hill-1",
          "kind": "earth",
          "x": 4,
          "y": 14,
          "w": 6,
          "h": 4
        },
        {
          "id": "hill-2",
          "kind": "earth",
          "x": 10,
          "y": 15,
          "w": 12,
          "h": 3
        },
        {
          "id": "hill-3",
          "kind": "earth",
          "x": 22,
          "y": 14,
          "w": 6,
          "h": 4
        },
        {
          "id": "hill-4",
          "kind": "earth",
          "x": 28,
          "y": 12,
          "w": 4,
          "h": 6
        }
      ],
      "ports": [
        {
          "side": "left",
          "y": 12,
          "role": "entry"
        },
        {
          "side": "right",
          "y": 12,
          "role": "exit"
        }
      ],
      "monsters": [
        {
          "id": "enemy-01",
          "species": "01_field_mouse",
          "x": 3,
          "y": 12
        },
        {
          "id": "enemy-02",
          "species": "07_scarecrow",
          "x": 5.5,
          "y": 14
        },
        {
          "id": "enemy-03",
          "species": "01_field_mouse",
          "x": 8,
          "y": 14
        },
        {
          "id": "enemy-04",
          "species": "01_field_mouse",
          "x": 11.5,
          "y": 15
        },
        {
          "id": "enemy-05",
          "species": "08_hay_bale",
          "x": 14,
          "y": 15
        },
        {
          "id": "enemy-06",
          "species": "10_water_bucket",
          "x": 17,
          "y": 15
        },
        {
          "id": "enemy-07",
          "species": "05_boar",
          "x": 20.5,
          "y": 15
        },
        {
          "id": "enemy-08",
          "species": "07_scarecrow",
          "x": 23.5,
          "y": 14
        },
        {
          "id": "enemy-09",
          "species": "02_mole",
          "x": 26,
          "y": 14
        },
        {
          "id": "enemy-10",
          "species": "01_field_mouse",
          "x": 30,
          "y": 12
        }
      ],
      "clearRoute": [
        "hill-0",
        "hill-1",
        "hill-2",
        "hill-3",
        "hill-4"
      ],
      "schema": "chapter1-room-draft-v1",
      "tilePixels": 32,
      "terrainRevision": "compact-v1",
      "mirrorable": false,
      "variant": "right"
    },
    "number": 5,
    "storage": "lost-words-chapter1-room-low_root_tunnel_05-compact-v1",
    "description": "이동 구간을 줄인 축소 전투방 · 10마리 순차 배치",
    "route": "입구 → 순차 전투 → 출구",
    "routeBody": "점프 높이와 통행 여유를 유지하며 빈 이동을 줄인 동선입니다.",
    "combat": "이끼 둔덕",
    "combatBody": "진행 중 새로운 전투면을 통과하며 이미 정리한 면을 왕복하지 않습니다.",
    "clearRoute": [
      "hill-0",
      "hill-1",
      "hill-2",
      "hill-3",
      "hill-4"
    ]
  },
  {
    "initial": {
      "id": "loop_courtyard_06",
      "name": "언덕 마당",
      "width": 32,
      "height": 18,
      "terrain": [
        {
          "id": "hill-0",
          "kind": "earth",
          "x": 0,
          "y": 15,
          "w": 4,
          "h": 3
        },
        {
          "id": "hill-1",
          "kind": "earth",
          "x": 4,
          "y": 12,
          "w": 5,
          "h": 6
        },
        {
          "id": "hill-2",
          "kind": "earth",
          "x": 9,
          "y": 10,
          "w": 14,
          "h": 8
        },
        {
          "id": "hill-3",
          "kind": "earth",
          "x": 23,
          "y": 12,
          "w": 5,
          "h": 6
        },
        {
          "id": "hill-4",
          "kind": "earth",
          "x": 28,
          "y": 15,
          "w": 4,
          "h": 3
        },
        {
          "id": "courtyard-balcony",
          "kind": "wood",
          "x": 14,
          "y": 7,
          "w": 4,
          "h": 0.5
        }
      ],
      "ports": [
        {
          "side": "left",
          "y": 15,
          "role": "entry"
        },
        {
          "side": "right",
          "y": 15,
          "role": "exit"
        }
      ],
      "monsters": [
        {
          "id": "enemy-01",
          "species": "01_field_mouse",
          "x": 3,
          "y": 15
        },
        {
          "id": "enemy-02",
          "species": "01_field_mouse",
          "x": 5.5,
          "y": 12
        },
        {
          "id": "enemy-03",
          "species": "07_scarecrow",
          "x": 8,
          "y": 12
        },
        {
          "id": "enemy-04",
          "species": "08_hay_bale",
          "x": 10.5,
          "y": 10
        },
        {
          "id": "enemy-05",
          "species": "02_mole",
          "x": 14,
          "y": 10
        },
        {
          "id": "enemy-06",
          "species": "10_water_bucket",
          "x": 18,
          "y": 10
        },
        {
          "id": "enemy-07",
          "species": "01_field_mouse",
          "x": 21.5,
          "y": 10
        },
        {
          "id": "enemy-08",
          "species": "02_mole",
          "x": 24,
          "y": 12
        },
        {
          "id": "enemy-09",
          "species": "07_scarecrow",
          "x": 26.5,
          "y": 12
        },
        {
          "id": "enemy-10",
          "species": "05_boar",
          "x": 30,
          "y": 15
        }
      ],
      "clearRoute": [
        "hill-0",
        "hill-1",
        "hill-2",
        "hill-3",
        "hill-4"
      ],
      "schema": "chapter1-room-draft-v1",
      "tilePixels": 32,
      "terrainRevision": "compact-v1",
      "mirrorable": false,
      "variant": "right"
    },
    "number": 6,
    "storage": "lost-words-chapter1-room-loop_courtyard_06-compact-v1",
    "description": "이동 구간을 줄인 축소 전투방 · 10마리 순차 배치",
    "route": "입구 → 순차 전투 → 출구",
    "routeBody": "점프 높이와 통행 여유를 유지하며 빈 이동을 줄인 동선입니다.",
    "combat": "언덕 마당",
    "combatBody": "진행 중 새로운 전투면을 통과하며 이미 정리한 면을 왕복하지 않습니다.",
    "clearRoute": [
      "hill-0",
      "hill-1",
      "hill-2",
      "hill-3",
      "hill-4"
    ]
  },
  {
    "initial": {
      "id": "rising_terraces_07",
      "name": "상승 숲 계단",
      "width": 32,
      "height": 18,
      "terrain": [
        {
          "id": "hill-0",
          "kind": "earth",
          "x": 0,
          "y": 15,
          "w": 8,
          "h": 3
        },
        {
          "id": "hill-1",
          "kind": "earth",
          "x": 8,
          "y": 12,
          "w": 8,
          "h": 6
        },
        {
          "id": "hill-2",
          "kind": "earth",
          "x": 16,
          "y": 9,
          "w": 8,
          "h": 9
        },
        {
          "id": "hill-3",
          "kind": "earth",
          "x": 24,
          "y": 6,
          "w": 8,
          "h": 12
        }
      ],
      "ports": [
        {
          "side": "left",
          "y": 15,
          "role": "entry"
        },
        {
          "side": "right",
          "y": 6,
          "role": "exit"
        }
      ],
      "monsters": [
        {
          "id": "enemy-01",
          "species": "01_field_mouse",
          "x": 3,
          "y": 15
        },
        {
          "id": "enemy-02",
          "species": "02_mole",
          "x": 5.5,
          "y": 15
        },
        {
          "id": "enemy-03",
          "species": "05_boar",
          "x": 9.5,
          "y": 12
        },
        {
          "id": "enemy-04",
          "species": "07_scarecrow",
          "x": 12,
          "y": 12
        },
        {
          "id": "enemy-05",
          "species": "08_hay_bale",
          "x": 14.5,
          "y": 12
        },
        {
          "id": "enemy-06",
          "species": "10_water_bucket",
          "x": 17.5,
          "y": 9
        },
        {
          "id": "enemy-07",
          "species": "01_field_mouse",
          "x": 21,
          "y": 9
        },
        {
          "id": "enemy-08",
          "species": "02_mole",
          "x": 25.5,
          "y": 6
        },
        {
          "id": "enemy-09",
          "species": "05_boar",
          "x": 28,
          "y": 6
        },
        {
          "id": "enemy-10",
          "species": "07_scarecrow",
          "x": 30,
          "y": 6
        }
      ],
      "clearRoute": [
        "hill-0",
        "hill-1",
        "hill-2",
        "hill-3"
      ],
      "schema": "chapter1-room-draft-v1",
      "tilePixels": 32,
      "terrainRevision": "compact-v1",
      "mirrorable": false,
      "variant": "right"
    },
    "number": 7,
    "storage": "lost-words-chapter1-room-rising_terraces_07-compact-v1",
    "description": "이동 구간을 줄인 축소 전투방 · 10마리 순차 배치",
    "route": "입구 → 순차 전투 → 출구",
    "routeBody": "점프 높이와 통행 여유를 유지하며 빈 이동을 줄인 동선입니다.",
    "combat": "상승 숲 계단",
    "combatBody": "진행 중 새로운 전투면을 통과하며 이미 정리한 면을 왕복하지 않습니다.",
    "clearRoute": [
      "hill-0",
      "hill-1",
      "hill-2",
      "hill-3"
    ]
  },
  {
    "initial": {
      "id": "descending_terraces_08",
      "name": "하강 숲 계단",
      "width": 32,
      "height": 18,
      "terrain": [
        {
          "id": "hill-0",
          "kind": "earth",
          "x": 0,
          "y": 6,
          "w": 8,
          "h": 12
        },
        {
          "id": "hill-1",
          "kind": "earth",
          "x": 8,
          "y": 9,
          "w": 8,
          "h": 9
        },
        {
          "id": "hill-2",
          "kind": "earth",
          "x": 16,
          "y": 12,
          "w": 8,
          "h": 6
        },
        {
          "id": "hill-3",
          "kind": "earth",
          "x": 24,
          "y": 15,
          "w": 8,
          "h": 3
        }
      ],
      "ports": [
        {
          "side": "left",
          "y": 6,
          "role": "entry"
        },
        {
          "side": "right",
          "y": 15,
          "role": "exit"
        }
      ],
      "monsters": [
        {
          "id": "enemy-01",
          "species": "01_field_mouse",
          "x": 3,
          "y": 6
        },
        {
          "id": "enemy-02",
          "species": "02_mole",
          "x": 6,
          "y": 6
        },
        {
          "id": "enemy-03",
          "species": "05_boar",
          "x": 9.5,
          "y": 9
        },
        {
          "id": "enemy-04",
          "species": "07_scarecrow",
          "x": 13,
          "y": 9
        },
        {
          "id": "enemy-05",
          "species": "08_hay_bale",
          "x": 17.5,
          "y": 12
        },
        {
          "id": "enemy-06",
          "species": "10_water_bucket",
          "x": 20,
          "y": 12
        },
        {
          "id": "enemy-07",
          "species": "01_field_mouse",
          "x": 22.5,
          "y": 12
        },
        {
          "id": "enemy-08",
          "species": "02_mole",
          "x": 25.5,
          "y": 15
        },
        {
          "id": "enemy-09",
          "species": "05_boar",
          "x": 28,
          "y": 15
        },
        {
          "id": "enemy-10",
          "species": "07_scarecrow",
          "x": 30,
          "y": 15
        }
      ],
      "clearRoute": [
        "hill-0",
        "hill-1",
        "hill-2",
        "hill-3"
      ],
      "schema": "chapter1-room-draft-v1",
      "tilePixels": 32,
      "terrainRevision": "compact-v1",
      "mirrorable": false,
      "variant": "right"
    },
    "number": 8,
    "storage": "lost-words-chapter1-room-descending_terraces_08-compact-v1",
    "description": "이동 구간을 줄인 축소 전투방 · 10마리 순차 배치",
    "route": "입구 → 순차 전투 → 출구",
    "routeBody": "점프 높이와 통행 여유를 유지하며 빈 이동을 줄인 동선입니다.",
    "combat": "하강 숲 계단",
    "combatBody": "진행 중 새로운 전투면을 통과하며 이미 정리한 면을 왕복하지 않습니다.",
    "clearRoute": [
      "hill-0",
      "hill-1",
      "hill-2",
      "hill-3"
    ]
  },
  {
    "initial": {
      "id": "wide_basin_09",
      "name": "넓은 숲 분지",
      "width": 32,
      "height": 18,
      "terrain": [
        {
          "id": "hill-0",
          "kind": "earth",
          "x": 0,
          "y": 9,
          "w": 4,
          "h": 9
        },
        {
          "id": "hill-1",
          "kind": "earth",
          "x": 4,
          "y": 12,
          "w": 4,
          "h": 6
        },
        {
          "id": "hill-2",
          "kind": "earth",
          "x": 8,
          "y": 15,
          "w": 16,
          "h": 3
        },
        {
          "id": "hill-3",
          "kind": "earth",
          "x": 24,
          "y": 12,
          "w": 4,
          "h": 6
        },
        {
          "id": "hill-4",
          "kind": "earth",
          "x": 28,
          "y": 9,
          "w": 4,
          "h": 9
        }
      ],
      "ports": [
        {
          "side": "left",
          "y": 9,
          "role": "entry"
        },
        {
          "side": "right",
          "y": 9,
          "role": "exit"
        }
      ],
      "monsters": [
        {
          "id": "enemy-01",
          "species": "01_field_mouse",
          "x": 3,
          "y": 9
        },
        {
          "id": "enemy-02",
          "species": "02_mole",
          "x": 6,
          "y": 12
        },
        {
          "id": "enemy-03",
          "species": "05_boar",
          "x": 9.5,
          "y": 15
        },
        {
          "id": "enemy-04",
          "species": "07_scarecrow",
          "x": 12,
          "y": 15
        },
        {
          "id": "enemy-05",
          "species": "08_hay_bale",
          "x": 15,
          "y": 15
        },
        {
          "id": "enemy-06",
          "species": "10_water_bucket",
          "x": 18,
          "y": 15
        },
        {
          "id": "enemy-07",
          "species": "01_field_mouse",
          "x": 21,
          "y": 15
        },
        {
          "id": "enemy-08",
          "species": "02_mole",
          "x": 23,
          "y": 15
        },
        {
          "id": "enemy-09",
          "species": "05_boar",
          "x": 26,
          "y": 12
        },
        {
          "id": "enemy-10",
          "species": "07_scarecrow",
          "x": 30,
          "y": 9
        }
      ],
      "clearRoute": [
        "hill-0",
        "hill-1",
        "hill-2",
        "hill-3",
        "hill-4"
      ],
      "schema": "chapter1-room-draft-v1",
      "tilePixels": 32,
      "terrainRevision": "compact-v1",
      "mirrorable": false,
      "variant": "right"
    },
    "number": 9,
    "storage": "lost-words-chapter1-room-wide_basin_09-compact-v1",
    "description": "이동 구간을 줄인 축소 전투방 · 10마리 순차 배치",
    "route": "입구 → 순차 전투 → 출구",
    "routeBody": "점프 높이와 통행 여유를 유지하며 빈 이동을 줄인 동선입니다.",
    "combat": "넓은 숲 분지",
    "combatBody": "진행 중 새로운 전투면을 통과하며 이미 정리한 면을 왕복하지 않습니다.",
    "clearRoute": [
      "hill-0",
      "hill-1",
      "hill-2",
      "hill-3",
      "hill-4"
    ]
  },
  {
    "initial": {
      "name": "ㄷ자 굽은 회랑",
      "width": 32,
      "height": 27,
      "terrain": [
        {
          "x": 6,
          "y": 6,
          "w": 26,
          "h": 12,
          "kind": "earth",
          "id": "compact-surface-01"
        },
        {
          "x": 0,
          "y": 24,
          "w": 32,
          "h": 3,
          "kind": "earth",
          "id": "compact-surface-02"
        },
        {
          "x": 0,
          "y": 9,
          "w": 4,
          "h": 0.5,
          "kind": "wood",
          "id": "compact-surface-03"
        },
        {
          "x": 0,
          "y": 12,
          "w": 4,
          "h": 0.5,
          "kind": "wood",
          "id": "compact-surface-04"
        },
        {
          "x": 0,
          "y": 15,
          "w": 4,
          "h": 0.5,
          "kind": "wood",
          "id": "compact-surface-05"
        },
        {
          "x": 0,
          "y": 18,
          "w": 4,
          "h": 0.5,
          "kind": "wood",
          "id": "compact-surface-06"
        },
        {
          "x": 0,
          "y": 21,
          "w": 4,
          "h": 0.5,
          "kind": "wood",
          "id": "compact-surface-07"
        }
      ],
      "monsters": [
        {
          "x": 29,
          "y": 6,
          "species": "01_field_mouse",
          "id": "enemy-01"
        },
        {
          "x": 24,
          "y": 6,
          "species": "01_field_mouse",
          "id": "enemy-02"
        },
        {
          "x": 19,
          "y": 6,
          "species": "02_mole",
          "id": "enemy-03"
        },
        {
          "x": 14,
          "y": 6,
          "species": "01_field_mouse",
          "id": "enemy-04"
        },
        {
          "x": 9,
          "y": 6,
          "species": "05_boar",
          "id": "enemy-05"
        },
        {
          "x": 5,
          "y": 24,
          "species": "08_hay_bale",
          "id": "enemy-06"
        },
        {
          "x": 10,
          "y": 24,
          "species": "07_scarecrow",
          "id": "enemy-07"
        },
        {
          "x": 16,
          "y": 24,
          "species": "01_field_mouse",
          "id": "enemy-08"
        },
        {
          "x": 22,
          "y": 24,
          "species": "10_water_bucket",
          "id": "enemy-09"
        },
        {
          "x": 28,
          "y": 24,
          "species": "05_boar",
          "id": "enemy-10"
        }
      ],
      "ports": [
        {
          "side": "right",
          "y": 6,
          "role": "entry"
        },
        {
          "side": "right",
          "y": 24,
          "role": "exit"
        }
      ],
      "id": "digeut_10",
      "clearRoute": [
        "compact-surface-01",
        "compact-surface-03",
        "compact-surface-04",
        "compact-surface-05",
        "compact-surface-06",
        "compact-surface-07",
        "compact-surface-02"
      ],
      "schema": "chapter1-room-draft-v1",
      "tilePixels": 32,
      "terrainRevision": "compact-v1",
      "mirrorable": true,
      "variant": "right"
    },
    "number": 10,
    "storage": "lost-words-chapter1-room-digeut_10-compact-v1",
    "description": "이동 구간을 줄인 축소 전투방 · 10마리 순차 배치",
    "route": "입구 → 순차 전투 → 출구",
    "routeBody": "점프 높이와 통행 여유를 유지하며 빈 이동을 줄인 동선입니다.",
    "combat": "ㄷ자 굽은 회랑",
    "combatBody": "진행 중 새로운 전투면을 통과하며 이미 정리한 면을 왕복하지 않습니다.",
    "clearRoute": [
      "compact-surface-01",
      "compact-surface-03",
      "compact-surface-04",
      "compact-surface-05",
      "compact-surface-06",
      "compact-surface-07",
      "compact-surface-02"
    ]
  },
  {
    "initial": {
      "name": "ㄹ자 층계 회랑",
      "width": 32,
      "height": 27,
      "terrain": [
        {
          "x": 0,
          "y": 6,
          "w": 26,
          "h": 3,
          "kind": "earth",
          "id": "compact-surface-01"
        },
        {
          "x": 6,
          "y": 15,
          "w": 26,
          "h": 3,
          "kind": "earth",
          "id": "compact-surface-02"
        },
        {
          "x": 0,
          "y": 24,
          "w": 32,
          "h": 3,
          "kind": "earth",
          "id": "compact-surface-03"
        },
        {
          "x": 28,
          "y": 9,
          "w": 4,
          "h": 0.5,
          "kind": "wood",
          "id": "compact-surface-04"
        },
        {
          "x": 28,
          "y": 12,
          "w": 4,
          "h": 0.5,
          "kind": "wood",
          "id": "compact-surface-05"
        },
        {
          "x": 0,
          "y": 18,
          "w": 4,
          "h": 0.5,
          "kind": "wood",
          "id": "compact-surface-06"
        },
        {
          "x": 0,
          "y": 21,
          "w": 4,
          "h": 0.5,
          "kind": "wood",
          "id": "compact-surface-07"
        }
      ],
      "monsters": [
        {
          "x": 3,
          "y": 6,
          "species": "01_field_mouse",
          "id": "enemy-01"
        },
        {
          "x": 12,
          "y": 6,
          "species": "02_mole",
          "id": "enemy-02"
        },
        {
          "x": 23,
          "y": 6,
          "species": "05_boar",
          "id": "enemy-03"
        },
        {
          "x": 29,
          "y": 15,
          "species": "07_scarecrow",
          "id": "enemy-04"
        },
        {
          "x": 22,
          "y": 15,
          "species": "08_hay_bale",
          "id": "enemy-05"
        },
        {
          "x": 15,
          "y": 15,
          "species": "10_water_bucket",
          "id": "enemy-06"
        },
        {
          "x": 8,
          "y": 15,
          "species": "01_field_mouse",
          "id": "enemy-07"
        },
        {
          "x": 5,
          "y": 24,
          "species": "02_mole",
          "id": "enemy-08"
        },
        {
          "x": 16,
          "y": 24,
          "species": "05_boar",
          "id": "enemy-09"
        },
        {
          "x": 28,
          "y": 24,
          "species": "07_scarecrow",
          "id": "enemy-10"
        }
      ],
      "ports": [
        {
          "side": "left",
          "y": 6,
          "role": "entry"
        },
        {
          "side": "right",
          "y": 24,
          "role": "exit"
        }
      ],
      "id": "rieul_11",
      "clearRoute": [
        "compact-surface-01",
        "compact-surface-04",
        "compact-surface-05",
        "compact-surface-02",
        "compact-surface-06",
        "compact-surface-07",
        "compact-surface-03"
      ],
      "schema": "chapter1-room-draft-v1",
      "tilePixels": 32,
      "terrainRevision": "compact-v1",
      "mirrorable": false,
      "variant": "right"
    },
    "number": 11,
    "storage": "lost-words-chapter1-room-rieul_11-compact-v1",
    "description": "이동 구간을 줄인 축소 전투방 · 10마리 순차 배치",
    "route": "입구 → 순차 전투 → 출구",
    "routeBody": "점프 높이와 통행 여유를 유지하며 빈 이동을 줄인 동선입니다.",
    "combat": "ㄹ자 층계 회랑",
    "combatBody": "진행 중 새로운 전투면을 통과하며 이미 정리한 면을 왕복하지 않습니다.",
    "clearRoute": [
      "compact-surface-01",
      "compact-surface-04",
      "compact-surface-05",
      "compact-surface-02",
      "compact-surface-06",
      "compact-surface-07",
      "compact-surface-03"
    ]
  },
  {
    "initial": {
      "id": "sam_12",
      "name": "三자 열린 누각",
      "width": 32,
      "height": 27,
      "terrain": [
        {
          "id": "lower",
          "kind": "earth",
          "x": 0,
          "y": 24,
          "w": 32,
          "h": 3
        },
        {
          "id": "middle",
          "kind": "wood",
          "x": 0,
          "y": 15,
          "w": 32,
          "h": 0.5
        },
        {
          "id": "upper",
          "kind": "wood",
          "x": 0,
          "y": 6,
          "w": 32,
          "h": 0.5
        },
        {
          "id": "right-21",
          "kind": "wood",
          "x": 28,
          "y": 21,
          "w": 4,
          "h": 0.5
        },
        {
          "id": "right-18",
          "kind": "wood",
          "x": 28,
          "y": 18,
          "w": 4,
          "h": 0.5
        },
        {
          "id": "left-12",
          "kind": "wood",
          "x": 0,
          "y": 12,
          "w": 4,
          "h": 0.5
        },
        {
          "id": "left-9",
          "kind": "wood",
          "x": 0,
          "y": 9,
          "w": 4,
          "h": 0.5
        }
      ],
      "ports": [
        {
          "side": "left",
          "y": 24,
          "role": "entry"
        },
        {
          "side": "right",
          "y": 6,
          "role": "exit"
        }
      ],
      "monsters": [
        {
          "id": "enemy-01",
          "species": "01_field_mouse",
          "x": 3,
          "y": 24
        },
        {
          "id": "enemy-02",
          "species": "02_mole",
          "x": 12,
          "y": 24
        },
        {
          "id": "enemy-03",
          "species": "05_boar",
          "x": 24,
          "y": 24
        },
        {
          "id": "enemy-04",
          "species": "07_scarecrow",
          "x": 29,
          "y": 15
        },
        {
          "id": "enemy-05",
          "species": "08_hay_bale",
          "x": 22,
          "y": 15
        },
        {
          "id": "enemy-06",
          "species": "10_water_bucket",
          "x": 15,
          "y": 15
        },
        {
          "id": "enemy-07",
          "species": "01_field_mouse",
          "x": 8,
          "y": 15
        },
        {
          "id": "enemy-08",
          "species": "02_mole",
          "x": 5,
          "y": 6
        },
        {
          "id": "enemy-09",
          "species": "05_boar",
          "x": 16,
          "y": 6
        },
        {
          "id": "enemy-10",
          "species": "07_scarecrow",
          "x": 28,
          "y": 6
        }
      ],
      "clearRoute": [
        "lower",
        "right-21",
        "right-18",
        "middle",
        "left-12",
        "left-9",
        "upper"
      ],
      "schema": "chapter1-room-draft-v1",
      "tilePixels": 32,
      "terrainRevision": "compact-v1",
      "mirrorable": false,
      "variant": "right"
    },
    "number": 12,
    "storage": "lost-words-chapter1-room-sam_12-compact-v1",
    "description": "이동 구간을 줄인 축소 전투방 · 10마리 순차 배치",
    "route": "입구 → 순차 전투 → 출구",
    "routeBody": "점프 높이와 통행 여유를 유지하며 빈 이동을 줄인 동선입니다.",
    "combat": "三자 열린 누각",
    "combatBody": "진행 중 새로운 전투면을 통과하며 이미 정리한 면을 왕복하지 않습니다.",
    "clearRoute": [
      "lower",
      "right-21",
      "right-18",
      "middle",
      "left-12",
      "left-9",
      "upper"
    ]
  }
];
  function mirrorRoom(data) {
    const next=JSON.parse(JSON.stringify(data));
    next.variant=data.variant==='left'?'right':'left';
    next.terrain.forEach(t=>t.x=next.width-t.x-t.w);
    next.monsters.forEach(m=>m.x=next.width-m.x);
    next.ports.forEach(p=>p.side=p.side==='left'?'right':'left');
    return next;
  }

  function migrateHills(entry,saved) {
    if(saved.terrainRevision===entry.initial.terrainRevision)return saved;
    const next=JSON.parse(JSON.stringify(entry.initial));
    for(const m of next.monsters){const old=saved.monsters?.find(x=>x.id===m.id);if(old&&species.some(s=>s.id===old.species))m.species=old.species;}
    return next;
  }

  function groundRoute(data) {
    if(data.terrainRevision === 'compact-v1') return data.ports.every(p=>data.terrain.some(t=>
      t.y===p.y && (p.side==='left'?t.x===0:t.x+t.w===data.width)));
    return false;
  }

  function forwardClearStatus(entry, data) {
    const byId = new Map(data.terrain.map(t => [t.id, t]));
    if (data.terrainRevision === 'compact-v1') {
      const route=(data.clearRoute||entry.clearRoute).map(id=>byId.get(id));
      if(route.some(t=>!t)) return {ok:false,reason:'전투 경로의 지형이 빠짐'};
      for(let i=1;i<route.length;i++) {
        const a=route[i-1],b=route[i],gap=Math.max(0,b.x-a.x-a.w,a.x-b.x-b.w);
        if(gap>3 || Math.abs(a.y-b.y)>3) return {ok:false,reason:'층 사이 이동 발판이 너무 멂'};
      }
      if(data.monsters.some(m=>!route.some(t=>m.x>=t.x+.4&&m.x<=t.x+t.w-.4&&Math.abs(m.y-t.y)<.26)))
        return {ok:false,reason:'전투 동선 밖에 몬스터가 있음'};
      return {ok:true,reason:'새 층으로 진행하며 모든 몬스터를 만나는 배치'};
    }
    const exit = data.terrain.filter(t => t.kind === 'ground').sort((a, b) => a.x - b.x).at(-1);
    const surfaces = [...entry.clearRoute.map(id => byId.get(id)), exit];
    if (surfaces.some(t => !t)) return { ok: false, reason: '추천 동선의 발판이 빠짐' };
    for (let i = 1; i < surfaces.length; i++) {
      const before = surfaces[i - 1], after = surfaces[i];
      if (after.x > before.x + before.w + 4 || after.x + after.w <= before.x ||
          before.y - after.y > 3 || after.y - before.y > 4)
        return { ok: false, reason: '추천 동선의 발판 간 이동이 어려움' };
    }
    const monsters = [...data.monsters].sort((a, b) => a.x - b.x);
    let stage = 0, previousX = -Infinity;
    for (const monster of monsters) {
      if (monster.x <= previousX) return { ok: false, reason: '같은 세로선에 몬스터가 겹침' };
      const next = surfaces.findIndex((t, index) => index >= stage &&
        monster.x >= t.x + .4 && monster.x <= t.x + t.w - .4 && Math.abs(monster.y - t.y) < .26);
      if (next < 0) return { ok: false, reason: '뒤돌아가야 만나는 몬스터가 있음' };
      stage = next;
      previousX = monster.x;
    }
    return { ok: true, reason: '왼쪽에서 오른쪽으로 모든 몬스터를 만나는 배치' };
  }

  const $ = id => document.getElementById(id);
  const canvas = $('room');
  const ctx = canvas.getContext('2d');
  const scroll = $('canvasScroll');
  const images = {};
  const assetPaths = [...species.map(item => item.art), 'MeadowDuskV3/Art/forest_depth.png'];
  for (const path of assetPaths) {
    const image = new Image();
    image.onload = draw;
    image.src = ART + path;
    images[path] = image;
  }
  const roomStates = new Map();
  let activeRoom = rooms[0];
  let draft = loadDraft(activeRoom);
  let selected = null;
  let tool = 'select';
  let terrainLocked = true;
  let grid = false;
  let zoom = 1;
  let drag = null;
  let serial = 100;
  let undo = [];
  let redo = [];

  function clone(value) { return JSON.parse(JSON.stringify(value)); }
  function validDraft(value, id = activeRoom.initial.id) {
    return value && value.schema === initial.schema && value.id === id && value.width === WIDTH && value.height === rooms.find(r=>r.initial.id===id)?.initial.height &&
      Array.isArray(value.terrain) && Array.isArray(value.monsters) && value.terrain.length < 200 && value.monsters.length < 200 &&
      value.terrain.every(t => typeof t.id === 'string' && ['ground', 'earth', 'wood'].includes(t.kind) &&
        [t.x, t.y, t.w, t.h].every(Number.isFinite) && t.w >= 1 && t.h >= 0.5 && t.x >= 0 && t.y >= 0 && t.x + t.w <= WIDTH && t.y + t.h <= value.height) &&
      value.monsters.every(m => typeof m.id === 'string' && species.some(s => s.id === m.species) &&
        Number.isFinite(m.x) && Number.isFinite(m.y) && m.x >= 0 && m.x <= WIDTH && m.y >= 0 && m.y <= value.height);
  }
  function loadDraft(room) {
    try {
      const saved = JSON.parse(localStorage.getItem(room.storage));
      if (validDraft(saved, room.initial.id)) {
        if (saved.terrainRevision !== room.initial.terrainRevision) {
          if (!localStorage.getItem(room.storage + '-before-compact-v1'))
            localStorage.setItem(room.storage + '-before-compact-v1', JSON.stringify(saved));
          return migrateHills(room, saved);
        }
        return saved;
      }
    } catch (_) { /* Storage may be unavailable for file URLs. */ }
    return clone(room.initial);
  }
  function persist() {
    try { localStorage.setItem(activeRoom.storage, JSON.stringify(draft)); $('saveState').textContent = '자동 보관됨'; }
    catch (_) { $('saveState').textContent = 'JSON 저장 필요'; }
  }
  function remember(before) {
    if (before === JSON.stringify(draft)) return;
    undo.push(before); if (undo.length > 60) undo.shift();
    redo.length = 0; persist(); refresh();
  }
  function historyStep(from, to) {
    if (!from.length) return;
    const next = JSON.parse(from[from.length - 1]);
    const mirrored = draft.mirrorable && next.variant !== draft.variant &&
      JSON.stringify(next.terrain) === JSON.stringify(mirrorRoom(draft).terrain);
    if (terrainLocked && !mirrored && JSON.stringify(next.terrain) !== JSON.stringify(draft.terrain)) return;
    to.push(JSON.stringify(draft));
    draft = JSON.parse(from.pop());
    selected = null; persist(); refresh();
  }
  function showRoom() {
    canvas.width=WIDTH*TILE; canvas.height=draft.height*TILE;
    $('roomDimensions').textContent=`${WIDTH} × ${draft.height}`;
    $('variantPicker').hidden=!draft.mirrorable; $('variantPicker').value=draft.variant||'right';
    $('fieldY').max=draft.height; $('fieldH').max=draft.height; resizeCanvas();
    $('headerRoom').textContent = `${activeRoom.initial.name} · 전투방 ${String(activeRoom.number).padStart(2, '0')}`;
    $('roomNumber').textContent = `ROOM ${String(activeRoom.number).padStart(2, '0')} / COMBAT`;
    $('roomTitle').textContent = activeRoom.initial.name;
    $('roomDescription').textContent = activeRoom.description;
    $('noteRouteTitle').textContent = activeRoom.route;
    $('noteRouteBody').textContent = activeRoom.routeBody;
    $('noteCombatTitle').textContent = activeRoom.combat;
    $('noteCombatBody').textContent = activeRoom.combatBody;
    document.querySelectorAll('#roomTabs button').forEach(button => {
      const current = button.dataset.room === activeRoom.initial.id;
      button.classList.toggle('active', current);
      button.setAttribute('aria-current', current ? 'true' : 'false');
    });
  }
  function switchRoom(room) {
    if (room === activeRoom) return;
    if (!terrainLocked) setTerrainLock(true);
    roomStates.set(activeRoom.initial.id, { draft, undo, redo });
    activeRoom = room;
    const state = roomStates.get(room.initial.id);
    draft = state ? state.draft : loadDraft(room);
    undo = state ? state.undo : [];
    redo = state ? state.redo : [];
    selected = null; drag = null;
    let stored = false;
    try { stored = !!localStorage.getItem(room.storage); } catch (_) { /* JSON export remains available. */ }
    $('saveState').textContent = state || stored ? '자동 보관됨' : '초안';
    showRoom(); refresh();
  }
  function setupRoomTabs() {
    const tabs = $('roomTabs');
    for (const room of rooms) {
      const button = document.createElement('button');
      button.type = 'button'; button.dataset.room = room.initial.id;
      const index = document.createElement('span'); index.className = 'room-index';
      index.textContent = `ROOM ${String(room.number).padStart(2, '0')}`;
      button.append(index, document.createTextNode(room.initial.name));
      button.onclick = () => switchRoom(room);
      tabs.append(button);
    }
    showRoom();
  }
  const snap = value => Math.round(value * 2) / 2;
  const clamp = (value, min, max) => Math.max(min, Math.min(max, value));
  const item = () => selected && (selected.group === 'terrain' ? draft.terrain : draft.monsters).find(x => x.id === selected.id);
  const kindName = kind => ({ ground: '지면', earth: '흙 언덕', wood: '나무 발판' })[kind] || kind;

  function setupOptions() {
    for (const target of [$('speciesPicker'), $('fieldSpecies')]) {
      for (const entry of species) target.add(new Option(entry.name, entry.id));
    }
    $('speciesPicker').value = '01_field_mouse';
  }
  function resizeCanvas() {
    const fitted=Math.min(scroll.clientWidth-2, Math.min(innerHeight*.7,790)*WIDTH/draft.height);
    canvas.style.width = `${fitted*zoom}px`;
    canvas.style.height = `${fitted*zoom*draft.height/WIDTH}px`;
    $('zoomValue').textContent = zoom === 1 ? '맞춤' : `${Math.round(zoom * 100)}%`;
  }
  function drawCover(image, x, y, width, height) {
    if (!image || !image.complete || !image.naturalWidth) return false;
    const ratio = Math.max(width / image.naturalWidth, height / image.naturalHeight);
    const sw = width / ratio, sh = height / ratio;
    ctx.drawImage(image, (image.naturalWidth - sw) / 2, (image.naturalHeight - sh) / 2, sw, sh, x, y, width, height);
    return true;
  }
  function drawBackground() {
    ctx.fillStyle = '#294c53'; ctx.fillRect(0, 0, canvas.width, canvas.height);
    drawCover(images['MeadowDuskV3/Art/forest_depth.png'], 0, 0, canvas.width, canvas.height);
    ctx.fillStyle = 'rgba(14,31,31,.23)'; ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = 'rgba(210,237,161,.10)'; ctx.fillRect(0, 420, canvas.width, 220);
  }
  function drawTerrain(t) {
    const x = t.x * TILE, y = t.y * TILE, w = t.w * TILE, h = t.h * TILE;
    if (t.kind === 'wood') {
      ctx.fillStyle = '#473022'; ctx.fillRect(x, y + 5, w, Math.max(8, h - 4));
      ctx.fillStyle = '#b78952'; ctx.fillRect(x, y, w, 8);
      ctx.fillStyle = '#e3b879'; ctx.fillRect(x + 3, y + 1, w - 6, 2);
      ctx.fillStyle = '#61462e';
      for (let i = 1; i < t.w; i++) ctx.fillRect(x + i * TILE - 2, y + 8, 3, Math.max(5, h - 10));
      return;
    }
    ctx.fillStyle = '#372d28'; ctx.fillRect(x, y, w, h);
    ctx.fillStyle = '#5e4c38';
    for (let row = 1; row < Math.ceil(t.h); row++) for (let col = 0; col < Math.ceil(t.w); col++) {
      const px = x + col * TILE + ((row * 13 + col * 7) % 17);
      if (px + 7 < x + w) ctx.fillRect(px, y + row * TILE + 7, 7, 2);
    }
    ctx.fillStyle = '#213d36'; ctx.fillRect(x, y, w, 5);
    ctx.fillStyle = '#66925a'; ctx.fillRect(x, y, w, 7);
    ctx.fillStyle = '#a7c079';
    for (let col = 0; col < t.w * 2; col++) {
      const px = x + col * 16 + (col % 3) * 2;
      if (px + 4 < x + w) ctx.fillRect(px, y - (col % 4 === 0 ? 3 : 0), 4, 3);
    }
    ctx.fillStyle = 'rgba(23,36,30,.42)'; ctx.fillRect(x, y + h - 4, w, 4);
  }
  function drawMonster(m, order) {
    const entry = species.find(s => s.id === m.species);
    const image = images[entry.art];
    const height = entry.width * TILE;
    const width = entry.width * TILE;
    const x = m.x * TILE - width / 2, y = m.y * TILE - height;
    ctx.fillStyle = 'rgba(6,22,19,.40)'; ctx.beginPath(); ctx.ellipse(m.x * TILE, m.y * TILE - 2, width * .36, 6, 0, 0, Math.PI * 2); ctx.fill();
    if (image && image.complete && image.naturalWidth) {
      if (entry.sheet) ctx.drawImage(image, 0, 0, 128, 128, x, y, width, height);
      else ctx.drawImage(image, x, y, width, height);
    } else { ctx.fillStyle = '#d9a46e'; ctx.fillRect(x + width * .3, y + height * .3, width * .4, height * .7); }
    ctx.fillStyle = '#1a302a'; ctx.beginPath();
    ctx.arc(m.x * TILE + width * .38, y + 12, 13, 0, Math.PI * 2); ctx.fill();
    ctx.strokeStyle = '#d8c68b'; ctx.lineWidth = 1.5; ctx.stroke();
    ctx.fillStyle = '#f4e5b8'; ctx.font = 'bold 13px Malgun Gothic, sans-serif';
    ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    ctx.fillText(String(order), m.x * TILE + width * .38, y + 12);
    ctx.textAlign = 'left'; ctx.textBaseline = 'alphabetic';
    if (selected && selected.group === 'monsters' && selected.id === m.id) {
      ctx.strokeStyle = '#f3df9b'; ctx.lineWidth = 2; ctx.strokeRect(x + 2, y + 2, width - 4, height - 3);
    }
  }
  function drawPorts() {
    for (const [i,port] of draft.ports.entries()) {
      const x=port.side==='left'?1:WIDTH-2, label=i?'출구':'입구', direction=port.side==='left'?1:-1;
      const px=x*TILE, py=(port.y-1.5)*TILE;
      ctx.fillStyle = 'rgba(12,36,32,.77)'; ctx.fillRect(px - 32, py - 41, 95, 30);
      ctx.fillStyle = '#e4e7b5'; ctx.font = 'bold 18px Malgun Gothic, sans-serif'; ctx.textAlign = 'center';
      ctx.fillText(label, px + 15, py - 20);
      ctx.fillStyle = '#d6e6a3';
      ctx.beginPath(); ctx.moveTo(px + 15 + direction * 20, py + 8); ctx.lineTo(px + 15 - direction * 5, py - 5);
      ctx.lineTo(px + 15 - direction * 5, py + 21); ctx.closePath(); ctx.fill();
    }
    ctx.textAlign = 'left';
  }
  function draw() {
    if (!ctx || !draft) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    drawBackground();
    for (const t of draft.terrain) drawTerrain(t);
    for (const overlap of terrainOverlaps()) {
      ctx.fillStyle = 'rgba(237,87,76,.55)';
      ctx.fillRect(overlap.x * TILE, overlap.y * TILE, overlap.w * TILE, overlap.h * TILE);
    }
    drawPorts();
    [...draft.monsters].sort((a, b) => draft.terrainRevision === 'compact-v1' ? 0 : a.x - b.x).forEach((monster, index) => drawMonster(monster, index + 1));
    if (selected && selected.group === 'terrain') {
      const t = item();
      if (t) {
        ctx.strokeStyle = '#f3df9b'; ctx.lineWidth = 3; ctx.setLineDash([9, 5]);
        ctx.strokeRect(t.x * TILE + 1, t.y * TILE + 1, t.w * TILE - 2, t.h * TILE - 2); ctx.setLineDash([]);
        ctx.fillStyle = '#f3df9b'; ctx.fillRect((t.x + t.w) * TILE - 11, (t.y + t.h) * TILE - 11, 11, 11);
      }
    }
    if (grid) {
      ctx.strokeStyle = 'rgba(220,240,220,.14)'; ctx.lineWidth = 1;
      for (let x = 0; x <= WIDTH; x++) { ctx.beginPath(); ctx.moveTo(x * TILE + .5, 0); ctx.lineTo(x * TILE + .5, canvas.height); ctx.stroke(); }
      for (let y = 0; y <= draft.height; y++) { ctx.beginPath(); ctx.moveTo(0, y * TILE + .5); ctx.lineTo(canvas.width, y * TILE + .5); ctx.stroke(); }
    }
  }
  function point(event) {
    const rect = canvas.getBoundingClientRect();
    return { x: (event.clientX - rect.left) * WIDTH / rect.width, y: (event.clientY - rect.top) * draft.height / rect.height };
  }
  function hit(p) {
    for (let i = draft.monsters.length - 1; i >= 0; i--) {
      const m = draft.monsters[i], s = species.find(v => v.id === m.species);
      if (Math.abs(p.x - m.x) <= Math.max(1.1, s.width * .45) && p.y >= m.y - s.width * .8 && p.y <= m.y + .3)
        return { group: 'monsters', id: m.id };
    }
    if (terrainLocked) return null;
    for (let i = draft.terrain.length - 1; i >= 0; i--) {
      const t = draft.terrain[i];
      if (p.x >= t.x && p.x <= t.x + t.w && p.y >= t.y && p.y <= t.y + Math.max(t.h, .7))
        return { group: 'terrain', id: t.id };
    }
    return null;
  }
  function select(value) { selected = value; refresh(); }
  function pointerDown(event) {
    if (event.button !== 0) return;
    if (terrainLocked && ['ground', 'earth', 'wood'].includes(tool)) return;
    event.preventDefault(); canvas.setPointerCapture(event.pointerId);
    const p = point(event), before = JSON.stringify(draft);
    if (tool === 'monster') {
      const monster = { id: `enemy-${Date.now()}-${++serial}`, species: $('speciesPicker').value,
        x: clamp(snap(p.x), 1, WIDTH - 1), y: clamp(snap(p.y), 1, draft.height - 1) };
      draft.monsters.push(monster); select({ group: 'monsters', id: monster.id });
      drag = { mode: 'move', start: p, origin: { ...monster }, before };
    } else if (['ground', 'earth', 'wood'].includes(tool)) {
      const terrain = { id: `terrain-${Date.now()}-${++serial}`, kind: tool,
        x: clamp(snap(p.x), 0, WIDTH - 5), y: clamp(snap(p.y), 0, draft.height - 2),
        w: tool === 'wood' ? 5 : 8, h: tool === 'wood' ? .5 : 2 };
      draft.terrain.push(terrain); select({ group: 'terrain', id: terrain.id });
      drag = { mode: 'move', start: p, origin: { ...terrain }, before };
    } else {
      select(hit(p));
      const target = item();
      if (!target) { drag = null; return; }
      const resize = selected.group === 'terrain' && Math.abs(p.x - target.x - target.w) < .7 && Math.abs(p.y - target.y - target.h) < .7;
      drag = { mode: resize ? 'resize' : 'move', start: p, origin: { ...target }, before };
    }
  }
  function pointerMove(event) {
    if (!drag || !selected) return;
    if (terrainLocked && selected.group === 'terrain') return;
    const target = item(); if (!target) return;
    const p = point(event), dx = snap(p.x - drag.start.x), dy = snap(p.y - drag.start.y);
    if (drag.mode === 'resize') {
      target.w = clamp(drag.origin.w + dx, 1, WIDTH - target.x);
      target.h = clamp(drag.origin.h + dy, target.kind === 'wood' ? .5 : 1, draft.height - target.y);
    } else {
      target.x = clamp(snap(drag.origin.x + dx), selected.group === 'monsters' ? 1 : 0,
        selected.group === 'monsters' ? WIDTH - 1 : WIDTH - target.w);
      target.y = clamp(snap(drag.origin.y + dy), selected.group === 'monsters' ? 1 : 0,
        selected.group === 'monsters' ? draft.height - 1 : draft.height - target.h);
    }
    draw(); showInspector();
  }
  function pointerUp() { if (!drag) return; remember(drag.before); drag = null; }
  function setTool(value) {
    if (terrainLocked && ['ground', 'earth', 'wood'].includes(value)) return;
    tool = value;
    document.querySelectorAll('[data-tool]').forEach(button => button.classList.toggle('active', button.dataset.tool === tool));
    $('speciesPicker').hidden = tool !== 'monster';
    canvas.style.cursor = tool === 'select' ? 'default' : 'crosshair';
  }
  function setTerrainLock(locked) {
    terrainLocked = locked;
    $('terrainLock').checked = locked;
    if (locked) {
      selected = null;
      drag = null;
      if (['ground', 'earth', 'wood'].includes(tool)) setTool('select');
      undo = [];
      redo = [];
    }
    document.querySelectorAll('[data-tool="ground"], [data-tool="earth"], [data-tool="wood"]')
      .forEach(button => button.disabled = locked);
    $('emptyInspector').textContent = locked
      ? '몬스터를 선택해 배치를 수정하세요.' : '방 안의 지형이나 몬스터를 선택하세요.';
    $('resetButton').textContent = locked ? '몬스터 처음 배치로 되돌리기' : '방 전체 처음 배치로 되돌리기';
    refresh();
  }
  function showInspector() {
    const target = item();
    $('emptyInspector').hidden = !!target; $('inspector').hidden = !target;
    $('selectionKind').textContent = !target ? '선택 없음' : selected.group === 'monsters' ? '몬스터' : kindName(target.kind);
    $('deleteButton').disabled = !target;
    if (!target) return;
    $('fieldX').value = target.x; $('fieldY').value = target.y;
    const geometry = selected.group === 'terrain';
    $('fieldW').parentElement.hidden = !geometry; $('fieldH').parentElement.hidden = !geometry;
    $('speciesField').hidden = geometry;
    if (geometry) { $('fieldW').value = target.w; $('fieldH').value = target.h; }
    else $('fieldSpecies').value = target.species;
  }
  function supported(monster) {
    return draft.terrain.some(t => monster.x >= t.x + .4 && monster.x <= t.x + t.w - .4 && Math.abs(monster.y - t.y) < .26);
  }
  function terrainOverlaps() {
    const overlaps = [];
    for (let i = 0; i < draft.terrain.length; i++) for (let j = i + 1; j < draft.terrain.length; j++) {
      const a = draft.terrain[i], b = draft.terrain[j];
      const x = Math.max(a.x, b.x), y = Math.max(a.y, b.y);
      const w = Math.min(a.x + a.w, b.x + b.w) - x;
      const h = Math.min(a.y + a.h, b.y + b.h) - y;
      if (w > 0 && h > 0) overlaps.push({ x, y, w, h });
    }
    return overlaps;
  }
  function tightPlatformGaps() {
    let count = 0;
    for (const platform of draft.terrain.filter(t => t.kind === 'wood'))
      for (const ground of draft.terrain.filter(t => t.kind !== 'wood')) {
        const dx = Math.max(0, platform.x - ground.x - ground.w, ground.x - platform.x - platform.w);
        const dy = Math.max(0, platform.y - ground.y - ground.h, ground.y - platform.y - platform.h);
        if (Math.hypot(dx, dy) < 1 - 1e-6) count++;
      }
    return count;
  }
  function review() {
    const floor = groundRoute(draft);
    const unsupported = draft.monsters.filter(m => !supported(m)).length;
    const nearPort = draft.monsters.filter(m => m.x < 2 || m.x > draft.width-2).length;
    const overlaps = terrainOverlaps().length;
    const tightGaps = tightPlatformGaps();
    const forward = forwardClearStatus(activeRoom, draft);
    const checks = [
      [draft.monsters.length >= 10, `전투방 몬스터 ${draft.monsters.length}마리 / 최소 10마리`],
      [floor, '입구와 출구에 실제 이동 지형이 연결됨'],
      [overlaps === 0, overlaps ? `서로 겹치는 지형 ${overlaps}곳` : '땅과 발판이 서로 겹치지 않음'],
      [tightGaps === 0, tightGaps ? `땅과 너무 가까운 나무 발판 ${tightGaps}곳` : '나무 발판과 땅 사이 최소 1칸 간격'],
      [unsupported === 0, unsupported ? `발판에 닿지 않는 몬스터 ${unsupported}마리` : '모든 몬스터가 발판 위에 배치됨'],
      [nearPort === 0, nearPort ? `출입 지점과 가까운 몬스터 ${nearPort}마리` : '출입 지점 주변이 비어 있음'],
      [forward.ok, forward.reason]
    ];
    $('checks').replaceChildren(...checks.map(([pass, label]) => {
      const li = document.createElement('li'); li.textContent = label; if (!pass) li.className = 'warn'; return li;
    }));
    $('reviewBadge').textContent = checks.every(([pass]) => pass) ? '배치 검사 통과' : '확인 필요';
    $('encounterState').textContent = `${draft.monsters.length} / 10`;
  }
  function roster() {
    const container = $('roster'); container.replaceChildren();
    [...draft.monsters].sort((a, b) => draft.terrainRevision === 'compact-v1' ? 0 : a.x - b.x).forEach((monster, index) => {
      const entry = species.find(s => s.id === monster.species);
      const button = document.createElement('button'); button.type = 'button';
      button.classList.toggle('selected', !!selected && selected.group === 'monsters' && selected.id === monster.id);
      const image = document.createElement('img'); image.src = ART + entry.art; image.alt = '';
      if (entry.sheet) { image.style.objectFit = 'cover'; image.style.objectPosition = 'left'; }
      const name = document.createElement('span'); name.className = 'species-name'; name.textContent = `${String(index + 1).padStart(2, '0')}  ${entry.name}`;
      const coord = document.createElement('span'); coord.className = 'coord'; coord.textContent = `${monster.x}, ${monster.y}`;
      button.append(image, name, coord);
      button.onclick = () => select({ group: 'monsters', id: monster.id });
      container.append(button);
    });
  }
  function refresh() {
    showRoom(); draw(); showInspector(); roster(); review();
    $('platformCount').textContent = draft.terrain.length;
    $('monsterCount').textContent = draft.monsters.length;
    $('undoButton').disabled = !undo.length; $('redoButton').disabled = !redo.length;
  }
  function deleteSelected() {
    if (!selected) return;
    if (terrainLocked && selected.group === 'terrain') return;
    const before = JSON.stringify(draft);
    const list = selected.group === 'terrain' ? draft.terrain : draft.monsters;
    const index = list.findIndex(entry => entry.id === selected.id);
    if (index >= 0) list.splice(index, 1);
    selected = null; remember(before);
  }
  function updateField(key, value) {
    const target = item(); if (!target) return;
    if (terrainLocked && selected.group === 'terrain') return;
    const before = JSON.stringify(draft);
    if (key === 'species') target.species = value;
    else {
      let number = snap(Number(value)); if (!Number.isFinite(number)) return;
      if (key === 'x') number = clamp(number, selected.group === 'monsters' ? 1 : 0,
        selected.group === 'monsters' ? WIDTH - 1 : WIDTH - target.w);
      if (key === 'y') number = clamp(number, selected.group === 'monsters' ? 1 : 0,
        selected.group === 'monsters' ? draft.height - 1 : draft.height - target.h);
      if (key === 'w') number = clamp(number, 1, WIDTH - target.x);
      if (key === 'h') number = clamp(number, target.kind === 'wood' ? .5 : 1, draft.height - target.y);
      target[key] = number;
    }
    remember(before);
  }
  function exportDraft() {
    const blob = new Blob([JSON.stringify(draft, null, 2) + '\n'], { type: 'application/json' });
    const url = URL.createObjectURL(blob); const link = document.createElement('a');
    link.href = url; link.download = `${draft.id}.json`; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    $('saveState').textContent = 'JSON 저장됨';
  }
  function applyRecommended() {
    const before = JSON.stringify(draft);
    if (!terrainLocked) {
      const byId = new Set(draft.terrain.map(t => t.id));
      for (const terrain of activeRoom.initial.terrain.filter(t => activeRoom.clearRoute.includes(t.id)))
        if (!byId.has(terrain.id)) draft.terrain.push(clone(terrain));
    }
    draft.monsters = clone((draft.variant==='left'?mirrorRoom(activeRoom.initial):activeRoom.initial).monsters);
    selected = null;
    remember(before);
  }

  setupRoomTabs(); setupOptions(); setTerrainLock(true); setTool('select'); resizeCanvas(); refresh();
  window.addEventListener('resize', resizeCanvas);
  canvas.addEventListener('pointerdown', pointerDown);
  canvas.addEventListener('pointermove', pointerMove);
  canvas.addEventListener('pointerup', pointerUp);
  canvas.addEventListener('pointercancel', pointerUp);
  document.querySelectorAll('[data-tool]').forEach(button => button.onclick = () => setTool(button.dataset.tool));
  $('variantPicker').onchange = () => { if(!draft.mirrorable) return; const before=JSON.stringify(draft); draft=mirrorRoom(draft); selected=null; remember(before); showRoom(); };
  $('terrainLock').onchange = event => setTerrainLock(event.target.checked);
  $('gridToggle').onchange = event => { grid = event.target.checked; draw(); };
  $('undoButton').onclick = () => historyStep(undo, redo);
  $('redoButton').onclick = () => historyStep(redo, undo);
  $('deleteButton').onclick = deleteSelected;
  $('inspectorDelete').onclick = deleteSelected;
  for (const [id, key] of [['fieldX', 'x'], ['fieldY', 'y'], ['fieldW', 'w'], ['fieldH', 'h'], ['fieldSpecies', 'species']])
    $(id).onchange = event => updateField(key, event.target.value);
  $('zoomIn').onclick = () => { zoom = clamp(zoom + .5, 1, 3); resizeCanvas(); };
  $('zoomOut').onclick = () => { zoom = clamp(zoom - .5, 1, 3); resizeCanvas(); };
  $('zoomFit').onclick = () => { zoom = 1; resizeCanvas(); };
  $('exportButton').onclick = exportDraft;
  $('importButton').onclick = () => $('importFile').click();
  $('importFile').onchange = async event => {
    const file = event.target.files[0]; if (!file) return;
    try {
      const next = JSON.parse(await file.text());
      if (!validDraft(next)) throw new Error('현재 선택한 방의 JSON 형식이 아닙니다.');
      if (terrainLocked && JSON.stringify(next.terrain) !== JSON.stringify(draft.terrain))
        throw new Error('지형이 다른 JSON입니다. 불러오려면 지형 잠금을 해제하세요.');
      const before = JSON.stringify(draft); draft = next; selected = null; remember(before);
      $('saveState').textContent = 'JSON 불러옴';
    } catch (error) { $('saveState').textContent = error.message; }
    event.target.value = '';
  };
  $('resetButton').onclick = () => {
    const before = JSON.stringify(draft);
    if (terrainLocked) draft.monsters = clone((draft.variant==='left'?mirrorRoom(activeRoom.initial):activeRoom.initial).monsters);
    else draft = clone(draft.variant==='left'?mirrorRoom(activeRoom.initial):activeRoom.initial);
    selected = null; remember(before);
  };
  $('recommendButton').onclick = applyRecommended;
  document.addEventListener('keydown', event => {
    const editing = ['INPUT', 'SELECT', 'TEXTAREA'].includes(document.activeElement.tagName);
    if (event.ctrlKey && event.key.toLowerCase() === 's') { event.preventDefault(); exportDraft(); }
    if (event.ctrlKey && event.key.toLowerCase() === 'z') { event.preventDefault(); historyStep(undo, redo); }
    if (event.ctrlKey && event.key.toLowerCase() === 'y') { event.preventDefault(); historyStep(redo, undo); }
    if (!editing && (event.key === 'Delete' || event.key === 'Backspace')) { event.preventDefault(); deleteSelected(); }
  });
})();
