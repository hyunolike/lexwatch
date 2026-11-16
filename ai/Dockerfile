# 로컬 compose용. 실제 배포는 Vercel(ai/vercel.json)이다.
FROM python:3.12-slim
COPY --from=ghcr.io/astral-sh/uv:0.6.10 /uv /usr/local/bin/uv
WORKDIR /app
COPY ai/pyproject.toml ai/uv.lock ./
# dev와 선택 그룹(guardrails)은 설치하지 않는다 — Vercel 번들과 같은 의존성이다.
RUN uv sync --frozen --no-dev --no-install-project
COPY ai/app ./app
ENV PATH="/app/.venv/bin:$PATH" PYTHONUNBUFFERED=1
RUN useradd --system --uid 10001 app
USER app
EXPOSE 8000
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--no-access-log"]
