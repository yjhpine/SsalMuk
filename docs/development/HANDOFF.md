# SsalMuk 재개 지점

- 갱신일: 2026-09-16. 재연결 확인 후 사용자가 선택한 A2 기본 구조 구현을 완료했다. A3 이후는 시작하지 않았다.
- 작업 경로: `C:/Users/ace21/Desktop/SsalMuk`.
- 브랜치: `codex/unit-foundation`. 이번 로컬 커밋 메시지: `feat: add shared definitions and runtime unit factories`. 원격 푸시하지 않았다.
- 다음 범위: 사용자 요청 후 A 계획의 A3(청크 생성·공간 검색·월드 보존). 루트 AGENTS와 기획·구조·하네스 문서를 먼저 읽는다.

## 현재 확인된 상태

- Unity `6000.4.6f1`, Test Framework `1.6.0`, URP `17.4.0` 유지.
- Unity MCP 패키지·서버 `10.2.0`, 기존 `http://127.0.0.1:8080/mcp` 설정 재사용. MCP 프로젝트 정보 읽기로 경로와 버전을 확인했다.
- 최종 검증: 판독기 28개, EditMode 33개, PlayMode 1개 통과. 테스트 0개 검색은 실패한다. 콘솔 오류 0개, 컴파일 오류 없음, PlayMode 종료 및 테스트 정리 완료.
- 열린 Editor를 사용했다. 닫힌 프로젝트용 배치 분기는 아직 실제 실행하지 않았다. Smoke/Stress는 후속 단계까지 명시적으로 실패한다.
- A2 구현: Unity에 의존하지 않는 Core, Presentation 조립 단위, 유닛 공유 정의·팩토리·등록소, 판별 ID, 검만 소유한 플레이어 초기 상태, 청크 좌표와 생존 시계, Unity 설정 카탈로그와 Inspector 오류 표시.
- 카탈로그의 기본 능력치는 계획에 적힌 임시 프리셋이다. 생성한 Core 정의는 이후 에셋 편집과 분리되며 같은 카탈로그의 유닛들은 정의 참조를 공유한다.
- 실제 피해 처리·AI·전투·맵 생성·플레이 화면·콘텐츠 에셋은 후속 단계다. 개별 체력의 피해 후 독립성 검증은 계획대로 B2에서 수행한다.

## 증거

모두 Git에서 제외된 `Logs/Validation/` 아래에 있다. 다른 PC로 저장소만 옮기면 원시 결과는 따라가지 않으므로 다시 실행한다.

| 확인 | 실행 ID/파일 |
| --- | --- |
| A2 Static | `0339360d47574ea9a153c097a2b9c86d` |
| A2 EditMode | `ea8da69f607a4212b97323e7c4647b80` |
| A2 PlayMode | `bd5d10fed0fb4373b4ea2598d8368f98` |
| A1 빈 검색 실패 | `5750b40408fe40fc8b05e28ba2214332` |
| A2 MCP 프로젝트 읽기 | `a2-baseline-a67024d6f46c4c11a085ed4bb621f374/mcp-project-evidence.json` |
| A2 작업 전 변경 보관 | `a2-baseline-a67024d6f46c4c11a085ed4bb621f374/baseline.json` |
| A2 구현 전 예상 컴파일 실패 | `a2-baseline-a67024d6f46c4c11a085ed4bb621f374/expected-missing-types.txt` |

## 재개 시 주의

- 현재 서버는 관리되는 터미널에서 실행된다. 앱/PC를 닫으면 중단될 수 있다. `tools/validation/start-unity-server.ps1`, `connect-unity.ps1`로 재연결한다. 전역 설정을 추가·덮어쓰지 않는다.
- 기존 `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/ProjectSettings.asset`, `.vsconfig`는 사용자 설정 내용을 보존했으며 커밋에 넣지 않았다. `ProjectSettings.asset`은 Unity가 줄바꿈을 바꿨지만 줄바꿈을 제외한 내용은 시작 시 백업과 동일하다.
- Unity가 갱신/생성한 `ProjectSettings/ShaderGraphSettings.asset`, `ProjectSettings/SceneTemplateSettings.json`도 이번 커밋에서 제외했다. 임의로 삭제·복구하지 않는다.
- 재개 시 발견한 `Assets/_Recovery/`와 해당 `.meta`는 사용자 파일로 보존했다. A2 작업 전 백업과 위 파일들의 바이트 해시가 일치한다.
- 테스트 조립 단위의 `optionalUnityReferences: ["TestAssemblies"]`가 Test Runner 참조를 추가한다. 같은 참조를 명시적으로 중복 추가하지 않는다.
- PlayMode 결과는 테스트 종료 콜백 뒤 씬·설정 복원이 끝나야 확정된다. 현재 연결기는 이 복원과 도메인 재로딩을 기다린다.
- 새 파일을 추가한 뒤 MCP로 갱신할 때는 `refresh_unity`의 `scope: all`을 사용한다. `scope: scripts`의 컴파일 요청만으로 새 파일이 import되지 않을 수 있다.
