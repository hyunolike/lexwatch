# Stage 5: Initial Zoom Out 보고서

- 대상: `docs/features/design-system/design.md` (전체)
- 기준: `zoom-out-standard.md`
- 검토자: egm-zoom-out, egm-inventive-catalyst (각각 새 문맥, 2026-11-19)
- 기준 자료: spec.md, research.md, assets.md, 2026-11-16 설계 문서, `web/src`

## egm-zoom-out

전체 판단: 수정 후 통과. 차단 없음. Major 5, Nit 5.

| ID | 등급 | 지적 | 제안 |
|---|---|---|---|
| ZO-M1 | Major | 기존 `components/*.tsx`(Nav, DemoBanner, WakeGate, ReviewChat, DraftEditor, Timeline, RoleSwitch, ReplayNote)의 위치가 정해지지 않았고, 원색 검사가 `src/app`과 `src/components/ds`만 봐서 DS-06이 지켜지지 않아도 통과한다 | 기존 부품 처리를 적고, 검사 범위를 `src/components/**`(단 `ui/` 제외)로 넓힌다 |
| ZO-M2 | Major | 새 부품마다 대응하는 기존 구현이 있다: SourceLabel↔ProviderBadge, StatusLozenge↔Timeline의 StatusPill, RedlineDiff↔OldNewDiff, CitationChip↔DraftEditor의 Chip, labels.ts↔api.ts의 STEP_LABELS. 교체·삭제가 적혀 있지 않다 | 대응표를 넣고 `stepLabel`을 labels.ts로 모은다 |
| ZO-M3 | Major | 토큰 원본이 둘(tokens.ts, tokens.css)이고 동기화 테스트가 하나 더 든다 | CSS `@theme`를 유일한 원본으로, 테스트는 CSS를 읽는다 |
| ZO-M4 | Major | 공식 변경 표시(`<P>`)를 지우고 화면에서 추정 계산한다. 대안 비교가 없다 | 대안을 고려했고 이번에 안 하는 이유를 적고, 근사라는 위험을 7절에 넣는다 |
| ZO-M5 | Major | 마감(11-25)에 비해 범위가 넓은데 축소 순서가 없다 | 필수(DS-01·03·05, 포커스 링)와 밀리면 뺄 것(포즈, /design-system, 막대, 모바일 탭)을 나눈다 |
| ZO-N1 | Nit | `lib/ime.ts`는 함수 하나. `reviewChat.ts`에 두면 된다. `keyCode 229` 같은 상세는 설계 수준이 아니다 | |
| ZO-N2 | Nit | 마스코트 `default`(lex-master)를 쓰는 화면이 없다. 명세는 5장, 설계는 6장 | |
| ZO-N3 | Nit | 4.1 출력이 조각 목록인데 상태(`added`, `tooLong`)도 돌려준다고 한다. 반환 타입을 하나로 | |
| ZO-N4 | Nit | 명세 DS-06의 Card가 설계에 없다 | 클래스 조합이면 그렇게 적는다 |
| ZO-N5 | Nit | CSS 직접 import로 `next/font`의 preload와 size-adjust를 잃는다 | 7절에 적는다 |

## egm-inventive-catalyst

전체 판단: 기초 구조는 균형 잡힘. 아래는 모두 권고.

| ID | 등급 | 거짓 절충 | 제안 |
|---|---|---|---|
| IC-1 | Major | 화면 diff가 공식 표시와 다를 수 있다. 또 백엔드는 한쪽이 없는 조문을 `"(없음)"` 글자로 저장하므로(`FetchArticlesStep`) 4.1의 "한쪽이 비면" 규칙은 실제 데이터에서 실행되지 않는다(실제 결함) | 수집 시 `<P>` 구간을 보존해 API로 내보내고 화면은 없을 때만 diff. 범위 밖이면 최소한 `"(없음)"`을 계약에 넣고 "화면이 계산한 추정"임을 UI에 밝힌다 |
| IC-2 | Nit | 6,000자 고정 상한은 길이로 비용을 추정한다 | 줄 단위 diff 후 바뀐 줄만 단어 diff, `maxEditLength`로 실제 비용 상한 |
| IC-3 | Major | 원색 금지를 문자열 검사로 사후 강제한다 | `@theme`에서 `--color-*: initial`로 기본 팔레트를 비워 원색 클래스가 생성되지 않게 한다. 검사는 보조로 남긴다 |
| IC-4 | Nit | TS 원본과 CSS 거울의 이중 관리 | 생성 스크립트 또는 CSS를 원본으로 |
| IC-5 | Nit | 열거형과 라벨 표의 어긋남을 사람이 기억해야 한다 | 테스트가 `Enums.cs`를 읽어 모든 멤버에 라벨이 있는지 확인. 알 수 없는 값은 개발 모드 경고 |

참고: 마스코트 원본 PNG를 버리면 후처리를 바꿀 때 되살릴 수 없다(낮은 우선순위).
