# 작업: LexWatch 디자인 시스템

- 설계: [design.md](design.md) v2 / 명세: [spec.md](spec.md) v2
- 브랜치: `feat/design-system`

## 진행 상태

| 단계 | 작업 | 요구 | 상태 |
|---|---|---|---|
| 0 | 외부 레퍼런스 조사 | — | 완료 (research.md) |
| 0 | 마스코트 생성(Codex)과 WebP 처리 | DS-14, DS-15 | 완료 (`web/public/mascot/`, `scripts/process_mascot.py`) |
| 0 | EGM 통합 검토와 조치 | — | 완료 (reviews/) |
| 1 | 의존성 고정 설치: `@base-ui/react@1.9.0`, `diff@9.0.0`, `pretendard@1.3.9`, `lucide-react@1.75.0` | — | 완료 |
| 1 | `styles/tokens.css`, `globals.css` 정리, 글꼴과 대체 글꼴 | DS-01, DS-02, DS-03 | 완료 |
| 1 | `lib/labels.ts`(`stepLabel` 이동)와 `labels.test.ts`(Enums.cs 계약) | DS-05 | 완료 |
| 1 | `design/contrast.test.ts`, `design/palette.test.ts` | DS-04, DS-06 | 완료 |
| 2 | `LawApiClient` 변경 표시 구간, `Evidence.ChangeMarksJson`과 마이그레이션, `FetchArticlesStep` 저장 | DS-08 | 완료 |
| 2 | 검토 API `oldNew` 필드와 `evidence.excerpt`, 백엔드 테스트 | DS-08, DS-09 | 완료 |
| 3 | `lib/redline.ts`와 테스트 | DS-08 | 완료 |
| 3 | `components/ui/*`(Button, ConfirmDialog, Popover, Tooltip, Tabs) | DS-06, DS-07 | 완료 |
| 3 | `components/ds/*` | DS-03, DS-06, DS-09, DS-11, DS-15 | 완료 |
| 4 | 화면 적용, 옛 부품 삭제 | DS-05~DS-11, DS-15 | 완료 |
| 4 | 수동 확인 절차 (design 6.2) | DS-01, 02, 07~11, 15 | 완료 |
| 5 | `app/icon.png`, `favicon.ico` 삭제, `/design-system` 화면 | DS-13, DS-14 | 완료 |

## 단계별 통과 명령

- 웹: `cd web && pnpm test && pnpm exec tsc --noEmit && pnpm lint && pnpm build`
- 백엔드: `make test-backend` (Docker 필요. 샌드박스에서는 로컬 DB 연결이 막혀 샌드박스 밖에서 실행)

## 검증 기록 (2026-11-19)

- 웹: 테스트 85건(라벨, 대비 32쌍, 출처 색상각, 신구 대조 9건, 원색 클래스 검사 포함), `tsc`, `lint`, `build` 통과. 글꼴 조각 92개가 빌드 산출물에 들어감.
- 백엔드: `make test-backend` 141건 통과(Testcontainers, 샌드박스 밖).
- 수동 확인(로컬 compose + `pnpm start`, Chrome):
  - 승인 대기함: 크리틱 적발 행이 먼저, 빨간 배지, 신뢰도 숫자와 막대 (DS-11)
  - 인용 칩 클릭 → 근거 본문 앞부분 팝오버, Esc로 닫힘 (DS-09)
  - 승인 → 확인 대화상자, 포커스가 "취소", Esc로 닫힌 뒤 포커스가 "승인"으로 돌아옴, 포커스 링 보임 (DS-07, DS-10)
  - 실행 이력, 타임라인, 알림함: 영어 상태값 없음 (DS-05). 빈 알림함에 렉스 (DS-15)
  - `/design-system`: 공식 표시(검찰청→공소청)와 화면 비교(는→가, 40→52) (DS-08, DS-13)
  - 390px 폭: 신구조문이 신조문/구조문 탭으로 (DS-08)
- 발견해 고친 것: 반려 버튼이 ghost 변형의 글자색에 밀려 회색으로 보임 → `danger-ghost` 변형 추가.
- 확인하지 못한 것: 윈도우 화면의 글꼴(DS-02, 맥에서만 확인), 다크 모드 에뮬레이션(다크 규칙과 `prefers-color-scheme` 분기를 지우고 `color-scheme: light`로 고정해 구조상 해당 없음).

## 다음 시작점

PR 리뷰. 배포 시 Render는 수동 배포가 필요하고 마이그레이션 `EvidenceChangeMarks`가 기동 때 적용된다.

## 화면 진단 후 개선 계획 (2026-11-19)

기준: `056cf11`, 작업 전 변경 없음. 공용 worktree에서 동시 문서 작업이 브랜치를 변경해 `/private/tmp/lexwatch-design-system-repair`의 `fix/design-system-repair`로 수정본을 옮겼다. 원래 작업공간에는 해당 수정만 되돌려 문서 작업을 보존했다.
사용자가 진단 결과에 대해 ‘진행해줘’로 복구와 개선을 승인했다. 구현은 현재 세션에서 진행한다.

1. [ ] DS-16: 배포 로그/소스/최종 CSS 비교. `web/scripts/verify-design-css.mjs`와 `.test.mjs`를 추가한다. 현 배포 CSS 누락을 재현하고, 정상 CSS/분할 CSS/누락/HTTP 실패 검사를 검증한다. `web/package.json`의 build 뒤 검사 연결. 캐시 없는 Vercel 빌드와 공개 CSS로 복구 확인.
2. [ ] DS-17~20: `web/src/lib/reviewPresentation.ts`와 테스트. 초안 null/0건/한 문장 여러 지적/여러 문장 지적을 검사하고 `flaggedSentences`, `firstFlaggedIndex`를 반환한다. `badges.tsx`의 그룹 관련성 표기, `page.tsx`의 목록 정렬, `DraftEditor.tsx`의 번호/앵커/접힌 검사 상세, `reviews/[id]/ReviewClient.tsx`의 읽기 순서와 문제 요약을 적용한다.
3. [ ] DS-19와 대화상자: `tokens.css`, `layout.tsx`, `ui/ConfirmDialog.tsx`, `globals.css`에 작업/읽기 폭, 레이어, 앵커 스크롤 여유를 정한다. `ds/AppHeader.tsx`와 `RoleSwitch.tsx`에서 모바일 헤더를 두 줄로 줄여 앵커가 가려지지 않게 한다. `design-system/{page.tsx,Showcase.tsx}`에 사용 규칙과 표기를 반영한다. 카드/버튼/색은 기존 부품을 사용한다.
4. [ ] 검증: `pnpm test`, `node --test scripts/verify-design-css.test.mjs`, `pnpm exec tsc --noEmit`, `pnpm lint`, `pnpm build`. 배포 CSS 검사. 브라우저에서 목록→상세→확인 필요 이동→인용 팝오버→승인 확인 후 취소. 모바일 375px과 키보드 포커스 확인. 독립 검토 결과는 reviews에 보관한다.

검토 초점: 초안 없는 검토의 승인 차단 유지, 관리자 읽기 전용 유지, 편집 시 미검증 지적을 해결됐다고 표시하지 않기, 지적 0건을 품질 보장으로 표현하지 않기, 모달이 sticky 헤더 아래로 가지 않기.

배포 및 검증 기록은 이 절에 이어 쓴다. Render/API와 데이터는 이번 변경 대상이 아니다.
