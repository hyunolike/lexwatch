# 설계: AI 거버넌스 (2026-11-24)

- 관련: [명세](spec.md), [작업](tasks.md)

## 1. 선택한 구조

```
웹 ──질문──▶ .NET API ──[가리기]──▶ Python AI 서비스 ──▶ 외부 모델
                 │
                 └─▶ ai_usage_logs (메타데이터만)
```

### 1.1 가리기는 .NET API에서 한다

| 대안 | 판단 |
|---|---|
| A. AI 서비스 입력 레일(guardrails)에 추가 | 원문이 이미 API를 넘어 AI 서비스(Render의 다른 컨테이너)로 간 뒤다. AI 서비스 로그와 오류 추적에 원문이 남을 수 있다 |
| **B. .NET API에서 AI 호출 직전에 가린다** | 법인 경계(API)를 넘기 전에 원문이 사라진다. 기록 테이블도 API의 DB라 같은 트랜잭션 밖에서 바로 남길 수 있다. **선택** |
| C. 웹에서 가린다 | 클라이언트는 우회할 수 있어 통제가 되지 못한다. 화면 표시만 웹이 한다 |

### 1.2 "가리기"이지 "막기"가 아니다

막으면 변호사는 질문을 다시 써야 하고, 같은 질문을 고쳐 쓰다 다른 정보를 넣는다. 가리고 보낸 뒤 무엇을 가렸는지 알리면 일이 끊기지 않는다. 가린 질문이 근거 밖 질문이면 기존 입력 레일이 거절한다.

### 1.3 교차검토는 "적발을 확인하고 승인한 경우"에만

모든 승인에 두 사람을 요구하면 데모 흐름(방문자가 한 번 승인해 보는 것)이 길어지고, 실무에서도 위험이 낮은 초안까지 파트너를 기다리게 된다. 위험 신호가 이미 있는 경우는 승인 직전 재검사의 적발이다. 적발이 있는데도 변호사가 그대로 승인하겠다고 한 경우에만 파트너 최종 승인을 요구한다.

## 2. 민감정보 탐지 (`Governance/SensitiveDataMasker.cs`)

순수 정적 함수 `Mask(string text, IReadOnlyList<string> clientNames) → (string Masked, IReadOnlyList<MaskHit> Hits)`. `MaskHit(Kind, Label, Count)`.

고객사 이름을 먼저 가린다(형식 규칙의 자리표시를 깨뜨리지 않게). 그다음 형식 규칙을 아래 순서로 적용한다. 자리표시(`[…]`)에는 숫자가 없어 뒤 규칙이 다시 잡지 않는다. 같은 숫자열이 두 규칙에 걸리면 먼저 도는 규칙의 라벨이 이긴다(붙여 쓴 13자리는 주민등록번호).

| 순서 | kind | 패턴(개요) | 추가 검사 |
|---|---|---|---|
| 0 | `client` | 설정 이름을 길이 내림차순으로 `Regex.Escape`해 대소문자 무시, 부분 문자열 허용 | |
| 1 | `email` | `[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}` | |
| 2 | `rrn` | `(?<!\d)\d{6}[- ]?[1-8]\d{6}(?!\d)` | 앞 6자리 YYMMDD의 월 01~12, 일 01~31(달별 일수는 보지 않는다) |
| 3 | `card` | `(?<!\d)(?:\d{13,19}\|\d{4}([ -])\d{4}\1\d{4}\1\d{1,7}\|\d{4}([ -])\d{6}\2\d{5})(?!\d)` | Luhn 통과 |
| 4 | `phone` | `(?<!\d)(?:0\d{1,2}([- ])\d{3,4}\1\d{4}\|01[016789]\d{7,8})(?!\d)` | |
| 5 | `brn` | `(?<!\d)\d{3}-\d{2}-\d{5}(?!\d)` | |
| 6 | `account` | `(?<!\d)\d{2,6}(?:-\d{2,8}){2,3}(?!\d)` | 숫자 합 10~14자리. `^(19\|20)\d{2}-(0?[1-9]\|1[0-2])-(0?[1-9]\|[12]\d\|3[01])(-\|$)`로 시작하면 제외 |
| 7 | `passport` | `(?<![A-Za-z0-9])[A-Za-z]\d{8}(?![A-Za-z0-9])` | |

오탐 방지 확인 대상(테스트): `제2조의3`, `2026-11-24`, `2026. 11. 24.`, `2026-11-24 2026-12-31`, `2026-11-24-01`, `3,000,000원`, `100분의 50`, `시행령 제12조제1항`, `법률 제20123호`. 카드 규칙은 실제 묶음 형식만 받아 날짜 두 개가 공백으로 이어진 글을 16자리로 읽지 않는다.

라벨: email 이메일, rrn 주민등록번호, card 카드번호, phone 전화번호, brn 사업자등록번호, account 계좌번호, passport 여권번호, client 고객사.

## 3. 채팅 엔드포인트 변경 (`ReviewEndpoints` `/chat`)

1. 역할 403, 질문 길이(원문 1~500자) 400, 검토 404, 결정 상태와 무관(채팅은 상태를 보지 않는다), 할당량 429 순으로 검사한다(지금과 같다).
2. 기록은 역할 필터와 `TakeLast(6)`를 먼저 하고, 각 사용자 턴의 **원문 전체**를 가린 뒤 2000자로 자른다. 자르기 전에 가려야 경계에 걸린 번호 앞부분이 새지 않는다. 어시스턴트 턴은 AI가 쓴 글이고 AI 서비스가 모델에 다시 넣지 않으므로 가리지 않는다.
3. 질문의 원문 전체를 가린 뒤 500자로 자른다. 자리표시가 원문보다 길 수 있어서다(`LG` → `[고객사]`). 자를 위치가 `[`와 `]` 사이면 `[` 앞에서 자른다(`GovernanceText.Clip`).
4. 가린 질문과 기록으로 AI를 부른다. 시간은 이 호출 직전부터 잰다.
5. 성공 응답에 `maskedQuestion`과 `masked`(질문에서 찾은 것만)를 더한다.
6. `finally`에서 기록 한 행을 저장한다(아래 4절). 할당량 반환은 지금과 같다.

응답 예:

```json
{ "answer": "…", "citations": ["law:…"], "refused": false,
  "provider": { "name": "…", "kind": "nvidia" },
  "maskedQuestion": "[고객사]에도 제2조가 적용되나요?",
  "masked": [{ "kind": "client", "label": "고객사", "count": 1 }] }
```

## 4. AI 사용 기록

### 4.1 테이블 `ai_usage_logs`

| 열 | 형식 | 설명 |
|---|---|---|
| id | uuid PK | |
| at | timestamptz | 요청 시각, 인덱스 |
| purpose | varchar(32) | `review_chat` |
| group_review_id | uuid null | |
| actor_role | varchar(16) | |
| outcome | varchar(16) | `answered`(refused=false), `refused`, `unavailable`(공급자 이름 `no-key`), `failed`(Failure, 예외, 취소) |
| provider_kind | varchar(16) null | failed면 null |
| provider_name | varchar(64) null | |
| duration_ms | int | |
| masked_json | jsonb | 질문에서 가린 건수 `{"rrn":1,"client":2}`. 없으면 `{}` |

외래키는 두지 않는다. 재현 데이터 초기화가 검토를 지우므로, 화면은 링크가 끊긴 검토를 404 화면으로 보낸다. 질문, 답변, IP, IP 해시는 두지 않는다. `DemoSeeder.ResetAsync`가 90일 지난 행을 지운다. 할당량 초과(429)와 입력 검증 실패(400)는 AI를 부르지 않았으므로 남기지 않는다.

저장은 별도 `SaveChangesAsync`로 하고 예외를 잡아 경고 로그만 남긴다(GV-05). 채팅 엔드포인트의 `CancellationToken`이 취소돼도 기록을 남기도록 `CancellationToken.None`을 쓴다.

### 4.2 `GET /api/governance/ai-usage` (관리자)

별도 그룹 `/api/governance`에 두고, 그룹 필터가 `X-Demo-Role`이 `admin`이 아니면 403을 준다. 7일은 `TimeProvider` 기준 지금부터 168시간이다. `byOutcome`은 네 키를 항상 낸다. `byProvider`는 공급자가 없는 행을 뺀다. `recent`는 `at` 내림차순 100건.

```json
{ "summary": { "days": 7, "total": 12, "byOutcome": { "answered": 9, "refused": 2, "unavailable": 0, "failed": 1 },
               "masked": { "rrn": 1, "client": 3 }, "byProvider": { "nvidia": 9, "fallback": 2 } },
  "recent": [{ "at": "…", "purpose": "review_chat", "groupReviewId": "…", "actorRole": "lawyer",
               "outcome": "answered", "providerKind": "nvidia", "providerName": "…", "durationMs": 2310,
               "masked": { "client": 1 } }] }
```

### 4.3 웹 `/governance`

`WakeGate label="AI 사용 기록"`로 감싼다. 위에 숫자 네 개(최근 7일 요청, 답변, 거절과 실패, 가린 민감정보 합계), 그 아래 가린 종류별 막대 없이 텍스트 칩, 최근 기록 표(시각, 결과, 공급자, 소요, 가린 정보, 검토 링크). 관리자가 아니면 "관리자로 보기에서 볼 수 있어요" 안내. 화면 아래에 탐지 한계(이름과 주소는 잡지 못함)를 적는다.

## 5. 파트너 최종 승인

### 5.1 상태

`ReviewStatus`에 `AwaitingPartner`를 더한다(문자열 저장, 길이 32 안이라 상태 열은 바뀌지 않는다). 마이그레이션 하나에 `ai_usage_logs` 테이블과 `approvals.findings_json`(text null) 열을 넣는다.

```
NeedsReview/WaitingApproval ──변호사 승인(적발 0)──▶ Approved ──notify──▶ Notified
NeedsReview/WaitingApproval ──변호사 승인(적발≥1 확인, 또는 돌려보낸 적 있음)──▶ AwaitingPartner
AwaitingPartner ──파트너 confirm──▶ Approved ──notify──▶ Notified
AwaitingPartner ──파트너 return──▶ NeedsReview
NeedsReview/WaitingApproval ──변호사 반려──▶ Rejected
```

### 5.2 API

- 결정 공통 규칙 `Decidable`: 상태가 엔드포인트의 시작 상태이고, 그 그룹(`GroupReviewId`)에 대기, 실행, 재시도 예정 단계가 없어야 한다. 단계가 남아 있으면 409 `ANALYSIS_IN_PROGRESS`(GV-13). 지금은 1·2단 적발로 `NeedsReview`가 된 직후 3단이 아직 돌 때 승인할 수 있고, 3단이 끝나며 상태를 덮어쓴다. 이 구멍을 막는다.
- `approve`: `findings.Count > 0`이거나 그룹에 `decision=return` 기록이 있으면 `AwaitingPartner`, notify 큐 넣지 않음. 승인 기록의 `FindingsJson`에 재검사 적발 목록(`[{sentenceIndex,tier,ruleId,message}]`)을 저장한다(적발 0이면 null). 응답 `{ id, status }`.
- `confirm`, `return`: 새 엔드포인트. 둘 다 `SaveDecisionAsync`(xmin)를 쓴다. confirm은 notify를 큐에 넣는다.
- 역할: 허용 역할 집합 `DemoRoles.All = {lawyer, partner, admin}`을 `/api`와 `/api/runs` 두 필터가 같이 쓴다. 거버넌스 그룹은 admin만.
- `GET /reviews` 기본 상태에 `AwaitingPartner` 추가. 상세 응답에 `lawyerApproval: { decidedAt, acknowledgedFindingCount, findings[] } | null`(AwaitingPartner일 때 최신 approve 기록).
- `NotifyStep`: confirm 기록이 있으면 본문 끝을 "(승인: 변호사 …, 파트너 최종 승인 …)"으로 쓴다(GV-14).

검사 순서와 응답(모든 결정 엔드포인트 공통: 역할 → 입력 → 존재 → 상태와 버전 → 단계 진행):

| 엔드포인트 | 허용 역할(그 밖 403) | 입력 400 | 시작 상태(그 밖 409 `REVIEW_ALREADY_DECIDED`) |
|---|---|---|---|
| approve | lawyer | 수정 문장 인용(`INVALID_CITATION`) | WaitingApproval, NeedsReview |
| reject | lawyer | 사유(`COMMENT_REQUIRED`) | WaitingApproval, NeedsReview |
| confirm | partner | 없음 | AwaitingPartner |
| return | partner | 사유(`COMMENT_REQUIRED`) | AwaitingPartner |

예: 변호사가 `AwaitingPartner`를 반려하면 409, 변호사가 사유 없이 return을 부르면 403.

### 5.3 실행 상태와 재실행

- `WorkflowWorker.UpdateRunStatusAsync`의 "사람 결정 대기" 판정에 `AwaitingPartner`를 더한다.
- `RunEndpoints.Retryable`: `AwaitingPartner => false`.

### 5.4 웹

- `Role`에 `partner`. 쿠키 값 `partner` 인식. 역할 전환 세 칸(변호사, 파트너, 관리자). 모바일 짧은 라벨.
- `decisionActions(role, status, hasDraft)`에 `confirm`, `return` 추가.
- `getRole`이 쿠키 `partner`를 읽는다. `submitDecision`이 `confirm`, `return`을 다룬다. 승인 결과에 `status`를 돌려 `AwaitingPartner`면 화면이 안내를 띄운다.
- 라벨: review `AwaitingPartner` "파트너 승인 대기"(warning), decision `confirm` "파트너 최종 승인"(success), `return` "파트너가 돌려보냄"(warning), role `partner` "파트너".
- 채팅: `ChatResponse`에 `maskedQuestion: string`, `masked: MaskHit[]`. 새 순수 함수 `maskedUserTurn(original: ChatMessage, res): ChatMessage`가 성공 응답이면 `text=maskedQuestion`, `masked=res.masked`를 담은 사용자 메시지를, 아니면 원래 메시지를 돌려준다. `maskNotice(masked): string | null`이 안내 문구를 만든다. `ReviewChat`이 답을 받으면 마지막 사용자 메시지를 이것으로 바꾼다.
- 처음 방문 안내의 승인 단계는 변호사 승인 성공(Approved든 AwaitingPartner든)으로 완료된다. 안내 문구 "승인한 초안은 …"에 적발이 있으면 파트너 승인을 거친다는 말을 덧붙인다.

## 6. 테스트

| 대상 | 종류 |
|---|---|
| SensitiveDataMasker: 종류별 탐지, 오탐 방지 목록, 고객사 이름, 여러 건 합산 | xUnit 단위 |
| `/chat`: AI로 간 요청에 원문이 없음(가짜 AI 클라이언트로 캡처), 응답 `masked`, 기록 행(질문 없음) | 통합(Testcontainers) |
| `/governance/ai-usage`: 관리자만, 요약 집계 | 통합 |
| approve 적발 있음 → AwaitingPartner, notify 없음 / confirm → Approved + notify / return → NeedsReview / 역할 403 / 409 | 통합 |
| UpdateRunStatusAsync InReview, Retryable false | 통합 |
| 승인 중 단계 진행 → 409 ANALYSIS_IN_PROGRESS / 돌려보낸 뒤 적발 0 승인 → AwaitingPartner / confirm 알림 본문 / 파트너 GET /api/runs/{id} 200 / lawyer, partner의 ai-usage 403 / 질문 경계 자르기 | 통합, 단위 |
| 웹: `decisionActions`, 라벨, 역할 쿠키, `maskedUserTurn`, `maskNotice` | Vitest |

## 7. 미정 사항

없음. 고객사 이름 목록은 데모용 가상 이름으로 시작하고 운영에서는 설정으로 바꾼다.
