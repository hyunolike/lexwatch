# Stage 9: Implementation Readiness 보고서

- 대상: `docs/features/design-system/design.md` DetailedRange 73–183행 (3–7절)
- 기준: `implementation-readiness-standard.md`
- 검토자: egm-implementation-readiness, egm-codebase-details, egm-style (각각 새 문맥, 2026-11-19)
- 판정: **구현 준비 안 됨.** 현재 코드에 대한 사실 오류는 없음(테스트 27건, 열거형, 알림 상태, 실무그룹 코드, Turbopack 규칙, 마스코트 스크립트 동작 확인됨).

## 중복을 합친 지적

| ID | 등급 | 지적 | 낸 검토자 |
|---|---|---|---|
| IR-B1 | Blocker | 만들 것/고칠 것/지울 것 파일 표가 없다. 기존 `components/*.tsx` 10개, 화면 7개, `globals.css`, `favicon.ico`, `/design-system` 화면 파일, 테스트 파일, shadcn 생성 파일(`components.json`, `lib/utils.ts`)의 처리가 없다 | readiness, codebase |
| IR-B2 | Blocker | 단계 구분과 단계마다 통과할 명령(test, tsc, lint, build)이 없다 | readiness, codebase |
| IR-B3 | Blocker | DS-09 팝오버는 근거 본문 앞부분을 요구하지만 `EvidenceItem`에는 `id, kind, title, sourceUrl`뿐이고 백엔드 변경은 범위 밖 | readiness, codebase |
| IR-M1 | Major | 새 의존성 이름과 버전이 없다: `diff`, `pretendard`, `@base-ui/react`, shadcn 간접 의존성, 아이콘, 토스트 구현체 | readiness, codebase |
| IR-M2 | Major | shadcn 변수(`primary`, `muted-foreground`, `ring`, `input`, `destructive`, `popover`)와 토큰 대응표, `components.json` 값, 최종 `globals.css` 구성, `next.config.ts` 유지 여부를 "보고 판단"으로 남겼다 | readiness, codebase |
| IR-M3 | Major | 부품 props와 함수 시그니처가 없다. `labels.ts`는 같은 값이 여러 영역에 겹치므로(`Running`, `Failed`, `NeedsReview`) 영역을 받는 시그니처가 필요. SourceLabel의 `none`(기존 코드는 렌더 안 함), fallback/replay 툴팁 문구. ConfidenceMeter 0–1 → 5칸 규칙. CountBadge `tone`. Mascot 변형↔파일↔표시 크기. RedlineDiff 입력 단위 | readiness, codebase |
| IR-M4 | Major | `redline.ts` 반환이 조각 목록인지 상태인지 모호. 둘 다 빈 경우(백엔드가 표지를 못 찾으면 둘 다 `""`), 공백만 있는 경우, 6,000자 셈법(UTF-16 length), "변경 n곳" 셈법 미정. jsdiff 단어 분할이 한글을 글자마다 쪼갤 가능성(미확인) — 버전 고정, 어절 테스트, 대체안(공백 토큰화 + `diffArrays`) 필요 | readiness, codebase, style |
| IR-M5 | Major | 대비 테스트 쌍 목록과 색상각 범위 숫자가 없다. `ink-subtle`/canvas 약 4.4:1, `line-strong`/canvas 약 2.9:1 실패 예상 | readiness, codebase |
| IR-M6 | Major | DS-06 원색 검사: 금지 접두어가 "등"으로 열림(현재 코드는 slate, gray, green, orange, red, amber, blue, yellow), 범위에서 기존 `components/*` 빠짐, 테스트 파일 위치 없음 | readiness, codebase |
| IR-M7 | Major | 영어로 남는 값: 타임라인 `decision`(approve/reject), `actorRole`(lawyer/admin), 알림 `channel`, `errorCode`. `runs/page.tsx`가 화면 표에 없음. 단계 이름 `STEP_LABELS`를 `labels.ts`로 옮길지 미정 | readiness, codebase |
| IR-M8 | Major | 대화상자 문구, 반려 사유 입력 위치, 토스트 구현체와 마운트 위치(결정 뒤 `Detail` 재마운트로 화면 안 토스트는 사라짐), 기존 안내(충돌, msg, 재실행 알림, 열린 질문, WakeGate 실패)의 부품 대응이 없다 | readiness, codebase |
| IR-M9 | Major | `ime.ts`: React 합성 `KeyboardEvent`에 `isComposing` 없음, 연결 방식(`onKeyDown`, `requestSubmit`) 미정 | readiness, codebase |
| IR-M10 | Major | 글꼴 CSS 정확한 경로, import 위치, `font-family`와 대체 글꼴 목록, `tnum` 실패 시 대체 글꼴 미정 | readiness |
| IR-M11 | Major | 마스코트 크기 불일치: 설계는 빈 화면 160px/헤더 28px, 스크립트는 360px/128px(표시 180/64px 전제). `icon.png` 만드는 방법과 `favicon.ico` 처리 없음. 120KB 검사 방법 없음 | readiness, codebase |
| IR-M12 | Major | 검증 표에 DS-11 없음. "화면 확인" 두 행에 통과 기준 없음 | codebase, style |
| IR-M13 | Major | 7절 위험 서술: Base UI 1년 미만이 무엇이 위험한지 없음, 원본 PNG를 버리는 이유가 위험을 키우는 것처럼 읽힘 | style |
| IR-N1 | Nit | 768px 전환은 CSS 반응형으로(SSR 불일치 회피) | readiness |
| IR-N2 | Nit | 이미 만든 `web/public/mascot/*.webp`와 스크립트를 그대로 커밋하는지 적기 | readiness |
| IR-N3 | Nit | 테스트 사례 입력과 기대값, `labels.test.ts`의 값 목록 확정 | readiness |
| IR-N4 | Nit | vitest가 node 환경이라 컴포넌트 테스트 불가. DS-07, 09, 10 수동 확인 절차를 단계별로 | readiness, codebase |
| IR-N5 | Nit | Card가 구조에 없다 | codebase |
| IR-N6 | Nit | 정적 Callout에 `role="alert"`를 고정하면 크리틱 Callout 여러 개가 한꺼번에 읽힌다 | codebase |
| IR-N7 | Nit | RoleSwitch 다섯 곳 제거, DemoBanner `sticky` 유지 여부, 대기함 행의 공포일과 재현 안내 처리 | codebase |
| IR-N8 | Nit | `*.css` Turbopack 규칙이 node_modules의 pretendard CSS에도 적용됨 | codebase |
| IR-N9 | Nit | 문체: 서술어 생략(104행), "위험 의미면" 주체(106행), "거울", "얇은 띠" 수치, Summary list 출처, 6,000 근거, 조건 나열 문장, "WebP로 줄인다", "27건"은 기준일과 함께 | style |
