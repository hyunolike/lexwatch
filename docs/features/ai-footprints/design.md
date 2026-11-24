# 설계: AI 사용 드러내기 (2026-11-24)

- 관련: [명세](spec.md), [작업](tasks.md)

## 1. 구조

- **백엔드에서 과정을 만든다.** 웹이 `/api/runs/{id}`를 따로 부르면 요청이 두 번이 되고, 단계 출력 JSON은 그 응답에서 300자로 잘려 있다. 그래서 `ReviewEndpoints`의 상세 응답에 `aiTrail`을 붙이고, 계산은 순수 함수 `AiTrail.Build(GroupReview review, steps, approvals, latestDraft, findingTiers)`로 둔다. DB 조회는 엔드포인트가 한다. 단계 기록은 `RunId == review.RunId && (GroupReviewId == null || GroupReviewId == review.Id)`로 읽고, `findingTiers`는 최신 초안 적발의 tier 목록이다.
- **웹은 표시만 한다.** `components/AiTrail.tsx`(띠), `components/ds/AiLabel.tsx`(라벨과 팝오버)를 둔다. 문장 만들기는 순수 함수 `lib/aiTrail.ts`로 두고 Vitest로 검사한다.

## 2. 단계 정의

| key | 단계 기록(step) | 범위 | actor | 이름(화면) |
|---|---|---|---|---|
| collect | fetch_articles | 실행 | rule | 법령 수집 |
| classify | classify | 실행 | ai | 관련 그룹 분류 |
| index | index_articles | 실행 | ai | 조문 색인 |
| analyze | analyze | 그룹 | ai | 영향 분석 초안 |
| critic_citation | critic_citation | 그룹 | rule | 인용 검사 |
| critic_source | critic_source | 그룹 | rule | 원문 대조 |
| critic_judge | critic_judge | 그룹 | ai | 과잉 해석 판정 |
| decision | (승인 기록) | 그룹 | human | 변호사 검토 |

- 마지막 기록은 `StartedAt ?? NotBefore`가 가장 늦은 행이다. 같으면 `Attempt`, 그다음 `Id`로 정한다. 자동 재시도는 같은 행의 `Attempt`를 올리고(StepQueue), 관리자 다시 실행은 `Attempt = 0`, `NotBefore = now`인 새 행을 만들어(RunEndpoints) 언제나 뒤에 온다.
- 표의 actor는 기본값이다. 공급자가 `fallback`이거나 색인의 `dense`가 `ok`가 아니면 rule로 바꾼다. rule 단계와 `none`에는 공급자를 싣지 않는다.
- 시도 횟수, 소요 시간, 시각은 내지 않는다(띠에서 쓰지 않고, 재시도 예정 행은 시각이 미래다).

## 3. detail 추출 (출력 JSON, 실측 형태)

| key | 출력 예(운영 b015c465) | detail |
|---|---|---|
| collect | `{"evidence":54,"oldnewStatus":{"oldnew":"ok"}}` | `{ evidence, oldNew }` |
| classify | `{"groups":[{"group":"finance","confidence":0.92,…}]}`, 재현은 `{"relevant":[…]}` | `{ confidence }`. 출력 대신 언제나 그룹 검토의 `Confidence`(같은 보정값)를 쓴다 |
| index | `{"chunks":54,"embedded":54,"dense":"ok","model":"@cf/baai/bge-m3"}` | `{ chunks, dense }` |
| analyze | `{"toolCalls":0,"newEvidence":0,"draftVersion":1,"retrieval":{"mode":"Hybrid","sent":…}}`, 근거 부족 시 단계 상태가 NeedsReview이고 출력은 `{"toolCalls":0,"newEvidence":0,"status":"Insufficient_evidence"}`(검색 정보 없음) | `{ toolCalls, retrievalMode, sent, insufficient }`. `insufficient`는 단계 상태가 NeedsReview인지로 정한다. `retrieval.status`가 `no_index`, `fallback_all`이면 `retrievalMode`는 그 값이다. `toolCalls`가 없으면 도구 호출 기록 수 |
| critic_* | `{"findings":2,"draftVersion":1}` | `{ findings, recheckedOnApprove? }`. 단계 출력이 아니라 최신 초안의 CriticFindings를 tier(1, 2, 3)별로 센다. 변호사가 고쳐 승인하면 새 초안에 다시 검사한 적발이 붙기 때문이다 |

- JSON 해석은 `TryGetProperty`로만 하고 실패하면 해당 필드를 뺀다.

## 4. 응답 예

```json
"aiTrail": { "stages": [
  { "key": "collect", "actor": "rule", "status": "done", "providerKind": null, "providerName": null, "detail": { "evidence": 54, "oldNew": "ok" } },
  { "key": "classify", "actor": "ai", "status": "done", "providerKind": "cloudflare", "providerName": "cloudflare-workers-ai", "detail": { "confidence": 0.92 } },
  { "key": "decision", "actor": "human", "status": "waiting", "providerKind": null, "providerName": null, "detail": { "edited": false } }
] }
```

## 5. 웹

- `lib/aiTrail.ts`
  - `STAGE_NAMES`, `ACTOR`(ai: "AI", rule: "규칙", human: "사람", 아이콘 Sparkles, ListChecks, UserCheck)
  - `stageSummary(stage)`: 한 줄 요약. 예: "근거 54건, 신구조문 있음", "관련성 0.92", "조문 54개 임베딩", "하이브리드 검색 근거 8건, 도구 3회", "적발 2건", "변호사 승인 11.24 14:02"
  - `stageState(stage)`: 상태 라벨과 톤(완료, 확인 필요, 실패, 진행 중, 대기, 건너뜀)
  - `aiExplain(kind, trail, extras)`: 라벨 팝오버 문장(무엇을, 모델, 근거, 한계)
- `components/AiTrail.tsx`: 칩 목록(줄바꿈되는 한 줄)과 `<details>` 펼침 목록. 칩에는 담당 배지, 이름, 경고 상태만 두고 요약은 펼침에 둔다. 8칸 세로 목록을 늘 펼쳐 두면 375px에서 약 500px가 되어 초안을 밀어내기 때문이다.
- `components/ds/AiLabel.tsx`: 기존 `Popover`를 쓴다. 트리거는 작은 버튼 "AI"(Sparkles 아이콘), 수정된 초안이면 "AI 초안, 변호사 수정".
- 배치(코드 기준)
  - `AiTrail`: `ReviewClient`에서 확인 안내 Callout 뒤, `PartnerPanel` 앞. 초안이 없어도 보인다.
  - `AiLabel` "AI 초안": h2 "영향 검토 초안"의 형제 요소(h2 안에 넣지 않는다). 공급자는 analyze 단계.
  - `AiLabel` "AI 판정": `DraftEditor`의 적발 Callout 중 `tier === 3`. 공급자는 critic_judge 단계. 재검사 목록과 파트너 패널에는 붙이지 않는다.
  - `AiLabel` "AI 답변": `ReviewChat`의 답변 `SourceLabel` 앞. 거절, 오류, 대체 구현에는 붙이지 않는다.

## 6. 검증 계획

- 백엔드 단위: `AiTrail.Build`에 다음 경우를 넣는다.
  - 운영 실측 출력과 재현 출력
  - 관리자 다시 실행(시도 5 실패 행 뒤의 시도 1 성공 행, 아직 집히지 않은 새 행)
  - 대체 구현과 키워드 색인
  - 근거 부족 뒤 멈춘 단계, 진행 중
  - 출력 JSON 깨짐
  - 검토 상태별 사람 단계(파트너 대기, 돌려보냄, 반려, 관련 없음)
  - 변호사 수정본의 재검사 적발
- 통합: 상세 응답에 `aiTrail.stages` 8개가 순서대로 있다.
- 웹: Vitest로 요약과 상태 문장, 빌드, 로컬에서 Playwright로 실제 실행 초안과 재현 초안을 1280px, 375px에서 보고 팝오버를 키보드로 연다.

## 출처

- Harvey, AI agents for legal work: https://www.harvey.ai/en-US/blog/ai-agents-for-legal-work
- Thomson Reuters, CoCounsel Legal reimagined: https://www.thomsonreuters.com/en-us/posts/innovation/cocounsel-legal-reimagined/
- IBM Carbon AI label: https://carbondesignsystem.com/components/ai-label/usage/
- SAP Fiori AI Notice: https://www.sap.com/design-system/fiori-design-web/v1-139/foundations/best-practices/global-patterns/ai-notice-base-concept
- Shape of AI, Trust builders: https://shapeof.ai/pattern-types/trust-builders
- NN/g Explainable AI: https://www.nngroup.com/articles/explainable-ai/
- 신김 뉴스레터, 인공지능기본법 투명성 의무: https://shinkim.com/kor/media/newsletter/3142
