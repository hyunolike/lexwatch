# 명세: Cloudflare Workers AI 공급자 (2026-11-19)

- 작성: hh.jang
- 관련: [데모 신뢰도 명세](../demo-readiness/spec.md), [디자인 시스템 설계 2.1절 출처 색](../design-system/design.md)

## 해결할 문제

공개 데모의 AI 서비스에 모델 키가 없어 분류, 초안, 크리틱이 모두 대체 구현으로 돈다. 무료로 쓸 수 있는 모델 경로가 필요하다. 조사 결과 Cloudflare Workers AI를 골랐다(하루 10,000 Neurons 무료, OpenAI 호환, 고객 데이터를 학습에 쓰지 않는다고 공식 문서에 명시).

## 요구사항

| ID | 요구사항 | 수용 기준 |
|---|---|---|
| CF-01 | AI 서비스가 Cloudflare Workers AI로 채팅을 부른다 | `CLOUDFLARE_ACCOUNT_ID`와 `CLOUDFLARE_API_TOKEN`이 있으면 `https://api.cloudflare.com/client/v4/accounts/<계정>/ai/v1/chat/completions`를 `@cf/openai/gpt-oss-120b`(기본값, `CLOUDFLARE_MODEL`로 변경)로 부른다. 재시도, `Retry-After`, 텍스트 도구 호출 해석은 NVIDIA 경로와 같다 |
| CF-02 | 공급자 선택 규칙이 분명하다 | `LLM_PROVIDER`가 `nvidia`, `cloudflare`, `mock`이면 그대로. `auto`(기본)는 NVIDIA 키가 있으면 NVIDIA, 없고 Cloudflare 값이 둘 다 있으면 Cloudflare, 둘 다 없으면 대체 구현 |
| CF-03 | 출처를 사실대로 표시한다 | Cloudflare가 처리한 단계와 답변은 출처 종류 `cloudflare`로 저장되고 화면에 "Cloudflare" 라벨이 붙는다. NVIDIA라고 표시하지 않는다 |
| CF-04 | 출처 색이 상태 색과 겹치지 않는다 | `source-cloudflare`(#8E2C7A, 배경 #F8E9F4) 대비 4.5:1 이상, 색상각이 상태 색 범위 밖. 대비 테스트가 자동 검사 |
| CF-05 | 한도 초과에도 화면이 깨지지 않는다 | Neurons 한도를 넘으면 기존 실패 처리(재시도 뒤 대체 구현)로 넘어가고 출처 라벨이 대체 구현으로 바뀐다 |

## 결정

- 검색(임베딩)은 이번에 바꾸지 않는다. `bge-m3`는 1,024차원이라 지금 2,048차원 열과 맞지 않아 마이그레이션이 필요하다. 키가 없으면 지금처럼 lexical 검색.
- 기존 `NvidiaNimProvider`를 OpenAI 호환 공급자로 일반화하고, NVIDIA와 Cloudflare는 이름과 출처 종류만 다른 두 인스턴스로 둔다. Nemotron 전용 사고 토글은 모델 이름이 nemotron일 때만 붙으므로 Cloudflare 요청에는 섞이지 않는다.
- 토큰은 작성자가 넣는다(Workers AI 읽기와 편집 권한, 계정 하나, 2027-11-19 만료). 계정 ID는 비밀이 아니라 Vercel 환경변수에 그대로 둔다.

## 검증

- Python: 공급자 선택(CF-02), 요청 주소와 인증 헤더와 출처 정보(CF-01, CF-03)
- .NET: `cloudflare` 출처 종류의 매핑과 저장 허용
- 웹: 대비와 색상각 테스트(CF-04), 출처 라벨
- 실호출: 토큰을 넣은 뒤 로컬에서 크리틱 평가 20건, 공개 데모에서 수집 한 번
