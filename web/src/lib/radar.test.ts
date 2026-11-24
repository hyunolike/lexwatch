import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { billBadges, billsOfGroup, formatDay, stageStep } from "./radar";
import { statusLabel } from "./labels";

const RULES = path.resolve(__dirname, "../../../backend/src/LexWatch.Api/Radar/BillRules.cs");

describe("법안 단계", () => {
  it("백엔드 BillStages의 모든 값에 라벨이 있다", () => {
    const src = readFileSync(RULES, "utf8");
    const values = [...src.matchAll(/public const string \w+ = "([A-Z]\w+)";/g)].map((m) => m[1]);
    expect(values).toEqual(["Received", "Committee", "Judiciary", "Plenary", "Passed", "Merged", "Closed"]);
    for (const v of values) expect(statusLabel("billStage", v).known, v).toBe(true);
  });

  it("걸음 칸: 대안에 반영은 의결 칸, 종료와 모르는 값은 null", () => {
    expect(stageStep("Received")).toBe(0);
    expect(stageStep("Plenary")).toBe(3);
    expect(stageStep("Passed")).toBe(4);
    expect(stageStep("Merged")).toBe(4);
    expect(stageStep("Closed")).toBeNull();
    expect(stageStep("Weird")).toBeNull();
  });
});

describe("billBadges", () => {
  const now = Date.parse("2026-11-24T12:00:00+09:00");
  it("발의일이 7일 안이면 새 발의", () => {
    expect(billBadges({ proposedOn: "2026-11-18", lastEvent: null }, now)).toEqual(["new"]);
    expect(billBadges({ proposedOn: "2026-11-17", lastEvent: null }, now)).toEqual([]);
  });
  it("최근 사건이 7일 안의 단계 바뀜이면 단계 바뀜", () => {
    const changed = { kind: "stage_changed" as const, at: "2026-11-20T02:00:00Z", fromStage: "Received" as const, toStage: "Committee" as const };
    expect(billBadges({ proposedOn: "2026-10-01", lastEvent: changed }, now)).toEqual(["changed"]);
    expect(billBadges({ proposedOn: "2026-10-01", lastEvent: { ...changed, at: "2026-11-10T00:00:00Z" } }, now)).toEqual([]);
    expect(billBadges({ proposedOn: "2026-10-01", lastEvent: { ...changed, kind: "proposed" } }, now)).toEqual([]);
  });
});

describe("billsOfGroup", () => {
  const bills = [{ id: "a", groupCodes: ["labor"] }, { id: "b", groupCodes: ["privacy", "finance"] }];
  it("그룹 코드로 거르고 all이면 그대로", () => {
    expect(billsOfGroup(bills, "all")).toHaveLength(2);
    expect(billsOfGroup(bills, "finance").map((b) => b.id)).toEqual(["b"]);
    expect(billsOfGroup(bills, "tax")).toEqual([]);
  });
});

it("formatDay", () => {
  expect(formatDay("2026-11-03")).toBe("2026. 11. 3.");
  expect(formatDay(null)).toBe("");
});
