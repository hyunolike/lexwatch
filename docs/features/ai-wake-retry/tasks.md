# 작업: AI 서비스가 깨어나지 않을 때 작업 단계 재시도

명세: [spec.md](spec.md) / 브랜치: `fix/ai-wake-retry`

| ID | 변경 | 검증 | 상태 |
|---|---|---|---|
| WR-01 | `AiGateway.Map`: 코드 없는 502, 503, 504를 `AI_WAKING`으로 | `AiGatewayTests`: 코드 없는 503은 `AI_WAKING`, 코드 있는 502는 그 코드, 코드 없는 500은 `AI_UPSTREAM_ERROR` | 완료 |
| WR-02, 03 | `RetryPolicy.Decide`: 깨는 중 오류의 대기(30초부터 2배, 최대 120초, 지터 0.8~1.2)와 상한 7, 지터 함수 주입 | `RetryPolicyTests`(DB 없음): 대기 순서, 지터 범위, 상한, 다른 오류는 그대로 | 완료 |
| WR-04, 05 | `collect.yml` 감시 25분, `timeout-minutes` 40, 주석 | 워크플로 구문 확인(`actionlint`가 있으면) | 완료 |
| WR-06 | `ChatRetry.Transient`에 `AI_WAKING` | `ChatRetryTests` | 완료 |
| 문서 | collect-health 명세의 "범위 밖" 줄에 이 명세 연결 | — | 완료 |

## 검증 기록 (2026-11-26, 로컬)

- `RetryPolicyTests` 10건, `AiGatewayTests`(코드 없는 502·503·504 → `AI_WAKING`, 코드 없는 500 → `AI_UPSTREAM_ERROR`), `ChatRetryTests` 합쳐 27건 통과.
- 백엔드 전체: DB가 필요 없는 204건 통과. 실패 130건은 모두 `DockerUnavailableException`(로컬 Docker API 1.41)이고 CI가 돌린다.
- `actionlint .github/workflows/collect.yml` 통과.
- 실패한 3건(2026-11-25 수집)은 수정 전에 관리자 재실행(`POST /api/runs/{id}/retry`)으로 회복했다. 세 건 모두 분류 1회에 성공했고 세 그룹 모두 관련 없음으로 닫혔다.
- 배포 뒤 확인할 것: 다음 예약 수집에서 AI가 잠든 채 시작하면 분류 단계가 `AI_WAKING`과 30초 이상 대기로 기록되는지. 이슈 [#42](https://github.com/hyunolike/lexwatch/issues/42)로 추적한다.
- 운영 배포: 2026-11-26, `156f17a`(API `/health` 커밋 일치, CI 백엔드 333건 통과).
