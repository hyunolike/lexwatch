# 기존 DOCKER_API_VERSION 환경변수는 그대로 통과한다 (구형 Docker 로컬 실행용).
.PHONY: test-backend
test-backend:
	cd backend && dotnet test LexWatch.sln --filter "Category!=eval"

.PHONY: eval-critic
eval-critic:
	cd ai && uv run python ../scripts/eval_critic.py

# 검색 평가(hybrid-rag RAG-06). 임베딩 캐시가 없으면 BM25만 재고 필요한 텍스트를 fixtures/retrieval/needed.jsonl에 남긴다.
.PHONY: eval-retrieval
eval-retrieval:
	cd backend && dotnet test LexWatch.sln --filter "Category=eval"

# needed.jsonl 가운데 캐시에 없는 것만 Cloudflare bge-m3로 계산한다(CLOUDFLARE_* 필요, 전체 약 100 Neurons).
.PHONY: eval-retrieval-embed
eval-retrieval-embed:
	cd ai && uv run python ../scripts/embed_cache.py
