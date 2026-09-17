# Unity MCP 개선 확인 보고

확인 시각: 2026-09-17 22:44 KST. 대상: `C:/Users/ace21/Desktop/SsalMuk`, Unity `6000.4.6f1`.

## 결론

공식 Unity 플러그인과 CLI로 전환되어 별도 Python 서버 및 전용 연결 브리지를 관리할 필요가 줄었다. 공식 스킬 31개도 설치되어 Unity 기능별 작업 지침을 사용할 수 있다. 교체 당시 실제 Editor 호출은 성공했다.

현재는 실행 중인 Unity Editor가 없어 실시간 제어 연결을 재확인하지 못했다. 공식 MCP 프로세스의 초기화는 성공하지만 제공 도구는 0개이고, CLI 상태는 `STATUS_NO_INSTANCES`다. 설치 완료와 현재 Editor 연결 상태를 구분해야 한다.

## 연결 구조의 변화

이전:

```text
Codex unityMCP
  → 별도 uv/Python mcpforunityserver 10.2.0 (127.0.0.1:8080)
  → com.coplaydev.unity-mcp / 프로젝트 전용 연결 브리지
  → Unity Editor
```

현재:

```text
공식 Unity 스킬 → 작업 지침 제공
Codex → 공식 Unity CLI의 stdio MCP → 프로젝트의 Pipeline → Unity Editor
터미널 → 공식 Unity CLI command     → 같은 Pipeline      → Unity Editor
```

stdio는 Codex와 CLI 사이의 통신 방식이다. Pipeline은 Editor 내부의 로컬 서버를 계속 사용한다. 모든 로컬 서버가 사라진 구조는 아니다. Pipeline은 교체 이전부터 프로젝트에 있었으며 이번에 새로 추가한 패키지가 아니다.

| 구성 | 확인 결과 |
| --- | --- |
| Codex 플러그인 | `unity@unity-agent-plugin` `0.1.6-beta`, 설치·활성화, 스킬 31개 |
| Unity CLI | `1.0.0-beta.10` |
| Pipeline | manifest·lock에 `com.unity.pipeline` `0.7.0-exp.1` |
| Codex MCP | `unity` 활성화, CLI 절대 경로와 SsalMuk 프로젝트 경로 지정 |
| 기존 구성 | `unityMCP` 설정, Coplay 패키지, `UnityMcpConnection.cs` 제거 확인; 8080 리스너 없음 |

공식 플러그인은 Codex에 설치하는 작업 지침 묶음이다. Unity Package Manager로 설치하는 게임 패키지와는 구분된다. [공식 설명](https://docs.unity.com/en-us/ai/unity-plugin/about-unity-plugin)

## 좋아진 점과 적용 범위

| 항목 | 구체적인 변화 | 이 프로젝트에서의 의미 |
| --- | --- | --- |
| 연결 운영 | 별도 Python HTTP 서버와 전용 연결 브리지 제거, 공식 CLI 진입점 사용 | 서버 실행 순서와 프로젝트 전용 연결 코드의 유지 관리 부담 감소 |
| 작업 지침 | Unity 기능 담당팀이 작성한 공식 스킬 31개 추가 | 픽셀 렌더링, 스프라이트 피벗·슬라이싱, uGUI, URP 발광 표현 작업에 해당 기능의 절차를 참고 가능 |
| 상태 진단 | `editor_status`, `recompile_status`, `eval` 등으로 상태와 실제 값을 읽는 경로 정리 | 재생 중, 컴파일 중, 대화상자 대기, 프로젝트 불일치를 구분하는 근거 확보 |
| 연결 확인 | `connect-unity.ps1`이 실제 프로젝트 경로와 Unity 버전을 읽고 예상값과 대조 | 도구 목록이나 서버 응답만으로 연결 성공을 판단하지 않음 |

31개 스킬의 범위와 Unity 6 이상 지원은 [Unity 공식 발표](https://unity.com/blog/unity-plugin-codex)에서도 확인했다. 스킬은 개발 절차이며 게임에 31개 기능이 자동 추가된다는 뜻은 아니다.

씬·에셋 조작과 테스트 실행은 기존 MCP에서도 제공하던 범주다. 해당 기능 전체가 이번 교체로 처음 가능해졌다고 보지는 않는다. 이번 변경의 확인 가능한 이점은 연결 구성 정리, 공식 작업 지침 추가, 진단 경로의 명확화다.

## 확인 근거

이번 확인:

- 설치 목록에서 공식 플러그인의 버전·활성 상태 확인, 로컬 스킬 폴더 31개 확인.
- Codex `unity` MCP의 stdio 설정 및 프로젝트 경로 확인.
- manifest·lock, 이전 커밋의 시작 스크립트, 현재 연결 스크립트 비교.
- 공식 MCP의 `initialize`와 `tools/list` 응답 확인. 현재 도구 수 0개.
- CLI가 Editor 인스턴스 0개를 반환했고, 실제 Unity Editor 실행 파일의 프로세스도 없음. 실행 중인 동명 `unity.exe`는 CLI이므로 Editor로 세지 않음.
- 기존 8080 포트의 리스너 없음.

과거 성공 기록을 이번에 다시 읽어 확인한 내용:

- 2026-09-17 21:04:53 KST: 공식 MCP 도구 151개 조회, `editor_status` 및 `eval` 성공. 실제 SsalMuk 경로와 Unity `6000.4.6f1` 일치.
- 21:06:28 KST: 프로젝트 시작 래퍼를 통한 같은 MCP 호출 성공.
- 교체 완료 시점: 컴파일 `up_to_date`, 오류 없음. 실제 등록 패키지 조회에서 Coplay 없음·Pipeline `0.7.0-exp.1` 확인.

위 151개 및 컴파일 성공은 당시 실행 결과다. 현재 열린 Editor에서 다시 검증한 결과로 취급하지 않는다. 현재 대화에 직접 노출된 Unity MCP 도구가 없어 이번 검사는 CLI와 별도 stdio 프로브로 수행했다.

## 한계 및 다음 연결 확인

- 실행 속도, 토큰 사용량, 장시간 안정성의 전후 비교 측정은 하지 않았다. 더 빠르거나 오류가 몇 퍼센트 줄었다는 수치는 제시할 수 없다.
- 플러그인·CLI는 beta, Pipeline은 experimental 버전이다. 교체 중 패키지 제거에 따른 재로딩 과정에서 상태 조회 시간 초과가 한 번 있었고, 이후 재접속 및 패키지·컴파일 상태 확인은 성공했다.
- 몬스터 이동 보간·길찾기·개체 상한 변경은 게임 코드 커밋 `8ef1ca9`의 작업이다. MCP 교체의 FPS 개선 효과로 산정하지 않는다.
- 기존 파일 기반 검증 하네스와 Unity Test Framework는 유지한다. 이번 보고를 위해 게임 테스트, 장시간 검사, 빌드를 새로 실행하지 않았다.
- SsalMuk Editor를 다시 연 뒤 `tools/validation/connect-unity.ps1`로 프로젝트·버전·실제 읽기를 확인해야 현재 연결을 완료 상태로 판단할 수 있다.

## 증거 위치

- 도구 교체 커밋: `56eff16`.
- 현재 상태: `Logs/Validation/mcp-report-20260917/current-state.json`.
- 현재 MCP 도구 목록: `Logs/Validation/mcp-report-20260917/mcp-tools.json`.
- 과거 성공 기록: `Logs/Validation/unity-plugin-migration-1789645736476/mcp-after.json`, `mcp-wrapper.json`, `recompile-final.json`, `loaded-packages-final.json`.
- 연결 절차: `docs/development/HARNESS.md` 3절, `tools/validation/connect-unity.ps1`.

원시 증거는 Git에서 제외되는 `Logs/Validation/`에 보관한다. 이번 요청에서는 보고서만 작성하고 게임 코드·도구 설정을 변경하지 않았다.
