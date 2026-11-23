# 설계: 승인 전 적발 확인과 비율 대조

- 명세: [spec.md](spec.md), 검토: [reviews/2026-11-23-round1.md](reviews/2026-11-23-round1.md)

## 1. 비율 토큰 (AG-01)

`SourceMatchCritic.Tokens`에 종류 `rat`를 더한다.

- `(?<den>\d[\d,]*\s?[천백만]?|[천백만])\s?분의\s?(?<num>\d+(?:\.\d+)?)` → 값 num/den
- `(?<num>\d+(?:\.\d+)?)\s?(?:%(?!p)|퍼센트(?!포인트))` → 값 num/100
- 키는 값을 소수 4자리로 반올림한 문자열. 표시는 원문 그대로.
- 금액과 기간보다 먼저 뽑고 남은 글에서 지운다.

대안: 표기 그대로 비교. 근거는 "100분의 50", 초안은 "50%"로 쓰는 일이 흔해 거짓 양성이 생기므로 값으로 비교한다. "프로" 접미사는 "프로그램" 같은 단어와 겹쳐 넣지 않는다.

## 2. 승인 API (AG-02~06)

`ApproveRequest`에 `int? AcknowledgedFindingCount`, `Approval`에 `int? AcknowledgedFindingCount` 열(마이그레이션)을 더한다.

```
approve(id, version, editedSentences?, comment?, acknowledgedFindingCount?)
  역할, 존재, 결정 가능, 버전, 초안 확인 (지금과 같음)
  before = 최신 초안 문장
  after  = 수정이 있으면 인용 검증(400 INVALID_CITATION) 후 수정본, 아니면 before
  evidence = 실행의 근거(ID, 본문), change = 실행의 LawChange
  findings = DraftRecheck.Run(before, after, 저장된 3단 적발, evidence, change)
           = CitationCritic(after)→tier 1 + SourceMatchCritic(after)→tier 2 + 옮긴 3단
  findings.Count > 0 && ack != findings.Count → 422 UNRESOLVED_FINDINGS { findings }
  저장: 수정본이면 새 초안 + 그 초안의 CriticFinding 행, 승인(AcknowledgedFindingCount = findings.Count), notify 큐
```

- `DraftRecheck`(Critic 폴더의 정적 클래스)가 판정을 모은다. 워크플로의 `CriticSteps`와 같은 `CitationCritic.Check`, `SourceMatchCritic.Check`를 부른다. `CriticFindingDto`에는 단이 없으므로 `DraftRecheck`가 단을 붙인 `RecheckFinding(SentenceIndex, Tier, RuleId, Message)`를 만든다.
- 수정 경로의 1단은 400 검증이 먼저 막으므로 사실상 0건이다. 그래도 같은 함수를 불러 워크플로와 판정을 맞춘다.
- 3단 옮기기: before의 i번 문장과 (문장, 인용)이 같은 after의 첫 문장 j로 옮긴다(이미 짝지은 j는 건너뜀). 짝이 없고 after에 i번 위치가 있으면 i로 옮기되 메시지 앞에 "(수정 전 문장) "을 붙인다. 위치가 없으면 버린다. 수정하지 않은 승인에서는 before = after라 모두 제자리로 옮겨진다.
- 확인 수 대조: bool 하나로는 무엇을 확인했는지 알 수 없다. 관리자가 크리틱을 다시 돌리면 적발이 바뀌는데, 상태가 이미 NeedsReview면 버전(xmin)이 바뀌지 않아 버전 검사로도 걸러지지 않는다. 적발 수를 대조하면 바뀐 목록을 다시 보여 줄 수 있다. 내용 해시까지는 하지 않는다.
- 422 응답 정렬: 문장 번호, 단, 규칙 ID 순.

## 3. 웹 (AG-07)

- `submitDecision`이 `acknowledgedFindingCount`를 받고, 422 `UNRESOLVED_FINDINGS`를 `{ kind: "findings", findings }`로 접는다. `DecisionFinding = { sentenceIndex, tier, ruleId, message }`.
- 승인(수정 없음): 화면 적발 수가 0보다 크면 대화상자 문구에 수를 더하고 그 수를 보낸다. 서버 값과 같으면 바로 승인되고, 다르면 422 목록이 뜬다.
- 수정 후 승인: 확인 수 없이 보낸다. 422면 목록과 "확인했고 그대로 승인" 대화상자를 보여 주고 응답 수로 다시 보낸다. 편집이 바뀌면 목록을 지운다.
