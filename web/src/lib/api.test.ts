import { afterEach, describe, expect, it, vi } from "vitest";
import { apiFetch } from "./api";

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("apiFetch", () => {
  it("apiFetch_maps_502_to_waking", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("bad gateway", { status: 502 })));
    expect(await apiFetch("/api/reviews")).toEqual({ waking: true });
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("", { status: 503 })));
    expect(await apiFetch("/api/reviews")).toEqual({ waking: true });
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    expect(await apiFetch("/api/reviews")).toEqual({ waking: true });
  });

  it("apiFetch_sends_role_header_from_cookie", async () => {
    const fetchMock = vi.fn().mockResolvedValue(Response.json([{ id: "1" }]));
    vi.stubGlobal("fetch", fetchMock);
    vi.stubGlobal("document", { cookie: "a=b; lw_role=admin; c=d" });
    const result = await apiFetch<{ id: string }[]>("/api/reviews");
    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/reviews$/);
    expect(new Headers(init.headers).get("X-Demo-Role")).toBe("admin");
    expect(result).toEqual({ waking: false, status: 200, data: [{ id: "1" }] });
  });
});

import { safeHttpUrl, submitDecision } from "./api";

describe("apiFetch robustness", () => {
  it("non_json_500_becomes_error_result", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("<html>oops</html>", { status: 500 })));
    const r = await apiFetch("/api/reviews");
    expect(r).toMatchObject({ waking: false, status: 500, data: { code: "NON_JSON_RESPONSE" } });
  });

  it("504_maps_to_waking", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("<html>timeout</html>", { status: 504 })));
    expect(await apiFetch("/api/reviews")).toEqual({ waking: true });
  });
});

describe("safeHttpUrl", () => {
  it("allows only http(s)", () => {
    expect(safeHttpUrl("https://www.law.go.kr/")).toBe("https://www.law.go.kr/");
    expect(safeHttpUrl("HTTP://a.b")).toBe("HTTP://a.b");
    expect(safeHttpUrl("javascript:alert(1)")).toBeNull();
    expect(safeHttpUrl(null)).toBeNull();
  });
});

describe("submitDecision", () => {
  it("approve_body_carries_editedSentences_when_edited", async () => {
    const fetchMock = vi.fn().mockResolvedValue(Response.json({ id: "1", status: "Approved" }));
    vi.stubGlobal("fetch", fetchMock);
    const edited = [{ sentence: "수정", citations: ["law:L1:제1조"] }];
    expect(await submitDecision("r1", "approve", 7, { editedSentences: edited })).toEqual({ kind: "ok" });
    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toMatch(/\/api\/reviews\/r1\/approve$/);
    expect(JSON.parse(init.body)).toEqual({ version: 7, editedSentences: edited });
  });

  it("plain_approve_has_no_editedSentences", async () => {
    const fetchMock = vi.fn().mockResolvedValue(Response.json({}));
    vi.stubGlobal("fetch", fetchMock);
    await submitDecision("r1", "approve", 7, {});
    expect(JSON.parse(fetchMock.mock.calls[0][1].body)).toEqual({ version: 7 });
  });

  it("409_maps_to_conflict", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json({ code: "REVIEW_ALREADY_DECIDED", message: "x" }, { status: 409 })));
    expect(await submitDecision("r1", "reject", 7, { comment: "사유" })).toEqual({ kind: "conflict" });
  });

  it("400_INVALID_CITATION_surfaces_backend_message", async () => {
    const message = "수정한 문장마다 근거 저장소에 있는 인용이 하나 이상 필요하다";
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(Response.json({ code: "INVALID_CITATION", message }, { status: 400 })));
    expect(await submitDecision("r1", "approve", 7, { editedSentences: [] })).toEqual({ kind: "error", message });
  });
});
