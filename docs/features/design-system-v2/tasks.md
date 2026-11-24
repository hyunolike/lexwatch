# 작업: LexWatch 디자인 시스템 v2

- 명세: [spec.md](spec.md) v2 / 설계: [design.md](design.md) v3 / 검토: [통합](reviews/consolidated_review_report.md), [재검토](reviews/re_review_report.md)
- 브랜치: `feat/design-system-v2`

## 진행 상태

| 단계 | 작업 | 요구 | 상태 | 커밋 |
|---|---|---|---|---|
| 0 | 레퍼런스 조사, 명세, 설계, EGM 통합 검토(10명), 재검토(2명) | — | 완료 | `d6d0261` |
| 1 | 두 테마 토큰(`@theme inline` + `--lw-*`), 테마 스크립트와 메뉴, 세리프 글꼴, cva와 tailwind-merge(`cx`), 대비와 소스와 빌드 검사, `source-*` 제거, 운영 현황 공급자 막대 | DS2-01~05, 15 | 완료 | `d6d0261` |
| 2 | `provenanceOf`와 출처 네 종류(SourceLabel, AiLabel, AiTrail, RunTimeline), Button과 Callout cva, CheckMark와 `checkState`, 평면 로고, 렉스 120px | DS2-05, 06, 14, 15 | 완료 | `d6d0261` |
| 3 | 검토 상세 분할 화면, 근거 패널, `oldNewFor`, SplitPane, 인용 미리보기 | DS2-07, 08 | 완료 | `e15434b` |
| 4 | 단축키, 도움말, 명령 팔레트, `inboxCache`, 재실행 토스트 | DS2-09~11 | 완료 | `2883b73` |
| 5 | 스켈레톤과 WakeGate 규칙(`gateView`), `/design-system` 두 테마 견본과 새 부품 | DS2-13, 16 | 완료 | 이 커밋 |
| 6 | 배포 뒤 화면 확인과 README 스크린샷 | 전체 | 병합 뒤 | |

## 검증 기록 (2026-11-25, 로컬)

- `pnpm test`: vitest 348건, 빌드 CSS 검사 스크립트 테스트 13건 통과
- `pnpm exec tsc --noEmit`, `pnpm exec eslint src`, `pnpm build`(빌드 CSS 두 테마 검사 포함) 통과
- 운영 API를 부르는 로컬 빌드(로컬 CORS 전달 서버)로 확인:
  - 시스템 어두운 설정에서 저장 값 없이 어두운 테마로 첫 그림. 밝게, 어둡게 전환과 새로 고침 유지
  - 검토 상세 1440px 분할: 문장 선택, 인용 칩 선택이 근거 패널을 바꾸고 팝오버는 열리지 않음
  - 대기함 `j`로 행 포커스 이동, `⌘K` 팔레트에서 "도시" 검색 후 Enter로 검토 이동
  - 검토 상세 `]` 두 번에 확인 필요 문장 2로 이동, `a`가 승인 확인 창을 열고 Esc로 닫힘(승인하지 않음), `?` 도움말
  - 390px iframe에서 검토 상세와 대기함 가로 넘침 없음(`scrollWidth == clientWidth`)
- 확인하지 못한 것: 실제 macOS 한글 입력기로 `ㅓ` 입력 상태의 `j`와 팔레트 조합 중 Enter(자동화 도구는 조합 이벤트를 만들지 못함. 판정은 `e.code`와 `isComposing` 단위 테스트로만 확인), 재실행 토스트(관리자 역할과 실패한 실행이 필요)

## 다음 시작점

PR 병합 → 운영 배포 확인(6단계): 일곱 화면 두 테마 스크린샷, 한글 입력기 수동 확인, README 스크린샷 갱신.
