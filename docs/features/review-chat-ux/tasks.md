# 작업: 검토 보조 UX

명세: [spec.md](spec.md)

| ID | 변경 | 검증 | 상태 |
|---|---|---|---|
| RC-01 | `ReviewChat` 머리와 카드 띠 | 화면 확인(밝게, 어둡게) | 완료 |
| RC-02 | `reviewChat.suggestQuestions()`, 추천 칩 | `reviewChat.test.ts` | 완료 |
| RC-03 | `TypingDots`, 답 나타남 | 화면 확인, 움직임 줄이기 | 완료 |
| RC-05 | AI `ReviewChatRequest.draft`(`DraftLine`), `draft_block`, 프롬프트 규칙 두 줄. 백엔드가 최신 초안과 적발을 가려 보낸다. `gen-ai-client.sh`로 클라이언트 재생성 | `test_draft_and_findings_reach_the_model_as_labelled_data`, `test_missing_draft_is_stated_to_the_model`, `Chat_sends_the_draft_and_its_findings_to_the_ai` | 완료 |
| RC-06 | `evidence_block`(예산 4,500자, 건당 800자, 인용과 신구조문 먼저, 나머지는 ID만), `DEADLINE_S` 36초, `_refuse(reason=)` | `test_evidence_block_keeps_cited_bodies_within_budget`. 운영 근거 54건을 2,000자로 늘린 재현: 수정 전 3회 중 2회 거절(30.1초), 수정 뒤 4개 질문 모두 답(8.8~20.9초) | 완료 |
| RC-07 | `base.OptionalTools`, `GroqProvider.chat`이 선택 도구를 뺀다 | `test_groq_drops_optional_tools_and_keeps_json_format`. 같은 재현을 Groq로: 수정 전 400(형식+도구), 413(프롬프트 8,015토큰), 없는 도구 호출 400. 수정 뒤 2.0~3.7초에 인용과 함께 답 | 완료 |
| RC-04 | 답 인용을 `CitationChip`으로, `ReviewClient`가 분할 화면 선택 함수를 넘김 | 화면 확인 | 완료 |

## 검증 기록 (2026-11-25)

- vitest 356건, tsc, eslint, build(두 테마 CSS 검사) 통과. AI 서비스 pytest 126건.
- 운영 API를 부르는 로컬 빌드, 1440px: 추천 칩을 누르면 바로 보내지고 점 세 개 표시 뒤 실제 Cloudflare 답과 인용 칩이 나온다. 인용 칩을 누르면 근거 패널로 스크롤하고 그 근거가 선택된다(`aria-pressed=true`).
- 어두운 테마, 390px(가로 넘침 없음) 확인.
- 같이 고친 것: 추천 질문과 사용자의 실제 질문("영향 검토 초안 문제점 요약해줘")이 입력 가드레일에서 범위 밖으로 거절됐다. [eval-fixes EF-13](../eval-fixes/spec.md).
