"""Unapproved static design candidates: urban objects, plants and animals."""
# name / role / visible identity / attack character / visible width / height / generation description
SETS={
 'A':{'title':'떠내려온 살림','direction':'생활 사물 50 · 동물 35 · 식물 15','monsters':[
 ['집게꼬리','근거리·빠른 접근','나무집게 주둥이와 낮은 회보라색 몸','짧게 달려들어 집게로 물기',64,38,'A tiny low mouse-like urban drift creature with an oversized wooden CLOTHESPIN muzzle opening horizontally, a compact grey-lilac furry body and a single curled cord tail. Four very short scampering feet. Its pin-head and soft body form one unusual original character; no dinosaur.'],
 ['터벅장화','근거리·기본 공격','갈라진 겨자색 고무장화와 천 조각 팔','발등의 틈을 벌려 물고 앞코로 차기',60,62,'ONE living discarded RUBBER BOOT, tall bent shaft as the main body, huge forward boot toe with a slit mouth in the cracked sole, two short wet cloth-strip arms from the shaft and tiny root toes under the sole. Hunched forward kick-ready pose. Object character, not a person wearing boots, no head growing out of it.'],
 ['대야소라','근거리·중압박','밝은 청색 대야 등껍질과 주황색 소라게 다리','낮게 밀고 들어와 넓은 집게로 치기',106,70,'A broad low HERMIT CRAB whose shell is a dented inverted enamel WASHBASIN, broad pale sky-blue round basin surface, warm orange crab legs and one wide blunt claw in front. The basin is its one key found object. No pipes or armor additions.'],
 ['씨앗받이','원거리·직사','분홍 도기 몸체와 옆으로 열린 연두색 씨방','씨방을 앞쪽으로 조여 씨앗 뱉기',66,60,'A small rooted seed-spitting PLANT in a cracked pink ceramic DRAIN CUP: broad rounded cup body, two root feet, a single pale yellow-green pea-pod head bent horizontally forward with an obvious open seed mouth. One simple leaf behind. Squat braced firing pose. No gun, no separate projectile.'],
 ['보따리박쥐','원거리·곡사','자주색 천보따리 몸과 접힌 크림색 날개','볼에 머금은 자갈을 위로 뱉기',76,58,'A squat BAT nestled in a plum rag bundle: broad soft bundle belly, tiny bat snout tilted diagonally upward with inflated cheeks holding pebbles, two cream clothlike wings folded upward like a kite behind its back, little hooked feet planted. Its raised snout clearly implies an upward lob. No reptile and no goggles.'],
 ['탈수집게','엘리트·긴 접근 견제','세로 탈수틀과 길게 뻗은 빨래집게 팔','긴 집게팔로 찌르고 천 다리로 버티기',110,106,'A dangerous upper drift-utility creature: upright old wooden LAUNDRY WRINGER frame body, one broad indigo folded towel through the frame as a torso, two long knotted cloth legs and a narrow elongated clothespin arm thrust forward. A short opposite roller brace. Strong open rectangular space in upper body. Related to the clothespin mouse by the wooden bite motif but radically different upright lever anatomy, no humanoid robot face.'],
 ['보따리어멈','보스·유실물 무리의 중심','처진 연자주색 빨래보따리와 넓은 빨래판 팔','큰 빨래판으로 쓸고 보따리 몸으로 밀기',224,180,'A large ORIGINAL living LAUNDRY BUNDLE boss, wide sagging dusty rose-lilac cloth sack body with a deep folded seam mouth facing left, one enormous pale wooden WASHBOARD forearm in front, one short knotted cloth hand behind, two thick padded cloth feet, a single red clothespin tag at its upper neck fold. Asymmetrical soft bundle silhouette, broad belly then huge flat washboard attack surface and small pin-tag identity. No animal head, no lizard, no industrial pump, no humanoid armor.']
 ]},
 'B':{'title':'배수 틈새 정원','direction':'식물 50 · 사물 25 · 동물 25','monsters':[
 ['물풀종종','근거리·빠른 접근','접힌 연두 잎몸통과 길게 꺾인 뿌리다리','낮게 튀어들어 잎끝으로 베기',62,40,'A low hopping waterweed creature: two folded chartreuse leaves forming an angular grasshopper-like body, three pale bent root legs and one short curled root antenna. Entirely botanical, leaf blade projected forward. Tiny, agile, no humanoid torso.'],
 ['뜰채걸음','근거리·기본 공격','뜰채 테와 민트색 그물주머니·굽은 손잡이 꼬리','그물 테를 앞쪽으로 휘둘러 걸기',82,64,'An animated SCOOP NET: large pale reed-gold oval net rim tilted forward as the head, simple broad mint algae-filled mesh bag as the body, a long curved wooden net handle trailing behind as a balancing tail, two rusty short crablike legs underneath. No human holding the net. Only a few big mesh openings, no fine net texture.'],
 ['화분등두꺼비','근거리·중압박','깨진 적갈색 화분등과 넓은 올리브색 두꺼비','몸을 낮춰 밀고 무겁게 앞발 치기',106,80,'A broad squat TOAD with a broken TERRACOTTA PLANTER around its back as a shell, two large dusty mauve leaves growing from the planter, warm yellow-olive head and wide planted legs, large pale cream throat. Very round and low, no elongated reptile, no spikes.'],
 ['부들분사초','원거리·직사','수평으로 꺾인 황토색 부들 이삭과 밝은 잎','이삭 끝을 조여 전방으로 씨털 분사',62,66,'A reed plant with two short root feet and one BENT CATTAIL stalk: a broad light mint leaf body, long curved stem bending forward, one large ochre sausage-shaped cattail seed head held HORIZONTALLY at the end with a clear front pore facing left. Plant shapes, not a cannon robot. Braced and leaning forward.'],
 ['물방울달팽이','원거리·곡사','연보라 도기껍질과 위로 열린 물받이 입','등껍질에 모은 물을 위로 튀겨 뱉기',76,56,'A low round water-catching SNAIL: pale lavender CERAMIC SHELL shaped as a tilted water bowl with an open upper lip facing diagonally up-left, muted apricot soft foot and two short curled antennae. Clear large shell, small head tilted upward preparing to lob stored water. No pipes, no glowing water or projectile.'],
 ['뿌리매듭꾼','엘리트·거리 압박','세 갈래 뿌리 지지대와 긴 상아색 뿌리창','큰 잎을 벌려 막고 뿌리창으로 찌르기',112,108,'An upper BOTANICAL knot creature related to the hopping weed, now a tall braided root knot with three stilt-like supporting roots, one broad pale mint folded leaf guard and one long warm ivory root spear reaching forward, a narrow ochre seedpod head nestled low between the leaves. Strong hollow gaps between long roots. No human shoulders, no mantis face or reptile.'],
 ['퇴적화관','보스·걸러진 정원의 중심','넓은 청록색 거름그릇·붉은 꽃잎 입·세 뿌리발','꽃잎 턱으로 쓸고 굵은 뿌리로 내려치기',232,184,'An original drainage-garden boss: a HUGE broad pale TURQUOISE COLANDER / drain strainer bowl is the upper living body and crown, a bulky plum tangled root bulb below it on three thick ochre root feet, a single broad CORAL FLOWER-PETAL jaw opening to the left, one long thick pale root striking arm and one short leaf brace behind. The distinct bowl body, petal attack jaw and rooted feet must read as a mobile creature. Only 5 large drainage holes, no busy lattice. No humanoid, no dinosaur, no stationary background machinery.']
 ]},
 'C':{'title':'검문선의 틈새 생명','direction':'경계 사물 50 · 동물 30 · 식물 20','monsters':[
 ['쪽지나방','근거리·빠른 접근','찢긴 종이날개와 붉은 인주표식','낮게 파고들어 접힌 날개로 베기',64,42,'A low hovering MOTH with folded torn PAPER TICKET wings, a broad ivory triangular forward wing edge, tiny dusty violet insect body, one simple faded red stamp mark on the wing (abstract circle, no letters). Strict side profile with two short pointed legs below; dynamic narrow wedge silhouette, no giant wingspan.'],
 ['손잡이족제비','근거리·기본 공격','황갈색 긴 몸과 등에 휜 황동 손잡이','몸을 낮춰 전진하고 길게 물기',88,56,'An alert upright-necked WEASEL with a long pale russet body and cream underside, one old curved brass DOOR HANDLE along its arched back, looped tail like a key ring. Four planted small feet and a short curious narrow muzzle, no fantasy armor. Animal dominates, handle is the one key silhouette feature.'],
 ['차단말뚝','근거리·중압박','황토·상아색 차단기둥과 넓은 고무 치맛단','묵직하게 전진해 치맛단으로 밀기',76,82,'ONE animated squat traffic BOLLARD / access post: broad upright short ochre cylinder with two large faded ivory bands, one hinged dull red STOP PADDLE at its side, a wide dark violet rubber skirt split into two thick stomping feet. Tiny side slit face cut into the post. No text, no modern LED, no person carrying a sign.'],
 ['우편함쏘개','원거리·직사','벽돌색 우편함 몸과 수평으로 열린 투입 덮개','덮개를 젖혀 접힌 종이를 직선 발사',70,64,'An animated old city MAIL DROP box, faded brick-red sloped mailbox body on two small ochre birdlike feet, one broad pale horizontal front flap open to face left, a single folded paper wedge just inside the mouth implying straight paper firing. No letters or logo. Compact box bird silhouette, not a humanoid gunner.'],
 ['도장꽃','원거리·곡사','위로 기운 황토색 도장꽃과 휘어진 보라 줄기','꽃머리를 뒤로 젖혀 둥근 씨앗 투척',62,68,'A STAMP FLOWER: one big pale ochre round stamp-disc blossom tilted diagonally up-left, pale pink broad petals underneath like a collar, one curved dusty violet stem body with two root feet. Its disc head is cocked back like a throwing arm to lob round seed-seals upward. No glyphs or magical glow.'],
 ['봉인실거미','엘리트·긴 포획 위협','붉은 봉랍 몸통과 벌어진 상아색 끈다리','긴 앞다리로 걸고 반대 다리로 찌르기',118,108,'An upper checkpoint SPIDER made from sealing-wax and ticket ribbons: chunky broad dusty red WAX SEAL abdomen, small slate head, SIX widely separated thick angular ivory ribbon legs including two long hooked front legs reaching toward left, one torn ticket plate on its back. No fine web, no spiderweb background. A biological guardian of discarded seals, dangerous reaching limb geometry rather than an enlarged moth.'],
 ['검표먹보','보스·잠입로 검표 장치의 변이','자주색 접지몸·붉은 천공틀·넓은 황동 압인턱','천공턱을 닫아 물고 큰 압인발로 찍기',230,190,'A unique LIVING TICKET PUNCH boss: a broad upright arched WINE-RED metal ticket-punch frame forms the head and shoulders, huge thick pale BRASS punching lower jaw projects forward as a stamp lever, a bulky dusty plum accordion-folded PAPER STACK body rests on two thick folded ticket legs, a short fan of blank ivory ticket stubs behind the head and a long curled blank ticket tail. Its silhouette is an upright hole-punch office tool becoming a squat creature, NOT a crocodile, no long reptilian snout, no dinosaur, no humanoid robot. One large negative-space arch inside the punch frame, minimal big moving masses, no lettering.']
 ]}
}

PALETTES={
 'A':[
 'Pale natural wood pin muzzle, grey-lilac furry back, dusty mauve cord tail. Muted but bright enough broad flank.',
 'MUSTARD YELLOW rubber boot with a pale worn upper plane, cool plum crack shadows, faded cream rag arms. Yellow is the main mass.',
 'PALE SKY BLUE enamel basin, vivid but worn coral-orange crab legs, warm ochre claw shadow. Light blue shell is much lighter than wall.',
 'Dusty ROSE PINK ceramic cup body, PALE YELLOW-GREEN seed pod, soft cream roots. Clearly pink plus green.',
 'DUSTY PLUM / mauve cloth-bundle belly, cream broad wings, cool slate small bat head and peach cheeks. No eye glow.',
 'Faded INDIGO towel torso, warm pale wooden wringer and pin arm, muted coral tied cloth legs. Bright wooden forearm contrasts with blue body.',
 'Broad dusty ROSE-LILAC sack with pale faded cloth front, warm ivory washboard arm, pale teal knotted hand, one brick-red pin tag. No cream-and-red uniform.'
 ],
 'B':[
 'Light CHARTREUSE / fresh yellow-green folded leaves, pale cream roots, olive shadow only in leaf folds. Broad light leaf body.',
 'Pale straw-gold net rim and handle, broad soft MINT net bag, rust-brown legs. No glowing outlines.',
 'Warm TERRACOTTA planter, yellow-olive toad face and legs, pale cream throat, two DUSTY MAUVE leaves. Broad worn clay color.',
 'Pale MINT BLUE-GREEN broad leaves, ochre cattail head and warm straw stem. Light plant masses contrast dark green stone.',
 'PALE LAVENDER ceramic bowl shell, soft APRICOT snail foot, dull violet shell shadow. No external bubbles or effects.',
 'Pale cool MINT leaf guard, warm IVORY root spear, muted golden root knot and dark olive internal knots. Large light planes.',
 'Broad PALE TURQUOISE colander, CORAL RED petal jaw, dusty PLUM root bulb and pale ochre root feet. Four big material planes, no neon.'
 ],
 'C':[
 'Warm IVORY paper wings, faded RED abstract stamp, tiny soft VIOLET body. Unprinted torn-paper texture, broad flat planes.',
 'Pale RUSSET / sandy tan weasel back, cream underside, old warm BRASS handle, cool purple-grey tail shadow.',
 'Warm OCHRE and IVORY post bands, dull RED side paddle, dusty VIOLET rubber foot skirt. Big stripe blocks, no neon.',
 'Faded BRICK RED mailbox with a lighter warm red front plane, CREAM flap and paper wedge, antique BRASS feet.',
 'Pale OCHRE stamp-disc flower, soft ROSE PINK petal collar, DUSTY VIOLET stalk and cream roots.',
 'Broad muted SEALING-WAX RED abdomen, pale warm IVORY ribbon legs, small SLATE BLUE head, yellow-grey blank paper plate.',
 'WINE RED punch-frame with a lighter faded-red outer plane, large pale BRASS jaw, dusty PLUM paper-stack body, ivory ticket crest and legs. Separate color masses, no uniform grey.'
 ]
}
