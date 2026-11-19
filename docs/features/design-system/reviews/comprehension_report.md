# Stage 6: Comprehension 보고서

- 대상: `docs/features/design-system/design.md` (FeatureRange 1–72, DetailedRange 73–183)
- 검토자: egm-comprehension (새 문맥, 대상 문서만 읽음, 2026-11-19)

## 이 문서가 제안하는 것

LexWatch 웹 화면(Next.js, `web/src`)에 shadcn/ui와 Base UI를 바탕으로 디자인 시스템을 세운다. 색과 글자 토큰은 `src/design/tokens.ts`의 16진수 값을 원본으로, `styles/tokens.css`의 `@theme`를 그 거울로 두고 테스트로 동일성을 확인한다. 화면 코드는 의미 클래스(`bg-surface` 등)만 쓰고 원색 클래스는 쓰지 않는다. 출처 라벨(NVIDIA, 대체 구현, 재현 데이터) 색은 상태 색과 겹치지 않는 계열로 분리한다. LexWatch 전용 부품(SourceLabel, StatusLozenge, RedlineDiff, CitationChip, ConfidenceMeter, 마스코트 "렉스" 등)을 `components/ds`에 두고 승인 대기함, 검토 상세, 실행 이력, 알림함, 서버 깨우기 화면에 적용한다. 신구 대조는 프런트엔드에서 단어 단위로 계산하고, 백엔드 열거형 값은 한국어 라벨과 의미 색으로 바꾸고, Enter 전송은 IME 조합 여부로 판단한다. 마스코트는 Codex로 생성해 투명 WebP로 가공한다. 다크 모드는 하지 않는다.

## 이 문서가 설명하는 기존 시스템

백엔드(`LexWatch.Api`)에 실행, 그룹 검토, 단계 상태 열거형이 있고 알림 상태는 `pending`/`sent` 문자열이다. 실무그룹은 노동, 개인정보, 금융이다. 각 단계는 NVIDIA 모델, 대체 구현, 재현 데이터 중 하나로 처리된다. "크리틱"이 초안 문장의 문제를 적발한다. 백엔드는 조문에서 `<P>` 표시를 지우고(#4), 서버를 깨우는 대기 과정(`WakeGate`)이 있다. 웹에는 `globals.css`, Turbopack CSS 로더 설정을 담은 `next.config.ts`, 테스트 27건이 있다. 출처 색은 "기존 설계 문서 5.3절"에서 초록/주황/회색으로 정해져 있었고 이 문서가 대체한다.

## 발견 사항

| ID | 등급 | 범위 | 지적 |
|---|---|---|---|
| CO-1 | Major | 6절, 1절, 3.2 Mascot, 7절 | DS-01~DS-15가 문서 안에서 정의되지 않는다. 6절 표를 ID만으로 이해할 수 없고 DS-11은 표에 없다. "DS-15의 금지 위치"가 본문에 없다 |
| CO-2 | Major | 2.1 "기존 설계 문서 5.3절" | 문서 이름과 링크가 없다 |
| CO-3 | Major | 1절 "`<P>` 표시를 지운다(#4)" | `<P>`가 무엇인지, #4가 무엇인지, 왜 그래서 화면에서 계산하는지 논리가 빠졌다 |
| CO-4 | Major | 크리틱 적발 | "크리틱"의 정의와 결과 데이터 형태(문장 단위인지)가 없다 |
| CO-5 | Major | 출처 3종 | 대체 구현과 재현의 뜻, 툴팁 문구, 값을 받는 백엔드 필드가 없다 |
| CO-6 | Major | 4.1 | 반환 타입(조각 목록 vs 상태)이 모호하다. 상태별 화면 표시, `added`/`removed` 방향, "변경 n곳" 세는 법이 없다 |
| CO-7 | Major | ConfidenceMeter | 신뢰도 값의 출처, 범위, 표시 형식, 5칸 매핑, 낮은 값 표시가 없다 |
| CO-8 | Major | 4.3, 7절 | `shouldSubmit`를 쓰는 입력 칸(검토 보조)이 무엇이고 어디 있는지 본문 부품표에 없다 |
| CO-9 | Nit | 6절 DS-03, 04 | 대비 검사 대상 쌍 목록, `ink-subtle` 예외, 출처 색상각 범위 값이 없다 |
| CO-10 | Nit | 3.1, 7절 | `tokens.css`와 `globals.css`의 관계, 거울을 손으로 쓰는지 생성하는지 |
| CO-11 | Nit | 3.3 | 서버를 왜 깨우는지, 데모 모드와 역할이 무엇인지 |
| CO-12 | Nit | 2.1, 2.2, 3.3, 5절 | Carbon, Atlassian, KRDS, Summary list, Codex CLI를 출처 없이 언급 |
| CO-13 | Nit | 3.2 CountBadge | 어떤 개수가 "위험 의미"인지 |
| CO-14 | Nit | 2.3, 4.2 | 그림자 값이 없고 알림 상태 문자열 출처가 없다 |

## 총평

1–2절은 자체로 이해할 수 있다. 3–7절은 뼈대는 따라갈 수 있지만 프로젝트 고유 개념(크리틱, 출처 3종, 신뢰도, 검토 보조), DS-xx 내용, 신구 대조 예외 상태의 화면 표시를 이 문서만으로는 구현하거나 검증할 수 없다. Blocker 없음.
