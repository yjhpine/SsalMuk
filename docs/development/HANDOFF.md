# SsalMuk 재개 지점

- 중단일: 2026-09-16. 사용자 요청으로 A1 완료 후 중단했다. A2 이후 구현은 시작하지 않았다.
- 작업 경로: `C:/Users/ace21/Desktop/SsalMuk`.
- 브랜치: `codex/validation-harness`. 이번 로컬 커밋 메시지: `chore: connect Unity and add validation harness`. 원격 푸시하지 않았다.
- 다음 범위: 사용자 재개 지시 후 A 계획의 A2(공유 정의·유닛 생성·시계). 루트 AGENTS와 기획·구조·하네스 문서를 먼저 읽는다.

## 현재 확인된 상태

- Unity `6000.4.6f1`, Test Framework `1.6.0`, URP `17.4.0` 유지.
- Unity MCP 패키지·서버 `10.2.0`, 기존 `http://127.0.0.1:8080/mcp` 설정 재사용. MCP 프로젝트 정보 읽기로 경로와 버전을 확인했다.
- 최종 검증: 판독기 28개, EditMode 6개, PlayMode 1개 통과. 테스트 0개 검색은 실패한다. 콘솔 오류 0개, 컴파일 오류 없음, PlayMode 종료 및 테스트 정리 완료.
- 열린 Editor를 사용했다. 닫힌 프로젝트용 배치 분기는 아직 실제 실행하지 않았다. Smoke/Stress는 후속 단계까지 명시적으로 실패한다.
- 게임, AI, 전투, 맵, 아트는 아직 구현하지 않았다.

## 증거

모두 Git에서 제외된 `Logs/Validation/` 아래에 있다. 다른 PC로 저장소만 옮기면 원시 결과는 따라가지 않으므로 다시 실행한다.

| 확인 | 실행 ID/파일 |
| --- | --- |
| Static | `137db41fe44e4571873c5edbfdfb9aec` |
| EditMode | `9a881004b47e4fa495bddb3e88a186f4` |
| PlayMode | `5e4bc523720544f8b48cdb8f46857223` |
| 빈 검색 실패 | `5750b40408fe40fc8b05e28ba2214332` |
| MCP 프로젝트 읽기 | `mcp-project-evidence.json` |
| 작업 전 변경 보관 | `bootstrap-1c6c7db7cb314c57b5ef952956699577/baseline.json` |

## 재개 시 주의

- 현재 서버는 관리되는 터미널에서 실행된다. 앱/PC를 닫으면 중단될 수 있다. `tools/validation/start-unity-server.ps1`, `connect-unity.ps1`로 재연결한다. 전역 설정을 추가·덮어쓰지 않는다.
- 기존 `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/ProjectSettings.asset`, `.vsconfig`는 사용자 설정 내용을 보존했으며 커밋에 넣지 않았다. `ProjectSettings.asset`은 Unity가 줄바꿈을 바꿨지만 줄바꿈을 제외한 내용은 시작 시 백업과 동일하다.
- Unity가 갱신/생성한 `ProjectSettings/ShaderGraphSettings.asset`, `ProjectSettings/SceneTemplateSettings.json`도 이번 커밋에서 제외했다. 임의로 삭제·복구하지 않는다.
- 테스트 조립 단위의 `optionalUnityReferences: ["TestAssemblies"]`가 Test Runner 참조를 추가한다. 같은 참조를 명시적으로 중복 추가하지 않는다.
- PlayMode 결과는 테스트 종료 콜백 뒤 씬·설정 복원이 끝나야 확정된다. 현재 연결기는 이 복원과 도메인 재로딩을 기다린다.
