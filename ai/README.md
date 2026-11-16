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

## Chat quota and client IP (backend `POST /api/reviews/{id}/chat`)

- Limits: 10 requests per IP hash per day, 200 per day overall (sentinel row `__global__`). The day is **UTC** (it resets at 09:00 KST).
- `IpHash = SHA256(ip + day + JOB_TOKEN)`. If `JOB_TOKEN` is empty the endpoint fails closed with 503 `CHAT_DISABLED`.
- Client IP is the connection's remote IP. With `TRUST_FORWARDED_FOR=true` the **last** `X-Forwarded-For` entry is used instead.
  That is only safe when the origin is reachable solely through one proxy that appends the real client address; if the origin is
  directly reachable or several proxies sit in front, leave it off (or all users behind the proxy share one bucket).
- A request that does not end with an answer from the AI service (upstream failure, cancellation, exception) is refunded; guardrail and keyless refusals are answers and stay counted.
- Chat history: only user turns reach the model, inside one labelled "prior UI transcript" user block; client-supplied assistant turns are dropped.
