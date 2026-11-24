# 설계: 공포 전 법안 레이더 (2026-11-24)

- 관련: [명세](spec.md), [작업](tasks.md)

## 1. 선택한 구조

```
GitHub Actions collect.yml  (bills 작업, 법령 수집 작업과 별개)
  └ POST /jobs/collect-bills ── BillRadarService
        ① 조회(트랜잭션 밖, 전체 120초 상한)
           ├ 지켜볼 법률마다   AssemblyApiClient.ListByLawNameAsync ─┐
           └ 재조회 대상마다   AssemblyApiClient.GetByBillNoAsync  ─┴→ 국회 열린국회정보
        ② 병합과 저장(짧은 트랜잭션, 자문 잠금) → Bills, BillEvents, BillCollectLogs
웹 /radar ── GET /api/radar ── Bills + 최근 사건 + 공포 후보(LawChanges, GroupReviews)
```

- **Workflow 엔진을 쓰지 않는다.** 엔진의 실행(`WorkflowRun`)은 공포 법령 하나(`LawChangeId`)에 묶여 있고, 단계는 AI 호출과 사람 승인을 전제로 한다. 레이더는 AI도 승인도 없는 목록 동기화다. 그래서 법령 수집(`CollectService`)처럼 잡 단위 서비스로 둔다. 엔진을 일반화하는 일은 법안에 AI 분석을 붙일 때 다시 판단한다.
- **AI를 쓰지 않는다.** 공포 전 법안은 조문이 확정되지 않아 근거 대조가 성립하지 않는다(전체 설계 9절 결정 1과 같은 이유). 그룹 배정도 임베딩 대신 법률명 정확 일치로 한다. 변호사는 이 법안이 왜 노동 그룹에 떴는지 바로 알 수 있다.
- **조회와 저장을 나눈다.** 외부 호출 수십 번을 트랜잭션과 잠금 안에서 돌리면, 국회 API가 느릴 때 DB 연결이 오래 묶인다. 그래서 조회를 모두 끝낸 뒤 짧은 트랜잭션에서 병합한다.

### 대안

| 대안 | 고르지 않은 이유 |
|---|---|
| 국회 API의 소관위원회로 그룹 배정 | 위원회 개편으로 이름이 바뀐다(2026-11 응답에 `환경노동위원회`와 `기후에너지환경노동위원회`가 섞여 있다). 접수 직후에는 위원회가 비어 있다 |
| 날짜 범위로 전체 목록을 받아 걸러 내기 | 키 없이는 호출마다 5건이라 하루 발의(수십 건)를 다 받지 못한다 |
| 입법예고를 합성 응답으로 먼저 구현 | 실응답을 본 적이 없어 필드와 오류 형태를 추측하게 된다. 승인 뒤 녹화 응답으로 붙인다 |
| 법안마다 그룹 코드 열을 저장 | 지켜볼 법률 목록을 바꾸면 저장된 값과 어긋난다. 법률명으로 조회 때 계산한다 |

## 2. 데이터

### `PracticeGroup.WatchLawNames` (text[], 기본 빈 배열)

명세 BR-01의 시드다. 마이그레이션이 기존 세 그룹 행에 값을 넣는다. `DemoSeeder.EnsureGroupsAsync`도 비어 있으면 채운다(새 DB 대비). 기존 `LawIds` 열은 쓰는 곳이 없는 예약 열이고 이번 기능과 관계없다.

### `Bills`

| 열 | 형식 | 설명 |
|---|---|---|
| Id | uuid PK | |
| Source | varchar(16) | `assembly`. 나중에 `lawmaking` |
| ExternalId | varchar(64) | `BILL_ID`. (Source, ExternalId) 고유 |
| BillNo | varchar(16) | `BILL_NO`. 재조회 키 |
| BillName | text | `BILL_NAME` |
| LawName | text | 일치한 법률명(지켜볼 목록의 값) |
| Stage | varchar(16) | `Received`, `Committee`, `Judiciary`, `Plenary`, `Passed`, `Merged`, `Closed` |
| ProposedOn | date | `PROPOSE_DT` |
| Proposer | text | `PROPOSER`("○○의원 등 N인") |
| LeadProposer | text? | `RST_PROPOSER` |
| Committee | text? | `COMMITTEE` |
| ProcResult | text? | `PROC_RESULT`. 없고 위원회 결과가 대안반영폐기류면 그 값 |
| ProcOn | date? | `PROC_DT`(본회의 처리일. Merged면 대안의 처리일) |
| DetailUrl | text | `DETAIL_LINK`의 `http://`를 `https://`로 바꾼 값 |
| RawJson | jsonb | 마지막 응답 행 |
| FirstSeenAt, LastCheckedAt, LastEventAt | timestamptz | `LastEventAt`은 그 법안 사건 중 가장 늦은 `At` |

인덱스: (Source, ExternalId) 고유, LastEventAt, LawName.

### `BillEvents`

Id, BillId(FK, 법안 삭제 시 함께 삭제), At, Kind(`proposed`, `stage_changed`), FromStage?, ToStage. 인덱스 (BillId, At).

- `proposed`의 `At`은 언제나 발의일 00:00 KST다. 그래서 키를 새로 넣거나 법률을 추가해 옛 법안이 한꺼번에 들어와도 "새 발의"로 보이지 않는다.
- `stage_changed`의 `At`은 수집 시각이다.

### `BillCollectLogs`

Id, StartedAt, FinishedAt, Ok(성공 호출 1건 이상), NewBills, StageChanges, Refreshed, FailureCount, FailuresJson(jsonb, 최대 50건). 502일 때도 남긴다. 90일 지난 행은 데모 초기화 작업이 지운다.

## 3. 국회 API 클라이언트

`AssemblyApiClient`. HttpClient 기본 주소는 `https://open.assembly.go.kr/portal/openapi/`이고 `User-Agent` 헤더가 꼭 있어야 한다. 없으면 400 `Bad Request.`를 받는다(2026-11-24 확인).

- `ListByLawNameAsync(lawName)`: `nzmimeepazxkubdpn?Type=json&pIndex=1&pSize=<n>&AGE=22&BILL_NAME=<법률명>`. 키가 있으면 `KEY=<키>&` 를 앞에 붙이고 `pSize=100`, 없으면 `pSize=5`.
- `GetByBillNoAsync(billNo)`: 같은 주소에 `BILL_NO=<번호>`.
- 응답 해석
  - 정상: `{"nzmimeepazxkubdpn":[{"head":[…{"RESULT":{"CODE":"INFO-000"}}]},{"row":[…]}]}`
  - 결과 없음: `{"RESULT":{"CODE":"INFO-200"}}` → 빈 목록(성공)
  - 그 밖의 결과 코드(`ERROR-*`, 모르는 `INFO-*`): 실패. 코드는 `ASSEMBLY_API_` 뒤에 원 코드의 하이픈을 밑줄로 바꿔 붙인다(`ERROR-290` → `ASSEMBLY_API_ERROR_290`, 잘못된 키)
  - HTTP 오류, 네트워크 오류와 시간 초과, JSON 아님: `ASSEMBLY_API_HTTP_<n>`, `ASSEMBLY_API_NETWORK`, `ASSEMBLY_API_ERROR_BODY`. 예외 메시지에 주소(키 포함)가 실릴 수 있어 형식 이름만 남긴다(`LawApiClient`와 같은 규칙)
- 행을 `AssemblyBill` 레코드로 바꾼다. 필드 이름 지식은 이 파일에만 둔다. 날짜는 `yyyy-MM-dd`이고, 빈 값과 `null`은 없음으로 본다. 필수 필드(의안 ID, 번호, 이름, 발의일)가 빠진 행은 `BadRows`로 돌려준다. 그런 행이 있어도 호출은 성공으로 센다.
- 호출마다 20초 제한을 둔다.

## 4. 단계 계산과 법안명 일치

- `BillStages.From(AssemblyBill)`: 명세 BR-03 순서. 녹화로 확인한 예는 다음과 같다.
  - `recorded_labor_keyless.json`: 2223132(접수, 위원회 없음)는 `Received`, 2222911(위원회 회부)은 `Committee`.
  - `recorded_bill_passed.json`: 2221367(국방위, `LAW_*` 있음, 원안가결)은 `Passed`.
  - `recorded_bill_judiciary_passed.json`: 2221357(법원조직법, 법사위 소관)은 `LAW_*` 없이 위원회 처리일만 있다. 처리 결과를 지우면 5번 규칙으로 `Plenary`다.
  - `recorded_bill_merged.json`: 2217813(근로기준법, 대안반영폐기, `PROC_DT=2026-05-07`)은 `Merged`.
  - `recorded_bill_closed.json`: 2200500(대기관리권역법, 대안반영폐기)도 `Merged`다. `Closed` 예는 합성 값(철회)으로 검사한다.
- `BillStages.NeedsRefresh(stage, procOn, today)`: `Closed`면 아니다. `Passed`와 `Merged`는 처리일이 없거나 처리일 뒤 30일 안이면 맞다. 나머지는 맞다.
- `BillNames.Matches(billName, lawName)`: 정규식은 `^<법률명 이스케이프> ?(일부개정|전부개정|폐지)법률안(\(대안\))?$`이다. 공백은 반각 한 칸이다. 비교 전에 양쪽 앞뒤 공백을 지운다.

## 5. 수집 순서 (`BillRadarService.CollectAsync`)

**① 조회** (트랜잭션 밖, 120초 상한)

1. 그룹을 읽어 지켜볼 법률명 집합을 만든다.
2. 재조회 후보를 읽는다. 조건은 다음과 같다.
   - `Source=assembly`, 법률명이 지켜볼 집합에 있다.
   - `NeedsRefresh`가 참이다.
   - `LastCheckedAt` 오름차순으로 정렬한다.
3. 법률명마다 목록을 받는다.
   - 실패하면 `failures`에 `law:<법률명>`을 넣는다.
   - 성공하면 일치하는 행만 남긴다.
   - `BadRows`는 `row:<의안번호>`, `ASSEMBLY_API_BAD_ROW`로 넣는다.
4. 2의 후보 중 3에서 나오지 않은 것을 상한(키 없으면 60, 있으면 200)까지 의안번호로 받는다.
   - 응답 행 중 `BILL_ID`가 같은 것만 쓴다. 다른 것뿐이면 `bill:<번호>`, `ASSEMBLY_API_BAD_ROW`다.
   - 결과 없음은 `bill:<번호>`, `ASSEMBLY_API_NOT_FOUND`이고 성공 판정에서 뺀다.
   - 재조회에서는 법안명 일치를 다시 검사하지 않는다.
5. 호출 사이에 250ms를 쉰다(키 없는 호출의 제한이 공개되어 있지 않다). 전체 상한 토큰은 요청 취소와 엮지 않는다. 그래서 curl이 먼저 끊겨도 서버는 받은 만큼 저장한다. 상한을 넘으면 남은 호출을 건너뛰고 `*`, `ASSEMBLY_BUDGET_EXCEEDED`를 넣는다.

**② 저장** (짧은 트랜잭션)

6. 성공한 호출이 0이고 시도가 1 이상이면 수집 기록(Ok=false)만 남기고 `BillCollectUnavailableException`을 던진다. 엔드포인트가 502로 바꾼다.
7. 트랜잭션을 열고 `pg_advisory_xact_lock(<상수>)`을 잡는다. 두 실행이 겹치면 두 번째는 첫 번째가 커밋한 결과를 보고 병합하므로 사건이 겹치지 않는다.
8. 받은 행마다 `ExternalId`로 법안을 찾는다.
   - 없으면 새 법안과 `proposed` 사건을 만든다(목록 조회 결과에서만).
   - 있으면 필드를 갱신하고, 단계가 다르면 `stage_changed` 사건을 만든다.
   - `LastCheckedAt`은 now다. 실패한 재조회 대상도 `LastCheckedAt`을 now로 바꿔 뒤로 미룬다.
   - `LastEventAt`은 사건 `At` 중 최댓값이다.
9. 수집 기록을 넣고 커밋한다.

- 최악의 호출 수는 키가 없으면 15 + 60 = 75회(약 40~60초), 키가 있으면 15 + 200회다. 키가 있는 경우는 120초 상한에 걸릴 수 있지만, 남은 것은 다음 날 이어서 본다(`LastCheckedAt` 순서).

## 6. 조회 API (`GET /api/radar`)

```json
{
  "lastCollectedAt": "2026-11-24T02:10:00Z",
  "sources": { "assembly": "ok", "lawmaking": "not_connected" },
  "watch": [{ "groupCode": "labor", "groupName": "노동", "lawNames": ["근로기준법", "…"] }],
  "bills": [{
    "id": "…", "billNo": "2223132", "billName": "근로기준법 일부개정법률안", "lawName": "근로기준법",
    "stage": "Received", "proposedOn": "2026-11-23", "proposer": "이헌승의원 등 11인", "leadProposer": "이헌승",
    "committee": null, "procResult": null, "procOn": null, "groupCodes": ["labor"],
    "detailUrl": "https://likms.assembly.go.kr/bill/billDetail.do?billId=…",
    "lastEvent": { "kind": "proposed", "at": "2026-11-22T15:00:00Z", "fromStage": null, "toStage": "Received" },
    "promulgated": null
  }]
}
```

- `group`: 알 수 없는 코드면 400 `UNKNOWN_GROUP`. 주어지면 그 그룹의 `WatchLawNames`에 법률명이 있는 법안만 고른다. 생략하면 어느 그룹에든 있는 법안이다.
- `groupCodes`: 그 법률명을 지켜보는 그룹 코드들(정렬).
- 최대 200건, `LastEventAt` 내림차순, 같으면 의안번호 내림차순.
- `lastCollectedAt`: `Ok=true`인 마지막 수집 기록의 `FinishedAt`이다. 없으면 null.
- `sources.assembly`
  - 기록이 없으면 `never`.
  - 마지막 기록이 `Ok=false`이거나 `lastCollectedAt`이 48시간보다 오래됐으면 `failing`.
  - 나머지는 `ok`.
- `promulgated`(BR-07): 처리일이 있는 `Passed`와 `Merged` 법안의 법률명을 모아 `LawChanges`를 한 번 조회한다. 조건은 다음과 같다.
  - `LawName`이 같다.
  - `PromulgationDate`가 처리일부터 처리일 + 40일 사이다.
  - 재현 실행의 법령이 아니다.
  - `RawJson`의 `제개정구분명`이 `타법개정`이 아니다.
  가장 이른 것을 고른다. `reviewId`는 그 법령 실행의 그룹 검토 중 법안 그룹 코드에 드는 첫 검토의 ID다(그룹 코드 순서).

## 7. 웹

- `lib/api.ts`: `getRadar(group?)`, 형식 `Radar`, `RadarBill`, `BillStage`.
- `lib/labels.ts`: `billStage` 도메인(접수, 위원회 심사, 법사위 심사, 본회의 대기, 본회의 의결, 대안에 반영, 종료)과 사건 배지(새 발의, 단계 바뀜).
- `lib/radar.ts`(순수 함수, Vitest)
  - `stageStep(stage)`: 걸음 칸 0~4. `Merged`는 의결 칸(4), `Closed`는 null.
  - `badges(bill, now)`: 발의일 7일 안이면 새 발의, 최근 사건이 `stage_changed`이고 7일 안이면 단계 바뀜.
- `app/radar/page.tsx`
  - 클라이언트 컴포넌트다. 기존 목록 화면처럼 서버 깨우기 대기 화면을 쓴다.
  - 전체 목록을 한 번 받고 그룹 탭(노동, 개인정보, 금융 순)은 화면에서 `groupCodes`로 거른다. API의 `group` 인자는 다른 소비자를 위해 둔다.
  - 법안은 카드 목록으로 보인다.
  - 출처 상태 줄: "국회 의원 발의 법률안, 마지막 수집 …", `failing` 경고, "입법예고 연결 준비 중".
- `components/BillStepper.tsx`: 다섯 칸 걸음 표시. 현재 칸까지 채운다.
- 머리 메뉴 `LINKS`에 `{ href: "/radar", label: "법안 레이더" }`를 넣는다.

## 8. 작업과 설정

- `POST /jobs/collect-bills`는 `JobEndpoints`에 둔다. 토큰 검사는 기존 `TokenOk`를 쓴다.
- 설정 `ASSEMBLY_API_KEY`는 선택이다. 없으면 키 없이 부른다.
- `collect.yml`에 `bills` 작업을 따로 둔다(`timeout-minutes: 10`).
  1. 서버를 깨운다(기존과 같은 `/health` 재시도).
  2. `curl --fail --max-time 200`으로 한 번 부른다. 재시도는 하지 않는다. 서버 상한 120초보다 길게 잡는다.
  3. 응답을 출력한다.
  4. `failures`가 있으면 `::warning::`으로 남긴다.

## 9. 실패 처리 요약

| 상황 | 처리 |
|---|---|
| 법률 하나 조회 실패 | 건너뛰고 `failures`에 넣고 나머지를 진행한다 |
| 전부 실패(국회 API 장애, 잘못된 키) | 수집 기록(Ok=false)만 남기고 502 `ASSEMBLY_API_UNAVAILABLE`. 작업이 실패한다 |
| 국회 API가 느림 | 120초 상한에서 멈추고 받은 만큼 저장한다. 남은 것은 다음 날 이어서 본다 |
| 같은 잡을 두 번 동시에 부름 | 조회는 따로 하고 저장은 자문 잠금으로 차례로 한다. 사건은 겹치지 않는다 |
| curl이 먼저 끊김 | 서버는 요청 취소와 무관하게 끝까지 저장한다 |
| 재조회한 법안이 사라짐(INFO-200) | 실패 목록에 넣고 `LastCheckedAt`만 갱신한다 |
| 행 해석 실패 | 그 행만 건너뛰고 `ASSEMBLY_API_BAD_ROW`로 넣는다 |

## 10. 검증 계획

- 단위 테스트
  - 단계 계산: 4절의 녹화 다섯 건과 합성 경계 값.
  - 법안명 일치: `은행법`과 `한국은행법`, `(대안)`, 공백 없음, 종류 없는 `법률안`은 거부.
  - 응답 해석: 정상, INFO-200, ERROR-290, HTML, 빈 본문.
  - 재조회 대상 판정.
- 통합 테스트(Testcontainers Postgres, 가짜 HttpMessageHandler)
  - 첫 수집에서 새 법안과 발의일 시각의 사건이 생긴다.
  - 두 번째 수집에서 단계가 바뀌면 사건이 생긴다.
  - 한 법률이 실패해도 나머지는 저장된다.
  - 전부 실패하면 502가 나고 법안은 저장되지 않으며 기록만 남는다.
  - 두 그룹에 걸친 법률은 `groupCodes` 두 개로 나온다.
  - 재조회 상한과 순서를 지키고, 실패한 재조회도 `LastCheckedAt`이 갱신된다.
  - `/api/radar`의 정렬, 그룹 필터, 400, 출처 상태, 공포 후보(기간과 타법개정 제외)가 맞다.
  - 401.
- 웹
  - Vitest로 순수 함수를 검사하고 빌드한다.
  - 로컬에서 실제 국회 API로 수집한 뒤 `/radar` 화면을 Playwright로 375px와 데스크톱에서 확인한다.
