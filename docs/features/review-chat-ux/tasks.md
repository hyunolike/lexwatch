# 작업: 검토 보조 UX

명세: [spec.md](spec.md)

| ID | 변경 | 검증 | 상태 |
|---|---|---|---|
| RC-01 | `ReviewChat` 머리와 카드 띠 | 화면 확인(밝게, 어둡게) | 완료 |
| RC-02 | `reviewChat.suggestQuestions()`, 추천 칩 | `reviewChat.test.ts` | 완료 |
| RC-03 | `TypingDots`, 답 나타남 | 화면 확인, 움직임 줄이기 | 완료 |
| RC-04 | 답 인용을 `CitationChip`으로, `ReviewClient`가 분할 화면 선택 함수를 넘김 | 화면 확인 | 완료 |

## 검증 기록 (2026-11-25)

- vitest 356건, tsc, eslint, build(두 테마 CSS 검사) 통과. AI 서비스 pytest 126건.
- 운영 API를 부르는 로컬 빌드, 1440px: 추천 칩을 누르면 바로 보내지고 점 세 개 표시 뒤 실제 Cloudflare 답과 인용 칩이 나온다. 인용 칩을 누르면 근거 패널로 스크롤하고 그 근거가 선택된다(`aria-pressed=true`).
- 어두운 테마, 390px(가로 넘침 없음) 확인.
- 같이 고친 것: 추천 질문과 사용자의 실제 질문("영향 검토 초안 문제점 요약해줘")이 입력 가드레일에서 범위 밖으로 거절됐다. [eval-fixes EF-13](../eval-fixes/spec.md).
