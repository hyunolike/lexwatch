import { describe, expect, it } from "vitest";
import { createAiWaker } from "./aiWaker";

/** 가짜 시계: sleep이 시간을 앞으로 민다. */
function clock() {
  let t = 0;
  return { now: () => t, sleep: async (ms: number) => void (t += ms), advance: (ms: number) => void (t += ms) };
}

type S = "up" | "waking";

describe("createAiWaker", () => {
  it("동시에 부르면 깨우기 하나를 함께 쓴다", async () => {
    const c = clock();
    const answers: S[] = ["waking", "waking", "up"];
    let calls = 0;
    const w = createAiWaker(async () => answers[calls++], c);
    const [a, b] = await Promise.all([w.warm(), w.warm()]);
    expect([a, b]).toEqual([true, true]);
    expect(calls).toBe(3);
    expect(w.state()).toBe("up");
  });

  it("1분 안에 up을 확인했으면 다시 묻지 않고, 지나면 다시 묻는다", async () => {
    const c = clock();
    let calls = 0;
    const w = createAiWaker(async () => (calls++, "up"), c);
    await w.warm();
    c.advance(59_000);
    expect(await w.warm()).toBe(true);
    expect(calls).toBe(1);
    c.advance(2_000);
    await w.warm();
    expect(calls).toBe(2);
  });

  it("force면 상태를 바로 waking으로 알리고 다시 깨기를 기다린다", async () => {
    const c = clock();
    const answers: S[] = ["up", "waking", "up"];
    let calls = 0;
    const w = createAiWaker(async () => answers[calls++], c);
    await w.warm();
    const seen: string[] = [];
    w.subscribe((s) => seen.push(s));
    expect(await w.warm({ force: true })).toBe(true);
    expect(seen).toEqual(["waking", "up"]);
    expect(calls).toBe(3);
  });

  it("마감 안에 깨지 않으면 false, 다음 부름은 새로 시작한다", async () => {
    const c = clock();
    let calls = 0;
    const w = createAiWaker(async () => (calls++, "waking"), { ...c, deadlineMs: 20_000, pollMs: 5_000 });
    expect(await w.warm()).toBe(false);
    expect(w.state()).toBe("waking");
    const before = calls;
    await w.warm();
    expect(calls).toBeGreaterThan(before);
  });

  it("상태 요청이 던지면 waking으로 본다", async () => {
    const c = clock();
    let calls = 0;
    const w = createAiWaker(async () => {
      if (calls++ === 0) throw new Error("network");
      return "up";
    }, c);
    expect(await w.warm()).toBe(true);
    expect(calls).toBe(2);
  });

  it("구독을 끊으면 더 알리지 않는다", async () => {
    const c = clock();
    const w = createAiWaker(async () => "up", c);
    const seen: string[] = [];
    const off = w.subscribe((s) => seen.push(s));
    off();
    await w.warm();
    expect(seen).toEqual([]);
  });
});

describe("createAiWaker poke", () => {
  it("상태를 물을 때 깨우기 요청을 보내되 끝나기 전에는 겹쳐 보내지 않는다", async () => {
    const c = clock();
    const answers: S[] = ["waking", "waking", "waking", "up"];
    let calls = 0;
    let pokes = 0;
    let release!: () => void;
    const pending = new Promise<void>((r) => (release = r));
    const w = createAiWaker(async () => answers[calls++] ?? "up", {
      ...c,
      poke: () => {
        pokes++;
        return pending;
      },
    });
    const done = w.warm();
    await done;
    expect(pokes).toBe(1);
    release();
    await pending;
    await Promise.resolve();
    await w.warm({ force: true });
    expect(pokes).toBe(2);
  });

  it("깨우기 요청이 실패해도 상태 확인은 계속한다", async () => {
    const c = clock();
    const w = createAiWaker(async () => "up", { ...c, poke: () => Promise.reject(new Error("blocked")) });
    expect(await w.warm()).toBe(true);
  });
});
