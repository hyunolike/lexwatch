# 명세: AI 서비스가 깨어나지 않을 때 작업 단계 재시도 (2026-11-26)

- 작성: hh.jang
- 관련: [collect-health](../collect-health/spec.md)(이 명세가 그 "범위 밖: 재시도 정책 변경"을 바꾼다), [review-chat-ux](../review-chat-ux/spec.md)의 `ChatRetry`

## 해결할 문제

2026-11-25 04:44 UTC 수집이 3건(응급의료에 관한 법률 시행규칙, 매장유산 보호 및 조사에 관한 법률 시행규칙, 구급차의 기준 및 응급환자이송업의 시설 등 기준에 관한 규칙)을 만들었다. 세 건 모두 분류 단계가 다섯 번 시도한 끝에 `AI_UPSTREAM_ERROR (502)`로 실패했다.

- 수집 잡의 AI 깨우기(CH-02)가 3분 동안 12번 모두 503을 받았다. Render 로그에는 01:14에 잠든 AI 서비스가 21:21까지 켜진 기록이 없고, 그 사이 분류 요청이 앱에 닿은 기록도 없다. 502와 503은 Render 프록시가 돌려준 것이다.
- Render 문서는 무료 서비스가 깨는 데 약 1분이 걸린다고 하지만 사용자 보고는 2분 30초에서 3분이고, 이번에는 3분을 넘겼다.
- 분류 재시도 대기는 2, 4, 8, 16초로 합쳐서 약 30초다. AI 서비스가 몇 분 동안 꺼져 있으면 다섯 번을 모두 써 버린다.
- 지금 `AiGateway`는 AI 서비스가 직접 보낸 오류(본문에 `code`가 있다)와 프록시가 보낸 오류(본문에 `code`가 없다)를 같은 `AI_UPSTREAM_ERROR`로 다룬다.
- 2026-11-23에도 같은 증상이 있었고(collect-health), 그때는 깨우기를 먼저 하는 것으로 막았다. 이번에는 깨우기 3분으로도 부족했다.
- 실패한 3건은 2026-11-26에 관리자 재실행으로 회복했다(분류 성공, 세 그룹 모두 관련 없음).

## 레퍼런스

- 재시도해서 나아질 오류만 재시도하고 나머지는 빨리 실패시킨다: [AWS Well-Architected REL05-BP03](https://docs.aws.amazon.com/ko_kr/wellarchitected/2023-04-10/framework/rel_mitigate_interaction_failure_limit_retries.html), [AWS Batch 서비스 작업 재시도](https://docs.aws.amazon.com/ko_kr/batch/latest/userguide/service-job-retries.html), [Temporal non-retryable errors](https://docs.temporal.io/best-practices/error-handling).
- 간격을 늘리고 무작위 값(지터)을 더하며 대기와 전체 시간 모두 상한을 둔다: [AWS Builders' Library](https://aws.amazon.com/builders-library/timeouts-retries-and-backoff-with-jitter), [Google SRE: Addressing Cascading Failures](https://sre.google/sre-book/addressing-cascading-failures/).
- 시도당 시간과 전체 시간을 따로 둔다: [Temporal timeouts and retries](https://docs.temporal.io/evaluate/features/timeouts-and-retries).
- 재시도는 거절한 층 바로 위 한 층에서만 한다: [Google SRE: Handling Overload](https://sre.google/sre-book/handling-overload/).
- 무료 서비스는 15분 동안 요청이 없으면 잠들고 다음 요청에 깬다: [Render: Your First Deploy](https://render.com/docs/your-first-deploy).

## 요구사항

| ID | 요구사항 | 수용 기준 |
|---|---|---|
| WR-01 | 꺼진 AI 서비스를 따로 분류한다 | `AiGateway`가 502, 503, 504 응답 중 본문에 `code`가 없는 것을 `AI_WAKING`으로 돌려준다. 메시지는 "AI 서비스가 아직 깨어나지 않았다(503)"처럼 상태 코드를 담는다. 본문에 `code`가 있으면(AI 서비스가 직접 보낸 오류) 지금처럼 그 코드로 돌려준다. 500과 그 밖의 5xx는 지금처럼 `code ?? AI_UPSTREAM_ERROR`다. 연결 실패는 지금처럼 `AI_UNAVAILABLE`이다 |
| WR-02 | 꺼진 상태일 때만 길게 기다린다 | `RetryPolicy`가 `AI_WAKING`과 `AI_UNAVAILABLE`을 "깨는 중" 오류로 본다. 대기는 `min(30 × 2^(시도−1), 120)`초에 0.8~1.2배 지터를 곱한다. 시도 상한은 단계 상한과 7 중 큰 값이다. 7번째 시도가 실패하면 실패로 끝난다. 지터 없이 계산한 대기 합은 30+60+120×4 = 570초(9분 30초)이고 시도 시간까지 더해 약 10분이다 |
| WR-03 | 다른 오류는 그대로다 | `LLM_RATE_LIMITED`(서버가 준 `retry_after`), `LLM_OUTPUT_INVALID`, `AI_TIMEOUT`, 코드가 있는 5xx는 지금 상한과 `2^시도`초 대기를 그대로 쓴다 |
| WR-04 | 재시도는 한 층에서만 한다 | 실패한 실행을 다시 돌리는 일은 워커의 재시도와 관리자 재실행만 한다. 수집 잡(GitHub Actions)은 실행을 다시 돌리지 않는다 |
| WR-05 | 수집 잡이 긴 대기를 끝까지 지켜본다 | 감시 단계를 최대 15분에서 25분으로 늘린다(깨는 대기 약 10분 + 분류, 분석, 크리틱). `timeout-minutes`는 30에서 40으로 늘린다 |
| WR-06 | 검토 보조도 새 코드를 일시 오류로 본다 | `ChatRetry`의 일시 오류 목록에 `AI_WAKING`을 더한다. 채팅의 대기 규칙(전체 90초, 시도당 40초, 간격 4초)은 그대로다 |

## 오류 상황

- 깨는 중 오류와 다른 오류가 섞이면 각 시도의 오류로 그때그때 판단한다. 예를 들어 분류의 1번째가 `LLM_UPSTREAM`, 2번째가 `AI_WAKING`이면 2번째 뒤에는 60초를 기다린다(상한 7). 5번째나 6번째 시도가 `AI_TIMEOUT`이면 분류 단계 상한(5)에 닿았으므로 그 자리에서 실패로 끝난다.
- 대기 중인 단계는 `RetryScheduled`로 남아 수집 잡의 감시가 "남은 일"로 센다(CH-04). API가 잠들지 않도록 감시 단계가 1분마다 `wake-ai`를 부르는 동작은 그대로다.
- 시도 시각은 `StepRun.StartedAt`이 시도마다 덮여 쓰여서 첫 시도 시각을 알 수 없다. 그래서 전체 시간 상한은 열을 더하지 않고 시도 상한과 고정 대기로 정한다(약 10분).
- AI 서비스가 깨어나 응답하기 시작하면 다음 시도부터 정상 경로를 탄다.

## 범위 밖

- AI 서비스를 깨어 있게 두는 예약 핑. Render 지원팀은 무료 인스턴스를 핑으로 깨워 두는 것을 서비스 남용으로 보고 유료 인스턴스를 권한다.
- `AI_TIMEOUT`을 깨는 중 오류로 보는 일. 느린 LLM 응답에서도 나므로 10분까지 기다리면 분석 단계 실패가 늦게 드러난다.
- 첫 시도 시각 열을 더하는 마이그레이션.
- Polly 같은 재시도 라이브러리 도입. 재시도는 DB 큐의 `NotBefore`로 이미 하고 있어 그대로 둔다.
