# LexWatch AI service

FastAPI, stateless. Endpoints: `/embed /classify /analyze /critique /review-chat`.

## Review chat guardrails (`/review-chat`)

| | Active engine |
|---|---|
| Deployed bundle (Vercel Python) and tests | **Deterministic input rails** in `app/guardrails/__init__.py` (`ENGINE = "deterministic-input-rails"`): prompt-injection, legal-advice requests, out-of-scope (no legal keyword and no overlap with the draft's evidence). No network, no keys. |
| NVIDIA NeMo Guardrails | **Not active anywhere.** `app/guardrails/config.yml` + `rails.co` are its declarative twin; `nemoguardrails` sits in the optional `guardrails` dependency group only (`uv sync --group guardrails`) and no service code imports it. |

Why not NeMo in the bundle: `nemoguardrails` pulls `fastembed`/`onnxruntime`/`numpy`/`pillow`/`tokenizers`
(measured on macOS arm64: site-packages 27 MB -> 179 MB, +152 MB; onnxruntime alone 80 MB; Linux wheels are larger),
and its intent matching needs an embedding model download or an LLM call at runtime, which the keyless
and offline-test requirements rule out. Keyless: with no `NVIDIA_API_KEY` the endpoint returns `refused=true`
with provider `fallback` and a message saying the assistant needs the NVIDIA key. Refusals log the rail name only,
never the question.

Answers may cite only evidence IDs sent in the request; others are stripped, and an answer left with no valid citation is refused.
