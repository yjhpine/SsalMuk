# 세션 D 독립 코드 리뷰

- 목적: 안정된 기존 시스템의 경계 조건, 수명, 정지/재개, 새 판, 정확한 보상, 중복 입력, 숫자 범위, 자원 정리를 읽기 중심으로 검토한다.
- 기준 커밋: `dcda7a043773680005562f7ce3b3e659cf8d8801`.
- 작업 폴더: `C:/Users/ace21/.codex/worktrees/ssalmuk-d-review/SsalMuk`.
- 결과: 정적 코드 추적으로 확인한 **P2 1건**. 아래 항목은 기준 커밋에 대한 지적이며 주 채팅의 이후 수정 상태를 판정하지 않는다.
- 변경 파일: 이 문서만. 프로덕션 코드, 테스트, 기획, 수치, AI 행동은 수정하지 않았다.
- 완료 커밋: 본문 검토 완료 후 로컬 커밋 식별자를 기록한다.

## 1. 검토 조건

`create_worktree`에 원본 절대 경로, `ref=dcda7a0`, `name=ssalmuk-d-review`를 지정해 관리 작업 폴더를 만들었다. 새 폴더는 기준 HEAD의 detached 상태이며 시작 시 추적/비추적 변경이 없었다. 원본에는 기존 복구 씬 `0 (3)`, `0 (4)`, `0 (5)` 및 `ProfilerCaptures/`가 있었으며 접근 시 읽기만 수행했다. 워크트리 등록에 필요한 Git 메타데이터 외에 원본 작업 파일을 쓰지 않았다.

사용자가 이 채팅에서 `fast 킴 진행`이라고 지시한 뒤 본 검토를 시작했다. Fast 활성 여부를 직접 반환하는 지원 도구는 확인하지 못했으며, 프롬프트만으로 활성 상태를 검증했다고 주장하지 않는다. 사용량은 작업 시작 52%, 검토 중 45% 잔여를 확인했고 중지 기준 3%에 도달하지 않았다.

현재 사용자 지시와 기준 커밋의 `AGENTS.md`를 우선했다. 기획, 구조, 하네스, 구현 계획, `HANDOFF.md`의 관련 계약을 읽었으며 과거 HANDOFF의 아이템 미승인/미구현 설명보다 최신 승인 및 실제 기준 소스를 따랐다. 아이템 UI 미완료 및 이를 전제로 한 구 UI 테스트는 결함으로 집계하지 않았다.

## 2. 지적 사항

### D-01 · P2 · 고유 효과 처리 순서가 실제 적중 시각과 달라진다

**주 위치:** `Assets/_SsalMuk/Core/Items/ItemEffectSystem.cs:67-79`.

**계약:** `docs/superpowers/specs/2026-11-04-items-and-synthesis-design.md:84`는 고유 효과를 가장 오래된 유효 직접 적중 이벤트부터 처리하도록 정한다.

**근거:**

- `ItemEffectSystem.cs:56-61`은 유효한 무기 적중을 수신 순서대로 `hits`에 넣는다.
- `ItemEffectSystem.cs:67-79`는 적중 시각 정렬 없이 그 순서로 흡혈/번개/처치 폭발을 발동하고 계열별 마지막 발동 시각을 저장한다.
- `RunSimulation.cs:74`는 무기를 보유 순서대로 하나씩 처리한다. `Assets/_SsalMuk/Core/Progression/WeaponInventory.cs:18`에서 획득한 무기를 목록 끝에 추가한다.
- 근접은 `Assets/_SsalMuk/Core/Combat/MeleeAttack.cs:61`에서 이번 판정 구간의 끝 `hi`를 적중 시각으로 기록한다. 파이어볼은 `Assets/_SsalMuk/Core/Combat/ProjectileSystem.cs:77`에서 구간 안의 보간 충돌 시각을 사용한다.
- 그러므로 먼저 갱신한 검의 늦은 적중이, 나중에 갱신한 파이어볼의 이른 적중보다 먼저 큐에 들어갈 수 있다.

**도달 가능한 재현 조건:**

1. 검으로 시작한 뒤 파이어볼과 흡혈 송곳니를 획득한다. 흡혈 간격은 이미 끝나 있고 플레이어 체력은 최대보다 충분히 낮다.
2. 같은 고정 주기 `[1.00, 1.02]`에서 검이 적 A를 `1.02`에 맞히고, 이미 날아가던 파이어볼은 서로 다른 적 B를 `1.005`에 맞히도록 배치한다.
3. A와 B는 각 직접 적중 직전 살아 있고 서로 다른 공격 키로 실제 체력 피해가 수락되어야 한다. A의 실제 체력 감소를 1, B의 감소를 10으로 잡는다. B에 대한 공격 스냅샷 피해가 10 이상인 강화 조건을 사용한다.
4. 검→파이어볼 순회로 큐에는 `A(1.02), B(1.005)`가 들어간다. 효과 처리 후 회복량을 비교한다.

**기대/현재:** 흡혈 송곳니의 10%, 1회 상한 2 기준으로 기대 회복은 먼저 적중한 B의 `1.0`이다. 현재 분기에서는 A의 `0.1`을 회복하고 `lastLife=1.02`를 저장하여 B를 제외한다. 번개와 처치 폭발도 같은 순서 문제로 발동 원점과 피해 기준 공격이 바뀔 수 있다. 이는 무기 획득 순서에 따라 전투 결과가 승인 계약과 달라지는 P2 문제다.

**검증 수준:** 코드 경로와 수치 분기를 두 검토자가 독립적으로 추적했다. 위 재현은 실행할 회귀 시나리오이며 Unity에서 실행한 관측 결과가 아니다.

**최소 수정 제안:** `ProcessHits` 전에 `CombatEvent.Time` 오름차순으로 안정 정렬한다. 같은 시각은 원래 수신 순서를 유지하도록 명시적인 순번을 사용할 수 있다. 계열별 한 번의 발동, 추가 적중 미적립, 수량 변경 후 간격 보존은 유지한다. 다른 무기·AI 순회 규칙 변경은 필요하지 않다.

**필요한 테스트:**

- EditMode `SsalMuk.Tests.ItemEffectTests`: `1.02 → 1.005` 순으로 수신한 유효 적중의 회복량, 번개/폭발 원점, 같은 시각의 순서, 간격 내 추가 발동 거절을 확인한다. 기존 테스트는 서로 다른 시각이 역순으로 한 큐에 들어오는 경우를 다루지 않는다.
- PlayMode `SsalMuk.Tests.MultiWeaponCombatTests`: 실제 검→파이어볼 갱신으로 시각 역전을 구성하고 위 결과를 확인한다.
- 관련 회귀 필터: EditMode `SsalMuk.Tests.DamageTests`, `SsalMuk.Tests.DeathDropTests`, `SsalMuk.Tests.ContactDamageTests`.

## 3. 추가로 추적한 범위

아래는 코드 및 기존 테스트를 읽어 보호 경로를 확인한 범위다. 테스트 실행 통과나 해당 시스템 전체 무결성을 뜻하지 않는다.

| 범위 | 정적 확인 내용 | 실행 검증 필터 |
| --- | --- | --- |
| 세션/시계/새 판 | 진행 중 시작 요청 병합, 결과 버튼 잠금, 정지 시 시계/시뮬레이션 중단, 결과 복사 후 정리, 새 스코프 생성, 잘못된 시각 및 정수/실수 범위 거절 | EditMode: `SsalMuk.Tests.RunCoordinatorTests`, `SsalMuk.Tests.RunClockTests`, `SsalMuk.Tests.RunResultTests`, `SsalMuk.Tests.UnitySceneLoaderTests` |
| 경험치/성장/기존 보상 | BigInteger 비용과 소수 잔여, 일괄 레벨 계산, 개수 제한 전 검증, RunId/제안 ID/소유권 버전/슬롯 검증, 중복 선택 거절, 사망 시 초기화 | EditMode: `SsalMuk.Tests.ProgressionTests`, `SsalMuk.Tests.RewardTests`, `SsalMuk.Tests.NumericRangeTests`, `SsalMuk.Tests.GrowthLimitTests`, `SsalMuk.Tests.ExperiencePickupTests` |
| 상자/아이템/합성 | 보상 FIFO와 이전 판 거절, 합성의 소유권/버전/재료 및 난수 소비 전 검사, 18개 레시피, 보호막 잔량/재충전, 무적 우선, 파생 피해 재발동 방지와 사망 보상 | EditMode: `SsalMuk.Tests.ItemsIntegration.ItemRewardTests`, `SsalMuk.Tests.ItemsIntegration.ItemStatTests`, `SsalMuk.Tests.ItemInventoryTests`, `SsalMuk.Tests.ItemEffectTests` |
| 표시/풀/입력 | View와 모델 수명 분리, 세대/RunId에 따른 이전 콜백 거절, 구독 해제, 메시/Animator 그래프 정리 경로, AUTO 예약 철회와 수동 예약 보존 | EditMode: `SsalMuk.Tests.WorldPresentationTests`, `SsalMuk.Tests.LevelUpPresenterTests`, `SsalMuk.Tests.AutoUpgradePresenterTests` |
| 버프/전투/스폰 | 버프 만료와 재획득, 접촉 경험치 지급, 공격 키 중복 방지와 완료 키 정리, 처치 한 번 지급, 일반 상한과 예약 정리, 웨이브 추적, 공중 무보상 퇴장 | EditMode: `SsalMuk.Tests.PowerupTests`, `SsalMuk.Tests.SpawnDirectorTests`, `SsalMuk.Tests.SurroundWaveTests`, `SsalMuk.Tests.AirDepartureTests` |

주요 파일은 `Core/Session`, `Core/Items`, `Core/Progression`, `Core/Combat`, `Core/World/WorldStore.cs`, `SpatialIndex.cs`, `Core/Spawning`, `Presentation/WorldPresenter.cs`, `LevelUpPresenter.cs`, `Unity/Bootstrap`, `Unity/Views`와 관련 테스트다. 전체 AI/길찾기 알고리즘, 생성 지형의 모든 배치, 모든 유효 수치 조합을 전수 검토한 것은 아니다.

## 4. 결함으로 집계하지 않은 검증 공백

### 실제 씬 로딩 도중 취소/파괴

- `Assets/_SsalMuk/Unity/Bootstrap/UnitySceneLoader.cs:21-27` 및 `Core/Session/RunCoordinator.cs:34-46,77-78`: Battle 로드 중 `ReturnToMenuAsync()`를 호출하면 아직 MainMenu가 활성 상태라 메뉴 로드가 즉시 끝날 수 있다. 모델 세대 검사는 있지만 이미 시작된 Unity 로드 자체는 취소하지 않는다. 현재 UI에는 Loading 중 메뉴 복귀 버튼이 없어 정상 사용자 입력의 확정 버그로 집계하지 않았다.
- `Assets/_SsalMuk/Unity/Bootstrap/AppRoot.cs:34-45,94-97`: 로드 중 AppRoot를 파괴한 뒤 늦은 `sceneLoaded`가 전달될 때 새 AppRoot/판이 생성되는지는 실제 Unity teardown 순서에서 확인해야 한다. 현재 제품 종료 경로의 재현이 확정되지 않아 결함으로 단정하지 않았다.
- 새 통합 테스트: 실제 로더로 로드 중 메뉴 복귀/재시작 및 AppRoot 파괴를 수행해 최종 활성 씬, RunId, AppRoot/WorldView/RunScope 수를 확인한다. 기존 GateLoader 테스트만으로 실제 씬 상태를 보증하지 않는다. 기존 필터는 PlayMode `SsalMuk.Tests.SessionLifecycleTests`다.

### 실제 표시 및 자원 회수

예약된 수동/AUTO 명령이 있는 상태에서 ESC 정지·재개, WorldView 파괴 후 설정 변경, 반복 재시작 후 Mesh/PlayableGraph 잔존 여부는 실제 실행 확인이 남는다.

필요한 PlayMode 필터: `SsalMuk.Tests.FeedbackAndPoolTests`, `SsalMuk.Tests.PersistentWorldViewTests`, `SsalMuk.Tests.AutoUpgradeLiveTests`, `SsalMuk.Tests.MenuSettingsTests`, `SsalMuk.Tests.MapFeature.PlayerAnimationTests`, `SsalMuk.Tests.DeathRestartTests`.

## 5. 실제 수행 검증 및 한계

- 자체 워크트리에서 `pwsh -NoLogo -NoProfile -File tools/validation/validate.ps1 -Mode Static -ProjectPath 'C:/Users/ace21/.codex/worktrees/ssalmuk-d-review/SsalMuk'`를 실행했다.
- Static 결과 `Passed`, 실행 ID `fd9759a5b42d40ad8eb4b7f3ba4eb7ed`.
- 소스 해시 `e6780236434c6f012aec2c8b02af0bf07c5be88b5bc56665ffaf3833fe94e28b`가 manifest/receipt에서 일치하고, 프로젝트 경로와 기준 HEAD, Unity 버전 `6000.4.6f1`도 확인했다.
- `static.txt`의 결과 판독기 검사는 누적 **39개 통과**다. 검증 스크립트의 PowerShell 구문 검사도 통과했다. Static의 `testCount=0`은 Unity 테스트를 실행하지 않았다는 뜻이며 C# 컴파일이나 게임 테스트 39개 통과로 해석하지 않는다.
- 원시 증거는 자체 폴더의 Git 제외 경로 `Logs/Validation/fd9759a5b42d40ad8eb4b7f3ba4eb7ed/`에 있다.
- D-01의 실행 재현, EditMode/PlayMode, 실플레이, 전체 검사, 성능/장시간 검사, 빌드는 수행하지 않았다. 원본 또는 다른 폴더의 Editor에 명령을 보내지 않았다.
- 주요 지적의 참조 위치와 기재한 테스트 필터 31개 클래스의 존재를 확인했다. 문서 작성 뒤에도 소스 해시는 위 값과 같으며 기준 이후 코드/테스트 변경은 없다. 문서만 스테이징해 diff를 검사한 뒤 로컬 커밋한다. 푸시·병합·다른 세션 커밋 가져오기는 하지 않는다.

## 6. 주 채팅 인계

주 채팅은 먼저 현재 소스에서 D-01의 처리 순서가 이미 수정되었는지 확인한다. 남아 있으면 최소 수정과 위 표적 검증을 수행하고, 실행하지 않은 필터를 통과로 기록하지 않는다. 세션 D는 리뷰 문서만 로컬 커밋하며 수정 구현이나 통합 완료를 주장하지 않는다.
