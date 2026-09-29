---
doc_kind: archive
source_system: Notion
notion_id: '71606c87-81b0-823a-b930-8168fefce14c'
notion_title: '디자인 참고용'
source_url: 'https://app.notion.com/p/71606c8781b0823ab9308168fefce14c?pvs=204'
snapshot_date: '2026-08-28'
---

# 디자인 참고용

> 이 파일은 Notion 원문 보관본이다. 현재 정책은 도메인 SSOT와 결정 로그를 따른다.

## 원문 전사

Here is the result of "fetch" for the Page with URL https://app.notion.com/p/71606c8781b0823ab9308168fefce14c as of 2026-08-28T00:23:18.169Z:
<page url="https://app.notion.com/p/71606c8781b0823ab9308168fefce14c" icon="icons/color-palette_yellow">
<ancestor-path>
<parent-data-source url="collection://4f806c87-81b0-8303-ad48-0755c71f61f1" name="프로젝트 자료실"/>
<ancestor-2-database url="https://app.notion.com/p/fe806c8781b08319832901d6a6520179" title=""/>
<ancestor-3-page url="https://app.notion.com/p/1dd06c8781b083669ab681e49cefffdd" title="자료실"/>
<ancestor-4-page url="https://app.notion.com/p/88306c8781b0821aa3cf016226b4de57" title=""/>
<ancestor-5-page url="https://app.notion.com/p/a7406c8781b082f5b3c381993d7952bf" title=""/>
<ancestor-6-page url="https://app.notion.com/p/df006c8781b0827a8bea01780f925f3f" title="특화 프로젝트"/>
</ancestor-path>
<properties>
{"url":"https://app.notion.com/p/71606c8781b0823ab9308168fefce14c","링크":"","생성일":"2026-08-28T00:23:18.235Z","이름":"디자인 참고용"}
</properties>
<content>
## **1. 자료 설명**
---
<file src="file://%7B%22source%22%3A%22attachment%3A67fa76db-f505-48dd-aaf1-aad471ef0c0a%3ADESIGN-REFERENCE.md%22%2C%22permissionRecord%22%3A%7B%22table%22%3A%22block%22%2C%22id%22%3A%223d906c87-81b0-8244-963d-01446daee586%22%2C%22spaceId%22%3A%22ccadcd0a-f169-412b-889a-adce96602b0b%22%7D%7D"></file>
<details>
<summary>가이드</summary>
	**파일 이름 권장 규칙**
	```plain text
{stage-number}-{stage-name}-far-v01.png
{stage-number}-{stage-name}-mid-v01.png
{stage-number}-{stage-name}-ground-v01.png
	```
	아래 프롬프트와 레퍼런스들 몇개 추가해서 (배경이면 art/bg, 몹이면 art/mob) 원하는 느낌으로 새로 생성해 달라고 한 다음 뽑아보기
	### 전투맵 AI 프롬프트 가이드 {toggle="true"}
		## 사용 방법
		아래 순서로 복사해서 사용한다.
		```plain text
공통 프롬프트
+ FAR / MID / GROUND 중 하나
+ 네거티브 프롬프트
		```
		장소를 바꿀 때는 `[장소 설명]`, `[배경 요소]`, `[오브젝트]`, `[바닥 재질]` 부분만 수정한다.
		---
		## 1. 공통 프롬프트
		```plain text
Use case: stylized-concept
Asset type: production-ready 2D pixel-art side-scrolling game background layer
Style: authentic 2D pixel art with no anti-aliasing, crisp large pixel clusters,
a limited color palette, consistent 1-2 pixel outlines. Flat 2D side view.
Canvas: 16:9 landscape, full bleed to all four edges. No frame, no border,
no vignette, no text, no logo, no watermark, no UI.

THIS IS A SLICE, NOT A PICTURE. The image is one horizontal section cut out of a
much longer environment that continues past both the left and the right edge. There is
no center of interest, no focal point, no symmetry. Objects are spread evenly
across the full width so that no part of the image looks more important than
another part.

EDGE RULE — this matters more than anything else. The image is built so that if
two copies of it were placed side by side, the right edge would meet the left
edge with no visible seam. To make that work, the leftmost 15% and the rightmost
15% of the image show the same single emptiest material in the scene — open sky,
bare wall, bare floor, empty haze, or dark glass reflection — and they show nothing else.

NOTHING TOUCHES THE LEFT EDGE AND NOTHING TOUCHES THE RIGHT EDGE. No wall corner,
building edge, pillar, shelf, rack, pipe, sign, prop, or object of any kind reaches
either side of the image or is cropped by it. The scene thins out into that empty
material well before it arrives at the sides. The two sides look the same as each other.

Ground line: a single perfectly horizontal line crosses the entire width of the
image. It never tilts, never steps, never curves. Its height is given in the
layer note below and it is different for each layer.

Camera: the eye level of a two-centimetre creature standing on that ground line,
looking straight ahead. Everything made by humans is enormous. Straight-on
orthographic side view with no perspective tilt and no camera rotation.

Light: all light comes from the upper left. Every shadow falls to the lower right.

Nothing living appears. No people, no animals, no faces, no monsters, no food
characters, no chopsticks, no weapons, no cutlery, no gameplay characters.
		```
		---
		## 2. FAR
		불투명한 먼 배경. 가장 흐리고 대비가 낮은 레이어.
		```plain text
LAYER: FAR BACKGROUND, fully opaque.

STAGE: [장소 설명, 시간대, 날씨, 분위기].

Show distant environmental forms such as [배경 요소]. Use a low-saturation palette that matches the stage. Everything is hazy, low-contrast, and less detailed than the other layers.

Use broad horizontal shapes. Keep the bottom 20% visually quiet so the gameplay layers can overlap it. Do not place any large foreground object or gameplay obstacle.

The leftmost and rightmost 15% contain only the same single empty distant material.
No architecture, pillar, shelf, prop, silhouette, or unique detail enters either edge zone.
		```
		---
		## 3. MID
		마젠타 배경 위에 중간 크기 오브젝트만 있는 레이어.
		```plain text
LAYER: MIDGROUND CUTOUT.

The entire background is flat pure magenta (#FF00FF). The magenta is a knockout color, not sky. Do not paint scenery, ground, haze, gradients, or detached shadows into the magenta.

Place [중간 오브젝트 5-8개] along one perfectly horizontal baseline at Y=74% measured from the top of the canvas.

The objects belong to [장소] and appear enormous from the eye level of a two-centimetre-tall creature. Object heights vary between 25% and 60% of the canvas. Leave clear magenta gaps between object groups.

Keep every object inside the central 70% of the canvas. The leftmost and rightmost 15% remain completely empty pure magenta.

Shadows stay attached directly to each object's base. Every pixel that is not an object or its attached shadow remains exactly #FF00FF.
		```
		---
		## 4. GROUND
		완전한 측면에서 본 **맨 바닥 단면** 레이어. 소품은 이 이미지에 넣지 않고 필요하면 별도 소품 시트로 생성한다.
		```plain text
LAYER: BARE GROUND CROSS-SECTION.

Pixel art side view of a strip of [바닥 재질], seen by an eye lying right down
on the ground. The camera is exactly level with the surface. The surface reads
as ONE SINGLE STRAIGHT HORIZONTAL PIXEL LINE running across the entire width.
NONE OF THE TOP FACE IS VISIBLE. There is no floor plane receding toward the
viewer, no perspective grid and no diagonal tile lines.

Below that line, the ground is cut open like a vertical cross-section. Show the
horizontal material layers as [상단 마감층], then [중간층], then [하부층]. All
layer boundaries run horizontally. The cross-section has no perspective and no
depth foreshortening.

The ground cross-section fills exactly the bottom third of the image. Everything
above it is flat pure magenta #FF00FF with nothing painted on it.

The strip is completely bare. Nothing stands on the surface line. No props,
utensils, tongs, paper, crumbs, flour, debris, drains, furniture, shadows or
objects appear anywhere in this layer.

The ground has exactly the same height, material order, colour and thickness at
the left edge and the right edge. The top line never tilts, steps or curves.
		```
		---
		## 5. 네거티브 프롬프트
		```plain text
anti-aliasing, blurry pixels, smooth gradients, painterly, watercolor,
photorealistic, 3D render, vector art, isometric view, top-down view,
perspective tilt, camera rotation, central focal point, symmetry,
dramatic close-up, characters, people, animals, faces, monsters,
food characters, chopsticks, weapons, cutlery, readable text, logo,
watermark, UI, frame, border, vignette, objects cropped by the edges,
uneven ground line, sloped ground, curved ground, floating shadows
		```
	### 몹 AI 프롬프트 가이드 {toggle="true"}
		```javascript
- 모델 시트는 2048×1536, 4열×3행, 12포즈다.
- 전투 시트는 1536×1024, 6열×4행, 24프레임이다.
- 전투 시트의 네 행은 걷기·공격·피격·사망이다.
- 전투 시트에는 승인된 모델 시트를 반드시 첨부한다.
- 음식의 실루엣 고정 특징과 고명 개수를 모든 프레임에서 유지한다.
		```
		<empty-block/>
		**제작 순서**
		1. 몹 디자인을 정한다.
		2. 4열×3행, 12포즈 모델 시트를 만든다.
		3. 승인된 모델 시트를 첨부해 6열×4행, 24프레임 전투 시트를 만든다.
		4. 전투 시트를 행별로 잘라 걷기·공격·피격·사망 GIF로 만든다.
		첫 번째 몹은 hero.png를 화풍 참고용으로 사용하고, 그다음 몹부터는 승인된 몹 시트를 스타일 참고 이미지로 사용한다. 참고 이미지 속 캐릭터는 결과물에 포함하지 않는다.
		---
		## 1. 몹별로 정할 내용
		```plain text
몹 이름:
음식 설명:
전체 실루엣:
실루엣 고정 특징:
주요 색:
표면 재질:
고명 또는 포인트 재료와 정확한 개수:
얼굴 위치:
팔다리 형태:
이동 방식:
기본 공격:
고유 행동 또는 방어:
다른 음식으로 오인하지 않게 할 요소:
		```
		실루엣 고정 특징은 모든 프레임에 유지할 특징 하나를 말한다. 예: 김치의 겹친 배추 잎, 버섯의 넓은 갓, 멸치의 갈라진 꼬리, 고추의 별 모양 꼭지.
		---
		## 2. 12포즈 모델 시트 공통 프롬프트
		아래 프롬프트의 대괄호 부분만 몹에 맞게 바꾼다.
		```plain text
첨부한 [hero.png 또는 승인된 몹 시트]를 스타일 참고 이미지로 사용하여 한국 음식 파밍 게임의 [몹 이름] 잡몹 픽셀 아트 캐릭터 모델 시트를 제작한다. 첨부된 캐릭터 자체는 결과물에 절대 포함하지 않는다.

캐릭터 디자인:
[어떤 음식인지 한 문장]

전체 실루엣은 [실루엣 설명]이다. [실루엣 고정 특징]은 12칸 전부에서 같은 크기와 방향으로 유지한다.

색과 재질:
[주요 색, 음식 표면, 단면, 양념과 조리 상태]
[고명 또는 포인트 재료]는 정확히 [개수]개이며 12칸 전부에서 같은 위치를 유지한다.

성격과 인상:
이 캐릭터는 귀여운 펫이 아니라 플레이어를 공격하는 작고 성질 사나운 잡몹이다. 보스처럼 거대하거나 흉악하게 만들지 않는다. 기본 자세는 몸을 앞으로 살짝 기울인 위협 자세다.

얼굴:
얼굴은 [얼굴 위치]에 고정한다. 어떤 자세에서도 다른 부위로 옮기지 않는다.
눈은 김치 잡몹과 같은 비율의 매우 작은 검은 점이다. 각 눈은 고명 한 조각보다 훨씬 작고 얼굴 전체는 몸 중앙의 작은 영역만 차지한다. 흰자, 홍채, 동공, 하이라이트와 눈썹은 없다. 큰 타원 눈, 굵은 화난 눈과 과장된 표정을 사용하지 않는다. 입은 눈보다도 작고 짧게 다문 일자이며 한쪽 끝에 작은 뾰족니 하나가 있다. 분홍 볼터치와 U자 미소는 넣지 않는다.

팔다리:
[팔다리 설명]

2048×1536 가로형 한 페이지에 정확히 동일한 캐릭터 12개만 정확한 4열 3행으로 배치한다. 각 칸의 크기는 동일하고 모든 캐릭터는 칸 중앙에 배치한다.

모든 칸에서 몸 크기와 비율, 실루엣 고정 특징, 색상 팔레트, 재료 위치, 얼굴 위치, 눈과 입 크기, 팔다리 길이를 동일하게 유지한다.

방향 일관성 잠금:
첫 번째 행의 네 캐릭터는 동일한 기준선, 동일한 머리 높이와 동일한 전체 크기를 사용한다. 세 번째 칸 왼쪽 옆모습과 네 번째 칸 오른쪽 옆모습의 기본 몸체는 서로 따로 다시 디자인하지 않는다. 하나의 동일한 측면 몸체 실루엣을 정확히 수평 반전한 한 쌍으로 제작한다. 두 측면의 바운딩 박스, 몸 너비와 높이, 음식 두께, 기본 곡률과 팔다리 위치가 같은 시각적 비율이어야 한다. 어느 한쪽도 더 얇거나 두껍게 만들지 않는다. 단, 한입 자국이나 한쪽 면에만 붙은 고명처럼 비대칭인 고정 특징은 실제 위치에 따라 한쪽 측면에서 숨길 수 있다. 몹별 프롬프트에 어느 칸에서 보이고 숨는지 정확히 지정하며, 반대편에 복제하지 않는다.

첫 번째 행 — 방향:
1. 기본 정면. 앞면의 재료 특징과 얼굴이 모두 보인다.
2. 정확한 180도 뒷모습. 눈, 입과 얼굴은 절대 보이지 않는다.
3. 정확한 왼쪽 옆모습. 왼쪽을 향한 눈 하나와 입이 보인다.
4. 정확한 오른쪽 옆모습. 왼쪽 옆모습을 반전한 동일한 비율이며 오른쪽을 향한 눈 하나와 입이 보인다.

두 번째 행 — 기본 행동:
5. 화면 왼쪽을 향해 [대기 또는 공격 준비 핵심 자세].
6. 화면 오른쪽에서 왼쪽으로 [이동하는 자세]. 얼굴과 진행 방향은 왼쪽이다.
7. 화면 왼쪽을 향해 [기본 공격 핵심 자세]. 상대와 무기는 그리지 않는다.
8. 화면 왼쪽을 향한 [고유 행동 또는 방어 자세]. 별도 장비는 그리지 않는다.

세 번째 행 — 전투 반응:
9. 화면 왼쪽의 주인공을 발견한 [등장 또는 경계 자세].
10. 화면 왼쪽에서 공격을 맞아 몸이 찌그러지고 화면 오른쪽으로 밀리는 자세. 공격자는 그리지 않는다.
11. 왼쪽을 향한 채 제자리에서 비틀거리며 기절한 자세. 왼쪽 눈은 `>` 모양, 오른쪽 눈은 `<` 모양의 짧고 각진 검은 선으로 서로 안쪽을 향한다. 감은 웃는 눈, 위로 휜 곡선 눈과 미소를 사용하지 않는다. 입은 웃는 곡선이 아니라 작게 벌어진 짙은색 네모 또는 짧은 일자다. 머리 위에는 작은 별 픽셀 2~3개만 사용한다.
12. 왼쪽을 향한 채 체력을 잃고 바닥에 옆으로 완전히 쓰러진 자세.

시트 표현 방식:
따뜻한 연한 아이보리색 단색 배경과 얇은 연회색 칸 구분선만 사용한다. 캐릭터 아래에는 작고 동일한 타원형 픽셀 그림자를 사용한다. 각 캐릭터는 서로 겹치지 않고 자르기 쉽도록 충분한 여백을 둔다.

안티앨리어싱 없는 정통 2D 픽셀 아트, 선명하고 큰 픽셀 클러스터, 제한된 색상 팔레트, 일정한 1~2픽셀 외곽선, 명확한 게임 스프라이트 실루엣.
		```
		---
		## 3. 24프레임 전투 시트 공통 프롬프트
		승인된 12포즈 모델 시트를 첨부하고 아래 프롬프트를 사용한다.
		```plain text
Use case: game-asset-generation
Asset type: 2D pixel-art enemy combat animation sprite sheet

Using the attached approved [몹 이름] model sheet as the exact character reference, create one production-ready combat animation sprite sheet.

CHARACTER LOCK:
Keep the exact same body proportions, silhouette, colors, material texture, face position, limbs, toppings, and outline thickness in all 24 frames.

SILHOUETTE ANCHOR:
[모든 프레임에서 유지해야 하는 특징]

COLOR AND MATERIAL:
[주요 색과 음식 재질]
[고명과 개수] remain in the same position in every frame.

FACE:
The face stays on [얼굴 위치]. Eyes are extremely small solid-black dots matching the eye-to-body ratio of the approved kimchi enemy reference. Each eye is much smaller than a topping piece. No whites, iris, pupil, highlights, eyebrows, blush, large angry eyes, or U-shaped smile.

CANVAS:
A single 1536×1024 image, exactly 6 columns by 4 rows. Each cell is exactly 256×256 with a fully transparent background. Every frame uses the same scale and ground baseline and faces LEFT. No motion crosses a cell boundary.

DIRECTION LOCK:
Enemies enter from the RIGHT side of the game screen and travel toward the player on the LEFT. Every walk, attack, alert, guard, stun, and death frame faces LEFT. Walk and attack motion travels from RIGHT to LEFT. Only hit knockback travels toward the RIGHT because the enemy is struck from the LEFT.

ROW 1 — WALK, 6 frames:
[이 몹의 이동 방식]. Show a complete looping movement cycle. The body must visibly rise and fall and the legs or moving parts alternate clearly.

ROW 2 — ATTACK, 6 frames:
Frame 1-2: [힘을 모으는 자세].
Frame 3-4: [왼쪽을 향해 공격하고 타격하는 자세].
Frame 5-6: [반동 후 기본 자세로 돌아오는 모습].
Do not draw an opponent.

ROW 3 — HIT, 6 frames:
The enemy is struck from the LEFT and knocked back toward the RIGHT. [이 음식 재질에 맞는 찌그러짐 또는 흔들림]. The final frame begins recovering.

ROW 4 — DEATH, 6 frames:
Stagger, buckle, tip sideways, fall, and settle. Keep the same visible side of the body and keep facing LEFT throughout the sequence. Do not somersault or roll forward.

STYLE:
Authentic 2D pixel art with no anti-aliasing, crisp hard square pixels, bold clean outlines, limited palette, and consistent light from the upper left.
		```
		---
		## 4. 공통 네거티브 프롬프트
		```plain text
No large eyes, eye whites, iris, pupils, eye highlights, eyebrows, blush, happy closed eyes, curved smiling eyes, or U-shaped smile.
No text, letters, Korean characters, numbers, titles, pose names, captions, labels, grid lines, frame borders, watermark, UI, opponents, weapons, chopsticks, hands, plates, bowls, tables, or extra characters.
No changing colors, changing proportions, changing face position, changing toppings, extra arms, extra legs, duplicated body parts, or inconsistent silhouette.
No general illustration, vector art, smooth painting, anti-aliasing, blurry pixels, 3D rendering, photorealism, or smooth gradients.
Do not turn [몹 이름] into [오인하기 쉬운 음식이나 사물].
		```
	### 보스 AI 프롬프트 가이드 {toggle="true"}
		```javascript
- 공통 블록은 모든 보스에 동일하게 사용한다.
- 음식별로 실루엣·재질·토핑·소스만 바꾼다.
- 각 보스마다 추가 네거티브를 따로 작성한다.
- 특별히 작거나 팔다리가 없는 보스는 충돌하는 공통 문장을 제거한다.
- 결과물은 2400×1309, 투명 배경, 정면 구도로 맞춘다.
		```
		<empty-block/>
		**사용 방법**
		보스는 한 장씩 생성한다.
		```plain text
공통 프롬프트
+ 공통 네거티브
+ 보스별 프롬프트
+ 보스별 추가 네거티브
		```
		결과물 기준은 2400×1309, 정면 구도, 투명 배경, **깨진 흰 도자기 접시 위에 앉은 단독 보스**다.
		→ 프롬프트는 이걸로 잡았는데 알아서 바꿔서 사용하십쇼
		---
		## 1. 공통 프롬프트
		```plain text
32-bit era pixel art game boss sprite, chunky visible pixels, hard black outlines,
no anti-aliasing, limited palette, dramatic warm rim light from the upper left,
deep shadow on the lower right, transparent background, no scenery, no floor,
the creature sits on a chipped white ceramic plate that fills the bottom of the frame,
hunched forward with both arms planted on the plate rim, glowing eyes,
centred, facing the viewer, filling the frame edge to edge, 2400x1309
		```
		---
		## 2. 공통 네거티브 프롬프트
		```plain text
avoid: background, scenery, floor, table, text, letters, logos, watermark,
anti-aliasing, smooth gradients, 3D render, photorealism, blurry edges,
generic rock golem, clay golem, mud monster, brown sludge, featureless lump,
human face, human hands, cute mascot, chibi
		```
		---
		## 3. 보스별로 정할 내용
		```plain text
보스 이름:
음식 이름:
전체 실루엣:
핵심 재질:
주요 색:
토핑 또는 고명:
접시 위 소스나 국물:
얼굴과 눈 위치:
다른 음식으로 오인하지 않게 할 요소:
공통 블록에서 빼야 할 문장:
		```
		보스는 실루엣과 음식 재질을 먼저 정한다. 갑옷, 근육, 뿔이나 돌을 추가해서 강해 보이게 만들지 않는다.
		---
		## 4. 보스별 프롬프트 템플릿
		```plain text
[공통 프롬프트]

[공통 네거티브 프롬프트]

A boss made of [음식 이름].

SILHOUETTE:
[전체 형태를 한 문단으로 설명한다. 음식의 크기, 쌓임, 늘어짐, 각짐, 둥글기와 대표 특징을 적는다.]

MATERIAL:
[표면 재질, 단면, 투명도, 윤기, 수분, 구운 자국과 색을 설명한다.]

DETAILS AND TOPPINGS:
[이 음식으로 알아보게 만드는 토핑과 고명을 정확한 개수와 위치까지 설명한다.]

PLATE AND LIQUID:
[접시 위 국물, 소스, 기름, 부스러기 또는 균열을 설명한다.]

EYES:
[눈의 위치, 색, 형태와 빛을 설명한다.]

Additional avoid:
[다른 음식으로 오인하게 만드는 형태]
[잘못 나오기 쉬운 색과 재질]
[보스마다 제외해야 할 요소]
		```
		---
		## 5. 작성 예시 구조
		```plain text
A boss made of [FOOD].

SILHOUETTE:
The body is [대표 실루엣]. It is never [오인하기 쉬운 형태].

MATERIAL:
The surface is [핵심 재질] with [색과 광택]. It must look like [실제 음식 상태],
not [돌, 진흙, 플라스틱 등 잘못된 재질].

DETAILS AND TOPPINGS:
[토핑 1], [토핑 2], and [토핑 3] are placed on [정확한 위치].
These toppings are what make the creature immediately readable as [음식].

PLATE AND LIQUID:
[국물 또는 소스] pools on the chipped white ceramic plate.

EYES:
[눈 묘사].

Additional avoid:
[오인 음식], [잘못된 실루엣], [잘못된 재질], [불필요한 요소]
		```
		---
		## 6. 공통 블록을 수정해야 하는 예외
		참깨처럼 작거나 팔다리가 없는 보스는 공통 블록을 그대로 쓰면 안 된다.
		- 팔이 없으면 hunched forward with both arms planted on the plate rim 문장을 뺀다.
		- 작은 보스면 filling the frame edge to edge 문장을 뺀다.
		- 발광 눈이 아니면 glowing eyes 문장을 뺀다.
		- 접시가 핵심이 아니어도 접시는 배경이 아니라 보스 받침으로 유지한다.
		예외 문장을 빼지 않으면 모델이 음식 설명보다 공통 블록을 우선해 모든 보스를 같은 골렘 형태로 만든다.
		---
		## 7. 보스 디자인 핵심
		- 우동처럼 늘어지는 보스는 굵기와 가닥 수를 적는다.
		- 묵처럼 투명한 음식은 각진 칼자국 면과 빛이 통과하는 가장자리를 적는다.
		- 장조림처럼 젖은 음식은 소스 색과 표면 광택을 적는다.
		- 작은 보스는 몸집 대신 접시 균열, 후광과 주변 효과로 존재감을 표현한다.
		- 보스마다 다른 음식으로 오인되는 형태를 추가 네거티브에 넣는다.
		- 중간 보스와 메인 보스는 기획상 한 쌍이지만 이미지는 한 장씩 따로 생성한다.
		---
</details>
<empty-block/>
<empty-block/>
<details>
<summary>강화 재료 후보</summary>
	밀가루,소금, 설탕, 후추, 고춧가루, 전분, 빵가루<br>간장, 고추장, 된장, 다진마늘, 버터, 치킨스톡, 케첩, 머스타드, 마요네즈<br>식용유, 올리브유, 참기름, 들기름, 참치액, 매실청, 굴소스, 식초, 맛술, 우유
</details>
## **2. 자료 첨부**
---
<file src="file://%7B%22source%22%3A%22attachment%3A98066290-2055-4aae-8a5a-97a1687c2de5%3A%ED%8A%B9%ED%99%94_%ED%94%84%EB%A1%9C%EC%A0%9D%ED%8A%B8.zip%22%2C%22permissionRecord%22%3A%7B%22table%22%3A%22block%22%2C%22id%22%3A%22d0c06c87-81b0-829a-90a0-81401364b27b%22%2C%22spaceId%22%3A%22ccadcd0a-f169-412b-889a-adce96602b0b%22%7D%7D"></file>
<empty-block/>
---
## 1. 재료 (보류)
총 21종.
- 소금
- 설탕
- 밀가루
- 후추
- 계란
- 버터
- 우유
- 치즈
- 초콜릿
- 딸기
- 닭고기
- 마늘
- 고추
- 소고기
- 새우
- 연어
- 트러플
- 사프란
- 캐비어
- 황금 허브
- 황금계란
재료마다:
- 인벤토리 아이콘
- 필드 드롭 이미지
- 획득 알림용 이미지
## 2. 장비·제작물
약 10종.
- 기본 반죽검
- 버터 쿠키 해머
- 초코 팬케이크 대검
- 불타는 치즈 치킨검
- 황금 연어 스테이크검
- 트러플 와규 대검
- 심해왕 캐비어 삼지창
- 황제의 사프란 치킨
- 갈릭 치즈스틱
- 트러플 와규 황제검
장비마다:
- 인벤토리 아이콘
- 상세 이미지
- 젓가락 장착 이미지
- 공격 이미지 또는 이펙트
- 잠금 실루엣
## 3. 장비 제작 UI
- 제작 트리 전체 화면
- 재료 노드
- 중간 제작물 노드
- 최종 장비 노드
- 연결선
- 제작 가능 상태
- 재료 부족 상태
- 레벨 부족 상태
- 잠금 상태
- 제작 완료 상태
- 장비 상세 팝업
- 제작 버튼
- 최종 무기 제작 연출
## 4. 주인공 젓가락
진화 단계는 없고 스킨만 제작.
- 기본 젓가락
- 첫 배포용 젓가락 스킨
- 스킨 아이콘
- 스킨 상세 이미지
- 잠긴 스킨 이미지
- 무기 장착 기준 이미지
필요한 GIF:
- 대기
- 걷기
- 공격
- 피격
- 쓰러짐
- 승리
스킨은 같은 애니메이션 틀을 재사용하도록 제작.
## 5. 일반 몹
전투 스테이지 3개 × 스테이지별 4종.
- 스테이지 1 몹 4종
- 스테이지 2 몹 4종
- 스테이지 3 몹 4종
총 12종.
필요한 GIF:
- 대기
- 이동
- 공격
- 피격
- 사망
총 몹 GIF 약 60개.
## 6. 보스
전투 스테이지마다 중간 보스와 메인 보스.
- 스테이지 1 중간 보스
- 스테이지 1 메인 보스
- 스테이지 2 중간 보스
- 스테이지 2 메인 보스
- 스테이지 3 중간 보스
- 스테이지 3 메인 보스
총 6종.
보스마다:
- 기본 이미지
- 보스 선택 아이콘
- 등장 이미지
- 도감 이미지
- 잠금 실루엣
- 전용 드롭 재료
- 전용 공격 이펙트
필요한 GIF:
- 등장
- 대기
- 공격
- 특수 공격
- 피격
- 사망
총 보스 GIF 약 36개.
## 7. 전투맵
전투 스테이지 3세트.
각 스테이지마다:
- 먼 배경
- 중간 배경
- 반복 가능한 바닥
- 앞쪽 오브젝트
- 배경 소품
- 장애물
- 시작 구역
- 일반 전투 구역
- 중간 보스 구역
- 메인 보스 구역
- 클리어 구역
- 지도용 미리보기
## 8. 아지트·채집맵
- 메인 아지트
- 재료 채집맵
- 강화·제작 공간
- 상점·거래 공간
- 인벤토리 오브젝트
- 도감 오브젝트
- 스킨 변경 공간
- 지도용 장소 미리보기
- 클릭 가능한 오브젝트 표시
채집 장소가 여러 개라면 장소별로:
- 전체 배경
- 바닥
- 전경 소품
- 채집 위치
- 재료 드롭 이미지
## 9. 전투 UI
- 플레이어 체력바
- 일반 몹 체력바
- 보스 체력바
- 스테이지 진행도
- 중간 보스 표시
- 메인 보스 표시
- 자동 전투 버튼
- 수동 전투 버튼
- 공격 버튼
- 스킬 버튼
- 데미지 숫자
- 크리티컬 표시
- 피격 표시
- 보스 등장 표시
- 클리어 표시
- 실패 표시
- 획득 재료 표시
## 10. 인벤토리 UI
- 인벤토리 아이콘
- 재료 탭
- 장비 탭
- 기타 아이템 탭
- 빈 슬롯
- 보유 슬롯
- 선택 슬롯
- 잠긴 슬롯
- 희귀도별 슬롯
- 수량 표시
- 장비 중 표시
- 신규 아이템 표시
- 인벤토리 용량 표시
- 인벤토리 확장 버튼
- 아이템 상세 팝업
## 11. 골드·재화
- 골드 아이콘
- 특수 재화 아이콘
- 보스 재화 아이콘
- 골드 획득 이미지
- 골드 부족 표시
- 재화 획득 효과
- 보상 묶음 이미지
## 12. 상점·거래 UI
- 상점 아이콘
- 구매 탭
- 판매 탭
- 거래 탭
- 상품 카드
- 가격 표시
- 수량 선택
- 구매 버튼
- 판매 버튼
- 재화 부족 상태
- 품절 상태
- 구매 완료 표시
- 판매 완료 표시
## 13. 지도·스테이지 선택
- 지도 아이콘
- 전체 지도
- 스테이지 카드
- 장소 미리보기
- 현재 위치 표시
- 잠긴 스테이지
- 열린 스테이지
- 클리어한 스테이지
- 중간 보스 표시
- 메인 보스 표시
- 드롭 재료 미리보기
## 14. 도감
- 몬스터 도감
- 보스 도감
- 재료 도감
- 장비 도감
- 스킨 도감
- 획득한 항목
- 미획득 실루엣
- 신규 항목 표시
- 상세 보기
## 15. 스킨 UI
- 스킨 메뉴 아이콘
- 스킨 목록
- 스킨 카드
- 기본 스킨
- 보유 스킨
- 잠긴 스킨
- 장착 중 상태
- 스킨 상세 미리보기
- 스킨 장착 버튼
- 스킨 획득 연출
## 16. 공통 메뉴 아이콘
- 지도
- 전투
- 제작
- 장비
- 인벤토리
- 상점
- 도감
- 스킨
- 보스
- 설정
- 나가기
- 닫기
- 이전
- 다음
- 잠금
- 정보
- 알림
각 아이콘은 기본·선택·비활성 상태 필요.
## 17. 공통 버튼·창
- 메인 버튼
- 보조 버튼
- 위험 버튼
- 비활성 버튼
- 아이콘 버튼
- 팝업 창
- 확인창
- 경고창
- 탭
- 카드
- 툴팁
- 스크롤바
- 로딩 표시
- 빈 상태
- 오류 상태
## 18. 게임 연출·이펙트
- 기본 공격
- 무기별 공격
- 피격
- 크리티컬
- 몬스터 사망
- 보스 등장
- 보스 사망
- 아이템 드롭
- 재료 획득
- 골드 획득
- 장비 제작
- 제작 성공
- 제작 실패
- 최종 무기 완성
- 스킨 획득
- 스테이지 시작
- 스테이지 클리어
- 게임 오버
## 19. 타이틀·배포용 디자인
- 게임 로고
- 타이틀 배경
- 게임 시작 버튼
- 이어하기 버튼
- 설정 버튼
- 로딩 화면
- 앱 아이콘
- 파비콘
- 대표 썸네일
- 소개용 배너
- 첫 실행 안내
- 기본 조작 안내
# 대략적인 핵심 물량
- 재료: 21종
- 장비·제작물: 약 10종
- 일반 몹: 12종
- 일반 몹 GIF: 약 60개
- 보스: 6종
- 보스 GIF: 약 36개
- 전투맵: 3세트
- 주인공: 기본 1종 + 스킨
- 주인공 GIF: 최소 6개
- UI 아이콘: 약 20종
- 공통 UI와 각종 이펙트 세트
첫 배포에 가장 먼저 필요한 순서는:
1. 재료와 장비
2. 기본 젓가락과 GIF
3. 스테이지 1\~3 전투맵
4. 일반 몹 12종과 GIF
5. 보스 6종과 GIF
6. 전투 UI
7. 인벤토리와 제작 트리
8. 골드·지도·도감·스킨 UI
9. 타이틀과 배포용 이미지
</content>
</page>
