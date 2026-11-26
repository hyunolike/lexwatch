# 작업: 검토 보조가 잠든 AI 서비스를 일찍 깨우고 깰 때까지 질문을 들고 기다린다

명세: [spec.md](spec.md) / 브랜치: `fix/chat-wake`

| ID | 변경 | 검증 | 상태 |
|---|---|---|---|
| CW-01 | `AiWarmup`(레이아웃) | 로컬 빌드: 첫 화면에서 `/api/ai/status`와 직접 깨우기 요청 | 완료 |
| CW-02 | `createAiWaker`, `aiWaker` 하나를 앱이 함께 씀 | `aiWaker.test.ts`: 동시 호출 하나로 합침, 1분 안의 `up`은 다시 묻지 않음, 마감, 예외 | 완료 |
| CW-03, 04 | `ReviewChat.send`가 깨우기를 먼저 기다림, 상태로 문구를 고름 | 로컬 빌드로 운영 API 재현 | 완료 |
| CW-05 | `poke` 옵션, `NEXT_PUBLIC_AI_WAKE_URL`(`netlify.toml`) | `aiWaker.test.ts`: 요청을 겹쳐 보내지 않음, 실패해도 계속 | 완료 |
| CW-06 | `collect.yml` `AI_PUBLIC_URL` 직접 요청 두 곳 | `actionlint` | 완료 |

## 검증 기록 (2026-11-26)

- vitest 364건, tsc, eslint, build(두 테마 CSS 검사), `actionlint` 통과.
- 운영 재현(로컬 빌드, Playwright가 API 요청을 운영으로 넘김, 추천 질문 1건, AI가 잠든 상태):
  - CW-01~04만 넣은 빌드(00:31 UTC): 질문 직후 말풍선이 "AI 서버를 깨우는 중이에요…"로 바뀌었다. 하지만 `/api/ai/status` 36번에도 AI가 깨지 않았고, 180초 뒤 "서버를 깨우는 중이에요. 잠시 뒤에 다시 물어봐 주세요."로 끝났다. 이 결과로 원인 4를 찾았다.
  - CW-05를 더한 빌드(00:57 UTC, API와 AI 모두 잠듦): 앱을 열자 바로 AI 직접 요청을 보냈고 17.9초에 깼다. 검토 화면에 들어갔을 때(28.8초) 머리는 "AI 준비됨"이었고, 질문 뒤 17.9초에 Cloudflare 답과 인용이 왔다(앱을 연 뒤 47초).
- 운영 배포 뒤 확인: 다음 예약 수집의 AI 깨우기 단계가 1~2번째 시도에 응답하는지.
