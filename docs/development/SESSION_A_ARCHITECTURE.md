# 세션 A 구조 리팩터링 인계

- 작업 폴더: `C:/Users/ace21/.codex/worktrees/ssalmuk-a-architecture/SsalMuk`
- 기준 커밋: `dcda7a043773680005562f7ce3b3e659cf8d8801`
- 구현 완료 커밋: `2fb214ec7791ea9947170e941cadfaa2d13faa43`
- 이 문서는 구현 완료 커밋 뒤의 별도 인계 문서 커밋이다. 통합할 때 구현 커밋과 문서 커밋을 구분할 수 있다.
- 시작 상태: 격리 폴더 깨끗함. 원본의 미추적 복구 씬 6개 파일과 `ProfilerCaptures/`는 수정하지 않았다.
- Fast: 지원 도구에서 상태를 확인하지 못했으며, 사용자의 `FAST는 내가 킴` 확인 이후 구현을 진행했다.

## 목적과 구조 분석

메뉴 전환 결정을 Unity 생명주기와 분리해 순수 C# 경계에서 확인할 수 있게 했다. 게임 규칙·수치·AI 동작은 유지했다.

| 경계/수명 | 현재 책임 및 의존 관계 |
| --- | --- |
| Core | `noEngineReferences: true`. RunCoordinator가 씬 로드 계약과 IRunBuilder를 사용해 로드→월드→플레이어→적→Running을 조정한다. 로드 실패는 부분 세션 정리 후 메뉴로 돌아간다. |
| Presentation | Core만 참조하고 `noEngineReferences: true`. Presenter가 표시와 명령을 연결한다. 이번 MenuNavigationPresenter는 Coordinator와 View 계약에만 의존한다. |
| Unity / AppRoot | Core·Presentation·UnityEngine·uGUI·InputSystem을 연결한다. AppRoot는 씬 간 유지되는 카메라/UI/설정/Coordinator를 소유하고 파괴 시 Presenter와 Coordinator를 정리한다. |
| RunScope | 한 판의 모델·월드·이동·피해·사망·전투·표시를 명시적으로 조립한다. 정리 시 runner 비활성화→simulation/death/damage/movement/builder Dispose→표시 root 비활성화/파괴 순서를 사용한다. |
| 메뉴 수명 | MenuController가 실제 입력·뷰 생성·저장 설정·포커스/종료를 유지한다. Presenter는 PhaseChanged 구독을 소유하고 Dispose에서 해제한다. 생성 시 추가 Refresh를 하지 않는다. |

Coordinator의 generation 기반 이전 비동기 작업 무효화, RunScope의 조립/정리, BattleRunner의 고정 주기/표시 갱신은 분석만 했다. 주 채팅과 C의 소유 파일은 수정하지 않았다. DisplaySettings 저장소 추상화와 시간 포맷 분리는 이점이 작아 채택하지 않았다.

## 변경 파일

- `Assets/_SsalMuk/Unity/Bootstrap/MenuController.cs`: 공개 Initialize/HandleEscape/PauseMenu/Settings 유지, 순수 Presenter 연결과 Unity 어댑터.
- `Assets/_SsalMuk/Presentation/MenuNavigationPresenter.cs`: ESC·설정 진입/복귀·재개·단계 전환 정리 및 구독 수명.
- `Assets/_SsalMuk/Presentation/ViewContracts/IMenuNavigationView.cs`, `MenuFocus.cs`: Unity 타입 없는 표시/포커스 계약.
- `Assets/_SsalMuk/Tests/EditMode/MenuNavigationPresenterTests.cs`: 실제 Coordinator/RunTestRig를 사용하는 메뉴 테스트 8개.
- 새 C# 파일 4개의 `.meta`, 설계/계획 문서, 이 인계 문서.

Core/Items·RunModel·RunSimulation·PlayerModel·보상/상자/피해·GameCatalog/DevelopmentDefaults/RunScope/AppRoot·아이템 UI·World/Movement/AI/렌더링 핫패스 변경 없음.

## 실제 검증

- RED: 테스트 먼저 작성 후 미구현 View/포커스 타입으로 CS0246 3개 확인.
- GREEN: .NET SDK 10.0.400에서 실제 작업 폴더 Core/Presentation/RunTestRig와 NUnit 단언을 사용해 새 메뉴 8개 + 기존 RunCoordinator 16개 = **24/24**. Unity 없는 별도 CLR 호출이며 Unity TestRunner 결과가 아니다. Unity 어댑터는 이 실행에 포함되지 않는다.
- CLR 실행/출력: `C:/Users/ace21/Documents/Codex/2026-11-04/ssalmuk-a-architecture/work/menu-navigation-check/`의 `Check.csproj`, `Program.cs`, `red-build.txt`, `green-tests.txt`. Unity 패키지의 기존 NUnit DLL을 읽기 전용 참조하며 원본에는 쓰지 않았다.
- `tools/validation/validate.ps1 -Mode Static`: Passed. 실행 ID `8f98fb182b354db0990ffb1db7644384`, manifest/receipt 일치. Static의 testCount 0을 Unity 테스트 통과로 해석하지 않는다.
- 검증 소스 해시: `6358c856fe47dca63197630c7756868aca1082f4ffad148d56b91074866b4ac2`.
- `git diff --check`, 허용 경로 확인, 새 .meta GUID 형식/중복 확인 통과.
- 독립 리뷰: Critical 0, Important 0, Minor 1. 기능 회귀는 발견하지 못했다.

## 미검증과 통합 담당자 확인

Unity 컴파일·실제 입력·EventSystem 선택 객체·UI 파괴 순서는 실행 검증하지 않았다. 원본 Editor 명령, 새 Editor, 전체 검사, 빌드, 장시간 테스트, 푸시, 병합은 하지 않았다.

필요한 한정 필터는 아래와 같다. 통합 후 **주 채팅의 실제 Editor와 그 소스**에서 실행해야 한다.

```powershell
./tools/validation/validate.ps1 -Mode EditMode -Filter SsalMuk.Tests.MenuNavigationPresenterTests
./tools/validation/validate.ps1 -Mode EditMode -Filter SsalMuk.Tests.RunCoordinatorTests
./tools/validation/validate.ps1 -Mode PlayMode -Filter SsalMuk.Tests.MenuSettingsTests
./tools/validation/validate.ps1 -Mode PlayMode -Filter SsalMuk.Tests.SessionLifecycleTests
```

리뷰의 보류 Minor: Presenter가 살아 있는 성공 로드에서 Running 이벤트가 View 호출을 추가하지 않는 별도 회귀 검사는 추가하지 않았다. 해당 구현 분기는 유지되어 있으며 통합 확인 시 함께 살핀다. 이는 발견된 기능 오류가 아니다.
