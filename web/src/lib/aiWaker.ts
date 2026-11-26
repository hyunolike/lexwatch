import { apiFetch, type ApiError } from "./api";
import { AI_POLL_MS, AI_WAKE_WAIT_MS, aiStateOf, waitForAi, type AiState } from "./reviewChat";

/** 이 시간 안에 `up`을 확인했으면 다시 묻지 않는다(chat-wake CW-02). */
export const AI_FRESH_MS = 60_000;

type Awake = Exclude<AiState, "checking">;

export interface AiWaker {
  /** AI 서비스가 깰 때까지 기다린다. 진행 중인 깨우기가 있으면 그것을 돌려준다. force면 최근 `up`도 다시 묻는다. */
  warm(opts?: { force?: boolean }): Promise<boolean>;
  state(): AiState;
  subscribe(fn: (s: AiState) => void): () => void;
}

/** 앱 전체가 함께 쓰는 AI 깨우기(chat-wake CW-01, CW-02). 시간과 요청은 테스트가 주입한다. */
export function createAiWaker(
  check: () => Promise<Awake>,
  opts: {
    deadlineMs?: number;
    pollMs?: number;
    freshMs?: number;
    now?: () => number;
    sleep?: (ms: number) => Promise<void>;
    /** 잠든 AI를 실제로 깨우는 요청. 상태를 물을 때마다 부르되 앞의 요청이 끝나기 전에는 겹쳐 부르지 않는다(CW-05). */
    poke?: () => Promise<unknown>;
  } = {},
): AiWaker {
  const { deadlineMs = AI_WAKE_WAIT_MS, pollMs = AI_POLL_MS, freshMs = AI_FRESH_MS, now = Date.now, sleep } = opts;
  let current: AiState = "checking";
  let upAt = -Infinity;
  let running: Promise<boolean> | null = null;
  let poking: Promise<unknown> | null = null;
  const subs = new Set<(s: AiState) => void>();

  const set = (s: AiState) => {
    if (s === "up") upAt = now();
    if (s === current) return;
    current = s;
    subs.forEach((f) => f(s));
  };

  const once = async (): Promise<Awake> => {
    if (opts.poke && !poking) {
      poking = opts.poke().catch(() => undefined).finally(() => {
        poking = null;
      });
    }
    let s: Awake;
    try {
      s = await check();
    } catch {
      s = "waking";
    }
    set(s);
    return s;
  };

  return {
    warm(o) {
      if (o?.force) set("waking");
      else if (current === "up" && now() - upAt < freshMs) return Promise.resolve(true);
      if (!running) {
        running = waitForAi(once, { deadlineMs, pollMs, now, sleep }).finally(() => {
          running = null;
        });
      }
      return running;
    },
    state: () => current,
    subscribe(fn) {
      subs.add(fn);
      return () => {
        subs.delete(fn);
      };
    },
  };
}

const WAKE_URL = process.env.NEXT_PUBLIC_AI_WAKE_URL;

/**
 * 브라우저에서 하나만 쓴다. 상태는 API(`/api/ai/status`)로 묻고, 깨우기는 AI 공개 주소로 직접 한다.
 * API가 AI를 부르는 요청은 Render가 바로 502로 돌려줄 뿐 잠든 AI를 깨우지 않는다(2026-11-26 확인, CW-05).
 * 응답은 읽지 않으므로 no-cors로 보낸다. Render는 깰 때까지 이 요청을 붙잡는다.
 */
export const aiWaker = createAiWaker(async () => aiStateOf(await apiFetch<{ state?: string } | ApiError>("/api/ai/status")), {
  poke: WAKE_URL ? () => fetch(WAKE_URL, { mode: "no-cors", cache: "no-store" }) : undefined,
});
