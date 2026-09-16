# SsalMuk 재개 지점

- 갱신일: 2026-09-16. **A1~A5·B1·B2 구현·검증 완료. 사용자가 B·C·D 전체 진행을 승인했다. 다음은 B3다.**
- 프로젝트: `C:/Users/ace21/Desktop/SsalMuk`, 브랜치: `codex/unit-foundation`.
- 최신 B2 로컬 커밋 메시지: `feat: implement damage knockback and single death drops`. B1은 `3c95e28`, A5는 `0599916`이다. 원격 푸시는 하지 않는다.
- 중간 저장 `e742523` 뒤 목표 재활성화를 확인해 재개했다. 사용량은 재개 시 주간 7%, 마무리 중 4% 잔여를 알렸다.

## 현재 동작

- B2: DamageService의 판·생존·수치·타격 키·무적 검증, 수락된 피해의 단일 이벤트, 이동 구간 접촉 순서, 짧은 무적 이후 재피격, 모델 넉백, 단일 사망 큐·처치·드롭을 구현했다. 빠른 공중 교차와 긴 접촉 주기의 무적 경계도 확인했다. 실제 BattleRunner의 공격·피해 순서는 B3 RunSimulation에서 연결할 차례다.

- B1: low AI의 Collect/Evade/Breakout 상태와 실제 BattleRunner 이동을 연결했다. 가치·거리·위험 기반 경험치 선택, 흡수 중 구슬 제외, 접근 불가 후보 건너뛰기, 긴급 접촉 회피, 포위 방향 유지·정체 시 재평가·안정 해제를 구현했다.
- 모든 무기의 공통 목표는 평상시 가장 가까운 적이며 돌파 중 통로를 막는 적을 우선한다. 다른 청크도 조회하고 동률은 ID 순서다. 피해·공격·흡수·성장과 최종 표시 연결은 B2 이후다.
- 철검·철도끼·철창·루비 금색 지팡이 원본 4종을 `Content/Sprites/Weapons/`에 보관했다. 출처·프롬프트·alpha 확인은 `docs/planning/ASSET_SOURCES.md`에 기록했다. 실제 무기 표시 참조와 크기·축 검증은 D2에 남아 있다.

- 빈 MainMenu/Battle 씬을 사용한다. 카메라·Canvas·EventSystem은 실행 중 생성한다.
- GameStart → 씬 로드 → 구조물 → 플레이어와 검 → 초기 일반 몬스터 12마리 → 시간·이동 시작을 연결했다.
- 연속 클릭은 하나의 판만 생성한다. 로드·생성 실패는 부분 상태를 정리하고 메뉴로 돌아간다.
- 메뉴와 월드에 MVP를 적용했다. 한글 UI는 OFL 라이선스의 Gowun Dodum을 사용하며 출처와 라이선스를 폰트 폴더에 포함했다.
- 지상 몬스터의 벽 우회·접선 이동·밀집과 공중의 통과를 실제 표시한다. 멀어진 View 회수는 논리 몬스터·경험치 삭제와 분리했다.
- 현재 단색 임시 그래픽에서 AI 이동이 동작한다. 실제 피해·무기 공격·성장·최종 아트는 후속 단계다.

## B2 검증

- EditMode: `f82a79532cc7442db992db7a90d570d3`, 89개 통과.
- PlayMode: `b4b182d49720485fb83229af95441a22`, 7개 통과.
- 두 실행의 manifest·receipt·XML을 현재 소스 해시와 대조했다. 기존 사용자 파일은 시작 해시와 일치한다.
- RED: 피해·접촉·사망·넉백 `bf031f0530ac4d469feb355d34135d44`, 무적 중 공중 통과 판단 `9e5e364ad2034051875052e4ebff93a3`.
- 실제 전투 순서·검 공격은 B3, HUD·결과·새 판은 B4, 흡수와 성장 UI는 C, 캐릭터/몬스터·효과·빌드는 D에 남아 있다.

## 이전 B1 검증

- EditMode: `b597e233bb52486284eb56ebae01c41e`, 77개 통과.
- PlayMode: `c589d34f4b834a4cbcc5d6a6c284231b`, 7개 통과. 실제 시작 버튼·BattleRunner에서 경험치 방향으로 플레이어가 이동하는 관측을 포함한다.
- 두 실행의 manifest·receipt·XML을 현재 소스 해시와 대조했다. 기존 사용자 파일은 시작 해시와 일치한다.
- RED 증거: 최초 판단 테스트 `a2ba02fb3ae84e049d24d186ed143ba1`, 목표 테스트 `734d302e2c8045a385747067d11ac25d`, 정체 방향 재선정 회귀 `4d270e590c2a4ac48437a5de83229479`.
- 이번 시작 상태와 Editor 원시 로그: `Logs/Validation/bcd-baseline/`. Editor가 닫혀 있음을 확인한 뒤 6000.4.6f1로 다시 열었고 사용자가 복구 팝업을 처리했다. 현재 Editor PID는 19020, 서버 PID는 34876이다. 재개 시 실제 생존 여부를 다시 확인한다.
- 원시 HTTP 도구의 `structuredContent.result` 래핑은 이 경로의 로컬 helper에서 처리한다. StrictMode를 켜는 검증 판독기와 RPC helper는 별도 PowerShell 프로세스에서 실행한다.

## 이전 A5 검증

현재 소스 해시: `4575a632c24316f5d063d9cd889ded8b80007bbdd7cfaba68fcb4404eef9c2b3`.

| 검증 | 실행 ID | 결과 |
| --- | --- | --- |
| Static | `b530056e124145e89e09198c1f9786e9` | 결과 판독기 28개 및 검증 스크립트 구문 검사 통과 |
| EditMode | `e54fcb219f7b4e0787e15466614197fe` | 67개 통과 |
| PlayMode | `b82ceb67da0a4012bf435e310cc0424a` | 7개 통과 |

새 manifest·receipt·XML을 현재 소스 해시와 대조했다. Static의 `testCount: 0`은 NUnit 실행을 하지 않는 모드의 출력이며, 실제 28개 판독기 검사는 해당 폴더의 `static.txt`에 있다. EditMode/PlayMode의 빈 검색은 실패한다.

- `CrowdPreviewTests`: 넓은 공간·1셀 통로·벽 옆 각각 실제 모델/이동/WorldView를 12초 진행한다. 매 단계 벽 침범과 과도한 위치 변화를 검사하고 14개 논리 개체 보존 및 공중의 직선 통과를 확인한다. 각 판의 시작·공중 통과·밀집 화면과 지표를 `Logs/Validation/CrowdPreview/<RunId>/`에 남긴다.
- 실제 Editor의 Domain Reload 켜짐 1회, 꺼짐 2회 실행에서 서로 다른 RunId 3개, 시작 순서, AppRoot·WorldView·EventSystem 각 1개를 확인했다. 꺼짐 상태에서 종료 후 해당 개체와 UnitView는 모두 0개다.
- AssemblyReloadEvents 관측으로 실제 재로딩 발생/미발생을 확인했다. 테스트 러너의 설정 표시만으로 판단하지 않았다.
- 원래 Editor 설정은 바이트 단위로 복원했다. 마지막 상태는 PlayMode 종료, 활성 검증 없음, 컴파일 오류 없음이다.
- 원시 결과는 Git에서 제외되는 `Logs/Validation/` 아래에 있다. 저장소만 다른 PC로 옮기면 원시 결과가 따라가지 않으므로 다시 검증한다.

## 재개 자료와 보존할 파일

- A5 시작 상태, 실제 메뉴/전투 화면, 재로딩 관측 JSON: `Logs/Validation/a5-resume-53a88c310bc048ab8efbfbe5bd33698e/`.
- A3~A5 최초 상태: `Logs/Validation/a3-a5-baseline-4b5939fd9b51407584602be5a4d638a1/baseline.json`.
- 기존 사용자 파일 `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/ShaderGraphSettings.asset`, `ProjectSettings/SceneTemplateSettings.json`, `.vsconfig`, 기존 `Assets/_Recovery/`와 해당 메타는 해시를 확인하고 보존했다.
- `EditorBuildSettings.asset`에는 새 MainMenu/Battle 항목만 추가했다. 기존 씬·설정 내용은 보존하고 이번 씬 항목만 스테이징한다.
- Unity가 재실행 중 추가한 `Assets/_Recovery/0 (1).unity`와 메타도 보존한다. 복구 파일을 삭제하거나 이번 기능 커밋에 포함하지 않는다.
- A3 커밋 `320c843`, A4 커밋 `1844d9e`, A5 중간 저장 `e742523`. A5 완료 커밋은 위 메시지로 확인한다.

## 다음 작업

1. [B 단계 계획](../superpowers/plans/2026-09-16-ssalmuk-b-ai-combat.md)의 B3부터 C·D 전체를 순차 진행한다. 현재 사용자 목표가 전체 범위를 승인했으므로 A5 정지 경계는 이전 기록이다.
2. 기획·구조·하네스와 Git 상태를 먼저 확인한다. 원본 프로젝트에서 작업하고 사용자 변경을 보존한다.
3. Unity `6000.4.6f1`, MCP 패키지·서버 `10.2.0`과 기존 `http://127.0.0.1:8080/mcp`를 유지한다. 대상 프로젝트와 버전 읽기로 연결을 검증하고 두 번째 Editor를 실행하지 않는다.
4. 서버가 종료됐으면 `tools/validation/start-unity-server.ps1`, `connect-unity.ps1`을 사용한다. 이 세션에서는 기존 서버의 HTTP 연결도 실제 확인했다.
5. 새 파일은 Unity에서 `scope: all`로 가져온다. 최종 검증 전에 생성 메타와 에셋 형식을 정리한다. 검증 후 소스가 바뀌면 이전 결과를 재사용하지 않는다.
6. 재로딩 설정을 임시 변경했으면 Unity의 저장을 마친 뒤 원본과 비교·복원한다. 값이 같아도 지연 저장으로 줄바꿈이 바뀔 수 있으므로 소스 해시를 재확인한다.
7. 콘텐츠 생성 도구는 저장된 씬이 열린 상태에서 실행한다. Unity는 이름 없는 미저장 초기 씬과 새 씬의 additive 생성을 함께 허용하지 않는다. 사용자 미저장 씬을 강제로 저장하거나 폐기하지 않는다.

범용 Smoke/Stress, 피해·경험치 흡수·성장·최종 아트 검증은 해당 후속 구현 단계에서 연결한다.
