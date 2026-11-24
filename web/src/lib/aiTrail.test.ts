import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import type { TrailStage } from "./api";
import { STAGE_NAMES, actorCounts, aiExplain, isAiProvider, stageState, stageSummary } from "./aiTrail";

const SRC = path.resolve(__dirname, "../../../backend/src/LexWatch.Api/Reviews/AiTrail.cs");

const st = (p: Partial<TrailStage> & Pick<TrailStage, "key">): TrailStage => ({
  actor: "ai", status: "done", providerKind: null, providerName: null, detail: {}, ...p,
});

describe("백엔드와의 계약", () => {
  it("백엔드 단계 키마다 화면 이름이 있다", () => {
    const keys = [...readFileSync(SRC, "utf8").matchAll(/new\("(\w+)", "\w+", (?:true|false)/g)].map((m) => m[1]);
    expect(keys).toEqual(["collect", "classify", "index", "analyze", "critic_citation", "critic_source", "critic_judge"]);
    for (const k of [...keys, "decision"]) expect(STAGE_NAMES[k], k).toBeTruthy();
  });
});

describe("stageSummary", () => {
  it("운영 실측 값으로 요약한다", () => {
    expect(stageSummary(st({ key: "collect", actor: "rule", detail: { evidence: 54, oldNew: "ok" } }))).toBe("근거 54건, 신구조문 있음");
    expect(stageSummary(st({ key: "classify", detail: { confidence: 0.92 } }))).toBe("관련성 0.92");
    expect(stageSummary(st({ key: "analyze", detail: { retrievalMode: "hybrid", sent: 8, denseUsed: true, toolCalls: 2 } }))).toBe(
      "하이브리드 검색 근거 8건, 도구 호출 2회",
    );
  });
  it("하이브리드인데 벡터를 못 썼으면 키워드 검색, 근거 부족이면 그 사실만", () => {
    expect(stageSummary(st({ key: "analyze", detail: { retrievalMode: "hybrid", sent: 8, denseUsed: false } }))).toBe("키워드 검색 근거 8건");
    expect(stageSummary(st({ key: "analyze", status: "needs_review", detail: { insufficient: true, toolCalls: 0 } }))).toBe(
      "근거가 부족해 초안을 쓰지 않음",
    );
  });
  it("대체 경로와 수정 재검사", () => {
    expect(stageSummary(st({ key: "index", actor: "rule", detail: { chunks: 10, fallback: true } }))).toBe("조문 10개 키워드 색인");
    expect(stageSummary(st({ key: "analyze", actor: "rule", detail: { toolCalls: 0, fallback: true } }))).toBe(
      "도구 호출 0회, AI 대신 규칙으로 처리",
    );
    expect(stageSummary(st({ key: "critic_citation", actor: "rule", detail: { findings: 1, recheckedOnApprove: true } }))).toBe(
      "적발 1건, 수정본 다시 검사",
    );
  });
  it("사람 단계", () => {
    expect(stageSummary(st({ key: "decision", actor: "human", status: "running", detail: { awaiting: "partner", edited: true } }))).toBe(
      "변호사 승인, 파트너 최종 승인 대기, 변호사가 문장 수정",
    );
    expect(
      stageSummary(st({ key: "decision", actor: "human", detail: { decision: "return", role: "partner", decidedAt: "2026-11-24T05:02:00Z" } })),
    ).toMatch(/^파트너 파트너가 돌려보냄 11\. 24\./);
  });
});

describe("stageState와 actorCounts", () => {
  it("적발이 있으면 완료여도 경고", () => {
    expect(stageState(st({ key: "critic_judge", detail: { findings: 2 } }))).toEqual({ label: "적발 2건", tone: "warning" });
    expect(stageState(st({ key: "critic_judge", detail: { findings: 0 } })).tone).toBe("success");
    expect(stageState(st({ key: "analyze", status: "not_run" })).label).toBe("실행 안 함");
  });
  it("실행 안 한 단계는 세지 않는다", () => {
    const stages = [
      st({ key: "collect", actor: "rule" }),
      st({ key: "classify" }),
      st({ key: "index", status: "skipped" }),
      st({ key: "critic_judge", status: "not_run" }),
      st({ key: "decision", actor: "human", status: "waiting" }),
    ];
    expect(actorCounts(stages)).toBe("AI 1단계, 규칙 1단계, 사람 1단계");
  });
});

describe("aiExplain", () => {
  it("모델, 근거, 한계를 채우고 재현이면 재현이라고 한다", () => {
    const e = aiExplain("draft", { providerKind: "cloudflare", providerName: "cloudflare-workers-ai", sent: 8 });
    expect(e.title).toBe("AI가 쓴 초안");
    expect(e.model).toBe("Cloudflare Workers AI (cloudflare-workers-ai)");
    expect(e.basis).toBe("AI에 보낸 근거 8건");
    expect(e.limit).toContain("인용 근거를 직접 확인");
    expect(aiExplain("draft", { providerKind: "replay", edited: true })).toMatchObject({ title: "AI 초안, 변호사 수정", model: expect.stringContaining("재현") });
    expect(aiExplain("judge", { providerKind: "nvidia", findings: 2 }).basis).toBe("이 초안에서 AI가 찾은 적발 2건");
  });
  it("대체 구현과 정보 없음은 AI 라벨 대상이 아니다", () => {
    expect(isAiProvider("fallback")).toBe(false);
    expect(isAiProvider("none")).toBe(false);
    expect(isAiProvider(null)).toBe(false);
    expect(isAiProvider("replay")).toBe(true);
    expect(isAiProvider("nvidia")).toBe(true);
  });
});
