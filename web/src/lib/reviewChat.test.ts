import { describe, expect, it } from "vitest";
import { MAX_HISTORY_TURNS, MAX_QUESTION_CHARS, mapChatResult, toHistory, type ChatMessage } from "./reviewChat";

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
