# 설계: 운영 시각화 (2026-11-24)

- 관련: [명세](spec.md), [작업](tasks.md)

## 1. 실행 타임라인 (웹만)

`GET /api/runs/{id}`의 `timeline`에는 단계 기록마다 `at`(시작 시각, 없으면 예정 시각), `durationMs`, `attempt`, `status`, `providerKind`, `groupCode`가 있다. 백엔드는 바꾸지 않는다.

### 배치 계산 `lib/runTimeline.ts` (순수 함수, Vitest)

`layoutTimeline(items, targetWidth = 720, now) → { rows, gaps, decisions, width, origin, busyMs, workMs, longest }`

1. **막대 구간:** step 항목만 쓴다(도구 호출, 적발, 알림은 그리지 않는다). `start = at`이다. 길이는 상태별로 정한다.
   - Pending: 0(빈 표시)
   - Running: `now - start`. 남아 있는 `durationMs`는 앞 시도의 값이다(StepQueue가 집을 때 지우지 않는다)
   - 그 밖(Succeeded, Failed, NeedsReview, RetryScheduled): `durationMs`. RetryScheduled의 `at`은 마지막 시도의 시작이고 다음 예정 시각은 응답에 없다
2. **줄:** (step, groupCode)마다 한 줄이다. 가장 이른 start 순서, 같으면 단계 순서(fetch_articles … critic_judge, notify), 그다음 그룹 코드 순이다. 같은 줄의 두 번째 이후 막대는 `again`이다.
3. **빈 시간 접기:** 모든 막대 구간과 결정 점을 시작 순으로 합치되, 앞 구간 끝과 60초 이내로 붙은 것은 같은 바쁜 구간으로 합친다(그 사이 빈 시간은 선형으로 남는다). 60초를 넘는 빈 구간은 28px로 접는다. 축척 `scale = 목표 폭 / Σ 바쁜 구간 길이`이므로 그래프 폭은 `목표 폭 + 28 × 접힌 수`다(검토 1차 지적: 짧은 빈 구간이 수천 px가 되지 않는다). 바쁜 구간 길이 합이 1초 미만이면 1초로 본다. 막대 최소 폭은 3px이고 다음 막대를 밀지 않는다.
4. **병목:** 끝난 막대(Succeeded, Failed, NeedsReview) 중 `durationMs`가 가장 큰 것, 같으면 먼저 시작한 것이다. 비율의 분모 `workMs`는 끝난 막대와 재시도 대기 막대 구간의 합집합 길이다(빈 시간 제외). 경고 톤은 쓰지 않는다.
5. **결정 점:** approval 항목을 결정 시각에 맨 아래 "사람" 줄로 둔다. 라벨은 "변호사 승인", "파트너 최종 승인"처럼 역할과 결정, 그룹을 쓴다.

### 담당 색

`lib/runTimeline.ts`의 `stepActor`가 정한다. 백엔드 `Reviews/AiTrail.cs`의 단계 정의와 같은 값이고(notify는 규칙 추가), `providerKind === "fallback"`이면 규칙이다. 재현(`replay`)은 담당을 바꾸지 않는다. 두 곳의 정의가 어긋나지 않도록 `aiTrail.test.ts`가 백엔드 단계 키를 대조한다.

### 컴포넌트 `components/RunTimeline.tsx`

- 절대 위치 div로 그린다. 그래프 영역은 `overflow-x-auto`다.
- 막대는 대화형 요소가 아니다. 소요 시간, 상태, 오류 코드, 시도, "다시 시도한 기록", "가장 오래 걸림"을 막대 오른쪽에 글로 늘 보인다(검토 1차: 툴팁은 터치에서 열리지 않고, 막대 버튼 수십 개는 키보드 사용자를 오래 붙잡는다).
- 그래프는 `aria-hidden`이고, 앞에 sr-only로 "그래프의 내용은 목록 탭에 글로 모두 있어요"를 둔다. 병목 문장과 범례는 보이는 글이다.
- 실패와 재시도 대기는 빗금, 확인 필요는 경고 테두리, 진행 중은 깜박임과 글이다.
- `RunClient`는 기존 `Tabs`(base-ui, 좌우 화살표)로 "목록"과 "타임라인"을 나눈다.

## 2. 운영 현황

### API `GET /api/ops/summary?days=14` (`Ops/OpsEndpoints.cs`)

- 관리자 필터는 거버넌스와 같은 방식이다(`X-Demo-Role: admin`이 아니면 403).
- 기간: `from = KST 오늘 - (days-1)일 00:00`, `to = now`. 일별 배열은 `from`부터 오늘까지 days개다.
- 재현 실행 제외: `WorkflowRuns.IsReplay == false`인 실행의 단계와 그룹만 쓴다.
- 데이터가 작다(하루 수십 건). 기간의 행을 메모리로 읽어 C#에서 집계한다. 순수 함수 `OpsSummary.Build(input, from, to, today)`로 두고 테스트한다.

```json
{
  "days": 14, "from": "2026-11-11", "to": "2026-11-24",
  "dates": ["2026-11-11", "…"],
  "runs": { "total": 23, "daily": [0, 3, …], "byStatus": { "InReview": 5, "Completed": 2, "ClosedIrrelevant": 15, "Failed": 1, "Running": 0, "Queued": 0 } },
  "collect": [null, 5, 0, …],
  "stepTotals": { "outcomes": 60, "failed": 1, "failureRate": 0.017 },
  "steps": [{ "step": "classify", "outcomes": 23, "failed": 1, "failureRate": 0.043, "autoRetries": 4, "extraRecords": 1, "samples": 22, "p50Ms": 6600, "p90Ms": 21000 }],
  "ai": {
    "total": 62, "byProvider": { "nvidia": 0, "cloudflare": 60, "groq": 0 }, "ruleFallback": 2, "failed": 1,
    "chat": { "total": 4, "byOutcome": { "answered": 2, "refused": 0, "unavailable": 0, "failed": 2 }, "byProvider": { "cloudflare": 2 } },
    "daily": [0, 5, …]
  },
  "critic": { "drafts": 7, "flagged": 3, "flaggedRate": 0.429, "byTier": { "1": 1, "2": 1, "3": 4 } },
  "decisions": { "total": 5, "byKind": { "approve": 3, "reject": 1, "confirm": 1, "return": 0 }, "firstDecisionSamples": 2, "medianHoursToFirstDecision": 2.5, "waiting": 6 },
  "bills": { "lastSuccessAt": "…", "failedRuns": 0 }
}
```

- 읽기: 기간 시작 30일 전부터 만든 비재현 실행과 그 단계, 그룹, 에이전트 초안, 적발, 결정을 읽는다(기간 전에 시작한 실행의 다시 시도와 결정 시간 계산에 필요하다). 채팅, 수집 기록, 법안 수집 기록은 기간으로 읽는다. 집계는 `OpsSummary.Build(OpsInput, fromDay, today)` 순수 함수다.
- 날짜 귀속: 실행은 CreatedAt, 단계는 FinishedAt ?? StartedAt, 채팅은 At, 결정은 DecidedAt, 법안 수집은 StartedAt이다. 구간은 [기간 시작, 지금]이다.
- 반올림: 비율은 소수 셋째 자리, 시간(h)은 첫째 자리. null이 될 수 있는 값은 failureRate(분모 0), p50Ms·p90Ms(표본 없음, p90은 5개 미만), flaggedRate, medianHoursToFirstDecision, bills.lastSuccessAt이다.
- 백분위는 nearest-rank, 중앙값(결정 시간)은 짝수일 때 가운데 두 값의 평균이다.

### 화면 `/ops` (`app/ops/page.tsx`)

- `WakeGate`로 불러온다. 관리자가 아니면 안내 Callout만 보인다.
- 기간 선택은 버튼 세 개(`aria-pressed`)다.
- 관리자가 아니면 API를 부르지 않고 안내 Callout만 보인다. `WakeGate`의 key는 역할과 기간이다.
- 숫자 카드 네 개. 실행 수와 AI 처리 카드에만 스파크라인(`components/ds/Sparkline.tsx`, SVG polyline, `aria-hidden`)을 달고, 하루 최대 값 같은 내용을 글로 둔다. 다른 지표는 날마다 0인 경우가 많아 추세선이 정보가 없다.
- 단계 목록: 기록이 있는 단계만 한 줄씩. 최종 실패율 막대(0~100% 같은 축)와 "(실패/분모)", 소요 시간 막대(같은 축, 가장 큰 p90 또는 p50 기준)와 "(n=표본)", 자동 재시도와 다시 시도한 기록 수를 글로 둔다.
- AI 공급자: 100% 누적 막대 하나와 범례(이름, 수, 비율 글). 규칙 대체, 실패, 채팅 결과는 글로 둔다.
- 사람 결정: 대기 수, 종류별 수, 결정까지 걸린 시간 중앙값과 n, 법안 수집 마지막 성공.
- 머리 메뉴 `ADMIN_LINKS`에 `{ href: "/ops", label: "운영 현황" }`을 넣는다.

## 3. 검증 계획

- 백엔드 단위 `OpsSummaryTests`: 빈 데이터(0과 null, 날짜 배열 길이), KST 자정 경계와 일별 합 = 합계, 수집 기록의 null과 0, 최종 결과 기준 실패율, 자동 재시도와 다시 시도한 기록, 진행 중 제외, p90 표본 하한, AI 최종 공급자와 규칙 대체와 실패 분리(일별 합 = AI 처리 합계), 판정 전 초안 제외, 결정 시간(마지막 판정 기준, 파트너만 결정한 그룹 제외, 짝수 중앙값).
- 통합: 403, 400, 200 형태.
- 웹 Vitest `runTimeline.test.ts`: 줄 순서, 접기(60초 경계, 폭 = 목표 + 28 × 접힌 수), 최소 폭, 병목과 분모, 대기, 진행 중(남은 durationMs 무시), 재시도 대기, 다시 시도한 기록, 결정 점, 빈 입력. `ops.test.ts`: 스파크라인 점, 비율과 시간 문구, 누적 막대.
- 로컬 Playwright: 실행 상세 타임라인(재현 02 재시도 실행), `/ops` 1280px와 375px, 키보드 탭 이동과 막대 포커스 툴팁.

## 출처

- Temporal Web UI: https://docs.temporal.io/web-ui , https://temporal.io/changelog/updated-event-history-timeline-view-is-now-available
- Langfuse timeline: https://langfuse.com/changelog/2024-06-12-timeline-view , 지연 색 https://langfuse.com/changelog/2024-10-10-text-color-for-latency-and-costs
- LangSmith waterfall: https://changelog.langchain.com/announcements/ann_t0pJcFOimB0OD
- Stephen Few, Why Most Dashboards Fail: https://blogs.ischool.berkeley.edu/i247s13/files/2013/02/WhyMostDashboardsFail.pdf
