# 사내 NIM 배포 경로

> **실행 검증하지 않음.** 이 문서는 절차를 글로 적은 것이다. NIM 컨테이너를 GPU에 올려 본 적도, LexWatch를 사내 엔드포인트에 연결해 본 적도 없다.
> 아래 컨테이너 실행 부분은 NVIDIA 공식 문서에서 확인한 뒤 따라야 하고, 이 저장소가 보증하는 것은 환경변수 이름과 코드가 그 값을 읽는 위치까지다.

## 왜 이 경로가 있나

법인 문서가 외부 LLM 사업자로 나가면 안 된다는 요구가 흔하다. NIM은 호스팅 API와 사내 배포가 같은 OpenAI 호환 인터페이스를 쓰므로, 데모에서는 호스팅 API를 쓰고 운영에서는 주소만 사내 서버로 바꾸는 구조를 택했다.

## 바꾸는 값

| 환경변수 | 기본값 | 사내 배포 시 |
|---|---|---|
| `NIM_BASE_URL` | `https://integrate.api.nvidia.com/v1` | 사내 NIM의 `/v1` 주소. 채팅(`{NIM_BASE_URL}/chat/completions`)과 임베딩(`{NIM_BASE_URL}/embeddings`)이 모두 이 값을 쓴다 |
| `NIM_MODEL` | `nvidia/nemotron-3-super-120b-a12b` | 사내에 올린 모델의 이름 |
| `EMBED_MODEL` | `nvidia/nemotron-3-embed-1b` | 사내에 올린 임베딩 모델 이름 |
| `RERANK_URL` | NVIDIA 호스팅 리랭커 주소 | 리랭커 주소는 `NIM_BASE_URL`을 따르지 않고 별도 변수다(`.env.example`에는 없고 `ai/app/rag/nvidia.py`의 기본값만 있다). 사내 리랭커 NIM의 reranking 엔드포인트로 바꾼다 |
| `RERANK_MODEL` | `nvidia/llama-nemotron-rerank-vl-1b-v2` | 사내 리랭커 모델 이름 |
| `NVIDIA_API_KEY` | (비어 있음) | 사내 NIM이 인증을 요구하지 않아도 이 값이 비면 코드가 키 없음으로 보고 대체 구현으로 내려간다. 사내 서버가 받는 아무 비어 있지 않은 값을 넣는다 |

읽는 위치: `ai/app/providers/llm/factory.py`, `ai/app/rag/nvidia.py`, `ai/app/rag/factory.py`. 컨테이너와 배포 설정은 `docker-compose.yml`, `docs/deploy.md`의 환경변수 표를 본다.

## 절차

1. 법인 GPU 서버에 Nemotron 채팅 NIM, 임베딩 NIM, 리랭커 NIM을 NVIDIA 공식 문서대로 올린다. 필요한 GPU 사양과 라이선스는 NVIDIA 문서를 확인한다.
2. 서버에서 각 엔드포인트가 응답하는지 확인한다. `GET {NIM_BASE_URL}/models`로 모델 이름을 읽고 그 이름을 `NIM_MODEL`에 쓴다.
3. AI 서비스의 환경변수에 위 표의 값을 넣고 재시작한다.
4. AI 서비스에서 법인 내부망으로 나갈 수 있어야 한다. 현재 데모는 Vercel 서버리스에서 도는데 이것은 사내망에 닿지 않는다. 사내 배포에서는 `ai/Dockerfile`로 사내에서 컨테이너를 띄운다.
5. `/classify`나 `/analyze`를 호출해 응답의 공급자 종류가 `nvidia`로 나오는지 본다. 화면에 보라색 NVIDIA 라벨이 붙는다. 단, 배지는 "NVIDIA 호환 서비스가 응답했다"는 뜻이지 호스팅인지 사내인지는 구분하지 않는다.
6. 한국어 법률 문장 품질과 지연을 `make eval-critic`과 `scripts/try_nemotron_ko.py`로 다시 측정한다. 사내에 올린 모델이 호스팅 모델과 같은 품질이라는 보장은 없다.

## NeMo Guardrails를 사내에서 켜려면

검토 보조 대화의 입력 규칙은 기본적으로 결정적 규칙이 맡는다(`ai/README.md`). NeMo Guardrails는 선택 의존 그룹 `guardrails`로만 있고 서비스 코드가 가져다 쓰지 않는다.
사내에서 이것을 쓰려면 다음이 필요하고, **어느 것도 실행해 본 적이 없다.**

```bash
cd ai
uv sync --group guardrails      # nemoguardrails>=0.24 설치. fastembed, onnxruntime 등으로 약 150MB 늘어난다
```

- 설정 파일은 `ai/app/guardrails/config.yml`과 `rails.co`다. 모델 엔진은 `nvidia_ai_endpoints`로 되어 있어 사내 NIM 주소로 바꿔야 한다.
- 서비스 코드(`ai/app/guardrails/__init__.py`)를 NeMo 엔진을 부르도록 바꾸는 작업이 필요하다. 지금은 이 연결이 없다.
- NeMo의 의도 매칭은 임베딩 모델 내려받기나 LLM 호출이 필요해서, 키 없는 오프라인 테스트 요건과 맞지 않는다. 사내 배포에서만 켜는 선택지로 남겨 둔다.

## 하지 않은 것

- GPU 위 NIM 실행, 사내 연결, 성능과 비용 측정
- 사내 인증, 감사 로그, 망 분리 설계
- OpenShell 등으로 에이전트의 아웃바운드를 제한하는 구성
