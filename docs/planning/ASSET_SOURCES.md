# SsalMuk 자산 출처와 적용 기록

## 도트 무기 원본

- 제작일: 2026-09-16. 제작 도구: 내장 imagegen. 사용자가 요청한 철검·철도끼·철창·루비 금색 지팡이 4종이다.
- 상태: 4종 생성·시각 확인·프로젝트 원본 보관 완료. 투명 alpha를 확인했다. Unity의 실제 공격 표시에 연결하고 크기·중심·축을 검증하는 작업은 D2에서 진행한다.
- 원본과 프로젝트 복사본을 보존한다. 지팡이는 기존 파이어볼 표시이며 새 무기 종류가 아니다.
- 위치: `Assets/_SsalMuk/Content/Sprites/Weapons/`. 픽셀 도트 외형이며 최종 표시 크기는 Unity에서 조절한다.

| 프로젝트 파일 | 생성 원본 파일명 | 원본 크기 |
| --- | --- | --- |
| IronSword.png | exec-b5c6ce59-6437-454e-af09-caa98b19ec1b.png | 1774×887 |
| IronAxe.png | exec-fbe6b918-fe05-4790-a19d-c10b7db78d9e.png | 1536×1024 |
| IronSpear.png | exec-8adc8251-d751-4253-94f2-0bef2b6d026f.png | 2172×724 |
| RubyStaff.png | exec-00360eaa-80bd-4be5-aa67-0a0f94f98712.png | 1983×793 |

생성 원본 폴더는 `C:/Users/ace21/.codex/generated_images/01a0a83f-e247-7640-8dd5-603e2f5206cc/`다. 프로젝트는 이 외부 폴더를 참조하지 않는다. alpha 검사 증거는 `Logs/Validation/bcd-baseline/weapon-alpha.json`에 있다.

### 철검 프롬프트

Create a single production-ready pixel art iron sword sprite for a top-down 2D fantasy survival game. One iron sword only, entire object isolated on a genuinely transparent alpha background, no checkerboard baked in, no text, no labels, no border or scene. Side view, horizontal, tip points exactly to the RIGHT, dark brown leather handle at LEFT, simple dark iron crossguard, straight tapered steel blade with silver edge, restrained blue-gray shading and one bright edge highlight. Crisp hand-clustered 48x24 logical pixel style enlarged with nearest-neighbor look, limited 10-color palette, dark 1 logical pixel contour, absolutely no blurry brushwork or gradients. Readable small in game. Center the object, occupy about 80 percent of canvas width, modest transparent padding. No glow, hands, character or attack trail. Save as a transparent PNG.

### 철도끼 프롬프트

Create one standalone iron battle axe pixel art sprite for a top-down 2D fantasy survival game. Entire object isolated on a genuinely transparent alpha background. Horizontal shaft, grip at LEFT and axe head at RIGHT. A large single broad crescent iron cutting blade projects downward from the RIGHT end of the shaft; the blade is broad and heavy, angular steel blue-gray metal with silver cutting edge and dark iron socket. Dark brown leather-wrapped wooden haft, simple utilitarian medieval construction, no ornament. Match a classic crisp pixel iron sword: limited 10-color palette, bold one logical pixel dark contour, hand placed pixel clusters, about 48x32 logical pixel style enlarged without smoothing, no soft gradients. Readable silhouette at tiny in-game scale. Centered entire object with modest transparent padding, no clipping. No text, labels, watermark, checkerboard pixels, hands, character, glow or attack trails. Transparent PNG.

### 철창 프롬프트

One standalone iron spear pixel art game sprite, for the same classic top-down 2D fantasy survival art style as simple iron sword and iron battle axe. Entire spear horizontally aligned: long straight dark brown wooden shaft runs from LEFT to a pointed silver iron spearhead at RIGHT. Small brown leather grip, dark steel socket and simple leaf-shaped iron point with a single silver edge highlight. Modest medieval utilitarian design. Flat profile with no perspective foreshortening. Crisp chunky hand placed pixel clusters, about 64x12 logical pixels enlarged with hard nearest-neighbor edges, limited brown and blue-gray metal palette, one logical pixel dark outline. Genuine transparent alpha background, absolutely no backdrop, baked checkerboard, glow, shadow, haze, text, labels, hands, character, or trailing effects. Entire object centered occupying 85 percent width, no clipping, consistent pixel density and thickness. Transparent PNG.

### 루비 지팡이 프롬프트

Create one standalone pixel art magic staff sprite for a top-down 2D fantasy survival game. A straight GOLD COLORED STAFF with a single faceted RED RUBY mounted at its RIGHT END. Horizontal object, lower end/handle on the LEFT, ruby tip on the RIGHT. Long slim golden rod with a modest grip and simple gold claws securely holding the ruby, no other gemstones, no silver parts. Large readable ruby silhouette, crimson shaded facet and tiny pale highlight. Classic crisp pixel art: limited 12-color palette, one logical pixel dark outline, chunky deliberate pixel clusters, about 64x16 logical pixels enlarged with nearest-neighbor edges. Entire object centered, 85 percent width, transparent padding, no clipping. Genuine transparent PNG alpha background. No glow, haze, shadows, blurry gradients, baked checkerboard, hands, character, spell effects, text, labels or watermark. It should match simple utilitarian pixel iron weapons in the same game.

## 캐릭터·몬스터·외부 이펙트

D2에서 실제 제작·취득과 적용 후 출처·라이선스·사용 파일·검증 증거를 추가한다. [기존 조사 후보](ART_AND_VFX.md)는 아직 취득·적용된 자산을 뜻하지 않는다.


## 캐릭터·몬스터 원본과 효과 취득

- 2026-09-17: 같은 도트 화풍의 기사·초록 슬라임·보라 박쥐·붉은 거대 오우거 아틀라스를 imagegen으로 생성했다. 출력은 1254×1254, 투명 alpha이며 무기는 본체에 포함하지 않았다. 결과를 시각 확인하고 원본과 동일한 파일을 `Assets/_SsalMuk/Content/Art/Characters/UnitsAtlas.png`에 보관했다.
- 생성 원본: `C:/Users/ace21/.codex/generated_images/01a0a83f-e247-7640-8dd5-603e2f5206cc/exec-e194b045-f3c1-414d-a80e-37634332a6b3.png`. 사분면과 실제 그림 경계에 맞춘 Sprite 분리·바닥 축·크기 설정은 D2에서 적용한다. 현재는 원본 보관 단계다.
- [Slash Effect Collection — MetaShinryu](https://opengameart.org/content/slash-effect-collection)의 Circular·Arcing·Lunge Thrust 원본, [Fireball Spritesheet — Umplix](https://opengameart.org/content/fireball-spritesheet)의 4프레임 시트, [Explosion — BenHickling](https://opengameart.org/content/explosion-7)의 100×100 픽셀 50프레임 시트를 취득했다. 각 제작자 페이지의 CC0 표시를 확인했으며 구매는 없었다.
- 효과 원본은 `Content/Art/ThirdParty/`의 제작자별 폴더에 있고 출처·라이선스는 같은 폴더의 `ATTRIBUTION.md`에 있다. 다운로드 직후 시각 확인과 원본/복사본 해시 일치를 확인했다. 실제 프레임 적용과 전투 표시는 D2에 남아 있다.
- 추가로 조사한 Cethiel의 Fireball Effect와 MSavioti의 Firebal 32x32는 검토용 다운로드만 `Logs/Validation/bcd-assets/`에 유지한다. 프로젝트 콘텐츠에는 선택한 Umplix 시트만 포함했다.
- 원본 크기·해시·모서리 alpha의 취득 증거는 `Logs/Validation/bcd-baseline/unit-vfx-originals.json`에 보관한다.

### 유닛 아틀라스 생성 프롬프트

Create a single square transparent PNG pixel-art character atlas for a top-down 2D idle vampire-survivor fantasy game. Exactly four equally sized cells in a clean 2 by 2 grid; NO grid lines, NO text, NO labels, NO props between cells, NO background, NO shadows on the background. Each creature is centered in its own quadrant, with generous transparent padding, entirely inside its quadrant, idle upright pose, isolated, crisp old-school hand-pixelled game sprites with visibly stepped hard square pixel clusters and a limited coherent palette; no blur, no smooth painted edges, no antialiasing. Consistent front three-quarter overhead game view. Top-left: small charming but sturdy adventurer knight, steel grey iron helmet with a little mint feather, clearly visible eyes in the helmet opening, brown leather belt and boots, muted teal tunic under iron shoulder armour, empty hands by the sides, absolutely no weapon in this knight sprite. Top-right: common enemy, a green slime blob with dark green outline, two simple bright eyes, slightly menacing face, cute compact silhouette, no accessories. Bottom-left: airborne enemy, a small purple bat with wide outstretched wings and two tiny eyes, simple clean wing contours, entirely a bat with no human body, easy to read while it rushes across the screen. Bottom-right: huge boss, a bulky red-orange horned ogre with dark burgundy shadow planes, thick arms, short legs, bronze belt and a few iron armour plates, bare empty hands, no weapon, intimidating solid silhouette. Each individual figure should read as an approximately 48 to 64 pixel tall native sprite scaled up by nearest neighbour, while the boss feels broad and large and the bat wing span is wide. All four characters must use the same art direction and outline thickness, camera angle, subdued fantasy palette with clear silhouette. Make the output a 1024 by 1024 square, transparent alpha surrounding every sprite. Place cell centres precisely at quarter and three-quarter of canvas width and height; avoid any part crossing a half-canvas boundary.
