# NVIDIA 연동과 검증 수준

이 문서는 LexWatch가 NVIDIA 기술을 어디에 쓰고, 그 연동을 어느 수준까지 확인했는지 적는다.
작성 시점(2026-11-16)에 `NVIDIA_API_KEY`는 한 번도 쓰지 않았다. 그래서 **실행 검증은 하나도 없다.**

## 검증 수준의 뜻

| 수준 | 뜻 |
|---|---|
| 실행 검증 | 실제 NVIDIA 엔드포인트(또는 사내 NIM)를 호출해 응답을 받고 결과를 확인했다 |
| 녹화 테스트 | 응답 모양을 재구성한 픽스처나 가짜 전송 계층(`httpx.MockTransport`)으로 우리 코드의 처리를 테스트했다. 실제 NVIDIA 서버가 그 모양으로 답하는지는 확인하지 않았다 |
| 문서만 | 코드나 설정을 준비했거나 절차를 글로만 적었다. 실행하지도 테스트하지도 않았다 |

`fixtures/nim/*`는 공개 문서와 이전 프로젝트(ReRoute) 코드의 응답 형태를 보고 만든 재구성본이다. 녹화 테스트가 통과해도 실제 모델의 한국어 법률 문장 품질이나 지연은 알 수 없다.

## 연동별 표

| 연동 | 쓰는 곳 | 수준 | 근거와 한계 |
|---|---|---|---|
| NIM 채팅(Nemotron, `/chat/completions`) | `ai/app/providers/llm/nim.py` | 녹화 테스트 | `ai/tests/test_nim.py`가 429와 `Retry-After` 재시도, 재시도 소진 시 오류, 텍스트 형태 도구 호출 파싱, `response_format` 전송, 마감 시각 초과, 잘못된 도구 인자를 가짜 전송으로 확인한다. 실제 모델 호출은 없다 |
| 임베딩과 리랭커(NeMo Retriever, `/embeddings`와 `reranking`) | `ai/app/rag/nvidia.py` | 녹화 테스트 | `ai/tests/test_retriever.py`가 가짜 응답으로 임베딩 정렬, 리랭크 순열, 깨진 응답 처리를 확인한다. 임베딩 차원 2048은 공개 문서 기준 값이고 실측하지 않았다 |
| NeMo Guardrails | `ai/app/guardrails/` | 문서만 | 실제로 도는 것은 결정적 입력 규칙(`deterministic-input-rails`)이다. 이것은 NeMo가 아니다. NeMo용 설정(`config.yml`, `rails.co`)은 짝으로만 있고 `nemoguardrails`는 선택 의존 그룹이라 배포 번들과 테스트에 들어 있지 않다. 이유는 `ai/README.md` 참고 |
| 사내 NIM 배포 | `docs/on-prem-nim.md` | 문서만 | `NIM_BASE_URL`과 `RERANK_URL`을 바꾸는 절차를 적었을 뿐 GPU에 올려 본 적이 없다 |
| 모델 ID 확인 | `.env.example`의 `NIM_MODEL` 등 | 문서만 | 모델 이름은 프로젝트가 지정한 값이고 `/v1/models`로 존재를 확인하지 않았다 |
| Nemotron 한국어 판정 | `scripts/try_nemotron_ko.py` | 문서만 | 스크립트는 있으나 실행하지 못했다. 분류, 초안, 크리틱 품질은 미측정이다 |

## 화면에 보이는 정직성 배지

모든 단계와 응답은 공급자 종류(`nvidia`, `fallback`, `replay`, `none`)를 달고 다닌다.

- NVIDIA(초록): NVIDIA 서비스가 실제로 돈 단계
- 대체 구현(주황): 키가 없거나 장애로 결정적 대체 경로가 돈 단계. NVIDIA라고 부르지 않는다
- 재현(회색): 녹화를 다시 재생한 단계. 법령 이름 앞에 `[재현 예시]`를 붙인다

키 없이 띄운 로컬 화면에는 NVIDIA 배지가 나오지 않는다. 초록 배지가 보이면 그 단계는 실제 호출이다.

## 키가 생기면 할 일

1. `scripts/record_lawapi.py`로 본인 OC를 써서 법령 응답을 녹화하고 `fixtures/lawapi`의 예시값을 교체한다.
2. `scripts/try_nemotron_ko.py`를 실행해 `docs/notes/2026-11-16-spike.md`의 한국어 판정 절을 채운다.
3. `make eval-critic`을 키를 넣고 다시 돌려 실모델 수치를 `eval/results`에 남긴다.
4. 위 표에서 실제로 확인한 항목만 "실행 검증"으로 올린다.
