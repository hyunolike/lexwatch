# EGM 통합 검토 보고서: 디자인 시스템 설계

- 대상: `docs/features/design-system/design.md` (2026-11-19 초안, 검토 중 변경 없음)
- 방식: 통합 보고서(중간 확인 없이 다섯 단계 실행). FeatureRange 1–72(1–2절), DetailedRange 73–183(3–7절)
- 검토자 10회 실행, 모두 새 문맥:

| 단계 | 검토자 | 보고서 |
|---|---|---|
| 5 범위 | zoom-out, inventive-catalyst | [zoom_out_report.md](zoom_out_report.md) |
| 6 이해도 | comprehension | [comprehension_report.md](comprehension_report.md) |
| 7 설계 비판 | design, structural, style | [critic_report.md](critic_report.md) |
| 8 중간 범위 | zoom-out | [intermediate_zoom_out_report.md](intermediate_zoom_out_report.md) |
| 9 구현 준비 | implementation-readiness, codebase-details, style | [implementation_readiness_report.md](implementation_readiness_report.md) |

## 결론

설계 방향(의미 토큰, 출처 색과 상태 색 분리, 한국어 라벨, 마스코트 위치 제한)에 반대한 검토자는 없다. 그러나 **구현에 들어갈 수 없다.** 대안 비교가 없고, 바꿀 파일과 단계가 없고, 몇몇 토큰이 명세의 대비 기준을 스스로 어기며, 명세가 범위 밖으로 둔 백엔드 변경 없이는 DS-09를 만족할 수 없다.

## 조치 항목

### 🔴 Blocker

| 조치 | 내용 | 원 지적 |
|---|---|---|
| A1 | 1절에 검토한 대안과 버린 이유를 적는다(UI 기반, 글꼴, 글꼴 불러오기, 신구 대조, 토큰 원본) | CR-B1 |
| A2 | 만들 것/고칠 것/지울 것 파일 표. 기존 부품 대응(ProviderBadge→SourceLabel, StatusPill→StatusLozenge, OldNewDiff→RedlineDiff, Chip→CitationChip, STEP_LABELS→labels.ts), 각 화면의 RoleSwitch 제거, `favicon.ico` 삭제 | IR-B1, ZO-M1, ZO-M2, ZO2-N2, IR-N7 |
| A3 | 단계 구분과 단계별 통과 명령. 마감 대비 필수와 밀리면 뺄 것 | IR-B2, ZO-M5 |
| A4 | **[작성자 결정]** 백엔드 범위: 근거 본문(DS-09 팝오버)과 공식 변경 표시 `<P>` 보존 | IR-B3, IC-1, ZO-M4, CR-M11 |

### 🟡 Major

| 조치 | 내용 | 원 지적 |
|---|---|---|
| A5 | **[작성자 결정]** shadcn 층을 둘지, Base UI를 직접 쓸지. 둔다면 shadcn 변수와 토큰 대응표, `components.json` 값, 최종 `globals.css`와 `next.config.ts` | ZO2-M1, IR-M2, CR-M5 |
| A6 | 토큰 수정: `ink-subtle`과 `line-strong`을 canvas 위 기준 통과 값으로, `neutral` 추가, 점선 테두리 토큰, 불투명 포커스 링(오프셋 포함), 대비 쌍 표, 출처 색상각 범위 | CR-M1~M4, IR-M5, CO-9, CR-N2 |
| A7 | 토큰 원본을 CSS `@theme` 하나로. 기본 색 팔레트를 비워 원색 클래스가 생성되지 않게 하고, 검사 테스트는 보조로 `src/**` 전체 | ZO-M3, IC-3, IC-4, IR-M6 |
| A8 | 글자 토큰을 DS-02와 맞춘다(명세를 "읽는 글 16px, UI 15px 이하 허용"으로). 글꼴 CSS 경로, import 위치, 글꼴 묶음, 전송량 측정 목표, Turbopack 확인 | CR-M7, CR-M8, IR-M10, ZO-N5, IR-N8 |
| A9 | `redline` 계약: 판별 유니언 반환, 둘 다 빈 경우, `"(없음)"` 처리, 길이 셈법, 변경 곳 셈법, 한글 어절 분할 검증과 대체안, 버전 고정 | IR-M4, IC-1, IC-2, CO-6, CR-M9 |
| A10 | 라벨: 영역을 받는 시그니처, 출처 `none`은 그리지 않음, `decision`/`actorRole`/`channel` 라벨, `Enums.cs`를 읽는 계약 테스트 | IR-M3, IR-M7, ZO2-N1, IC-5 |
| A11 | 결정 흐름: 확인 대화상자는 승인과 수정 후 승인에만, 반려는 기존 두 단계 유지, Toast 대신 `aria-live` Callout. 대화상자 문구 | ZO2-M2, IR-M8 |
| A12 | DS-12(Enter 전송)를 이번 범위에서 뺀다 | ZO2-M3, IR-M9, ZO-N1 |
| A13 | 부품 인터페이스: ConfidenceMeter 규칙, CountBadge `tone`, Mascot 변형/파일/표시 크기와 스크립트 크기 일치, 최적화 끄기, Card는 클래스 조합으로 | IR-M3, IR-M11, CO-7, CO-13, ZO-N2, ZO-N4, ZO2-N3, IR-N5 |
| A14 | 맥락: 크리틱, 출처 3종, 신뢰도, 검토 보조, DS ID 요약, 기존 설계 문서 링크와 대체 표시 | CO-1~5, CO-8, CO-11, CO-12, CR-M10, CR-N4 |
| A15 | 검증 표: DS-11 추가, 화면 확인 행의 통과 기준, 수동 확인 절차, 정적 Callout은 `role="alert"` 없음 | IR-M12, IR-N4, IR-N6 |

### 🟢 Nit

| 조치 | 내용 | 원 지적 |
|---|---|---|
| A16 | 문체와 표기 정리(축약 토큰 표기, 빈 칸, 단위, 서술어, 은유) | CR-N3, CR-N5, CR-N6, IR-N9, IR-M13 |
| A17 | 신구 대조와 KRDS "밑줄은 링크에만" 충돌 처리, 768px 전환은 CSS 반응형 | CR-N1, IR-N1 |
| A18 | 이미 만든 마스코트 WebP와 스크립트를 커밋 대상에 명시 | IR-N2 |

## 추적표

| 원 지적 | 조치 |
|---|---|
| ZO-M1, ZO-M2 | A2 |
| ZO-M3 | A7 |
| ZO-M4 | A4 |
| ZO-M5 | A3 |
| ZO-N1 | A12 |
| ZO-N2, ZO-N4 | A13 |
| ZO-N3 | A9 |
| ZO-N5 | A8 |
| IC-1 | A4, A9 |
| IC-2 | A9 |
| IC-3, IC-4 | A7 |
| IC-5 | A10 |
| CO-1~CO-5, CO-8, CO-11, CO-12 | A14 |
| CO-6 | A9 |
| CO-7, CO-13 | A13 |
| CO-9 | A6 |
| CO-10 | A7 |
| CO-14 | A6, A10 |
| CR-B1 | A1 |
| CR-M1~CR-M4 | A6 |
| CR-M5 | A5 |
| CR-M6 | A6 |
| CR-M7, CR-M8 | A8 |
| CR-M9 | A9 |
| CR-M10, CR-N4 | A14 |
| CR-M11 | A4 |
| CR-N1 | A17 |
| CR-N2 | A6 |
| CR-N3, CR-N5, CR-N6 | A16 |
| ZO2-M1 | A5 |
| ZO2-M2 | A11 |
| ZO2-M3 | A12 |
| ZO2-N1 | A10 |
| ZO2-N2 | A2 |
| ZO2-N3 | A13 |
| IR-B1 | A2 |
| IR-B2 | A3 |
| IR-B3 | A4 |
| IR-M1 | A3 (의존성 표) |
| IR-M2 | A5 |
| IR-M3 | A10, A13 |
| IR-M4 | A9 |
| IR-M5 | A6 |
| IR-M6 | A7 |
| IR-M7 | A10 |
| IR-M8 | A11 |
| IR-M9 | A12 |
| IR-M10 | A8 |
| IR-M11 | A13 |
| IR-M12 | A15 |
| IR-M13, IR-N9 | A16 |
| IR-N1 | A17 |
| IR-N2 | A18 |
| IR-N3, IR-N4, IR-N6 | A15 |
| IR-N5 | A13 |
| IR-N7 | A2 |
| IR-N8 | A8 |
