# 경험치 구역 수집과 몹몰이 복귀 구현 계획

**Goal:** 가까운 경험치를 적극적으로 수집하고, 쫓겨난 구역을 기억해 무리를 옆으로 돌아 회수한다. 경험치 증가에 따라 AI 계산이 구슬 수×적 수로 커지는 부분을 제거한다.

**Architecture:** 기존 Collect/Evade/Breakout의 긴급 FSM을 유지한다. CollectState의 구역별 계획과 별도 LootRecovery의 유인/우회/복귀 전술을 결합한다. 주변 적의 상대 좌표는 관측 주기마다 한 번 계산해 재사용한다.

**Tech Stack:** Unity 6000.4.6f1, 순수 C# Core, NUnit EditMode, 연결된 Editor의 PlayMode.

**Spec:** 사용자 승인(2026-09-25), GAME_DESIGN 3절, 구조 설계 7절 AI. 가까운 수집 → 한 방향 유인 → 덜 막힌 좌우 우회 → 경험치 회수. 판단 주기 0.2초, 긴급 위험은 기존 고정 주기.

**Global Constraints:** 현재 프로젝트에서 작업. 박쥐 퇴장·스폰·성장·경험치 보존/값/비행/지급은 변경하지 않는다. boss 우선 공격과 풍부한 경험치 우선 접근을 유지한다. 새 빌드/전체/장시간 검사/푸시는 하지 않는다. 기존 복구 씬은 제외한다.

**Review Focus:** 청크 경계·큰 좌표·거대 XP, 같은 구역의 접근 불가능한 구슬, 수집/흡수 중 목표 무효화, 계속 추격하는 군중에 대한 우회 진전, 보스 예고 중 즉시 취소, 경로 요청과 기록의 무한 누적 여부.

## Task 1: 회귀 사례와 구역 계획

- [x] `Tests/EditMode/LootRecoveryTests.cs`에 밀집 경험치 선호·유인 후 시야 밖 기억·우회 방향·흡수 중 목표 해제·실제 회수 사례 작성.
- [x] 새 fixture 실행. Expected: 현재 AI는 구역 합산/복귀 행동 사례 실패, 컴파일 오류는 없음.
- [x] `Core/AI/AiContext.cs`, `CollectState.cs`, `AiSettings.cs`와 구역 계획 클래스를 수정한다. 4×4 구역의 합계/접근 위험을 0.2초마다 평가하고 현재 목표로의 입력과 안전 판단은 매 주기 갱신한다.

## Task 2: 유인·우회·복귀 연결

- [x] `Core/AI/LootRecovery.cs`를 추가하고 `LowAiController.cs`에 연결한다. 기존 FSM은 긴급 동작을 맡고 복귀 기억은 상태 전환을 넘어 유지한다.
- [x] `AiContext`가 보스 예고/돌진 선분을 위험으로 취급한다. 즉시 회피하고 안전해진 뒤 우회 전술로 복귀한다.
- [x] LowAiTests, LootRecoveryTests, 관련 교전/경험치/보스 검증 실행. Expected: 실제 발견된 사례 전부 통과.

## Task 3: 측정과 통합 검증

- [x] 동일한 AI 단독 500적·경험치 0/200/1000 배치, 청크 경계/중앙을 기존 probe와 비교한다. 계획 주기와 비계획 주기 모두 포함한다. Expected: XP×적 병목 감소; 전체 게임 FPS로 해석하지 않는다.
- [x] 실제 플레이 진입/자동 검 공격 및 경험치 표시 PlayMode를 실행한다. 새 fixture를 통해 몹몰이 후 실제 접촉 지급을 검증한다.
- [x] 기획·구조·하네스·구현 계획·HANDOFF 최신화, 소스 해시/manifest/receipt/XML을 대조하고 작업 파일만 커밋한다. `log.md`는 상세 요청이 없으므로 갱신하지 않는다.

## Execution ledger

- Pre-flight: Task 1의 구역 후보/목표와 Task 2의 복귀 기억은 같은 WorldPosition/XP ID를 사용한다. Task 3은 최종 컴파일 소스 해시를 검증한다. 인터페이스 충돌 없음.
- 사용자 승인으로 현재 프로젝트를 직접 사용하며 추가 승인 없이 구현한다.

- Task 1/2: complete. 최초 새 사례 3개 실패(`3ed80a9796014eb09147d49a9dd2aeda`) 후 구현, 새 fixture 11개와 기존 AI 18개 통과. 초기 도주 드롭 사례의 거리 0.7은 제동으로 위험하지 않아 0.6으로 고쳤으며, 실제 기억 실패는 `52daca54349f47b48d86f42fd23823c8`에서 확인했다.
- Final review: 요청한 별도 검토에서 중요한 항목 2개(먼 복귀 전체 지형 검사, 접근 불가능 XP의 점수 부풀림)를 확인했다. 두 재현 테스트 `f6629728f1634e75aeee38d6efd89edd` RED → `1c6f233abcc64174bbe19420fa69e3b0` GREEN. 미해결 중요/경미 항목 없음.
- Task 3: complete. 최종 소스 `c8ffb1758b547207ef34763935e79338c8df7a340daa1212fa224c386445dea1`에서 관련 EditMode 68/68·PlayMode 4/4, receipt/XML/새 테스트 이름 검증. 실제 카탈로그 몹몰이 후 21.64초 모델 시간에 XP 지급. 동일 500적/1000XP 단독 AI 평균은 경계 106.351→1.366ms·중앙 69.839→1.658ms이며 전체 FPS는 미측정. 증거 경로 `Logs/Validation/loot-kiting-20260925/`.
- 통합 방식은 기존 사용자 지시대로 현재 브랜치에 로컬 커밋이다. 별도 병합/푸시/새 빌드/전체 검사와 기존 복구 씬 변경은 범위 밖이다.
