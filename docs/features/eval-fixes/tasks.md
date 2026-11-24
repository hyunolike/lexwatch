# 작업: 2026-11-24 평가 결함 수정

명세: [spec.md](spec.md)

| ID | 변경 | 검증 | 상태 |
|---|---|---|---|
| EF-01 | `SourceMatchCritic.PeriodRx`에 `시간`, `주일`, `주(?!택)` 추가 | `CriticTests` 시간·주 불일치 3건, 일치 2건, "3주택" 1건. 먼저 실패를 확인한 뒤 고쳤다 | 완료 |
| EF-02 | `Reviews/ChatRetry.cs` 추가, 채팅 엔드포인트가 사용. `Chat:RetryDelayMs`로 테스트 대기를 줄인다 | `ChatRetryTests` 6건(깨는 중 재시도, 429와 시간 초과 미재시도, 거절 미재시도, 최대 횟수, 남은 시간 한도) | 완료 |
| EF-03 | `review_chat.py` 시스템 프롬프트 규칙 두 줄 | 프롬프트 변경. 운영 배포 뒤 같은 질문으로 재확인 | 배포 후 확인 |
| EF-04 | 가드레일 거절에 `RAIL_PROVIDER(guardrail, fallback)` | `test_rail_refusal_is_labelled_as_rule_not_model` | 완료 |
| EF-05 | `loop.group_name()`과 분석 프롬프트 규칙 | `test_analyze_prompt_names_group_in_korean` | 완료 |
| EF-06 | 분석 프롬프트에 신구조문 서술 규칙 | 프롬프트 변경. 다음 수집분에서 확인 | 배포 후 확인 |
| EF-07 | 분류 프롬프트에 관련 판단 기준 세 줄 | Cloudflare gpt-oss로 평가 법령 7건 비교(아래 표) | 완료 |
| EF-08 | `/api/radar`의 `limit`, `totals`. 웹 `tabCount`, `truncationNote`, 전부 보기 버튼 | `BillRadarTests` 합계와 잘림, 400. `radar.test.ts` 2건 | CI 확인 대기 |
| EF-09 | `reset-demo.yml` 예약 2개, `--max-time 90`, 작업 한도 25분 | 다음 예약 실행 결과 | 배포 후 확인 |
| EF-10 | `guide.roleSentence()` | `guide.test.ts` | 완료 |
| EF-11 | `reviewPresentation.splitForRole()`, 대기함 묶음 | `reviewPresentation.test.ts` | 완료 |
| EF-12 | `ChatRetry` 마감 90초, 최대 40회. 채팅 `AI_WAKING`, `GET /api/ai/status`. 웹 `waitForAi`, `isAiWaking`, `aiStateOf`, 검토 보조 상태 표시와 자동 재전송 | `ChatRetryTests` 기본값, `ReviewEndpointTests` AI_WAKING과 한도 환불, 상태 up/waking. `reviewChat.test.ts` 7건 | 완료 |
| EF-13 | `guardrails._DRAFT`, `_REVIEW_ACTION` | `test_questions_about_the_draft_itself_are_in_scope`(화면 추천 질문 포함), `test_draft_word_alone_does_not_open_scope` | 완료 |

## EF-07 실제 모델 비교 (2026-11-24, Cloudflare `@cf/openai/gpt-oss-120b`)

요약은 평가 화면의 개정 내용을 한두 문장으로 옮겨 썼다. 숫자는 relevant일 때의 confidence이고 아니면 0이다.

| 법령 | 이전 프롬프트 | 새 프롬프트 |
|---|---|---|
| 헌법재판소 사무기구에 관한 규칙 | 개인정보 0.92 | 0 |
| 헌법재판소 공무원 명예퇴직수당 등 지급 규칙 | 노동 0.92 | 0 |
| 주택공급에 관한 규칙 | 개인정보 0.88 | 개인정보 0.73 |
| 방문판매 등에 관한 법률 시행령 | 금융 0.68 | 0 |
| 직업안정법 시행령 | 노동 0.85 | 노동 0.92 |
| 고등교육법 시행령 | 0 | 0 |
| 도시개발법 시행규칙 | 0 | 0 |

주택공급 규칙은 주민등록표 제출과 행정정보 공동이용이 요약에 있어 경계 사례로 남는다. 실제 관련성 판단은 변호사 몫이고, 진행 기준 0.7을 조금 넘는다.

## 검증 기록

- 백엔드: 로컬은 Docker API 1.41이라 Testcontainers(1.44 필요)를 쓰는 126건이 돌지 않는다. DB 없는 185건 통과. 전체는 PR CI에서 확인한다.
- AI 서비스: `pytest` 124건 통과.
- 웹: vitest 196건, `tsc --noEmit`, `eslint src` 통과.

## 다음 시작점

PR CI 통과 확인 → 병합 → 운영에서 EF-03(같은 질문 재질문), EF-09(다음 예약 실행) 확인.
