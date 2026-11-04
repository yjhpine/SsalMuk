# 세션 C: 반복 조회와 월드 표시 비용

- 기준: `dcda7a043773680005562f7ce3b3e659cf8d8801`.
- 격리 폴더: `C:/Users/ace21/.codex/worktrees/ssalmuk-c-performance/SsalMuk`.
- 목적: 게임 결과를 유지하면서 경험치 수집 조회의 임시 할당과 표시 ID 집합 복사를 줄인다.
- 범위: `WorldQuery`, `ExperienceCollector`, `WorldPresenter`의 작은 변경 2건. 아이템·상자·합성·피해 시스템 및 조립 코드는 수정하지 않는다.
- 유지 계약: 일반 상한 500, 원거리 일반/보스/경험치 보존, 공중 퇴장, 충돌/피격/넉백, AI 목표·주기, 경험치 ID/값/흡수/접촉 지급, 표시 보간·히스테리시스.
- 원본 시작 상태: 추적 파일 변경 없음. 미추적 복구 씬 `0 (3)`, `0 (4)`, `0 (5)`의 3쌍과 `ProfilerCaptures/`를 보존했다. 격리 폴더 시작 상태는 깨끗했다.

## 먼저 확인한 자료와 병목 후보

과거 문서는 과거 소스와 조건의 기록이다. 이번 작업에서 해당 Unity 캡처를 다시 실행하거나 원시 기록을 재검증하지 않았다.

| 경로 | 근거와 판단 |
| --- | --- |
| `docs/development/HANDOFF.md`의 2026-09-30 성능 기록, 기존 `log.md:14-45` | 과거 검 계산 300/500마리 30.80/41.77 → 4.64/6.05ms, GC.Alloc 6,408.1/7,875.3 → 37.3/37.7회. 후반 공격과 초기 표시 생성이 컸지만 현재 수치·아이템과 다른 부하다. 이번에는 재수정하지 않는다. |
| `docs/development/HANDOFF.md:124` | 과거 500적/1000XP AI 단독 경계/중앙 조회 개선이 기록됨. 현재 AI 비용의 실측 증거로 사용하지 않는다. |
| `Core/World/SpatialIndex.cs:51-77` | 최근접은 청크 목록 생성·정렬, 반환형 원형 조회는 List 및 읽기 전용 래퍼 생성. 이미 셀별 HashSet, 지역 청크 한정, 호출자 버퍼 경로가 있다. 최근접 동률·필터 호출 순서·재진입 위험을 피하고 기존 버퍼 경로만 재사용한다. |
| `Core/Progression/ExperienceCollector.cs:28-31` | 매 고정 주기에 경험치 조회 결과를 임시 목록으로 받아 HashSet과 정렬 목록에 복사. **수정 대상:** 기존 정렬 목록을 조회용으로도 재사용한다. |
| `Core/Movement/MovementSystem.cs:63-106`, `Core/Movement/CrowdSolver.cs` | 매 주기 ID 정렬·이동·군중 격자 재구축, 셀 경계 이동 시 공간 색인 HashSet 생성 가능. 표본/처리 순서와 충돌 계약에 민감하므로 이번에는 변경하지 않는다. |
| `Core/AI/CollectState.cs`, `Core/AI/LootRecovery.cs`, `Core/AI/ChestSeeker.cs` | 경험치 반환형 조회 사용이 남아 있다. 목표·상자 작업과 연결되므로 이번 수집기 개선 범위에 포함하지 않는다. |
| `Presentation/WorldPresenter.cs:15-89` | 전체 보존 유닛/XP 순회 및 매 프레임 표시 ID 재삽입. **수정 대상:** 완료한 두 표시 집합을 교환해 복사만 제거. 거리 판정·보간·순회 순서는 유지한다. |
| `Unity/Views/ViewPool.cs`, `Unity/Views/ExperienceView.cs:22-44` | 풀 자체는 active/free 재사용. Return/Rent/Bind에서 ResetVisuals/Draw가 겹쳐 XP 메시 재생성 가능. 실제 Unity 측정·풀링/색/반경 회귀 검사가 필요하므로 이번에는 변경하지 않는다. |

## 구현 선택

1. `WorldQuery.QueryExperienceCircle(position, radius, List<long>)` 구체 클래스 오버로드를 추가한다. 기존 반환형 API와 `IWorldQuery`는 그대로 둔다. 수집기는 기존 `orderedIds`에 조회하고, attracting ID와 합친 뒤 같은 ID 오름차순으로 처리한다. 콜백·수집·이동 전 후보 스냅샷을 완성하므로 중복 지급이나 순서 변경이 없다.
2. `WorldPresenter`의 두 표시 집합 쌍을 기존 확정 위치에서 교환한다. 프레임 시작의 다음 집합 초기화, 판 변경 초기화, 모든 Show 호출 후/EndFrame 전 확정은 유지한다. Show 도중 예외는 이전 표시 집합을 유지하고 EndFrame 예외는 이번 표시 집합을 유지한다.

## 검증과 독립 측정

2026-11-04, Windows x64 / .NET 10.0.11 / SDK 10.0.400, Release, tiered compilation 비활성, workstation GC. 시나리오마다 2,000회 예열 후 2,000회씩 5배치의 중앙값이다. GC 바이트 계수기는 별도 4,096바이트 배열을 실제로 관측해 보정했다. 다른 채팅도 작업 중인 호스트의 짧은 순차 측정이므로 시간 차이는 참고값이며 Unity 성능으로 환산하지 않는다.

| 시나리오 | 전 ms/호출 | 후 ms/호출 | 전 B/호출 | 후 B/호출 |
| --- | ---: | ---: | ---: | ---: |
| 수집기 XP 0 | 0.000121 | 0.000117 | 56.208 | 24.208 |
| 수집기 XP 300 | 0.039365 | 0.034643 | 8448.624 | 24.208 |
| 수집기 XP 500 | 0.061829 | 0.056712 | 8448.624 | 24.208 |
| 표시 일반 300 + XP 200 | 0.062080 | 0.053532 | 472 | 392 |
| 표시 일반 500 + XP 200 | 0.078472 | 0.073428 | 472 | 392 |

수집 측정은 시계 0, 생성 시각 1인 XP를 흡수 반경 안 0.75 거리에 두어 조회만 반복한다. 실제 비행·지급 비용을 포함하지 않는다. 표시 측정은 반경 60, 보간 1, 지형 9청크, 게임 시간 진행 없음, 계수만 하는 View다. 일반 적 외에 플레이어 1개가 더 있다. 양쪽의 개체·경험치 수와 표시 호출수가 일치했다(각 10,000프레임, 유닛 3,010,000/5,010,000회, XP 각각 2,000,000회). 수집기 XP의 ID·위치·상태도 보존했다. 남은 할당을 0이라고 주장하지 않는다.

- **기준 실행:** `Logs/Validation/session-c-82134f3669b044efa4274b0ce826bd81/`의 `manifest.json`, `report.json`, `build.log`, `probe.log`. 신규 회귀 검사 포함 23개 중 21개 통과, `SessionCPerformanceTests.WarmCollectorAllocationDoesNotGrowWithNearbyExperience(300)`와 `(500)`만 예상대로 실패했다. 생산 코드는 기준 커밋과 같았다.
- **수정 후 실행:** `Logs/Validation/session-c-2f716ad03e04485cba7ed581c8066c34/`의 같은 파일. **23/23 통과**, 측정 오류 0, 컴파일 오류/경고 0. 기존 WorldStore 6개, ExperiencePickup 10개, WorldPresentation 1개, 새 할당 2개, 새 표시 계약 4개다. 측정 C#은 전후 동일하며 컴파일 입력 변경은 생산 코드 3파일뿐이다.
- **기준 소스 해시:** `8664990868062aa69cc33ae893742a4655c703dc74290bb58b869008cd780341`.
- **수정 후 소스 해시:** `a58493a79cb159e5263a11ab85e17b0ac18f4f915b63fc15ef723f6481a44acd`. 각 실행 전후 해시 동일, 개별 컴파일 입력 해시도 manifest에 보존했다.
- **Static:** `Logs/Validation/cbb369d3066049b0b314791e2f1bee07/`의 manifest/receipt/static.txt 통과. 결과 판독기 39개 확인과 PowerShell 문법 검사이며 Unity 게임 테스트 수는 0이다. 게임 검사가 통과했다는 증거로 합산하지 않는다. 새 측정 PowerShell 파일은 별도 파서 검사도 통과했다.
- **독립 코드 검토:** 조회·정렬·수집 중 제거, 표시 집합 확정 시점과 예외 의미에 중요한 문제 없음. `git diff --check` 통과.

독립 도구는 실제 Core/Presentation 및 선택한 기존/신규 테스트 소스를 .NET으로 컴파일한다. Unity Editor, Scene, 렌더러, GameCatalog 에셋을 실행하는 검사가 아니다. Unity 테스트의 실행 완료 증거로 사용하지 않는다.

도구는 이 5개 fixture의 지원 가능한 `[Test]`/`[TestCase]`만 실행하고, 지원하지 않는 생명주기/속성 및 빈 발견을 실패로 기록한다. 실행기 자체의 통과·실패·매개변수·생명주기 거절도 양성/음성 확인했다. 생성 프로젝트·컴파일 산출물·원시 증거는 Git 제외 경로 `Logs/Validation/`에만 둔다. 재실행 예:

```powershell
./tools/performance/measure-core.ps1 -NUnitPath 'C:/Users/ace21/Desktop/SsalMuk/Library/PackageCache/com.unity.ext.nunit@d8c07649098d/net40/unity-custom/nunit.framework.dll' -Label after
```

NUnit 경로는 이 PC에서 확인한 읽기 전용 참조다. 다른 환경에서는 해당 Editor 프로젝트의 기존 NUnit DLL 경로를 지정한다. 패키지 다운로드나 Unity 실행을 하지 않는다.

## 변경 파일

- 생산 코드: `Assets/_SsalMuk/Core/World/WorldQuery.cs`, `Assets/_SsalMuk/Core/Progression/ExperienceCollector.cs`, `Assets/_SsalMuk/Presentation/WorldPresenter.cs`.
- 새 검사: `Assets/_SsalMuk/Tests/EditMode/SessionCPerformanceTests.cs`, `SessionCWorldPresentationTests.cs` 및 각각 `.meta`.
- 독립 진단: `tools/performance/measure-core.ps1`, `tools/performance/CoreProbe.cs`.
- 통합 기록: 이 문서. 원시 검사 결과는 커밋하지 않는다.

## 주 채팅의 순차 확인

주 채팅만 원본 Editor를 운용한다. 통합 후 `tools/validation/validate.ps1`로 다음 작은 필터를 차례대로 실행하고 최신 manifest/receipt/XML과 통합 소스 해시를 확인한다.

1. EditMode `SsalMuk.Tests.SessionCPerformanceTests` — Unity의 보정된 GC.Alloc 이벤트 수로 조회 할당 회귀 확인.
2. EditMode `SsalMuk.Tests.ExperiencePickupTests`, `SsalMuk.Tests.WorldStoreTests` — 접촉 1회 지급, 이동/시간 경계, 원거리 보존과 색인 결과.
3. EditMode `SsalMuk.Tests.SessionCWorldPresentationTests`, `SsalMuk.Tests.WorldPresentationTests`, `SsalMuk.Tests.MonsterStabilityTests` — 히스테리시스, 판 변경, 예외 시 확정 시점과 보간.
4. PlayMode `SsalMuk.Tests.PersistentWorldViewTests`, `SsalMuk.Tests.ExperienceVisualTests` — 실제 표시 회수 후 재등장과 XP 시각 표현.

짧은 비교 시나리오: 같은 설정에서 300/500 일반과 XP 200개, 표시 반경 60으로 각각 예열 후 짧게 비교한다. 전후 유닛/XP 수·표시 호출 수·히스테리시스 결과를 먼저 대조하고 CPU 시간과 할당을 비교한다. 실제 화면에서는 22→24 경계를 왕복해 표시만 회수되는지 확인한다. 기존 3연타·옛 성장 조건과 혼합하지 않으며 전체 FPS·30분 생존 보증으로 해석하지 않는다.

## 미검증 및 통합 경계

- Unity 컴파일/Mono 할당 계수기, 실제 GameObject 풀·렌더링·프레임 시간, 실제 카탈로그 조건, 장시간 플레이와 빌드는 실행하지 않았다.
- 원본 Editor/프로파일링/새 Editor, 전체 검사/새 빌드, 푸시/병합/다른 세션 커밋 가져오기는 하지 않는다.
- 기존 `log.md`, 공용 HANDOFF, 주 채팅 소유 파일은 변경하지 않는다.
- 코드·검사·독립 도구 완료 커밋: `2be558cc2b1527959a2a7aa08db5637c4029fe7b` (`perf: reuse experience query and visible-set buffers`). 브랜치 `codex/ssalmuk-c-performance`. 이 문서만 후속 기록 커밋으로 추가한다.
- 최종 확인에서 실제 컴파일 입력 173개의 해시와 실행 후 소스 해시가 동일했다. 검증 뒤 생산 코드와 테스트는 변경하지 않았다.
