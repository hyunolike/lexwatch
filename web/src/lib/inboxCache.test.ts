import { describe, expect, it, vi } from "vitest";
import type { ReviewSummary } from "./api";
import { inboxCache } from "./inboxCache";

describe("inboxCache", () => {
  it("비어 있다가 채우면 구독자에게 알린다", () => {
    expect(inboxCache.get()).toEqual([]);
    const l = vi.fn();
    const off = inboxCache.subscribe(l);
    inboxCache.set([{ id: "1" } as ReviewSummary]);
    expect(l).toHaveBeenCalledOnce();
    expect(inboxCache.get()).toHaveLength(1);
    off();
    inboxCache.set([]);
    expect(l).toHaveBeenCalledOnce();
  });
});
