# 세션 B — EditMode 사각지대와 실패 조사

- 날짜: 2026-11-04.
- 기준 커밋: `dcda7a043773680005562f7ce3b3e659cf8d8801`.
- 작업 폴더: `C:/Users/ace21/.codex/worktrees/ssalmuk-b-tests/SsalMuk` (`create_worktree`로 생성).
- 시작 상태: 기준 커밋의 깨끗한 detached HEAD. 원본의 복구 씬 3개와 `ProfilerCaptures/`는 읽기 확인만 했고 수정하지 않았다.
- 사용자의 이 채팅 지시 `fast킴 진행`을 받은 뒤 본 작업을 시작했다. 지원 도구로 Fast 설정을 독립 조회한 것은 아니다.
- 목적: 현재 숲/도서관의 탐색·보존, 풀 대여 세대, 비동기 세션 오류 격리를 검증하는 행동 테스트를 추가한다.
- 범위: 신규 테스트 3개 파일과 각각의 `.meta`, 이 문서만 변경. 게임 기획·수치·AI·프로덕션·아이템 테스트는 변경하지 않았다.
- 상태: 작성·정적 확인 완료, Unity 실행 및 RED/GREEN 확인 미완료. 아래 예상 결과는 실행 결과가 아니다.

## 변경 파일과 잡으려는 결함

| 파일 (`Assets/_SsalMuk/Tests/EditMode/`) | 사례 수 | 보호하는 행동 | 기준 코드의 예상 |
| --- | ---: | --- | --- |
| `ThemedNavigationContractTests.cs` | 4 | 숲/도서관의 가구 우회·청크 횡단 경로를 탐색하는 중 지형 캐시를 제거해도 전체 이동 선분이 안전함. 역순 재생성·부분/전체 제거 뒤 지형·일반/보스·지상/비행 경험치와 공간 색인 보존 | 4 통과 예상, 미실행 |
| `ViewPoolLeaseContractTests.cs` | 4 | 활성 ID 재대여는 현 표시를 유지. 같은 ID 재사용 시 이전 세대를 거절. 다른 RunId의 동일 ID/세대 토큰을 거절. 활성/반환 객체를 모두 정리 | 4 통과 예상, 미실행 |
| `RunCoordinatorFailureIsolationTests.cs` | 1 | 취소된 시작 작업의 늦은 메뉴 로드 실패가 새 판의 `LastError`를 변경하지 않음 | 1 실패 예상, 미실행 |

맵 테스트의 생성·경로 코드를 복제하지 않았다. 실제 생성된 장애물이 직선을 막고 경로가 청크를 넘는지 먼저 확인하며, 기존 `NavigationTests.AssertSafe`로 실제 이동용 sweep 쿼리를 대조한다. 기존 맵 일반 검사는 주로 `ChunkGenerator`, 도서관 검사는 중앙 통로에 집중해 현재 선택형 지형의 탐색 도중 캐시 제거를 놓치고 있었다. 보존 검사는 개체 수만이 아니라 기존 객체·위치·체력·경험치 값/비행 상태와 이전/현재 공간 색인을 확인한다.

기존 풀 검사는 다른 ID로 재사용하는 경우여서 ID 비교만으로도 이전 토큰 거절이 통과할 수 있었다. 새 검사는 같은 ID를 다시 빌린 후 이전 토큰이 현재 표시를 반환할 수 없는지 확인한다. 테스트 전용 빈 `PooledView`만 사용하고 상자 UI 구현에 의존하지 않는다.

## 우선 재현할 세션 오류 — 구현 담당에게 전달

위치: `Assets/_SsalMuk/Core/Session/RunCoordinator.cs:89`.

1. 첫 `LoadBattleAsync`가 실패하고 자동 복구 메뉴 로드 A가 대기한다.
2. `ReturnToMenuAsync`가 새 전환 세대를 만들고 메뉴 로드 B가 완료된다.
3. 새 시작이 정상적으로 `Running`에 진입하고 `LastError`는 비어 있다.
4. 취소된 작업의 메뉴 로드 A가 뒤늦게 실패한다.
5. 내부 `catch (Exception menuError)`는 세대/Disposed 확인 전에 `LastError`에 오류를 덧붙인다. 새 판의 시계·Run은 유지돼도 오류 상태가 오염된다.

새 테스트는 이 순서를 `TaskCompletionSource`로 제어한다. 기준에서 마지막 `LastError` 빈 값 단언이 ` / Stale menu load failed.`로 실패할 것으로 예상한다. 기존 `RunCoordinatorTests`의 메뉴 로더는 항상 즉시 완료여서 이 순서를 다루지 않는다. 실제 실패 실행 전에는 결함 재현 완료나 수정 완료라고 부르지 않는다. A/통합 담당이 재현한 뒤 해당 catch의 현재 전환 소유권 검사만 검토할 수 있도록 위치를 남겼으며, B에서는 구현을 수정하지 않았다.

## 추가 읽기 후보 — 미재현·미수정

- `Core/Navigation/NavigationService.cs:69,75`: 취소된 요청만 남으면 실행할 작업이 0개여서 요청 목록 정리 전에 반환한다. `RequestPath → Cancel → Advance`를 반복하면 취소 요청을 계속 보유할 수 있다. 공개 대기 수/최장 대기는 취소 요청을 제외한다. 내부 목록 크기를 고정하는 테스트는 추가하지 않았으며, C/탐색 담당의 후속 수명·메모리 재현 대상으로 남긴다.
- `Unity/Bootstrap/UnitySceneLoader.cs:21,23`: 메뉴에서 전투 로드를 시작한 직후 메뉴 복귀를 요청하면 현재 활성 씬이 메뉴라는 이유로 복귀가 즉시 완료될 수 있다. 진행 중인 전투 로드가 뒤늦게 활성 씬을 바꾸는지는 실제 PlayMode 확인이 필요하다. fake loader 기반 세션 테스트만으로 확정할 수 없다. 후속 시나리오는 두 요청 후 원래 시작 Task까지 완료시켜 `Coordinator.MainMenu`, 실제 활성 MainMenu, 남은 RunScope 없음이 동시에 유지되는지 보는 것이다. 이번에는 PlayMode 테스트를 추가하거나 실행하지 않았다.

## 기존 실패 기록 대조

원본 `Logs/Validation/`의 manifest/XML은 읽기 전용으로 조사했다. 과거 기록을 현재 B 작업 소스의 실행 결과로 사용하지 않는다.

| 과거 실행 | 실패 내용과 원인 대조 | 현재 판단 |
| --- | --- | --- |
| `aa16a47d4121436d8a4dcf9f2ea9b2e3` | 위/아래 벽에서 군중이 사라진 뒤에도 돌파가 벽 여유 약 0.651에 머문 2사례. HANDOFF의 안쪽 경로 재선정 수정과 일치 | 후속 `b2bfbf4484f94c4387d0cb5304b3e9bb` XML에서 같은 2사례 포함 9/9 통과를 확인. 이 과거 실패를 새 미해결 결함으로 세지 않음 |
| `7e1aaf3a02b440748e5b872a010287d6` | 벽 여유 선호가 빈 통로/긴급 회피 우선순위와 충돌한 2사례 | 후속 `4511030125a54c5c8e4fc03cf59bb586` XML에서 해당 2사례 포함 11/11 통과 확인. AI 변경 범위 제외 |
| `312087b04e5b4647ad88ad086135f708` | 도서관 초기 적이 카메라 밖이어야 한다는 단언 실패(기대 >9.2139, 관측 5.9417) | 당시 초기 스폰 위치 계약 실패 기록. 현재 원인 재실행/수정 없음; 스폰/설정 담당 영역 |
| `2189e627319847c4a75ebe128d001ac7` | `GroundEnemiesDoNotSteerAwayFromOverlappingNeighbors`가 이동 후 좌표를 0.03으로 고정했으나 중앙 군중 보정 때문에 31.955 관측 | HANDOFF에 공유 방향·경로 요청 계약으로 테스트를 정정한 이력 있음. 이 문서에서 현재 재통과를 주장하지 않음 |

최근 아이템 관련 RED/NoTests 기록도 존재하지만 주 채팅 소유 영역으로 수정하지 않았다. 전체 실패 기록을 모두 조사한 것은 아니며, 이번 범위의 대표 실패와 후속 증거를 구분했다.

## 실제 정적 확인

- C# Roslyn 의미 분석: Core 소스, 풀 3개 클래스, 새 테스트 3개와 기존 `NavigationTests`/`WorldStoreTests` 의존성을 Unity 6000.4.6f1의 .NET Standard 참조·UnityEngine.CoreModule·설치된 NUnit 참조로 검사했다. 오류 0, 경고 0. 기존 `DropPlacement.cs`의 불필요 using 힌트(CS8019, hidden) 1개만 있었다. 어셈블리 Emit·Unity 실행·테스트 호출은 하지 않았다.
- 원시 의미 분석 결과: `Logs/Validation/session-b-static/csharp-diagnostics.json`. 실행 스크립트는 `C:/Users/ace21/Documents/Codex/2026-11-04/ssalmuk-b-tests/work/check-targeted-csharp.ps1`에 있다. 처음 SDK Roslyn 로드는 PowerShell 내장 버전과 충돌해 실패했으며 내장 Roslyn으로 재검사했다.
- 하네스: `tools/validation/validate.ps1 -Mode Static` 통과. 최종 실행 ID `c0b3662e54c94438b273ed928555f633`, 결과 판독기 자체 검사 39개. manifest/receipt의 실행 ID·소스 해시와 현재 소스를 대조했다. `testCount: 0`은 이 Static 모드에서 Unity 테스트를 실행하지 않았다는 뜻이며, 새 테스트 통과 수로 세지 않는다.
- 검증 소스 해시: `909f399e6edb3dd8a5233c75e939013da66fa517b7e58368f515e9a31df8e953`.
- 공백 검사, 새 파일 `.meta` GUID 충돌 검사 및 독립 코드 검토를 수행했다. 비동기 테스트가 회귀 상황에서 무한 대기할 수 있다는 지적 1건은 작업/로더 가드와 5초 제한 대기로 보완하고 재검토했다. 이 수정 후 의미 분석·Static도 다시 확인했다. Unity 임포트·EditMode 실행·PlayMode·빌드·장시간 검증은 모두 미실행이다.

## 주 채팅 예약 후 실행할 필터

실제 실행은 해당 변경을 통합한 체크아웃을 소유하는 주 채팅만 수행한다. B 폴더에서 아래 명령을 실행하면 새 Editor가 시작될 수 있으므로 예약 없이 실행하지 않는다. 필터는 메서드의 매개변수 문자열 대신 정확한 fixture 이름을 사용한다.

```powershell
./tools/validation/validate.ps1 -Mode EditMode -Filter 'SsalMuk.Tests.ThemedNavigationContractTests'
./tools/validation/validate.ps1 -Mode EditMode -Filter 'SsalMuk.Tests.ViewPoolLeaseContractTests'
./tools/validation/validate.ps1 -Mode EditMode -Filter 'SsalMuk.Tests.RunCoordinatorFailureIsolationTests'
```

발견 기대 수는 차례대로 **4, 4, 1**이다. 첫 두 fixture는 통과 기대이며 마지막 fixture는 기준 코드의 예상 실패 재현용이다. 실제 마지막 실패를 확인한 뒤 소유 담당이 수정하고 1/1 GREEN을 확인해야 한다. 추가 영향 확인은 `SsalMuk.Tests.RunCoordinatorTests`, `SsalMuk.Tests.NavigationTests`, `SsalMuk.Tests.WorldStoreTests`의 정확한 클래스 필터로 한정할 수 있다. 실행 때는 새 manifest/receipt/XML과 통합 소스 해시를 다시 대조한다.

## 커밋 인계

- 테스트 작성 완료 커밋: `88e150a4769c289e3003c1b3259a192b602519d3` (`test: cover themed navigation pool leases and stale session errors`).
- 브랜치: `codex/ssalmuk-b-tests`. 기준부터 테스트 커밋까지 신규 테스트와 `.meta` 6개만 포함한다. 테스트 실행 완료 커밋을 뜻하지 않는다.
- 이 문서는 별도 로컬 인계 커밋으로 저장한다. 푸시·병합·다른 세션 커밋 가져오기·원본 Editor 명령은 수행하지 않았다.
