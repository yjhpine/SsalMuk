# Menu Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox syntax for tracking.

**Goal:** 기존 메뉴 동작을 유지하며 Unity 입력/표시와 메뉴 전환 규칙을 분리한다.
**Architecture:** MenuNavigationPresenter는 RunCoordinator와 IMenuNavigationView를 사용한다. MenuController는 기존 공개 진입점을 유지하는 Unity 어댑터다.
**Tech Stack:** C#, Unity 6000.4.6f1, NUnit, PowerShell 7.5.
**Spec:** `docs/superpowers/specs/2026-11-04-menu-navigation-design.md`

## Global Constraints

- 기준 dcda7a0의 관리 작업 폴더에서만 수정한다. 원본 파일·Editor는 건드리지 않는다.
- Core/Items·RunModel·RunSimulation·PlayerModel·보상/상자/피해·카탈로그·RunScope/AppRoot·아이템 UI·World/Movement/AI/렌더링은 수정하지 않는다.
- 전체 검사·빌드·장시간 검사·푸시·병합은 하지 않는다. Unity 검증은 통합 담당자에게 필터로 인계한다.
- 사용자 위임에 따라 작은 설계 선택과 구현을 계속하며 별도 승인을 반복 요청하지 않는다.

## Review Focus

- 설정에서 ESC를 눌러도 전투가 재개되지 않는다.
- 설정 닫기 후 메인 메뉴/일시정지 메뉴의 올바른 버튼에 포커스가 돌아간다.
- 로딩·결과 단계에서는 설정이나 전투 재개를 수락하지 않는다.
- 단계 전환은 모달·입력 차단·포커스를 정리하고 Running 진입은 추가 UI 호출을 만들지 않는다.
- Dispose 이후 Coordinator 이벤트가 이전 View에 전달되지 않는다.

## Task 1: 메뉴 전환 책임 분리

**Files:**
- Create: `Assets/_SsalMuk/Presentation/MenuNavigationPresenter.cs`
- Create: `Assets/_SsalMuk/Presentation/ViewContracts/IMenuNavigationView.cs`
- Create: `Assets/_SsalMuk/Presentation/ViewContracts/MenuFocus.cs`
- Modify: `Assets/_SsalMuk/Unity/Bootstrap/MenuController.cs`
- Test: `Assets/_SsalMuk/Tests/EditMode/MenuNavigationPresenterTests.cs`
- 새 C# 파일의 `.meta`도 추가한다.

**Interfaces:**
- Consumes: RunCoordinator.Phase/Run/Pause()/Resume()/PhaseChanged.
- Produces: MenuNavigationPresenter(RunCoordinator, IMenuNavigationView), HandleEscape()/OpenSettings()/CloseSettings()/Resume()/Dispose().
- View: bool IsSettingsVisible, SetMenuBlocked(bool), ShowPause(bool), ShowSettings(bool), Focus(MenuFocus).
- MenuFocus: None, PauseResume, PauseSettings, MainMenuSettings, DarkMode.

- [x] 테스트를 먼저 작성: 설정 왕복·ESC·금지 단계·단계 정리·Dispose 후 무응답. 실제 Coordinator의 IsPaused와 Clock 및 View 출력 상태를 함께 단언한다.
- [x] Editor 없는 한정 CLR 검사에서 미구현 타입 실패를 확인한다. 새 Editor는 실행하지 않는다.
- [x] Presenter와 View 계약을 추가하고 기존 제어문·뷰 호출 순서를 옮긴다. MenuController의 공개 진입점과 Unity 책임은 유지한다.
- [x] 같은 한정 CLR 검사, `tools/validation/validate.ps1 -Mode Static`, `git diff --check`를 수행한다. CLR 결과와 Unity 미검증을 구분한다.
- [x] 독립 코드 검토 후 허용 파일만 로컬 커밋하고 세션 A 인계 문서에 실제 증거·필터·기준/완료 커밋을 기록한다.

## 실행 기록

- 시작 Git 상태: 격리 작업 폴더 깨끗함. 원본의 복구 씬 6개 파일 및 ProfilerCaptures는 보존.
- Ruling: 사용자 지정 Editor 금지에 따라 Unity 테스트 실행을 생략하고 한정 CLR 실행으로 보완한다. Unity 어댑터·실제 포커스/이벤트 입력 검증은 통합 시 남는다.
- Pre-flight: 단일 작업이며 다른 세션 변경 파일과 겹치지 않는다.
- RED: 새 View 계약/포커스 타입이 아직 없어서 C# 컴파일 오류 CS0246 3개. GREEN: 새 메뉴 테스트 8개 + 기존 Coordinator 테스트 16개 = CLR 한정 24/24. Unity Test Runner는 실행하지 않았다.
- Static: `8f98fb182b354db0990ffb1db7644384`, sourceHash `6358c856fe47dca63197630c7756868aca1082f4ffad148d56b91074866b4ac2`, manifest/receipt 일치. testCount 0은 Static 모드이며 Unity 테스트 통과 수가 아니다.
- 변경 경로 11개 허용 목록 일치, 새 .meta 4개 GUID 형식/중복 없음, `git diff --check` 통과.
- 독립 검토: Critical 0, Important 0, Minor 1. 기능 회귀는 발견하지 못했다.
- Final: minor (deferred): Running 진입의 추가 View 호출 0회를 고정하는 성공 로드 회귀 테스트는 별도 추가하지 않았다. 구현의 Running 예외 분기는 유지되며 통합 검증 시 확인한다.
- Final: Ruling: 실제 Unity 입력·포커스·파괴 순서 및 전체/빌드/장시간 실행은 사용자 지정 경계로 미실행이다. 코드 검토나 CLR 검사를 해당 실행 증거로 대체하지 않는다.
- 로컬 커밋을 유지하고 작업 폴더를 보존한다. 사용자 지시상 푸시·병합 선택을 다시 묻지 않는다.
