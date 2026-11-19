import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { groupLabel, REPLAY_NOTE, statusLabel, stepLabel, type Domain } from "./labels";

const ENUMS = path.resolve(__dirname, "../../../backend/src/LexWatch.Api/Data/Entities/Enums.cs");

/** Enums.cs에서 `public enum Name { A, B }` 멤버를 뽑는다. */
function enumMembers(name: string): string[] {
  const src = readFileSync(ENUMS, "utf8");
  const m = src.match(new RegExp(`enum\\s+${name}\\s*\\{([^}]*)\\}`));
  if (!m) throw new Error(`${name} 열거형을 찾지 못함`);
  return m[1].split(",").map((s) => s.trim()).filter(Boolean);
}

describe("statusLabel: 백엔드 열거형과의 계약", () => {
  const cases: [string, Domain][] = [
    ["RunStatus", "run"],
    ["ReviewStatus", "review"],
    ["StepStatus", "step"],
  ];
  for (const [enumName, domain] of cases) {
    it(`${enumName}의 모든 멤버에 한국어 라벨이 있다`, () => {
      const members = enumMembers(enumName);
      expect(members.length).toBeGreaterThan(0);
      for (const v of members) {
        const r = statusLabel(domain, v);
        expect(r.known, `${enumName}.${v}`).toBe(true);
        expect(r.label).not.toBe(v);
      }
    });
  }
});

describe("statusLabel", () => {
  it("같은 값도 영역에 따라 라벨이 다르다", () => {
    expect(statusLabel("run", "Running").label).toBe("진행 중");
    expect(statusLabel("review", "NeedsReview")).toMatchObject({ label: "검토 필요", tone: "warning" });
    expect(statusLabel("notification", "pending").label).toBe("발송 대기");
    expect(statusLabel("step", "Pending").label).toBe("대기");
  });

  it("결정, 역할, 채널", () => {
    expect(statusLabel("decision", "approve")).toMatchObject({ label: "승인", tone: "success" });
    expect(statusLabel("decision", "reject")).toMatchObject({ label: "반려", tone: "danger" });
    expect(statusLabel("role", "lawyer").label).toBe("변호사");
    expect(statusLabel("channel", "demo_inbox").label).toBe("그룹 알림함");
  });

  it("의미 색", () => {
    expect(statusLabel("step", "Failed").tone).toBe("danger");
    expect(statusLabel("step", "Succeeded").tone).toBe("success");
    expect(statusLabel("run", "ClosedIrrelevant").tone).toBe("neutral");
  });

  it("모르는 값은 원문을 neutral로 보여 주고 숨기지 않는다", () => {
    expect(statusLabel("run", "Paused")).toEqual({ label: "Paused", tone: "neutral", known: false });
  });
});

describe("groupLabel, stepLabel", () => {
  it("실무그룹 코드", () => {
    expect(groupLabel("labor")).toBe("노동");
    expect(groupLabel("privacy")).toBe("개인정보");
    expect(groupLabel("finance")).toBe("금융");
    expect(groupLabel("tax")).toBe("tax");
  });

  it("단계 이름", () => {
    expect(stepLabel("critic_judge")).toBe("크리틱 판정");
    expect(stepLabel("zzz")).toBe("zzz");
  });

  it("재현 안내 문구", () => {
    expect(REPLAY_NOTE).toContain("재현 예시");
  });
});
