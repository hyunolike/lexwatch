import { describe, expect, it } from "vitest";
import { MAX_HISTORY_TURNS, MAX_QUESTION_CHARS, aiStateOf, isAiWaking, mapChatResult, waitForAi, maskNotice, maskedUserTurn, toHistory, type ChatMessage } from "./reviewChat";

const ev = [{ id: "law:L1:23", kind: "law", title: "근로기준법 제23조", sourceUrl: null }];

describe("mapChatResult", () => {
  it("maps an answer with only known citations to chips", () => {
    const m = mapChatResult({ waking: false, status: 200, data: { answer: "답", citations: ["law:L1:23", "fake:1"], refused: false, provider: { name: "n", kind: "nvidia" } } }, ev);
    expect(m).toMatchObject({ role: "assistant", refused: false, providerKind: "nvidia" });
    expect(m.citations).toEqual([{ id: "law:L1:23", title: "근로기준법 제23조" }]);
  });

  it("Cloudflare가 답하면 출처를 cloudflare로 남긴다", () => {
    const m = mapChatResult({ waking: false, status: 200, data: { answer: "답", citations: [], refused: false, provider: { name: "cloudflare-workers-ai", kind: "cloudflare" } } }, ev);
    expect(m.providerKind).toBe("cloudflare");
  });

  it("Groq가 답하면 출처를 groq로 남긴다", () => {
    const m = mapChatResult({ waking: false, status: 200, data: { answer: "답", citations: [], refused: false, provider: { name: "groq", kind: "groq" } } }, ev);
    expect(m.providerKind).toBe("groq");
  });
  it("shows refusals plainly and never labels fallback as nvidia", () => {
    const m = mapChatResult({ waking: false, status: 200, data: { answer: "이 개정안의 근거 안에서만 답할 수 있어요.", citations: [], refused: true, provider: { name: "no-key", kind: "fallback" } } }, ev);
    expect(m.refused).toBe(true);
    expect(m.providerKind).toBe("fallback");
  });
  it("maps 429 to a quota message", () => {
    const m = mapChatResult({ waking: false, status: 429, data: { code: "CHAT_QUOTA_EXCEEDED", message: "x" } }, ev);
    expect(m.text).toContain("오늘 사용 한도를 다 썼어요");
    expect(m.providerKind).toBeNull();
  });
  it("maps waking and errors", () => {
    expect(mapChatResult({ waking: true }, ev).text).toContain("깨우는 중");
    expect(mapChatResult({ waking: false, status: 424, data: { code: "AI_UNAVAILABLE", message: "x" } }, ev).text).toContain("지금 쓸 수 없어요");
  });
});

describe("toHistory", () => {
  it("sends at most 6 turns and skips errors", () => {
    const msgs: ChatMessage[] = Array.from({ length: 9 }, (_, i) => ({ role: i % 2 ? "assistant" : "user", text: `t${i}`, citations: [], refused: false, providerKind: null, error: false }));
    msgs.push({ role: "assistant", text: "err", citations: [], refused: false, providerKind: null, error: true });
    const h = toHistory(msgs);
    expect(h).toHaveLength(MAX_HISTORY_TURNS);
    expect(h.at(-1)?.content).toBe("t8");
  });
  it("limits question length constant", () => expect(MAX_QUESTION_CHARS).toBe(500));
});

describe("maskedUserTurn", () => {
  const mine: ChatMessage = { role: "user", text: "한빛전자 900101-1234567", citations: [], refused: false, providerKind: null, error: false };
  const ok = {
    waking: false as const,
    status: 200,
    data: {
      answer: "답", citations: [], refused: false, provider: { name: "n", kind: "nvidia" },
      maskedQuestion: "[고객사] [주민등록번호]",
      masked: [{ kind: "client", label: "고객사", count: 1 }, { kind: "rrn", label: "주민등록번호", count: 1 }],
    },
  };
  it("replaces_the_bubble_with_the_masked_question", () => {
    const m = maskedUserTurn(mine, ok);
    expect(m.text).toBe("[고객사] [주민등록번호]");
    expect(m.masked).toHaveLength(2);
    expect(toHistory([m])).toEqual([{ role: "user", content: "[고객사] [주민등록번호]" }]);
  });
  it("keeps_the_bubble_on_errors_and_old_servers", () => {
    expect(maskedUserTurn(mine, { waking: true })).toBe(mine);
    expect(maskedUserTurn(mine, { waking: false, status: 429, data: { code: "CHAT_QUOTA_EXCEEDED", message: "x" } })).toBe(mine);
    const old = { waking: false as const, status: 200, data: { answer: "답", citations: [], refused: false, provider: { name: "n", kind: "nvidia" } } };
    expect(maskedUserTurn(mine, old)).toBe(mine);
  });
});

describe("maskNotice", () => {
  it("lists_kinds_in_server_order", () => {
    expect(maskNotice([{ kind: "rrn", label: "주민등록번호", count: 1 }, { kind: "client", label: "고객사", count: 2 }])).toBe(
      "민감정보를 가리고 보냈어요: 주민등록번호 1건, 고객사 2건",
    );
  });
  it("is_null_when_nothing_was_masked", () => {
    expect(maskNotice([])).toBeNull();
    expect(maskNotice(undefined)).toBeNull();
  });
});

describe("isAiWaking", () => {
  it("AI 서비스가 깨는 중(424 AI_WAKING)과 API가 잠든 경우만 다시 보낼 대상이다", () => {
    expect(isAiWaking({ waking: true })).toBe(true);
    expect(isAiWaking({ waking: false, status: 424, data: { code: "AI_WAKING", message: "" } })).toBe(true);
    expect(isAiWaking({ waking: false, status: 424, data: { code: "AI_UNAVAILABLE", message: "" } })).toBe(false);
    expect(isAiWaking({ waking: false, status: 429, data: { code: "CHAT_QUOTA_EXCEEDED", message: "" } })).toBe(false);
  });
});

describe("aiStateOf", () => {
  it("200 up만 up이고 나머지는 waking", () => {
    expect(aiStateOf({ waking: false, status: 200, data: { state: "up" } })).toBe("up");
    expect(aiStateOf({ waking: false, status: 200, data: { state: "waking" } })).toBe("waking");
    expect(aiStateOf({ waking: true })).toBe("waking");
    expect(aiStateOf({ waking: false, status: 403, data: { code: "FORBIDDEN", message: "" } })).toBe("waking");
  });
});

describe("waitForAi", () => {
  const clock = () => {
    let t = 0;
    return { now: () => t, sleep: async (ms: number) => void (t += ms) };
  };

  it("깰 때까지 묻고 깨면 true", async () => {
    const states = ["waking", "waking", "up"] as const;
    let i = 0;
    const c = clock();
    expect(await waitForAi(async () => states[i++], { ...c, pollMs: 5000 })).toBe(true);
    expect(i).toBe(3);
  });

  it("마감을 넘기면 false", async () => {
    let n = 0;
    const c = clock();
    expect(await waitForAi(async () => (n++, "waking"), { ...c, deadlineMs: 20_000, pollMs: 5000 })).toBe(false);
    expect(n).toBe(5); // 0, 5, 10, 15, 20초
  });

  it("취소되면 더 묻지 않는다", async () => {
    const ac = new AbortController();
    let n = 0;
    const c = clock();
    const r = await waitForAi(async () => { n++; ac.abort(); return "waking"; }, { ...c, signal: ac.signal });
    expect([r, n]).toEqual([false, 1]);
  });
});
