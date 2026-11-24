# 작업: AI 거버넌스 (2026-11-24)

- 관련: [명세](spec.md), [설계](design.md), [검토 1차](reviews/2026-11-24-round1.md)

## 현재 단계

구현과 로컬 확인 완료. PR 대기.

## 작업

| # | 작업 | 요구사항 | 상태 |
|---|---|---|---|
| 1 | `SensitiveDataMasker`(규칙, Luhn, 날짜 제외, 고객사 먼저, `Clip`) | GV-01~03 | 완료 |
| 2 | `ai_usage_logs`, `approvals.findings_json` 마이그레이션(`AiGovernance`) | GV-05, GV-07 | 완료 |
| 3 | 채팅: 원문 가리기 → 한도 자르기, 응답 `maskedQuestion`/`masked`, 기록 저장 | GV-03~05 | 완료 |
| 4 | `GET /api/governance/ai-usage`(admin), 90일 정리(데모 초기화) | GV-05, GV-06 | 완료 |
| 5 | `AwaitingPartner`, confirm/return, 돌려보낸 뒤 재승인, 단계 진행 중 409 | GV-07~10, GV-13 | 완료 |
| 6 | 실행 상태 `InReview`, 재실행 제외, NotifyStep 본문 | GV-12, GV-14 | 완료 |
| 7 | 웹: 역할 세 칸, 파트너 패널과 버튼, 채팅 가림 표시, `/governance`, 라벨, 안내 문구 | GV-04, GV-06, GV-11 | 완료 |
| 8 | README, 디자인 시스템 라벨 표 | | 완료 |

## 검증 (2026-11-24)

- 백엔드: .NET 10 SDK 컨테이너에서 `dotnet test --filter "Category!=eval"` 252건 통과. 새 테스트는 `SensitiveDataMaskerTests`(37건, 오탐 방지 포함)와 `GovernanceTests`(8건)다. 기존 승인 테스트 2건은 "적발 없는 승인" 경로로 시드를 바꿨다(적발이 있으면 이제 파트너를 기다린다).
- 웹: `tsc --noEmit`, `eslint src`, Vitest 162건 통과, `next build` 통과.
- 로컬 실제 동작(docker compose API + 빌드한 웹, Playwright):
  - 변호사가 노동 예시(크리틱 2단 적발 1건)를 승인하면 `파트너 승인 대기`가 된다. 패널에 승인 시각과 확인한 적발이 보인다.
  - 검토 보조에 고객사 이름, 주민등록번호, 전화번호를 넣으면 말풍선이 `[고객사] 대표 [주민등록번호], 연락처 [전화번호]…`로 바뀐다. "민감정보를 가리고 보냈어요: 고객사 1건, 주민등록번호 1건, 전화번호 1건"이 뜬다.
  - 파트너로 바꾸면 최종 승인과 돌려보내기 버튼이 보이고 채팅은 보이지 않는다. 최종 승인하면 알림함 본문에 "파트너 최종 승인 YYYY-MM-DD HH:mm"이 붙는다. 실행 이력 타임라인에 "파트너 최종 승인"이 보인다.
  - 관리자 메뉴에만 "AI 사용 기록"이 보인다. 요약은 요청 1, 가린 민감정보 3이고, 기록 행의 결과는 "모델 없음"이다(로컬은 모델 키가 없다).
  - 375px에서 세 화면 모두 가로 넘침 0, 콘솔 오류 0.

## 다음 작업

- 운영 배포 뒤 Render 환경변수 `Governance__ClientNames`는 설정하지 않아도 appsettings의 가상 고객사 이름이 쓰인다.
