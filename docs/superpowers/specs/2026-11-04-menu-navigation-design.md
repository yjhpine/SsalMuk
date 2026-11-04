# 세션 A: 메뉴 전환 책임 분리

기준: `dcda7a043773680005562f7ce3b3e659cf8d8801`. 사용자가 A의 작은 리팩터링 선택·구현·로컬 커밋을 위임했고 Fast 활성화를 직접 확인했다.

## 목적과 선택

`MenuController`가 Unity 입력·뷰 생성·포커스 처리와 메뉴 전환 규칙을 함께 소유한다. ESC, 설정 왕복, 단계 전환 규칙을 `MenuNavigationPresenter`로 옮겨 Unity 없이 검사할 수 있게 한다.

- 채택: 기존 Coordinator와 얇은 View 계약을 사용하는 메뉴 Presenter. 기존 Presenter 구조와 일치하고 다른 세션 수정 파일을 건드리지 않는다.
- 보류: `DisplaySettings` 저장소 추상화. 현재 20줄 정도여서 추가 추상화의 이점이 작다.
- 보류: 결과 시간 포맷 분리. 위험은 작지만 세션·표시 경계 개선 효과도 작다.

## 유지할 계약

- `MenuController.Initialize(AppRoot, Font, DisplaySettings)`, `HandleEscape`, `PauseMenu`, `Settings`를 유지한다.
- 설정은 메인 메뉴 또는 정지된 전투에서만 연다. 닫으면 원래 메뉴로 돌아가며 전투는 계속 정지한다.
- ESC는 열린 설정을 먼저 닫는다. 전투에서는 정지/재개하며 그 외 단계에서는 무시한다.
- 재개와 Running 이외 단계 진입 시 모달·입력 차단·선택 포커스를 정리한다. 생성 시 추가 Refresh는 하지 않는다.
- Unity는 키보드, UI 생성, 실제 포커스, 종료, DisplaySettings 연결을 맡는다. Presenter는 Coordinator의 PhaseChanged를 구독하고 Dispose에서 해제한다.
- 공개 게임 규칙·수치·AI·초기화/정리 순서는 바꾸지 않는다.

## 변경 경계와 검증

Core/Items, RunModel, RunSimulation, PlayerModel, 보상/상자/피해, GameCatalog/DevelopmentDefaults/RunScope/AppRoot, 아이템 UI 및 World/Movement/AI/렌더링 핫패스는 수정하지 않는다.

새 NUnit 테스트는 실제 RunCoordinator와 RunTestRig를 사용한다. View만 관측 가능한 대역으로 바꿔 설정 복귀·정지/재개·금지 단계·구독 해제를 확인한다. 정적 검사와 Editor 없는 한정 CLR 검사를 수행할 수 있으나 Unity 실행 증거로 취급하지 않는다. 원본 Editor, 새 Editor, 전체 검사, 빌드, 장시간 검사는 실행하지 않는다.
