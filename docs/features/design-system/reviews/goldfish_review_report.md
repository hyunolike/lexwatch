# 검토 조치 기록 (Elephant)

- 대상: [consolidated_review_report.md](consolidated_review_report.md)의 조치 A1–A18
- 반영본: design.md v2, spec.md v2 (2026-11-19)

## 작성자 결정

| 조치 | 질문 | 결정 |
|---|---|---|
| A5 | shadcn 층을 둘지 | Base UI 직접 사용 |
| A4 | 백엔드 범위 | 근거 본문 앞부분과 공식 변경 표시 모두 백엔드에서 |

## 조치 분류

| 조치 | 분류 | 반영 위치 |
|---|---|---|
| A1 대안 | PROPOSE FIX | design 1.2 |
| A2 파일 표 | PROPOSE FIX | design 4.1, 4.3 (RoleSwitch 제거, favicon 삭제 포함) |
| A3 단계 | PROPOSE FIX | design 6 (단계별 명령, 빼는 순서) |
| A4 백엔드 | 작성자 결정 반영 | design 3, spec 3절 |
| A5 shadcn | 작성자 결정 반영 | design 1.1, 4.1 (의존성 고정) |
| A6 토큰 대비 | PROPOSE FIX | design 2.1, 2.2. `ink-subtle` `#6B7385`→`#5F6779`, `line-strong` `#8A93A6`→`#737D93`, `focus` `#4F46E5`→`#1F6FEB`, `neutral` 추가, 포커스 링 2.4 |
| A7 토큰 원본 | PROPOSE FIX | design 1.1, 6.1 `palette.test.ts` |
| A8 글자와 글꼴 | PROPOSE FIX | design 2.3, spec DS-02 |
| A9 redline | PROPOSE FIX | design 5.1. jsdiff 9.0.0 한글 분할을 실행으로 확인함 |
| A10 라벨 | PROPOSE FIX | design 5.2, 6.1 `labels.test.ts`가 `Enums.cs`를 읽음 |
| A11 결정 흐름 | PROPOSE FIX | design 1.1, 4.3, spec DS-10 |
| A12 DS-12 | PROPOSE FIX | spec DS-12 제외, design 1.2 |
| A13 부품 | PROPOSE FIX | design 4.2 |
| A14 맥락 | PROPOSE FIX | design 0.1, 0.2, 머리말 링크, 기존 설계 문서 5.3절에 대체 표시 |
| A15 검증 | PROPOSE FIX | design 6.1, 6.2 |
| A16 문체 | PROPOSE FIX | 전체 재작성 |
| A17 KRDS, 768px | PROPOSE FIX | design 2.1, 4.2 RedlineDiff |
| A18 에셋 커밋 | PROPOSE FIX | design 4.1 |

CONTEST 없음.

## 재검토 (Stage 9, 1회, 상한 도달)

egm-implementation-readiness 새 문맥 재검토 결과: **조건부 준비, Blocker 0, Major 6.** 모두 반영했다.

| 지적 | 반영 |
|---|---|
| 백엔드 파일 표와 `ChangeMarksJson` 저장 위치 없음 | design 3.4 |
| `ReplayNote` 삭제 뒤 재현 안내 처리 없음 | design 4.1(`REPLAY_NOTE`를 labels.ts로), 4.3 재현 안내 행 |
| 대비 계산 공용 모듈 없음 | design 4.1 `design/tokens.ts`, 4.3 정적 서버 컴포넌트 |
| 3.3 테스트 기대값이 녹화본과 다름 | design 3.3 정확한 구간 문자열 |
| 공식 구간이 비었는데 본문이 다르면 "변경 없음" | design 5.1 3번 규칙 |
| 원색 검사가 white/black을 놓침 | design 6.1 정규식과 대체 토큰 |
| Nit: 타임라인 channel 없음, `<P>` 짝 규칙, 삭제 조문 excerpt, api.ts 단계, icon.png 명령, spec의 Toast·IME 잔재, AppHeader Suspense | 모두 반영 |

재검토 상한(1회)에 도달해 검토를 마친다. 남은 쟁점 없음.
