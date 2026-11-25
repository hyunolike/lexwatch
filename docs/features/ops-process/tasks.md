# 작업: 운영 프로세스 개선

명세: [spec.md](spec.md) / 브랜치: `chore/ops-process`

| ID | 변경 | 검증 | 상태 |
|---|---|---|---|
| OP-01 | `docs/incident-review.md`, ai-wake-retry 명세에 회고 표 | 문서 확인 | 완료 |
| OP-02 | `collect.yml` 예약 두 개와 `concurrency`, `reset-demo.yml` 7분 | `actionlint` | 완료 |
| OP-03 | `collect.yml`의 `alert` 작업, 성공 신호 단계, deploy.md 시크릿 설명 | `actionlint`. 병합 뒤 실패 경로는 실제 실패 때 확인 | 완료 |
| OP-04 | deploy.md "로컬 개발의 LLM 키", `.env.example` 주석 | 문서 확인 | 완료 |
| OP-05 | ai-wake-retry 배포 뒤 확인 이슈 | 이슈 번호를 ai-wake-retry tasks.md에 적음 | 완료 |

## 사용자가 할 일

- 데드맨 스위치 모니터 만들기(Healthchecks.io 등, 주기 1일, 유예 2시간)와 GitHub 시크릿 `HEALTHCHECK_URL` 등록.
- 저장소 Watch를 켜 두어 `ops-alert` 이슈 메일 받기.
- 운영과 다른 계정의 Cloudflare, Groq 키를 받아 로컬 `.env`에 넣기.
