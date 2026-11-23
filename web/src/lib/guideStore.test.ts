import { describe, expect, it, vi } from "vitest";
import { GUIDE_KEY, INITIAL_GUIDE } from "./guide";
import { createGuideStore } from "./guideStore";

function memory(initial?: string) {
  const data = new Map<string, string>(initial ? [[GUIDE_KEY, initial]] : []);
  return {
    getItem: (k: string) => data.get(k) ?? null,
    setItem: vi.fn((k: string, v: string) => void data.set(k, v)),
    removeItem: (k: string) => void data.delete(k),
    clear: () => data.clear(),
    key: () => null,
    length: 0,
    data,
  };
}

const throwing = () => {
  throw new Error("SecurityError");
};

describe("createGuideStore", () => {
  it("저장된 값을 읽고, 바뀌기 전까지 같은 스냅샷", () => {
    const s = createGuideStore(() => memory('{"v":1,"dismissed":true,"done":["approve"]}'));
    const a = s.getSnapshot();
    expect(a.state).toEqual({ v: 1, dismissed: true, done: ["approve"] });
    expect(s.getSnapshot()).toBe(a);
  });

  it("깨진 값과 저장소 없음은 처음 상태", () => {
    expect(createGuideStore(() => memory("{")).getSnapshot().state).toEqual(INITIAL_GUIDE);
    expect(createGuideStore(() => undefined).getSnapshot().state).toEqual(INITIAL_GUIDE);
  });

  it("읽기가 예외를 내도 처음 상태", () => {
    const st = { ...memory(), getItem: throwing };
    expect(createGuideStore(() => st).getSnapshot().state).toEqual(INITIAL_GUIDE);
    expect(createGuideStore(throwing).getSnapshot().state).toEqual(INITIAL_GUIDE);
  });

  it("쓰기가 예외를 내도 닫기는 메모리에 남는다", () => {
    const st = { ...memory(), setItem: throwing };
    const s = createGuideStore(() => st);
    const cb = vi.fn();
    s.subscribe(cb);
    s.dismiss();
    expect(s.getSnapshot().state.dismissed).toBe(true);
    expect(cb).toHaveBeenCalledTimes(1);
  });

  it("닫기와 완료를 저장한다", () => {
    const st = memory();
    const s = createGuideStore(() => st);
    s.markStep("open-review");
    s.dismiss();
    expect(JSON.parse(st.data.get(GUIDE_KEY)!)).toEqual({ v: 1, dismissed: true, done: ["open-review"] });
  });

  it("이미 완료한 단계는 저장도 알림도 하지 않는다", () => {
    const st = memory();
    const s = createGuideStore(() => st);
    s.markStep("approve");
    const cb = vi.fn();
    s.subscribe(cb);
    const before = s.getSnapshot();
    s.markStep("approve");
    expect(cb).not.toHaveBeenCalled();
    expect(st.setItem).toHaveBeenCalledTimes(1);
    expect(s.getSnapshot()).toBe(before);
  });

  it("다시 열기는 포커스를 요청하고, consumeFocus가 저장 없이 해제한다", () => {
    const st = memory('{"v":1,"dismissed":true,"done":["open-review"]}');
    const s = createGuideStore(() => st);
    s.reopen();
    expect(s.getSnapshot()).toMatchObject({ state: { dismissed: false, done: ["open-review"] }, focusRequested: true });
    const writes = st.setItem.mock.calls.length;
    s.consumeFocus();
    expect(s.getSnapshot().focusRequested).toBe(false);
    expect(st.setItem.mock.calls.length).toBe(writes);
  });

  it("구독을 해제하면 알림이 오지 않는다", () => {
    const s = createGuideStore(() => memory());
    const cb = vi.fn();
    const off = s.subscribe(cb);
    off();
    s.dismiss();
    expect(cb).not.toHaveBeenCalled();
  });
});
