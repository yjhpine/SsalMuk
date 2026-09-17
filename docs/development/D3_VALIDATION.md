# D3 통합 검증

상태: 구현 완료·사용자 지시로 추가 검사 종료(2026-09-17). 30분 누적 검사는 제한 시간 초과로 미완료이며 통과로 처리하지 않는다. 사용자는 지금까지 검사로 다음 단계에 진행하도록 지시했다. 현재 실행 중인 검사는 없으며 장시간 검사나 전체 재검사를 자동 재개하지 않는다.

후속 규칙 정정: 공중 무리는 돌진 후 화면 밖 일정 범위에서 퇴장하도록 사용자가 설명했다. 아래 결과는 **이 정정 전 공중까지 영구 보존하던 버전의 기록**이다. 후속 구현·관련 확인은 [실제 플레이·밸런스](PLAYTEST_TUNING.md)를 따른다. 새 장시간 성능이나 수정 후 Windows 빌드를 검증한 것으로 해석하지 않는다.

## 종료 시점과 다음 단계

- 게임 코드·아트는 아래 `cba4d64a…` 실행 이후 바뀌지 않았다. 이후 변경은 Windows 검사 실행 도구의 시나리오·기한·취소 지원이다. 같은 게임 코드의 기존 Windows 빌드를 일반 플레이에 사용할 수 있다.
- 도구 변경 후 소스 `0d32d4cb32230394b5e4bef20d78df1be9254f7dd9d61888c0d0eba8cd02e57c`의 Static `65b6fc5b6d0d4c91ba080673bf5c2f1c`(판독 39개·구문), EditMode `bcf898f8138a49598e4d86f39b6519e0`(175개)는 통과했다. 후속 PlayMode `f4a4f164d3994efa87d2920e18ec7d86`는 `SourceChanged`로 거절됐으며 성공으로 기록하지 않는다. 새 Windows 장시간 실행·취소 동작의 실제 검사는 하지 않았다.
- PlayMode 종료 시 이전 빌드가 메모리에 남긴 `m_BuildTargetBatching`의 Standalone 항목이 다시 저장됐다. 기존 백업 해시와 유일한 차이를 대조한 뒤 자동 추가 항목만 메모리에서 제거하고 원래 파일 바이트를 복원했다. 사용자 원본 SHA-256은 `28FD8C6F23218B825193C836D2CD181593FB53F21ADDA4E1F61C43D2EDBE9623`이다. 현재 빌드 도구는 디스크 복원만 구현하므로 향후 빌드 후 재검사를 다시 요청받으면 이 캐시 직렬화 문제를 먼저 다룬다.
- 다음은 기본 설정의 일반 플레이와 체감에 따른 수치 조정이다. [플레이·밸런스 조정](PLAYTEST_TUNING.md)에 기준값과 조정 순서를 남겼다. 장시간 결과를 기본 난이도의 생존율이나 정상 게임 FPS로 해석하지 않는다.

## 실행 조건

- 모든 시나리오는 실제 Core 서비스와 Unity View를 사용한다. 전투 시계를 건너뛰지 않고 0.02초 주기를 모두 처리한다.
- Smoke의 EndToEnd·RepeatedRestart는 실제 GameStart·선택·Restart 버튼을 호출한다. 경험치가 대기·흡수·비행·몸 접촉을 거치는지 관측하고, 접촉 피해로 사망시킨다.
- PersistentWorld10m/30m은 시드 260917, 실제 생성 맵·일정 스폰을 사용한다. 장시간 보존을 관측하기 위한 프리셋은 플레이어·보스의 기준 체력 10억, 네 무기 공격력 +1000·범위 +20·속도 +10이다. 보스에도 생성 시점의 기존 난이도 배율 `1 + 시간/300`을 적용한다. 일반/공중 기준 스펙과 출현 일정은 기본값을 유지한다. 기본 난이도의 생존율을 뜻하지 않는다.
- 원거리 일반·공중 sentinel, 멀리 놓인 경험치 12개(각 25), 흡수 중 청크 경계 이동을 별도로 배치한다. 살아 있는 몬스터 삭제, 경험치 값/ID/위치 손실, 공중 이동 중단을 검사한다. 실제 보스가 300초마다 중첩되는지 확인한다.
- 10분/30분 종료 뒤 원거리 경험치 View 왕복과 600개 발광 구슬의 별도 표시 부하를 관측한다. 이 추가 구슬은 장시간 스폰 개체 수와 구분한다.
- CrowdCorridor는 4칸 개구부가 있는 벽과 일반 몬스터 72마리, 12초의 실제 이동·전투다. 지점별 시작/종료 좌표와 이동량을 남긴다.
- HighGrowth의 계산기 입력은 10^400 경험치·개수·연속 단계다. 실제 실행은 네 무기, 개수/연속 강화 0·2·8, 속도 +2, 각각 5초다. 계산기 통과를 무한 물리 객체의 실행 가능성으로 해석하지 않는다.
- RepeatedRestart는 세 번 사망·재시작하며 이전 판의 경험치 비행, 투사체, 공격 예약, View 세대, scope를 검사한다.

## 측정값의 의미

- 모델과 표시 갱신 시간은 각각 호출 구간의 Stopwatch 시간이다. 실행 CPU/그래픽 장치·메모리·해상도·Editor/WindowsPlayer를 결과에 저장한다.
- 여러 주기를 한 렌더 프레임에 묶어 실행한다. 보고한 모델 주기 시간과 렌더 프레임의 실제 경과 시간을 구분하며, 가속 검사의 프레임 시간을 정상 게임 FPS로 환산하지 않는다.
- 할당은 Unity의 GC Allocated In Frame 카운터를 사용한 렌더 프레임 전체의 합계다. Editor와 검증 코드 비용을 포함한다. 모델 전용 할당량이 아니다.
- 메모리는 Unity Mono 사용량, Unity 할당량, Windows GetProcessMemoryInfo의 실제 working set을 구분한다. Mono의 일반 .NET 할당/working-set API가 0을 반환해 엔진·운영체제 카운터로 대체했다.
- 메모리의 최대값은 시나리오 표본과 종료 시점에 읽은 값 중 최대다. 모든 프레임을 연속 측정한 최고치는 아니다.
- 경로 요청 수와 가장 오래 대기한 요청의 시뮬레이션 시간, 발동 예약 수와 최대 처리 지연을 기록한다.
- 개체 삭제·업데이트 생략·게임 종료 시각·강화 상한으로 처리량을 맞추지 않는다. 수치 표현 한계는 예외로 확인하며 적용을 원자적으로 거부한다.

## 발견한 문제와 재현

- 초기 통합 runner 미연결: EndToEnd RED `a85d3f4ea94c4f468ff5e33655102846`. 실제 버튼/전투 연결 후 PlayMode `56ca6c2959594efe8899f61e20e5394b`, Smoke `c3721e0ffeb743d28968ab5fa434a73d`에서 성공했다. 최종 소스에 대한 전체 재검증은 별도 기록한다.
- 군집 경로: `a669d16cd6ca464fb79c33e166fef768`에서 72마리 중 43마리만 0.5 이상 이동했다. 시작 경로 중심을 반드시 밟으려다 뒤쪽 개체가 막혔다. 몸 반경으로 안전이 확인된 앞쪽 경로 지점을 선택하도록 보완했다.
- 위 보완 후 실제 밀집이 벽 모서리에 도달하면서 아주 작은 보정 이동의 충돌 누락이 드러났다(`e1b8ad5f41214f3a95b2649952b066b5`). 제곱 이동량을 일반 오차 상수와 비교해 모서리 검사를 건너뛰던 조건을 수정했다. 작은 모서리 충돌 RED `e781a966e1904159b334ba4c30a5eb6b`, 관련 4개 GREEN `985cd60d03e747b888624fa154453aa4`.
- 장시간 예비 실행에서 창·도끼의 진행률이 부동소수점 반올림으로 1 직전에 남아, 종료된 공격과 표시가 누적되는 문제를 발견했다. 해당 실행 `4d351e1f962f4b1192ecb3cc98119250`은 중단했으며 성공 증거로 쓰지 않는다. 종료 시각에 도달하면 진행률을 정확히 1로 처리하도록 수정했다. RED `86da97a9a669438d89d7b565f4bd5687`, 전체 EditMode 170개 GREEN `412a9a49733f41b1b8f420a1ef69433b`. 장시간 실행에서도 매 60초에 만료된 공격이 남지 않는지 검사한다.
- 구조 설계의 보유 무기 HUD 항목을 연결했다. 실제 네 무기를 가진 화면에서 철검·철창·철도끼·파이어볼이 함께 표시되고 경험치/레벨 및 선택창과 겹치지 않는 것을 확인했다.
- Windows 실행 검증은 빌드가 반환한 경로의 슬래시를 정규화한 후 경로와 실행 파일 해시를 모두 비교한다. 같은 파일을 다른 경로 표기 때문에 거절하던 부분을 바로잡았다.
- 첫 Windows 빌드 `e6a3d19bdb334e7fbb60dc6061d3f261`의 BuildReport는 성공·오류 0이었으나 Unity의 자동 저장으로 소스 해시가 바뀌어 검증에서는 실패시켰다. 플레이어 배칭·서비스·그래픽 설정과 URP의 빌드용 직렬화를 원인으로 확인했다. 사용자의 기존 ProjectSettings 파일을 해시가 일치하는 원본으로 복원했으며, 빌드 도구는 영향을 받은 설정 6개를 실행 전에 백업하고 성공/실패 모두 원본 바이트로 복원한다. 검증은 파일별 전후 해시와 전체 소스 해시를 함께 확인한다.
- 실제 exe의 첫 실행 `1ddae21bfea54eabad84430cb5a2aeca`에서 게임 흐름은 성공했지만 전체 검증은 실패했다. JSON의 UTC 문자열이 PowerShell DateTime으로 자동 변환된 뒤 지역 시각으로 재해석되는 오류였다. 판독 시 문자열과 소수 초를 그대로 보존하도록 수정하고 해당 회귀 검사를 추가했다(RED `d3-timestamp-red.txt`, 판독 검사 총 39개). 검증 진입점은 PowerShell 7.5 이상이며 이번 환경은 7.6.5다.
- 숨긴 Windows 창의 기본 화면 캡처는 시작 화면 종료를 기다린 뒤에도 Direct3D 12/11 모두 단색이었다(`8f820462848d4f359a9839f276edd26a`, `1d5b66fbd9404c3aa89a405eeba42e56`). 단색은 실패로 판독한다. 숨긴 실행에서만 같은 실제 런타임 카메라와 활성 UI를 RenderTexture에 직접 렌더링하는 대체 촬영을 사용하고, 직후 원래 카메라·Canvas 상태를 복원한다. 이 촬영은 보이는 데스크톱 창의 캡처가 아니며 결과에 `OffscreenCameraCapture`로 기록한다. `271fbbf548d2427bb583c751cbcb836b`에서 실제 exe의 전투 HUD·유닛과 결과·재시작 버튼을 확인했다.
- Editor가 백그라운드일 때 inspector의 지연 콜백이 실행되지 않아 빌드가 대기했다(`02616673e8124ef4b14fd124f4462b86`, 실제 빌드는 시작하지 않음). 빌드 예약을 다음 Editor update에서 한 번 실행하도록 수정했다.
- 별도 시나리오는 Unity TestRunner와 달리 백그라운드 실행을 자동 설정하지 않아 프레임 2에서 대기했다. `runInBackground=false`를 관측했고, 일시 활성화 후 실제 11주기와 경험치 경계 비행까지 진행되는 것을 확인했다. ScenarioRunner는 실행 동안만 이 값을 활성화하고 완료·취소·파괴 때 이전 값으로 복원한다. 진단 때문에 사용자 프로젝트 설정 파일을 바꾸지 않는다.
- `cfe4a29fb55a439db9f293728126bf87`은 17,817주기·356.34초 뒤 이동 보정의 벽 충돌 검증에서 실패했다. 같은 소스에서 허용 오차 이내로 벽에 붙어 있을 때 진입 시각이 아주 작은 음수라 충돌을 놓치는 경우를 재현했다. 면/모서리 두 RED는 `f25e4af3919f47ed977311964fdd391d`다. 겹침 허용 오차와 같은 접촉을 시각 0으로 판정하도록 수정했다. 전체 EditMode `2a4f509ccdd3402db5b5246ef0079f92` 173개와 별도 8,448개 근접 접촉 조합을 확인했다. 장시간 실패 시 이동 시작·결과·이동량·반경을 원래 정밀도로 기록한다.
- 위 실패 실행의 모델 주기는 평균 14.19ms/P95 35.68ms였다. 작은 반경 검색이 모든 원거리 청크를 순회하던 부분을 인접 최대 9청크 조회로 바꾸고, 같은 청크 안의 변위는 작은 좌표끼리 직접 뺀다. 큰 반경은 기존 전체 검색을 유지하며 어느 개체도 삭제하거나 업데이트에서 제외하지 않는다. 정수 좌표 양 끝·청크 경계·32 초과 반경·최대 유한 반경의 결과를 전체 데이터 대조로 확인했다.
- 분리된 반복 측정(1,064개 위치, 반경 2.5의 검색 500회)은 145.51ms → 11.65ms, 같은 먼 청크 내 변위 50만 회는 61.09ms → 13.08ms였다. 결과 합계 10,500/62,500은 동일했다. 이는 해당 연산의 측정이며 전체 게임 속도 배율을 뜻하지 않는다. 원시 값은 `d3-spatial-benchmark-before/after.json`, 접촉 조합은 `d3-contact-grid-probe.json`에 있다.
- 이후 30분 실행 `2068dd85e90045ffb526afd4094a0f67`의 699.28초 구간에서 최근 1,000주기 모델 평균 57.46ms, 437개 지상 개체의 근처 검색 한 순회가 6.13ms였다. 해당 실행은 성능 개선을 위해 취소했으며 30분 통과로 사용하지 않는다. 722.02초의 개체 598개 배치를 보관했다. 작은 원 검색에서 같은 청크의 모든 점유 셀을 훑지 않고, 후보 사각형의 셀 수가 더 적으면 직접 조회하도록 보완했다. 정확한 거리·선택 조건·ID 정렬과 모든 원거리 데이터는 유지한다.
- 저장한 동일 배치(유닛 598개, 지상 477개)의 근처 검색 50회 순회는 344.94ms → 104.77ms였다. 검색 결과 개수 38,400개·ID 합계 103,084,250은 동일했다. 단일 셀·청크 경계·아주 먼 좌표의 밀집 배치에서 이동·제거 전후의 원 검색을 전체 데이터와 대조하는 회귀 검사도 추가했다. 원시 자료는 `d3-dense-query-fixture.json`, `d3-dense-query-before.json`, `d3-dense-query-final.json`이다.
- 긴 진단의 제한 시간은 실행 요청에 명시해 최대 14,400초까지 허용한다. 기본값 180초와 만료 요청 거절은 유지하며 게임 생존 시간에는 영향을 주지 않는다. 7,200초 요청 수락 및 만료·상한 초과 거절의 RED는 `e457ec62d01b4278975b6f0879fafdd6`이다.
- Editor의 30분 실행 `a84b49b7803449bb9f164a19eb0d8c59`는 요청한 실제 경과 시간 14,400초 제한으로 실패했다. 중단 직전 별도 관측은 86,616주기·1,732.32초, 유닛 6,063개, 오류 0건이었다. 이는 부분 관측이며 30분 통과가 아니다. 원시 관측은 `d3-30m-partial-metrics-late.json`에 있다. 메모리 표본의 최대 working set은 5,962,375,168바이트였고, 누적 프레임 할당 2,077,088,287,619바이트와 구분한다.
- 실제 Windows 실행 파일에서도 여섯 시나리오를 선택하고 최대 43,200초를 명시할 수 있도록 실행 도구를 확장했다. 기본 제한은 180초다. `standalone-manifest.json`에 요청 기한과 해당 실행의 프로세스 ID를 남긴다. 실행 ID가 일치하는 `cancel.json`을 받으면 실패/취소 기록을 남기고 직접 시작한 프로세스만 정리한다. 지원하지 않던 인수의 RED는 `d3-standalone-stress-arguments-red.txt`다. 게임 코드·프리셋·실제 처리 주기 수는 변경하지 않았다.
- 결과 판독기는 기존 오래된 XML·0개·Skipped·실행 ID·소스 해시 거절과 함께 시나리오 관측·성능 필드 누락, 잘못된 실행 환경, 숫자 NaN, 실제 주기 미실행을 거절한다. 초기 RED는 `Logs/Validation/bcd-baseline/d3-reader-red.txt`에 있다.

## 최종 검증 결과

실행 파일 검증 도구 확장 전 소스 해시: `cba4d64aa647e189c3abfe740c7c0e3f1cde43998ebe4f4237c010774c578573`. 아래는 그 시점의 manifest·receipt·XML 또는 scenario.json과 대조한 결과다. 현재 소스 `0d32d4cb32230394b5e4bef20d78df1be9254f7dd9d61888c0d0eba8cd02e57c`로 다시 확인 중이며, 이전 결과를 수정 후 검증으로 사용하지 않는다.

| 검사 | 실행 ID | 현재 결과 |
| --- | --- | --- |
| EditMode 전체 | `3908fd6c8fa044028e700c64e5a0c635` | 175개 통과 |
| PlayMode 전체 | `08ca1bd550c34883857dd6a89b8ae358` | 21개 통과 |
| EndToEnd | `5f9f1ed646644e12a350f8bff03e7fb8` | 21주기, 버튼·전투·경험치·선택·사망·재시작 통과 |
| RepeatedRestart | `6e5d038cd3284ed6bfdf366ba7a3e6ee` | 63주기, 3회 사망·재시작과 이전 판 정리 통과 |
| CrowdCorridor | `92163404753a48bda475f0217a9caf71` | 600주기·12초, 72마리 군집·벽·순간이동 검사 통과 |
| HighGrowth | `29c782e224404b3fb90c2386c9ae99e8` | 750주기, 계산기와 실제 강화 0·2·8단계 통과 |
| Static | `c1db35f408164db5a4d3de4534061040` | 판독 회귀 39개와 구문 검사 통과; Unity 테스트 수와 별개 |
| Windows 개발 빌드 | `b134f0b955e54bde8b1966c3d029ff46` | Succeeded·오류 0, 261,803,673바이트, 28.44초 |
| WindowsPlayer EndToEnd | `87ed5b34676d4d41bf5007a5b0d2ac6c` | 실제 exe 21주기 통과·종료 코드 0 |
| PersistentWorld10m | `f9a77ad72dbf4dffbdd0d8991bb9068d` | 30,000주기·600초 및 보존·표시 복원 통과 |
| PersistentWorld30m | `a84b49b7803449bb9f164a19eb0d8c59` | 실제 경과 시간 4시간 제한으로 실패; 30분 미완료 |

Windows 실행 파일 SHA-256은 `098a43c3b20762e4bdf938771c36f0fb116126aec8932b2a77eb403f0cb77938`이다. 실제 `reward-selected.png`와 `results.png`에서 전투 HUD·플레이어·슬라임·한글 결과와 버튼을 확인했다. 숨긴 창이므로 `OffscreenCameraCapture` 관측을 포함한다. 빌드에 따른 설정 6개와 기존 Build Settings의 전후 해시가 같다. 출력 위치는 `Builds/SsalMuk/SsalMuk.exe`다.

HighGrowth의 마지막 실제 단계는 발동 3,009회·투사체 374개·예약 22개, 최대 발동 지연 0.02초였다. RepeatedRestart의 마지막 새 판은 유닛 13개·경험치/투사체/예약 0개·scope 1개로 돌아왔다.

### 실행 파일 검증 도구 확장 전의 10분 측정

측정 장비는 Windows 11 `10.0.26200`, AMD Ryzen 7 250 w/ Radeon 780M Graphics, NVIDIA GeForce RTX 5060 Laptop GPU, 시스템 메모리 32,058MB다. 아래 장시간 결과는 Unity `6000.4.6f1` Editor, 1920×1080, 위 보존 프리셋에서 얻었다.

| 10분 측정 항목 | 값 |
| --- | --- |
| 실제 처리한 모델 주기 / 시뮬레이션 시간 | 30,000 / 600초 |
| 검사 실제 경과 시간 | 729.85초 |
| 모델 주기 평균 / P95 / 최대 | 18.48 / 41.52 / 249.62ms |
| 표시 갱신 평균 / P95 | 1.54 / 2.88ms |
| 렌더 프레임 경과 시간 P95 | 53.81ms; 실시간 게임 FPS로 환산하지 않음 |
| 최대 Mono / Unity 할당 / working set | 1,535,299,584 / 571,585,436 / 5,926,354,944바이트 |
| 전체 렌더 프레임 누적 GC 할당 | 85,022,716,419바이트; 상주 메모리 크기가 아님 |
| 600초 종료 시 논리 유닛 / 경험치 | 335 / 903 |
| 600초 종료 시 표시 유닛 / 경험치 | 133 / 129 |
| 누적 일반 / 공중 / 보스 생성 | 2,100 / 305 / 2; 초기·별도 배치와 구분 |
| 종료 시 경로 요청 / 가장 오래된 대기 | 207 / 0.64초 |
| 종료 시 공격 예약 / 최대 처리 지연 | 4 / 0.02초 |
| 별도 구슬 600개 추가 후 논리 / 표시 경험치 | 1,503 / 725 |

10분 동안 생존 개체 원장과 만료 공격 정리를 검사했다. 원거리 공중은 계속 직진하고, 일반 몬스터는 이동했으며, 흡수 중 구슬은 표시가 없어도 접촉 지급됐다. 원거리 구슬 12개의 ID·값·위치와 왕복 표시를 확인했고 보스 2마리는 함께 생존했다. 실제 `far-experience-return.png`와 `glow-load.png`도 확인했다. 성능 수치는 기본 난이도 생존율이나 모든 강화 단계에서의 일정 프레임률을 보증하지 않는다.

| 계약 | 대응 검사 |
| --- | --- |
| H01~H03 | RunCoordinatorTests, SessionLifecycleTests, RunClockTests, LevelUpLiveCombatTests, EndToEnd |
| H04~H06 | WorldGenerationTests, WorldPositionTests, WorldStoreTests, NavigationTests, CircleSweepTests, PersistentWorldViewTests, PersistentWorld10m/30m |
| H07~H09 | TargetResolverTests, LowAiTests, FeedbackAndPoolTests |
| H10~H13 | MeleeGeometryTests, SwordAttackTests, FireballTests, WeaponSchedulingTests, WeaponGrowthTests, MultiWeaponCombatTests, AttackVisualTests |
| H14~H16 | RewardTests, ProgressionTests, NumericRangeTests, LevelUpLiveCombatTests, HighGrowth |
| H17~H19 | SpawnScheduleTests, SpawnDirectorTests, AirMovementTests, EnemyArchetypeTests, CrowdMovementTests, CrowdCorridor |
| H20~H22 | DamageTests, ContactDamageTests, DeathDropTests, FeedbackAndPoolTests |
| H23~H25 | RunResultTests, DeathRestartTests, SessionLifecycleTests, FeedbackAndPoolTests, RepeatedRestart |
| H26~H28 | LevelUpPresenterTests, DeathRestartTests, PersistentWorld10m/30m, WindowsPlayer EndToEnd |
| H29~H30 | ExperiencePickupTests, ExperienceVisualTests, PersistentWorldViewTests, ArtPresentationTests, AttackVisualTests |
