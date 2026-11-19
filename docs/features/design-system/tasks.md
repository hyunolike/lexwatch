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
