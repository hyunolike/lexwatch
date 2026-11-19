# Stage 7: Critic 보고서

- 대상: `docs/features/design-system/design.md` FeatureRange 1–72행 (1절 결정, 2절 토큰)
- 기준: `feature-spec-standard.md` (design, structural), 페르소나만 (style)
- 검토자: egm-design, egm-structural, egm-style (각각 새 문맥, 2026-11-19)

## 중복을 합친 지적

| ID | 등급 | 지적 | 낸 검토자 |
|---|---|---|---|
| CR-B1 | Blocker | 1절 결정 표에 검토한 대안과 버린 이유가 없다. UI 기반(Radix, React Aria), 글꼴(SUIT, Wanted Sans, Noto Sans KR), 글꼴 불러오는 방식(`next/font/local`), 신구 대조(백엔드가 `<P>` 보존), 색 정의 형식(oklch, CSS 원본). "사용자 결정"만으로는 근거가 안 된다 | design, structural |
| CR-M1 | Major | `ink-subtle #6B7385`가 `canvas`(약 4.44:1)와 `surface-sunken`(약 4.20:1) 위에서 4.5:1 미달. 13px 캡션은 큰 글자가 아니다. 글자/배경 허용 쌍 표가 없다 | design, structural |
| CR-M2 | Major | `line-strong #8A93A6`이 `canvas`(약 2.88:1), `surface-sunken`(약 2.72:1) 위에서 3:1 미달 | design, structural |
| CR-M3 | Major | 포커스 링 `#4F46E5`가 `brand` 버튼과 맞닿으면 약 2.27:1. shadcn 기본 `ring-ring/50` 반투명 링은 3:1 아래일 가능성 | design |
| CR-M4 | Major | `neutral` 상태 토큰이 없는데 StatusLozenge, CountBadge, 라벨 표가 쓴다. `source-replay` 점선 테두리 토큰도 없다 | design, structural |
| CR-M5 | Major | 22행 예시 `text-muted`가 표의 `ink-muted`(`text-ink-muted`)와 다르고 shadcn `muted`와 겹친다. shadcn 변수(`primary`, `input`, `ring`, `destructive`, `muted-foreground`)와 토큰 연결 표가 없다. `radius-*`가 Tailwind 기본과 shadcn `--radius`를 덮어쓰는지 불명 | design, structural |
| CR-M6 | Major | 2.3절에 그림자 값, 움직임 토큰 이름과 easing, 목록 행 여백 토큰이 없다. 모서리, 그림자, 움직임 원본 위치가 없다 | structural, style |
| CR-M7 | Major | 2.2 `body` 15px가 명세 DS-02 "본문 16px"과 어긋난다. 글꼴 묶음(대체 글꼴) 토큰이 없다 | design, structural |
| CR-M8 | Major | 글꼴 "평균 약 32KB"는 조각 하나 크기라 페이지 전송량으로 읽힌다. CSS import는 preload와 size-adjust를 잃는다. Turbopack CSS 규칙이 node_modules 글꼴 CSS의 `url()`을 풀지 확인 안 됨 | design, style |
| CR-M9 | Major | `diffWordsWithSpace`가 한글을 어떻게 나누는지 확인 안 함. 조사 변경("근로자는"→"근로자가")이면 어절 전체가 바뀐 것으로 표시. `diff` 버전 미고정 | design |
| CR-M10 | Major | "기존 설계 문서 5.3절"에 링크가 없고 기존 문서에도 대체 표시가 없다 | structural, style, design |
| CR-M11 | Major | 15행 `<P>` 지움에서 "그래서 화면 계산"으로 가는 인과가 없다 | style |
| CR-N1 | Nit | KRDS의 "적록 조합 회피", "밑줄은 링크에만"과 신구 대조 표시의 충돌을 언급하지 않음 | design |
| CR-N2 | Nit | `focus`와 `source-nvidia` 색상각 차이 약 10°. 마스코트 보라 `#6D5BD0`이 토큰 표에 없음 | design |
| CR-N3 | Nit | "72자 폭" 단위가 모호(`72ch`면 한글 약 40자) | design, style |
| CR-N4 | Nit | 참조 형식 혼재(명세 절 앵커 없음, `#4` 링크 없음, Carbon/Atlassian 링크 없음), 요구사항 ID 열 없음 | structural |
| CR-N5 | Nit | `brand-hover` 쓰는 곳 비어 있음, `success` / `-bg` 축약 표기 | structural, style |
| CR-N6 | Nit | 문체: "실제로 돈 단계"(→ 실행한 단계), "마음대로", "거울", 명사 나열, "같은 색"(→ 비슷한 색), "12px 위아래", "0"(→ 0ms), "업무용 밀도를 위해" 막연 | style |

## 원 보고 요약

- egm-design: 수정 필요. Blocker 1, Major 8, Nit 4. 대비 값은 WCAG 상대 휘도 공식으로 손 계산.
- egm-structural: Blocker 1(대안 규칙), Major 6, Nit 3. 헤딩 계층과 표 문법은 이상 없음.
- egm-style: Major 5, Nit 14. 가운뎃점과 AI 상투구는 없음.
