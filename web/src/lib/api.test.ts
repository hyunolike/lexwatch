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
