# 작업: LexWatch 디자인 시스템

- 설계: [design.md](design.md) v2 / 명세: [spec.md](spec.md) v2
- 브랜치: `feat/design-system`

## 진행 상태

| 단계 | 작업 | 요구 | 상태 |
|---|---|---|---|
| 0 | 외부 레퍼런스 조사 | — | 완료 (research.md) |
| 0 | 마스코트 생성(Codex)과 WebP 처리 | DS-14, DS-15 | 완료 (`web/public/mascot/`, `scripts/process_mascot.py`) |
| 0 | EGM 통합 검토와 조치 | — | 완료 (reviews/) |
| 1 | 의존성 고정 설치: `@base-ui/react@1.9.0`, `diff@9.0.0`, `pretendard@1.3.9`, `lucide-react@1.75.0` | — | 대기 |
| 1 | `styles/tokens.css`, `globals.css` 정리, 글꼴과 대체 글꼴 | DS-01, DS-02, DS-03 | 대기 |
| 1 | `lib/labels.ts`(`stepLabel` 이동)와 `labels.test.ts`(Enums.cs 계약) | DS-05 | 대기 |
| 1 | `design/contrast.test.ts`, `design/palette.test.ts` | DS-04, DS-06 | 대기 |
| 2 | `LawApiClient` 변경 표시 구간, `Evidence.ChangeMarksJson`과 마이그레이션, `FetchArticlesStep` 저장 | DS-08 | 대기 |
| 2 | 검토 API `oldNew` 필드와 `evidence.excerpt`, 백엔드 테스트 | DS-08, DS-09 | 대기 |
| 3 | `lib/redline.ts`와 테스트 | DS-08 | 대기 |
| 3 | `components/ui/*`(Button, ConfirmDialog, Popover, Tooltip, Tabs) | DS-06, DS-07 | 대기 |
| 3 | `components/ds/*` | DS-03, DS-06, DS-09, DS-11, DS-15 | 대기 |
| 4 | 화면 적용, 옛 부품 삭제 | DS-05~DS-11, DS-15 | 대기 |
| 4 | 수동 확인 절차 (design 6.2) | DS-01, 02, 07~11, 15 | 대기 |
| 5 | `app/icon.png`, `favicon.ico` 삭제, `/design-system` 화면 | DS-13, DS-14 | 대기 |

## 단계별 통과 명령

- 웹: `cd web && pnpm test && pnpm exec tsc --noEmit && pnpm lint && pnpm build`
- 백엔드: `make test-backend` (Docker 필요. 샌드박스에서는 로컬 DB 연결이 막혀 샌드박스 밖에서 실행)

## 다음 시작점

단계 1의 의존성 설치.
