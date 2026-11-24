# 작업: 공포 전 법안 레이더 (2026-11-24)

- 관련: [명세](spec.md), [설계](design.md), [검토 1차](reviews/2026-11-24-round1.md)

## 현재 단계

구현과 로컬 확인 완료. PR 대기.

## 작업

| # | 작업 | 요구사항 | 상태 |
|---|---|---|---|
| 1 | `AssemblyApiClient`, 응답 녹화(`fixtures/assembly`), 단계와 법안명 규칙 | BR-02, BR-03, BR-05 | 완료 |
| 2 | `WatchLawNames`, `Bills`, `BillEvents`, `BillCollectLogs` 마이그레이션(`BillRadar`) | BR-01, BR-05 | 완료 |
| 3 | `BillRadarService`(조회와 저장 분리, 120초 상한, 재조회) | BR-02, BR-04, BR-05 | 완료 |
| 4 | `POST /jobs/collect-bills`, `GET /api/radar`(그룹, 출처 상태, 공포 후보) | BR-05~07 | 완료 |
| 5 | 웹 `/radar`, 걸음 표시, 배지, 머리 메뉴 | BR-08 | 완료 |
| 6 | `collect.yml`의 `bills` 작업, README | BR-09 | 완료 |

## 검증 (2026-11-24)

- 백엔드: .NET 10 SDK 컨테이너에서 `dotnet test --filter "Category!=eval"` 286건 통과. 새 테스트는 `AssemblyApiClientTests`(녹화 응답 해석, 단계, 법안명 일치)와 `BillRadarTests`(첫 수집, 단계 변화와 재조회, 부분 실패와 502, 재조회 상한과 순서, 대안 반영, 작업 엔드포인트, 레이더 조회와 공포 후보, 48시간 경고)다.
- 웹: `tsc --noEmit`, `eslint src`, Vitest 168건(`radar.test.ts` 포함), `next build` 통과.
- 로컬 실제 동작(docker compose API, 실제 국회 API, 키 없음)
  - 첫 수집: 15개 법률에서 71건, 6.6초, 실패 0건. 두 번째 수집: 새 법안 0, 단계 변화 0.
  - 단계 분포: 위원회 심사 61, 접수 5, 대안에 반영 5. `은행법`으로는 `은행법 일부개정법률안` 1건만 들어왔다(`한국은행법` 제외 확인).
  - `/radar` 화면을 Playwright로 1280px와 375px에서 확인했다. 가로 넘침 0, 콘솔 오류 0. 탭 순서는 전체, 노동, 개인정보, 금융이다. 대안에 반영된 법안은 의결 칸까지 채워지고 "위원회 대안에 내용이 반영됐어요(본회의 날짜)"가 보인다.

## 다음 작업

- 운영 배포 뒤 `collect.yml`을 수동 실행해 `bills` 작업과 화면을 확인한다.
- 국민참여입법센터 이용 승인이 나면 입법예고를 `Source=lawmaking`으로 붙인다. 녹화 응답부터 만든다.
- 운영에 `ASSEMBLY_API_KEY`를 넣으면 법률마다 100건을 받는다(열린국회정보 회원가입 후 즉시 발급).
