"""무료 모델 한도 소진 시 자동 교체(docs/features/llm-failover/spec.md).

요청마다 새로 만든다(get_llm). 앞 공급자가 한도 초과, 서버 오류, 연결 실패, 시간 초과로 실패하면 같은 요청을
다음 공급자로 보낸다. 출력 형식 오류(OutputInvalid)는 모델 답의 문제라 넘기지 않는다.
한도 초과를 받은 공급자는 이 요청의 나머지 호출에서 건너뛰고, 같은 서버 인스턴스에서도 잠시 건너뛴다.
"""

from __future__ import annotations

import logging
import time
from typing import Any

from app.providers.llm.base import LLMError, LLMProvider, LLMResponse, RateLimited

log = logging.getLogger("lexwatch.failover")

MIN_COOLDOWN_S = 300.0
_FAILOVER_CODES = {"LLM_RATE_LIMITED", "LLM_TIMEOUT", "LLM_UNREACHABLE", "LLM_UPSTREAM"}
_now = time.monotonic  # 테스트에서 바꾼다
_cooldown_until: dict[str, float] = {}


def reset_cooldowns() -> None:
    _cooldown_until.clear()


class FailoverLLM(LLMProvider):
    def __init__(self, providers: list[LLMProvider]) -> None:
        if not providers:
            raise ValueError("at least one provider")
        self.providers = providers
        self.info = providers[0].info
        self._skip: set[str] = set()

    @property
    def reasoning_effort(self) -> str | None:
        return getattr(self.providers[0], "reasoning_effort", None)

    @reasoning_effort.setter
    def reasoning_effort(self, value: str | None) -> None:
        for p in self.providers:
            if hasattr(p, "reasoning_effort"):
                p.reasoning_effort = value

    def _available(self) -> list[LLMProvider]:
        now = _now()
        ready = [p for p in self.providers if p.info.name not in self._skip and _cooldown_until.get(p.info.name, 0) <= now]
        return ready or [self.providers[-1]]   # 모두 쉬는 중이면 마지막 공급자라도 시도한다

    async def chat(
        self,
        messages: list[dict[str, Any]],
        tools: list[dict[str, Any]] | None = None,
        response_format: dict[str, Any] | None = None,
        deadline: float | None = None,
    ) -> LLMResponse:
        last: LLMError | None = None
        for p in self._available():
            if last is not None and deadline is not None and deadline - time.monotonic() <= 0:
                break   # 시간 예산은 하나다(FO-04)
            try:
                resp = await p.chat(messages, tools=tools, response_format=response_format, deadline=deadline)
            except LLMError as e:
                if e.code not in _FAILOVER_CODES:
                    raise
                last = e
                if isinstance(e, RateLimited):
                    self._skip.add(p.info.name)
                    _cooldown_until[p.info.name] = _now() + max(MIN_COOLDOWN_S, e.retry_after_s or 0)
                log.warning("failover: %s failed with %s, trying next provider", p.info.name, e.code)
                continue
            self.info = resp.provider
            return resp
        assert last is not None
        raise last
