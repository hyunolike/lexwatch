# NVIDIA 연동과 검증 수준

이 문서는 LexWatch가 NVIDIA 기술을 어디에 쓰고, 그 연동을 어느 수준까지 확인했는지 적는다.
2026-11-17에 NVIDIA 호스팅 API에서 채팅과 임베딩을 검증했다. 분류와 초안, 크리틱은 합성 예시로 실행했다.

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
| NIM 채팅(Nemotron, `/chat/completions`) | `ai/app/providers/llm/nim.py` | 실행 검증 | 기존 모델은 HTTP 410. 후속 모델 `nvidia/nemotron-3.5-super-vl-120b-a12b`로 구조화 응답과 한국어 분류, 초안, 크리틱을 확인했다. 429 재시도와 도구 호출 파싱은 녹화 테스트 범위다 |
| 임베딩(NeMo Retriever, `/embeddings`) | `ai/app/rag/nvidia.py` | 실행 검증 | `nvidia/nemotron-3-embed-1b`로 한국어 질의의 벡터가 2,048차원임을 확인했다 |
| 리랭커(NeMo Retriever, `reranking`) | `ai/app/rag/nvidia.py` | 녹화 테스트 | `ai/tests/test_retriever.py`가 순위와 깨진 응답 처리를 확인한다 |
| NeMo Guardrails | `ai/app/guardrails/` | 문서만 | 실제로 도는 것은 결정적 입력 규칙(`deterministic-input-rails`)이다. 이것은 NeMo가 아니다. NeMo용 설정(`config.yml`, `rails.co`)은 짝으로만 있고 `nemoguardrails`는 선택 의존 그룹이라 배포 번들과 테스트에 들어 있지 않다. 이유는 `ai/README.md` 참고 |
| 사내 NIM 배포 | `docs/on-prem-nim.md` | 문서만 | `NIM_BASE_URL`과 `RERANK_URL`을 바꾸는 절차를 적었을 뿐 GPU에 올려 본 적이 없다 |
| 모델 ID 확인 | `.env.example`의 `NIM_MODEL`, `EMBED_MODEL` | 실행 검증 | 두 모델을 `/v1/models`에서 확인했다 |
| Nemotron 한국어 판정 | 분류·분석·크리틱 서비스 | 실행 검증 | 예시 3건의 분류와 근거를 인용한 초안 1건을 확인했다. 크리틱은 합성 16건씩 두 모드로 실행했다 |

## 화면에 보이는 정직성 배지

모든 단계와 응답은 공급자 종류(`nvidia`, `fallback`, `replay`, `none`)를 달고 다닌다.

- NVIDIA(초록): NVIDIA 서비스가 실제로 돈 단계
- 대체 구현(주황): 키가 없거나 장애로 결정적 대체 경로가 돈 단계. NVIDIA라고 부르지 않는다
- 재현(회색): 녹화를 다시 재생한 단계. 법령 이름 앞에 `[재현 예시]`를 붙인다

키 없이 띄운 로컬 화면에는 NVIDIA 배지가 나오지 않는다. 초록 배지가 보이면 그 단계는 실제 호출이다.

국가법령정보센터 연동을 재개하면 실제 조문을 녹화해 평가를 다시 실행한다. 현재 검증의 입력과 결과는 [스파이크 노트](notes/2026-11-16-spike.md)에 기록했다.
