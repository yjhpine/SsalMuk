# A·B·C·D 다음 작업 경계 — 2026-11-04

사용자가 네 세션의 인계 확인·검증 뒤 다음 작업을 각각 배정하도록 지시했다. 각 채팅에 전달하는 확정 통합 커밋을 같은 출발점으로 쓴다. 이전 인계의 검증 수준과 이번 통합 검증은 `SESSION_INTEGRATION_2026-11-04.md`에서 구분한다.

## 공통 운영

- 기존 별도 작업 폴더와 GPT-6 Astra / Ultra 설정을 유지한다. Fast는 사용자가 설정하며 도구로 임의 변경하지 않는다.
- 자신의 Git 상태가 깨끗하고 현재 HEAD가 전달받은 통합 커밋의 조상인지 확인한 뒤 `git merge --ff-only <전달받은 커밋>`으로 동기화한다. 이 최초 동기화만 이전의 다른 세션 커밋 가져오기 금지 규칙의 예외다. 변경이 있으면 보존하고 상황을 보고한다.
- 원본 `C:/Users/ace21/Desktop/SsalMuk`과 다른 세션 폴더는 읽기만 한다. 파일 수정·로컬 커밋은 자신의 폴더에서 담당 범위에 한정한다.
- 원본 Unity Editor는 주 채팅이 독점한다. 별도 Editor, 원본 Editor 명령, 전체 테스트·빌드·장시간 실행·푸시·병합·작업 폴더 삭제는 수행하지 않는다.
- 게임 수치·승인된 아이템/맵/AI 계약을 유지한다. 먼저 재현과 근거를 확보하고 확인된 결함만 최소 수정한다.
- 정적/독립 검사는 Unity 통과로 부르지 않는다. 필요한 Unity fixture와 예상 개수를 인계하고 주 채팅이 순차 실행한다.
- 각자 `SESSION_A_ROUND2.md` / `SESSION_B_ROUND2.md` / `SESSION_C_ROUND2.md` / `SESSION_D_ROUND2.md`에 목적·재현·실제 검증·한계·커밋을 남긴다. 공용 HANDOFF와 다른 담당자의 문서는 수정하지 않는다.
- 계정 잔여 3% 이하면 저장 후 중지한다. 사용자 초기화 전 새 작업을 시작하거나 크레딧을 사용하지 않는다.

## A — 씬 전환 수명

실제 Battle 로드 도중 메뉴 복귀, 소유 AppRoot 파괴 뒤 늦은 완료 경계를 재현한다. 새 판의 Run·LastError·활성 씬에 이전 전환이 영향을 주지 않도록 한다. 이미 수정한 늦은 복구 오류 문구 문제는 반복 작업하지 않는다.

- 생산 코드: `Core/Session/RunCoordinator.cs`, `Unity/Bootstrap/UnitySceneLoader.cs`, 직접 필요한 작은 관련 계약.
- 테스트: 관련 EditMode와 전용 `SceneTransitionCancellationTests.cs` PlayMode.
- AppRoot/RunScope 변경이 필요하면 근거와 최소안을 인계하며 직접 변경하지 않는다.
- 정상 시작 순서·ESC·결과·재시작 동작을 유지한다.

## B — 아이템 데이터·명령 경계 테스트

기존 검사와 겹치지 않는 6~10사례로 누락/null 데이터, 중복 정의, 잘못된 레시피/등급, NaN·Infinity 가중치, 숫자 한계에서 실패 시 상태·버전·난수 보존을 확인한다.

- 테스트: 전용 `ItemCatalogBoundaryTests.cs`, `ItemCommandBoundaryTests.cs`.
- 생산 코드: 재현된 입력 검증 결함에 한해 `Unity/Configuration/ItemCatalogSettings.cs`의 국소 수정 허용.
- Core/Items의 수정 필요성은 재현과 최소안으로 인계한다. 24종·18레시피·수치는 바꾸지 않는다.
- 세션/탐색/UI 테스트는 다른 담당 영역이다.

## C — 취소된 길찾기 요청 수명

`RequestPath → Cancel → Advance` 반복에서 실행할 작업이 0개인 경로가 취소 요청을 보존하는지 측정한다. 확인되면 누적을 해소하되 상태·결과·예산·처리 순서·AI 주기를 유지한다.

- 생산 코드: `Core/Navigation/NavigationService.cs`와 직접 필요한 탐색 내부 수명 코드.
- 테스트: 전용 `NavigationCancellationLifetimeTests.cs`, 작은 독립 측정 도구.
- 인계된 경험치 조회/표시 집합 최적화와 통합 때 보완한 Mono 정렬 비교자 재사용은 새 근거 없이 다시 변경하지 않는다.
- WorldPresenter·UI·아이템·RunCoordinator·게임 수치는 이번 범위에서 제외한다.

## D — 통합 경계 읽기 전용 리뷰

최신 통합본의 새 판·사망·ESC·보상/합성 대기·이전 콜백·툴팁 소유자·선택 인스턴스·표시 풀 수명·보상 한 번 지급을 검토한다.

- 프로덕션 코드와 테스트는 수정하지 않는다. 독립 문서와 근거만 커밋한다.
- 확인한 문제마다 심각도·정확한 위치·재현 조건·사용자 영향·최소 수정안·필요한 검사를 기록한다.
- A/B/C 소유 문제를 중복 구현하지 않는다. 해결된 D-01·늦은 복구 오류·화살표 가림·강화 잘림은 미해결로 다시 세지 않는다.
- 단순 스타일 취향과 미재현 가설을 확정 버그로 표시하지 않는다.
