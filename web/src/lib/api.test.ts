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

import { formatDuration, retryRun } from "./api";

describe("retryRun", () => {
  const stub = (res: Response) => vi.stubGlobal("fetch", vi.fn().mockResolvedValue(res));
  it("maps_responses_to_ui_state", async () => {
    stub(Response.json({ id: "r", requeued: ["notify"] }));
    expect(await retryRun("r")).toEqual({ kind: "ok", requeued: ["notify"] });
    stub(Response.json({ code: "NOTHING_TO_RETRY", message: "다시 실행할 실패 단계가 없다" }, { status: 409 }));
    expect(await retryRun("r")).toEqual({ kind: "nothing", message: "다시 실행할 실패 단계가 없다" });
    stub(Response.json({ code: "FORBIDDEN", message: "x" }, { status: 403 }));
    expect(await retryRun("r")).toMatchObject({ kind: "forbidden" });
    stub(Response.json({ code: "NOT_FOUND", message: "없음" }, { status: 404 }));
    expect(await retryRun("r")).toEqual({ kind: "error", message: "없음" });
    stub(new Response("", { status: 503 }));
    expect(await retryRun("r")).toEqual({ kind: "waking" });
  });
  it("posts_to_retry_path", async () => {
    const f = vi.fn().mockResolvedValue(Response.json({ requeued: [] }));
    vi.stubGlobal("fetch", f);
    await retryRun("abc");
    expect(String(f.mock.calls[0][0])).toMatch(/\/api\/runs\/abc\/retry$/);
    expect(f.mock.calls[0][1].method).toBe("POST");
  });
});

describe("formatDuration", () => {
  it("formats", () => {
    expect(formatDuration(null)).toBe("—");
    expect(formatDuration(450)).toBe("450ms");
    expect(formatDuration(2500)).toBe("2.5초");
  });
});

import { hasRetryableStep, stepLabel, type TimelineItem } from "./api";

const st = (step: string, attempt: number, status: string, groupCode: string | null = "g1"): TimelineItem => ({
  kind: "step", at: "t", step, attempt, status, durationMs: null, errorCode: null, errorMessage: null,
  providerKind: "none", providerName: null, groupCode, summary: null,
});

describe("hasRetryableStep", () => {
  it("superseded_failure_has_no_button", () => {
    expect(hasRetryableStep([st("analyze", 1, "Failed"), st("analyze", 2, "Succeeded")], [{ groupCode: "g1", status: "WaitingApproval" }])).toBe(false);
  });
  it("requeued_row_blocks_button", () => {
    const g = [{ groupCode: "g1", status: "Analyzing" }];
    expect(hasRetryableStep([st("analyze", 3, "Failed"), st("analyze", 0, "Pending")], g)).toBe(false);
    expect(hasRetryableStep([st("analyze", 3, "Failed"), st("analyze", 0, "Succeeded")], g)).toBe(false);
    expect(hasRetryableStep([st("analyze", 3, "Failed"), st("classify", 0, "Pending")], g)).toBe(true);
  });
  it("decided_groups_have_no_button", () => {
    for (const status of ["Rejected", "Notified", "ClosedIrrelevant"])
      expect(hasRetryableStep([st("notify", 1, "Failed")], [{ groupCode: "g1", status }])).toBe(false);
    expect(hasRetryableStep([st("analyze", 1, "Failed")], [{ groupCode: "g1", status: "Approved" }])).toBe(false);
  });
  it("notify_on_approved_and_undecided_failed_have_button", () => {
    expect(hasRetryableStep([st("notify", 1, "Failed")], [{ groupCode: "g1", status: "Approved" }])).toBe(true);
    expect(hasRetryableStep([st("analyze", 1, "Failed")], [{ groupCode: "g1", status: "Analyzing" }])).toBe(true);
    expect(hasRetryableStep([st("fetch_articles", 1, "Failed", null)], [])).toBe(true);
  });
});

describe("stepLabel", () => {
  it("maps_known_and_falls_back", () => {
    expect(stepLabel("critic_judge")).toBe("크리틱 판정");
    expect(stepLabel("zzz")).toBe("zzz");
  });
});
