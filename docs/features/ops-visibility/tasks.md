# 작업: 운영 시각화 (2026-11-24)

- 관련: [명세](spec.md), [설계](design.md), [검토 1차](reviews/2026-11-24-round1.md)

## 현재 단계

구현 완료. 백엔드 전체 테스트와 화면 확인은 PR CI와 운영 배포 뒤에 한다(로컬 Docker가 다른 작업으로 불안정해 테스트 컨테이너가 멈췄다).

## 작업

| # | 작업 | 요구사항 | 상태 |
|---|---|---|---|
| 1 | `lib/runTimeline.ts` 배치 계산과 테스트 | OV-02~06 | 완료 |
| 2 | `RunTimeline` 컴포넌트와 목록/타임라인 탭 | OV-01, OV-03, OV-07 | 완료 |
| 3 | `OpsSummary.Build`, `GET /api/ops/summary` | OV-10~16 | 완료 |
| 4 | `/ops` 화면, 스파크라인, 관리자 메뉴 | OV-17 | 완료 |
| 5 | README | | 완료 |
| 6 | PR CI 백엔드 전체 테스트, 운영 화면 확인 | | 대기 |

## 검증 (2026-11-24, 로컬)

- 백엔드: `OpsSummaryTests` 6건 통과(SDK 컨테이너). 전체 테스트와 `OpsEndpointTests`(403, 400, 기본 14일)는 PR CI에서 확인한다.
- 웹: `tsc --noEmit`, `eslint src`, Vitest 190건(`runTimeline.test.ts` 10건, `ops.test.ts` 3건 포함), `next build` 통과.
