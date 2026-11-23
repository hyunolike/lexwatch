# 작업: 승인 전 적발 확인과 비율 대조, 수집 실패 알림

- 명세: [approval-guard/spec.md](spec.md), [collect-health/spec.md](../collect-health/spec.md)
- 설계: [design.md](design.md)
- 진행: 작업 1~5 완료(2026-11-23). 작업 5에서 찾은 감시 조기 종료는 #20에서 고쳤다
- 검증(2026-11-23):
  - 백엔드 `make test-backend` 207건 통과. 새 테스트: 비율 대조 12건, 승인 재검사와 확인 6건, 재현 fixture 적발 = 승인 재검사 결과, `wake-ai` 2건, `runIds`
  - 웹 vitest 96건, 타입 검사, `eslint src`, `pnpm build` 통과
  - 로컬 화면(API + Postgres + 웹, 재현 데이터): 적발 1건 초안 승인 대화상자에 "확인이 필요한 문장 1개" 표시 후 승인 기록 `AcknowledgedFindingCount = 1`. 2026-11-23 배포본에서 그대로 통과했던 "100분의 80" 수정 승인이 422로 막히고 결정 영역에 "문장 1 · 검사 2단 · 인용한 근거 본문에 없는 표기: 100분의 80"이 뜸. 문장을 "50%"로 고치자 목록이 지워지고 확인 없이 승인, 알림 발송. 로컬 `wake-ai`는 AI가 없을 때 503, 토큰 없으면 401
  - `collect.yml`은 actionlint 통과
  - 운영(#19 배포 fbb09b1): 재현 예시에 "100분의 80" 수정 승인 시 422와 2단 적발, 상태와 초안 불변. 수집 수동 실행에서 `wake-ai` 첫 시도 응답, 5건 생성과 `runIds` 확인. 감시가 2분 만에 끝나 크리틱이 멈춘 문제는 collect-health 운영 기록 참고
  - 운영(#20 배포 62bb8c6): 수동 실행 성공(새 법령 없음). 감시 본문을 첫 판 5건에 돌려 남은 일 0건, 종료 코드 0

| # | 작업 | 요구사항 | 검증 |
|---|---|---|---|
| 1 | `SourceMatchCritic` 비율 토큰 | AG-01 | 단위 테스트: 100분의 70 vs 100분의 50 적발, 100분의 50 = 50% = 50 퍼센트 = 2분의 1, 1천분의 30 = 100분의 3, `%p` 무시, 3분의 2 = 66.67%, 기존 테스트 전부 통과 |
| 2 | `DraftRecheck`와 승인 API, `Approval.AcknowledgedFindingCount` 마이그레이션 | AG-02~06 | 통합 테스트: 저장된 적발 있으면 확인 없이 422 + 상태 불변, 맞는 수로 승인 + 기록, 틀린 수 422, 수정 문장 2단 재검사 422와 새 초안 적발 저장, 3단 내용 짝짓기(문장 삽입 시 따라감), 고친 문장의 3단 "(수정 전 문장)", 적발 0건은 확인 없이 승인. 기존 승인 테스트는 시드에 3단 적발이 있으므로 `acknowledgedFindingCount = 1`을 보낸다 |
| 3 | 웹 `submitDecision`과 승인 흐름 | AG-07 | vitest: 422 접기, 본문의 `acknowledgedFindingCount`. 배포 후 재현 예시로 수동 확인 |
| 4 | `POST /jobs/wake-ai`, collect 응답 `runIds` | CH-01, 03 | 통합 테스트: 401, AI 응답 200, AI 오류 503, `runIds` 개수 = created |
| 5 | `collect.yml` 깨우기와 실패 감시 | CH-02, 04, 05 | 머지 후 워크플로 수동 실행 (2026-11-23 완료, 위 운영 기록) |
