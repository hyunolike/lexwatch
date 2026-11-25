# 명세: 운영 프로세스 개선 (2026-11-26)

- 작성: hh.jang
- 관련: [ai-wake-retry](../ai-wake-retry/spec.md), [collect-health](../collect-health/spec.md), [장애 회고 규칙](../../incident-review.md)

## 해결할 문제

2026-11-25 수집 실패(분류 3건)를 돌아보니 고친 코드와 별개로 프로세스에 빈틈이 다섯 가지 있었다.

1. 11-23에 같은 증상이 있었고 그때는 깨우기만 더하고 재시도 정책 변경은 "범위 밖"으로 뺐다. 피해를 줄이는 조치만 하고 예방은 하지 않아 이틀 뒤 다시 났다.
2. 잡은 04:49 UTC에 실패했는데 21:21에야 발견했다(약 16시간). 사용자가 "다시 확인"을 요청해서 알았다. GitHub는 예약 실행 실패를 워크플로를 만든 사람에게만 메일로 알리고, 예약이 빠진 경우는 알리지 않는다.
3. 예약은 22:00 UTC(07:00 KST)였지만 04:44 UTC에 시작했다. collect-health 기록에도 실제 시작이 10:50~12:00 KST였고, reset-demo는 11-23에 아예 돌지 않았다. GitHub는 부하가 높으면 예약 실행을 늦추거나 빼며 매 정시가 가장 붐빈다.
4. 11-25 로컬 재현이 운영과 같은 Cloudflare 계정의 하루 한도를 다 써서 운영 검토 보조가 실패했다.
5. "배포 뒤 확인할 것"이 `tasks.md`에만 있어 확인했는지 추적되지 않는다.

## 레퍼런스

- 회고 조치의 갈래와 되풀이되는 장애: [Lunney 외, Postmortem Action Items(2017)](https://sre.google/static/pdf/login_spring17_09_lunney.pdf), [SRE Workbook: Postmortem Culture](https://sre.google/workbook/postmortem-culture/), [Google Cloud: 철저한 사후 분석 실시](https://docs.cloud.google.com/architecture/framework/reliability/conduct-postmortems?hl=ko), 카카오 2022년 장애 회고([바이라인네트워크](https://byline.network/2022/12/1208/)).
- 예약 실행 알림의 범위: [GitHub Docs: Notifications for workflow runs](https://docs.github.com/en/actions/concepts/workflows-and-actions/notifications-for-workflow-runs).
- 예약 실행 지연과 누락, 정시를 피하는 방법: [GitHub Community #185355](https://github.com/orgs/community/discussions/185355), [Crontap: cron drift](https://crontap.com/blog/github-actions-cron-drift-problem).
- 데드맨 스위치: [Healthchecks.io 문서](https://healthchecks.sre.cfainstitute.org/docs/monitoring_cron_jobs/).
- 환경별 키와 계정 단위 한도: [OpenAI Community](https://community.openai.com/t/how-to-prevent-development-api-usage-from-impacting-production/1383970), [Google AI Forum](https://discuss.ai.google.dev/t/how-to-manage-one-gemini-api-key-per-client-in-a-saas-app/88823).

## 요구사항

| ID | 요구사항 | 수용 기준 |
|---|---|---|
| OP-01 | 장애를 고칠 때 회고 표를 쓴다 | [incident-review.md](../../incident-review.md)에 표와 규칙이 있다. 감지, 피해 줄이기, 예방, 배포 뒤 확인 칸이 있고 예방을 비우면 이유를 쓴다. ai-wake-retry 명세에 첫 적용 |
| OP-02 | 예약을 정시에서 옮기고 한 번 더 건다 | `collect`는 22:13과 00:13 UTC(07:13, 09:13 KST), `reset-demo`는 19:07과 21:07 UTC. `collect`는 `concurrency: collect`로 한 번에 하나만 돈다. 두 번째 실행은 이미 가져온 개정을 건너뛰므로 결과가 같다 |
| OP-03 | 실패와 미실행을 사람에게 알린다 | (a) `collect`나 `bills`가 실패하면 `alert` 작업이 `ops-alert` 라벨의 열린 이슈에 덧붙이고, 없으면 새로 연다. 실행 주소와 각 작업 결과를 적는다 (b) 수집이 성공하면 `HEALTHCHECK_URL`(선택 시크릿)에 신호를 보낸다. 시크릿이 없으면 건너뛴다. 모니터 설정은 deploy.md에 적는다 |
| OP-04 | 개발과 운영의 LLM 한도를 나눈다 | deploy.md와 `.env.example`에 "로컬은 다른 계정의 키"를 적는다. 개발 계정 키를 받기 전에는 로컬에서 LLM을 반복 호출하지 않는다 |
| OP-05 | 배포 뒤 확인을 이슈로 추적한다 | ai-wake-retry의 배포 뒤 확인을 `verify` 라벨 이슈로 연다 |

## 범위 밖

- 바깥 스케줄러로 예약을 옮기는 일. GitHub 예약을 두 번 걸고 데드맨 스위치로 빠짐을 알리는 것으로 먼저 본다.
- 데드맨 스위치 계정 만들기와 개발용 LLM 계정 키 발급. 사용자가 한다.
- 알림을 Slack 같은 메신저로 보내는 일.
