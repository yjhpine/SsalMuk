# SsalMuk 재개 지점

- 갱신일: 2026-09-16. 주간 사용량 10% 잔여를 알린 뒤 사용자가 중간 저장과 일시 정지를 요청했다. **사용자의 재개 요청 전에는 구현·수정·테스트를 이어가지 않는다.**
- 작업 경로: `C:/Users/ace21/Desktop/SsalMuk`, 브랜치: `codex/unit-foundation`. 원본 프로젝트에서 작업하며 원격 푸시하지 않았다.
- A1~A4는 완료했다. A5는 작업 중인 소스·테스트·폰트를 로컬 체크포인트로 저장하며, 완료 또는 검증 통과 상태가 아니다.
- 이번 저장 커밋 메시지: `wip: save paused runtime scene implementation`. 마지막 검증된 기능 커밋은 A4 `1844d9e`다.

## 중단 시점

- Unity `6000.4.6f1`, 열린 Editor 프로세스 `22536`. 중단 직전 상태는 PlayMode 종료, 실행 중인 테스트 없음, 컴파일 중 아님이다.
- **현재 컴파일 실패:** `WorldPresentationTests.cs`가 아직 연결하지 않은 `RunTestRig.Run`을 참조한다. `CS1061` 네 건은 같은 원인이다. 새 테스트를 먼저 작성한 지점에서 사용자 요청에 따라 멈췄으며, 저장을 위해 추가 구현하지 않았다.
- A5의 실행 순서 관리, 메뉴/월드 표시, 런타임 시작 연결, 콘텐츠 생성 도구와 테스트 초안을 작성했다. 이 묶음의 테스트와 실제 플레이 화면은 아직 검증하지 않았다.
- `DevelopmentContentBuilder.Build()`는 아직 실행하지 않았다. `Assets/_SsalMuk/Scenes/`와 `Resources/` 및 실제 카탈로그·프리팹·재질 에셋은 아직 생성하지 않았다. 빌드 설정도 A5에서 변경하지 않았다.
- 한글 UI용 `GowunDodum-Regular.ttf`와 OFL 라이선스·출처를 `Assets/_SsalMuk/Content/Fonts/`에 저장했다. 최종 캐릭터 스프라이트·공격 이펙트는 후속 단계다.

## 재개할 순서

1. 사용자 재개 요청을 확인하고 Git 상태, 인계와 해당 계획, Unity 대상 프로젝트·버전·연결 상태를 읽는다. 두 번째 Editor를 실행하지 않는다.
2. `RunTestRig`의 실제 이동 테스트 환경을 `RunModel`·`RunCoordinator`와 연결하고 `Run`을 노출한다. 기존 World/Player/Clock/Movement도 같은 판을 사용하도록 유지한다. 이것이 중단 직전의 다음 작업이다.
3. 새 파일을 포함해 Unity를 `scope: all`로 갱신하고 컴파일 및 EditMode를 검증한다.
4. `DevelopmentContentBuilder.Build()`로 빈 메뉴·전투 씬과 개발용 콘텐츠를 생성한다. 기존 사용자 씬·설정을 보존하고, 빌드 설정을 커밋할 때는 이번에 추가한 항목만 포함한다.
5. 실제 GameStart → 씬 로드 → 구조물 → 플레이어 → 몬스터 → 시간·이동 진행을 PlayMode와 화면으로 확인한다. 넓은 공간·좁은 통로·벽 옆 군집 이동도 실제 표시로 확인한다.
6. Domain Reload 켜짐/꺼짐 각각에서 재시작·중복 생성 방지와 정리를 검증한다. 설정을 바꾸기 전에 원본을 보관하고 검증 후 복원한다.
7. A5 계획의 나머지 항목을 검증하고 현재 소스에 대응하는 새 결과를 확인한 뒤 문서와 로컬 커밋을 마무리한다. A5 체크박스는 그때 완료로 바꾼다. B1 이후는 현재 범위 밖이다.

구체적인 승인 범위와 체크리스트는 [A 단계 계획](../superpowers/plans/2026-09-16-ssalmuk-a-foundation-world.md)을 따른다. 재개 후에는 이미 승인된 A5 범위의 승인을 반복해서 묻지 않는다.

## 마지막 완료 단계와 검증 기록

현재 A5 소스의 검증 결과와 아래의 이전 완료 결과를 혼동하지 않는다. 원시 결과는 Git에서 제외된 `Logs/Validation/` 아래에 있으며 다른 PC로 저장소만 옮기면 다시 검증해야 한다.

| 단계 | 완료 내용 | 커밋 / 검증 실행 ID |
| --- | --- | --- |
| A4 | 반경별 길찾기·경로 처리 예산, 벽 미끄러짐, 지상 군집 보정과 접선 이동, 공중 예외, 실제 이동 테스트 환경 | `1844d9e`; EditMode 58개 `ec5a765f74c044319d59ca6501555550`, PlayMode 1개 `18e90ab2876e4d3d8fa1ff489e4d8722` |
| A3 | 결정적 청크와 공유 출입구, 보스 반경 연결 검증, 공간 색인·충돌 질의, 모든 몬스터·경험치 상태 보존 | `320c843`; EditMode 48개 `14b2660c164045ba8667b3b58a54b513` |
| A2 | 공유 정의·유닛 생성·등록소, 검 초기 상태, 좌표·시계·Unity 설정 검사 | `f5ae8ed`; Static 28개 `0339360d47574ea9a153c097a2b9c86d`, EditMode 33개 `ea8da69f607a4212b97323e7c4647b80`, PlayMode 1개 `bd5d10fed0fb4373b4ea2598d8368f98` |

실제 피해·자동 공격·플레이어 AI·성장과 최종 아트는 후속 단계다. Smoke/Stress도 해당 구현 전까지 명시적으로 실패한다.

## 보존할 상태와 검증 주의

- 작업 전 상태와 사용자 파일 SHA256: `Logs/Validation/a3-a5-baseline-4b5939fd9b51407584602be5a4d638a1/baseline.json`.
- 사용자 변경인 `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/ShaderGraphSettings.asset`, `ProjectSettings/SceneTemplateSettings.json`, `.vsconfig`, `Assets/_Recovery/`와 해당 `.meta`는 체크포인트에 넣지 않고 그대로 보존한다. 임의로 삭제·복구·스테이징하지 않는다.
- Unity MCP 패키지·서버 `10.2.0`과 기존 `http://127.0.0.1:8080/mcp`를 재사용한다. 앱/PC 종료로 연결이 끊겼으면 `tools/validation/start-unity-server.ps1`, `connect-unity.ps1`을 확인한다. 전역 설정을 덮어쓰지 않는다.
- 검증 진입점은 PowerShell 7의 `tools/validation/validate.ps1`이다. 새 manifest·receipt·XML과 현재 소스 해시를 확인한다. 이전 성공 결과나 0개 테스트를 현재 성공으로 보고하지 않는다.
- 새 파일은 `refresh_unity`의 `scope: all`로 가져온다. 새 `.meta` 등 소스 해시에 포함되는 파일 정리는 최종 검증 전에 마친다.
- PlayMode 결과는 종료 콜백 뒤 씬·설정 복원과 도메인 재로딩까지 끝난 후 확정한다. Test Runner 참조를 중복 추가하지 않는다.
