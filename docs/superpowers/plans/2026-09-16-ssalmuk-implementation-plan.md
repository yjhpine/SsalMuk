# SsalMuk Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 승인된 기획과 구조에 따라 무한 생존 자동 전투 게임과 개발·검증 하네스를 단계별로 구현한다.

**Architecture:** C# Core가 월드·유닛·전투·성장을 소유한다. MVP Presenter가 Unity View를 갱신하고, Factory Method·Flyweight·State를 지정한 위치에 적용한다. 논리 몬스터·경험치와 표시 오브젝트의 수명을 분리한다.

**Tech Stack:** Unity `6000.4.6f1`, C#, URP `17.4.0`, uGUI `2.0.0`, Input System `1.19.0`, Unity Test Framework `1.6.0`, PowerShell, MCP for Unity `10.2.0` 설치 대상.

**Spec:** [승인된 구조 설계](../specs/2026-09-16-ssalmuk-architecture-design.md), [게임 기획서](../../planning/GAME_DESIGN.md), [하네스 계약](../../development/HARNESS.md).

## Global Constraints

- 작업 경로는 `C:/Users/ace21/Desktop/SsalMuk`다. 사용자가 지정한 원본 프로젝트에서 작업하고 작업 단위로 로컬 커밋한다.
- Unity `6000.4.6f1`을 유지한다. 별도 승인 없이 엔진 업그레이드나 DOTS/ECS·외부 길찾기·DI 패키지를 추가하지 않는다.
- `검 60도·보스 300초·선택지 3개·무기 4종·강화 5종`을 유지한다.
- **경험치와 모든 몬스터를 계속 유지한다.** 거리·누적 수를 이유로 일반·공중·보스·경험치를 삭제하지 않는다.
- 경험치는 초록 < 파랑 < 빨강의 작은 발광 원형 구슬이다. 근접 시 날아오고 몸 접촉 시 ID당 한 번만 지급한다.
- 플레이어·몬스터 스프라이트는 AI 제작 가능. 공격 이펙트는 [아트·이펙트 기준](../../planning/ART_AND_VFX.md)의 외부 2D 후보를 검토해 도입한다.
- 메뉴·전투 씬에 게임 개체를 미리 배치하지 않는다. 씬 로드 → 맵·구조물 → 플레이어와 검 → 몬스터 → 자동 전투 순서를 지킨다.
- 사람이 이동을 조작하지 않는다. low AI가 이동하고, 플레이어는 전투가 계속되는 동안 레벨업 보상을 선택한다.
- 무기별 강화와 선택 기회에 임의 상한을 넣지 않는다. 표현 범위 초과·처리 지연을 숨은 Clamp나 공격 누락으로 처리하지 않는다.
- 구조물은 이동을 막고 공격은 통과한다. 공중은 구조물·군집을 통과하며 넉백 뒤 기존 방향으로 복귀한다.
- 사망 시 판의 성장을 초기화하고 결과 스냅샷을 보존한다. 다음 판은 검만 가진 새 상태다.
- 변경 전부터 존재한 사용자 파일을 보존하고 이번 작업의 변경만 커밋한다. 푸시·공개는 별도 요청 사항이다.
- 일반 보고는 `작업 완료.`로 짧게 한다. 사용자가 상세 요청을 하지 않으면 `log.md`를 생성하거나 갱신하지 않는다.

---

## 1. 승인과 실행 범위

2026-09-16 사용자가 구조 설계를 승인하고 구현 계획 작성을 요청했다. 몬스터 밀집은 원형 충돌 반경·지속 추적·겹침 보정·접선 이동으로 만들고, 공중을 제외한다는 설명까지 확인했다. 이 문서와 하위 계획은 **실행 전 계획**이며 체크되지 않은 항목을 구현 완료로 해석하지 않는다.

같은 프로젝트에서 순차 실행하는 방식을 기본으로 정리했다. 실행 시 `executing-plans`를 사용한다. 병렬 에이전트는 공유 Unity Editor와 씬 변경 충돌을 고려해 사용자가 요청할 때 별도로 선택한다. 이미 승인된 게임 구조를 작업마다 다시 승인받지 않는다.

추가 사용자 지시를 A3의 경험치 상태, C1의 접촉 획득, D2의 아트/이펙트/발광 구슬, H29~H30 검증에 연결했다. 기존의 반경 진입 즉시 획득 계획은 비행 후 접촉 획득으로 변경한다. 스프라이트 생성과 외부 에셋 취득·적용은 아직 실행하지 않았다.

## 2. 계획 작성 당시 출발점과 현재 진행

2026-09-16 A1 완료: 패키지·서버 `10.2.0` 연결, 대상 프로젝트/버전 읽기, 결과 판독기 28개, EditMode 6개, PlayMode 1개 검증을 완료했다. 빈 테스트 검색은 `NoTests`로 실패하며 현재 콘솔 오류는 0개다. A2 이후 게임 구현은 시작하지 않았다. 중단·재개 정보는 [인계](../../development/HANDOFF.md)를 따른다.

아래 항목은 계획 작성 당시의 기록이다.

- 계획 작성 전 HEAD: `da5ed21`. 자체 게임 스크립트와 asmdef는 아직 없다.
- Unity `6000.4.6f1` Editor가 SsalMuk 프로젝트를 열고 있다. 동일 프로젝트의 두 번째 Editor를 시작하지 않는다.
- Editor 실행 파일: `C:/Program Files/Unity/Hub/Editor/6000.4.6f1/Editor/Unity.exe`.
- 기존 `unityMCP` 설정은 활성화되어 있으며 주소는 `http://127.0.0.1:8080/mcp`다. 프로젝트 패키지 설치와 연결 성공은 확인되지 않았다.
- `uv`와 Git은 실행 경로에 있다. MCP 패키지·서버 `10.2.0`은 공식 태그와 패키지 메타데이터로 버전을 확인했다. 실제 호환성·연결 검증은 A1의 작업이다. [공식 릴리스](https://github.com/CoplayDev/unity-mcp/releases/tag/v10.2.0)
- 기존 변경: `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/ProjectSettings.asset`, 미추적 `.vsconfig`. 구현 시작 시 다시 확인한다.

기존에 변경된 파일을 수정해야 할 때는 시작 상태를 보관하고 필요한 항목만 추가한다. 특히 Build Settings의 기존 씬·설정을 대체하지 않는다. 새 씬 등록 부분만 분리해 스테이징하고, 사용자 변경 전체가 커밋에 섞이지 않았는지 확인한다. 분리가 불가능한 충돌이면 해당 파일의 작업을 멈추고 구체적인 충돌만 알린다.

## 3. 단계별 실행 문서

| 순서 | 실행 계획 | 독립적으로 확인할 결과 | 선행 |
| --- | --- | --- | --- |
| A | [실행 토대·맵·군집 이동](2026-09-16-ssalmuk-a-foundation-world.md) | 검증을 실행할 수 있고, GameStart로 생성된 지상 몬스터가 장애물을 돌아 밀집하는 장면 | 없음 |
| B | [AI·검 전투·사망과 재시작](2026-09-16-ssalmuk-b-ai-combat.md) | low AI가 회피·돌파하고 검으로 싸우며 죽으면 결과·새 판으로 연결 | A |
| C | [성장·보상·무기 4종](2026-09-16-ssalmuk-c-growth-weapons.md) | 전투 중 선택·누적 보상·무기별 무한 강화와 4종 동시 전투 | B |
| D | [무한 스폰·연출·통합 검증](2026-09-16-ssalmuk-d-integration.md) | 공중·보스·모든 개체 보존·연출·빌드 실행까지 연결 | C |

각 단계의 완료는 그 단계 결과만 뜻한다. A의 군집 장면이나 B의 검 전투를 전체 게임 완료로 표시하지 않는다. 패키지 연결 실패와 게임 규칙 실패를 분리하며, 막히지 않은 문서·순수 모델 작업은 계속할 수 있다.

## 4. 파일·이름·공통 계약

새 게임 코드는 `Assets/_SsalMuk/` 아래에 둔다. 하위 계획의 `Core/`, `Presentation/`, `Unity/`, `Editor/`, `Tests/`, `Content/`, `Resources/`, `Scenes/`는 이 루트에 대한 경로다. 같은 Files 그룹에서 파일명만 이어 적은 항목은 직전에 명시한 디렉터리를 사용한다. 각 C# 파일의 주요 타입과 파일명을 일치시키고 Unity가 생성한 `.meta`도 함께 관리한다.

| 경계 | 이름 공간 / 파일 루트 | 참조 |
| --- | --- | --- |
| 모델과 규칙 | `SsalMuk.Core`, `Core/` | UnityEngine 없음 |
| Presenter와 View 계약 | `SsalMuk.Presentation`, `Presentation/` | Core |
| Unity 연결과 표시 | `SsalMuk.Unity`, `Unity/` | Core, Presentation, UnityEngine, uGUI/InputSystem |
| 에디터 검증·에셋 생성 | `SsalMuk.Editor`, `Editor/` | Unity, TestRunner API, UnityEditor |
| 규칙 테스트 | `SsalMuk.Tests`, `Tests/EditMode/` | Core, Presentation, NUnit |
| 런타임 테스트 | `SsalMuk.Tests`, `Tests/PlayMode/` | 위 런타임 코드, Unity Test Tools |

계획의 코드 블록은 지정된 테스트 클래스의 메서드 또는 해당 파일에 작성할 핵심 코드다. 테스트에는 `NUnit.Framework`, 필요한 경우 `System`, `System.Linq`, `System.Numerics`, `SsalMuk.Core`, `SsalMuk.Presentation`을 가져온다. 예시의 이름은 아래 작업에서 정의하며, 프로젝트에 이미 있는 API라고 가정하지 않는다.

| 기본 타입 | 정의 작업 | 계약 |
| --- | --- | --- |
| `DVec2` | A2 | `double X/Y`, 생성자 `(double x, double y)`, 벡터 산술, `Length`, 안전한 정규화 |
| `ChunkCoord` | A2 | `long X/Y`, 생성자 `(long x, long y)`, 값 동등성·해시 |
| `WorldPosition` | A2 | 청크 좌표 + 내부 DVec2. `FromLocal(DVec2)`는 원점 청크 기준 정규화. 초기 청크 크기는 32 월드 단위 |
| `UnitKind` | A2 | `Player`, `Normal`, `Air`, `Boss` |
| `WeaponKind` | A2 | `Sword`, `Spear`, `Axe`, `Fireball` |
| `UpgradeKind` | C1 | `Damage`, `Copies`, `Repeats`, `Speed`, `Range` |
| `ExperienceState` | A3 | `Grounded`, `Attracting`, `Collected`; 지급 후 Collected 레코드 정리 |
| `ExperienceTier` | C1 | `Green`, `Blue`, `Red`; 실제 BigInteger 값과 설정 구분선으로 결정 |
| `RunId` | A2 | `Guid`; 판마다 새 값 |
| 유닛·경험치·Offer ID | A2 / C1 / C2 | 판 안의 양수 `long`, 재사용하지 않음. 외부 명령에는 RunId 포함 |
| `RunPhase` | A5 | `MainMenu`, `Loading`, `BuildingWorld`, `CreatingPlayer`, `CreatingEnemies`, `Running`, `Results`, `Disposed` |

`Tests/Shared/RunTestRig.cs`는 A4에서 월드·이동 실제 서비스를 조립하는 테스트 편의 코드로 시작하고 이후 작업이 기능을 추가한다. 테스트 전용 소형 asmdef `SsalMuk.Tests.Shared`로 EditMode·PlayMode에서 공유한다. 결과를 흉내 내는 가짜 전투 엔진을 만들지 않는다.

| 테스트 편의 API | 추가 작업 | 의미 |
| --- | --- | --- |
| `RunTestRig.Create(int seed = 1234, bool scheduledSpawns = false)` | A4, D1 | 기본 정의와 장애물 확률 0인 실제 생성기·월드·이동을 조립. 이후 구현된 서비스도 여기 연결. 일정 스폰 연결은 D1에서 추가 |
| `long Spawn(UnitKind kind, DVec2 position, double health = 10)` | A4 | 실제 팩토리로 생성. 테스트 체력은 생성 정의 복사본으로 지정 |
| `void PlacePlayer(DVec2 position)` | A4 | 테스트 배치를 지정하고 공간 색인을 함께 갱신 |
| `void Advance(double seconds)` | A4 | 테스트 간격 0.02초의 정수 배만 허용하고 실제 고정 주기 경로 호출 |
| `UnitModel Unit(long id)`, `PlayerModel Player` | A4 | 실제 모델 읽기. 테스트에서 직접 체력·상태 필드를 바꿔 통과시키지 않음 |
| `long DropXp(DVec2 position, BigInteger amount)` | B1 | 실제 경험치 저장 경로로 배치. 수집과 레벨업은 C1에서 연결 |
| `bool Hit(long targetId, double amount)` | B2 | 현재 시계와 고유 HitKey로 실제 DamageService에 피해 요청 |
| `OfferSnapshot CurrentOffer`, `bool Choose(long offerId, int slot)` | C2 | 읽기 전용 제시와 실제 명령 큐. bool은 큐 수락이며 적용은 다음 주기 |
| `void GrantExperience(BigInteger amount)` | C1 | 같은 ProgressionService에 테스트 입력을 주입 |
| `void Equip(WeaponKind kind)`, `void Upgrade(WeaponKind kind, UpgradeKind upgrade)` | C1 | 실제 무기 획득·강화 서비스에 테스트 입력을 주입 |
| `RunModel Run`, `RunResult Result` | A5 / B4 | 실제 판 상태와 종료 스냅샷 |
| `Task RestartAsync()` | B4 | 실제 coordinator를 통해 새 판 생성. 테스트에서만 scene loader 대체 |

몸체·공격·시간의 임시 수치는 `DevelopmentDefaults.asset`에 모은다. 처음부터 최종 밸런스로 기록하지 않는다. 단위 테스트에는 결과를 판단할 수 있는 작은 고정값을 전달한다.

초기 연결을 재현할 개발 프리셋은 다음 값으로 시작한다. 이 표는 구현에 필요한 출발값이며 플레이테스트에서 조정한다.

| 유닛 | 체력 | 이동 속도 | 몸 반경 | 접촉 피해 | 드롭 경험치 |
| --- | --- | --- | --- | --- | --- |
| 플레이어 | 100 | 3 | 0.28 | 없음 | 없음 |
| 일반 | 10 | 1.5 | 0.26 | 5 | 1 |
| 공중 | 6 | 6 | 0.22 | 5 | 1 |
| 보스 | 600 | 1.1 | 0.75 | 15 | 30 |

| 무기 | 기본 피해 | 기본 범위 | 주기 | 판정 시간·형태 |
| --- | --- | --- | --- | --- |
| 검 | 8 | 1.6 | 1초 | 0.25초 휘두르기, 60도 |
| 창 | 10 | 2.2 | 1.2초 | 0.18초 찌르기, 폭 0.35 |
| 도끼 | 6 | 1.8 | 1.6초 | 0.6초 회전, 날 반경 0.25 |
| 파이어볼 | 6 | 폭발 반경 0.8 | 1.4초 | 속도 8, 투사체 반경 0.1, 수명 3초 |

- 초기 무기 복사본·연속 횟수는 각각 1, 좌우 간격은 0.25, 도끼 추가 위상 간격은 20도, 반복 구간은 묶음 주기의 60%다.
- 짧은 피격 무적 0.25초, 빨간 점멸 0.10초, 팽창 1.15배 후 0.12초 내 복귀, 넉백 기준 거리 0.3·시간 0.15초로 시작한다.
- 보행은 기울기 8도, 높이 0.08, 좌우 한 주기 1.8회/초다. 실제 몸 반경과 분리한다.
- 경험치 테스트 설정은 흡수 반경 1.5, 비행 속도 6, 접촉용 구슬 반경 0.08이다. 값 1~4는 초록, 5~24는 파랑, 25 이상은 빨강으로 표시하되 실제 값을 바꾸지 않는다. 시각 검증에 1·5·25 구슬을 함께 배치하며 몬스터별 드롭량은 위 표를 유지한다. 이 설정은 최종 밸런스가 아니다.
- AI는 주변 적 8·경험치 10의 거리에서 탐색하고 16방향을 비교한다. 예상 접촉 0.35초 이내는 긴급 위험, 방향 차단 비율 0.75 이상은 돌파 진입, 0.35 미만이 0.35초 유지되면 해제다. 돌파 방향 최소 유지 0.5초, 진행 없음 재검토 1초로 시작한다.
- 초기 일반 몬스터는 12마리다. 이후 일반 생성률은 초당 `1+t/120`, 공중은 20초마다 `8+floor(t/120)`마리, 보스는 300초마다 1마리다. 생성 시 체력·피해 배율은 `1+t/300`이며 t는 생존 초다. 플레이어 체력·피해에는 이 난이도 배율을 적용하지 않는다.
- 기본 테스트 Rig는 플레이어만 배치하고 일정 스폰도 끈다. 실제 GameStart와 통합 시나리오는 위 초기 생성과 일정을 사용한다. 고립된 단위 테스트를 일반 플레이의 증거로 대신하지 않는다.

## 5. 공통 작업·검증 루프

각 작업의 체크박스는 테스트 작성 → 예상 실패 확인 → 필요한 코드 작성 → 관련 검증 → 한 작업 커밋 순서다. 미구현 단계의 실패는 예상한 컴파일 오류 또는 특정 단언 실패여야 하며, Editor 연결 실패를 기능 테스트의 정상적인 실패로 대신하지 않는다.

검증 진입점 A1을 만든 뒤 아래 형식을 사용한다. 아직 이 스크립트가 존재하지 않는 현재 시점에 실행했다고 기록하지 않는다.

```powershell
& ./tools/validation/validate.ps1 -Mode EditMode -Filter 'SsalMuk.Tests.WorldGenerationTests'
& ./tools/validation/validate.ps1 -Mode PlayMode -Filter 'SsalMuk.Tests.SessionLifecycleTests'
& ./tools/validation/validate.ps1 -Mode Smoke -Scenario 'EndToEnd'
& ./tools/validation/validate.ps1 -Mode Stress -Scenario 'PersistentWorld30m'
```

실행 중 Editor가 있으면 같은 프로젝트의 Editor 검증 연결을 사용한다. 결과는 `Logs/Validation/<실행 ID>/`에 두고 이번 소스 해시·테스트 수·필수 시나리오·오류를 판독한다. 실패하면 해당 계약을 해결한 뒤 종속 작업을 진행한다. 관련 검증이 통과한 후 이유 없이 전체 테스트를 반복하지 않는다.

각 작업의 커밋 전에 `git diff --check`, 변경 목록과 `.meta`를 확인한다. 깨끗했던 작업 전용 파일은 명시한 경로로 스테이징하고, 기존 사용자 변경이 있는 설정 파일은 추가한 부분만 스테이징한다. `git add .`나 저장소 전체 복구를 사용하지 않는다. 커밋 후 사용자 변경이 그대로 남았는지 확인한다.

## 6. 하네스 요구사항 추적

| 요구 ID | 구현·검증 작업 |
| --- | --- |
| H01, H02 | A5, B4 |
| H03 | B3, C2, D1 |
| H04 | A3 |
| H05 | A4 |
| H06 | A3, D2, D3 |
| H07, H08 | B1 |
| H09 | B1, D2 |
| H10 | B3, C3, D2 |
| H11, H12, H13 | B3, C1, C3 |
| H14, H15 | C2 |
| H16 | C1, D3 |
| H17, H18 | D1 |
| H19 | A4, B2, D1 |
| H20 | B2 |
| H21 | D2 |
| H22 | B2, C1 |
| H23, H24 | B4, C2, D3 |
| H25 | D2 |
| H26 | B4, C2, D2 |
| H27 | D1, D3 |
| H28 | D3 |
| H29 | A3, B1, C1, D2, D3 |
| H30 | C1, D2, D3 |

## 7. 실행 완료 조건

- [ ] A1~A5: 실행·맵·군집 장면을 확인하고 단계 커밋 완료.
- [ ] B1~B4: low AI·검 전투·사망·재시작을 확인하고 단계 커밋 완료.
- [ ] C1~C3: 선택 기회 누적·무기별 성장·무기 4종을 확인하고 단계 커밋 완료.
- [ ] D1~D3: 무한 스폰·개체 보존·연출·빌드 실행과 측정 결과를 확인하고 단계 커밋 완료.
- [ ] H01~H30의 실제 결과·미검증 항목·성능 측정 범위가 구분되어 있음.

계획 작성 단계에서는 위 실행 항목을 체크하지 않는다. 실행 도중 계약이 바뀌면 기획·구조·계획·검증 기준을 함께 갱신하며, 수치 조정만으로 해결되지 않는 결함을 완료로 넘기지 않는다.
