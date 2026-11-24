import type { ProviderKind, TrailActor, TrailStage, TrailStatus } from "./api";
import type { Tone } from "./labels";

/** ai-footprints. 단계 이름(설계 2절). */
export const STAGE_NAMES: Record<string, string> = {
  collect: "법령 수집",
  classify: "관련 그룹 분류",
  index: "조문 색인",
  analyze: "영향 분석 초안",
  critic_citation: "인용 검사",
  critic_source: "원문 대조",
  critic_judge: "과잉 해석 판정",
  decision: "변호사 검토",
};

export const ACTOR_LABEL: Record<TrailActor, string> = { ai: "AI", rule: "규칙", human: "사람", replay: "재현" };

const STATUS: Record<TrailStatus, [string, Tone]> = {
  done: ["완료", "success"],
  needs_review: ["확인 필요", "warning"],
  failed: ["실패", "danger"],
  running: ["진행 중", "info"],
  waiting: ["대기", "neutral"],
  skipped: ["건너뜀", "neutral"],
  not_run: ["실행 안 함", "neutral"],
};

const DECISIONS: Record<string, string> = { approve: "승인", reject: "반려", confirm: "파트너 최종 승인", return: "파트너가 돌려보냄" };
const ROLES: Record<string, string> = { lawyer: "변호사", partner: "파트너", admin: "관리자" };

/** 상태 라벨과 톤. 적발이 있으면 완료여도 경고 톤이다(AF-05). */
export function stageState(s: TrailStage): { label: string; tone: Tone } {
  const [label, tone] = STATUS[s.status] ?? [s.status, "neutral" as Tone];
  if (s.status === "done" && (s.detail.findings ?? 0) > 0) return { label: `적발 ${s.detail.findings}건`, tone: "warning" };
  return { label, tone };
}

function kst(iso: string | undefined): string {
  if (!iso) return "";
  return new Date(iso).toLocaleString("ko-KR", { timeZone: "Asia/Seoul", month: "numeric", day: "numeric", hour: "2-digit", minute: "2-digit" });
}

const RETRIEVAL: Record<string, string> = { hybrid: "하이브리드 검색", bm25: "키워드 검색", no_index: "색인 없이 전체", fallback_all: "검색 실패로 전체" };

/** 한 줄 요약. 값이 없으면 빈 문자열. */
export function stageSummary(s: TrailStage): string {
  const d = s.detail;
  const parts: string[] = [];
  switch (s.key) {
    case "collect":
      if (d.evidence !== undefined) parts.push(`근거 ${d.evidence}건`);
      if (d.oldNew === "ok") parts.push("신구조문 있음");
      else if (d.oldNew === "absent") parts.push("신구조문 없음");
      else if (d.oldNew === "error") parts.push("신구조문 받기 실패");
      break;
    case "classify":
      if (d.confidence !== undefined) parts.push(`관련성 ${d.confidence.toFixed(2)}`);
      break;
    case "index":
      if (d.chunks !== undefined) parts.push(d.fallback ? `조문 ${d.chunks}개 키워드 색인` : `조문 ${d.chunks}개 임베딩`);
      break;
    case "analyze":
      if (d.insufficient) parts.push("근거가 부족해 초안을 쓰지 않음");
      else {
        if (d.retrievalMode) {
          const mode = d.retrievalMode === "hybrid" && d.denseUsed === false ? "키워드 검색" : (RETRIEVAL[d.retrievalMode] ?? d.retrievalMode);
          parts.push(d.sent !== undefined ? `${mode} 근거 ${d.sent}건` : mode);
        }
        if (d.toolCalls !== undefined) parts.push(`도구 호출 ${d.toolCalls}회`);
      }
      break;
    case "decision":
      if (d.awaiting === "partner") parts.push("변호사 승인, 파트너 최종 승인 대기");
      else if (d.decision) parts.push(`${ROLES[d.role ?? ""] ?? d.role} ${DECISIONS[d.decision] ?? d.decision} ${kst(d.decidedAt)}`.trim());
      if (d.edited) parts.push("변호사가 문장 수정");
      break;
    default:
      if (d.findings !== undefined) parts.push(d.findings === 0 ? "적발 없음" : `적발 ${d.findings}건`);
      if (d.recheckedOnApprove) parts.push("수정본 다시 검사");
  }
  if (d.fallback && s.key !== "index") parts.push("AI 대신 규칙으로 처리");
  return parts.join(", ");
}

/** 띠 위의 한 줄 요약: "AI 4단계, 규칙 3단계, 사람 1단계". 실행 안 한 단계는 세지 않는다. */
export function actorCounts(stages: TrailStage[]): string {
  const ran = stages.filter((s) => s.status !== "not_run" && s.status !== "skipped");
  return (["ai", "rule", "human"] as TrailActor[])
    .map((a) => [a, ran.filter((s) => s.actor === a).length] as const)
    .filter(([, n]) => n > 0)
    .map(([a, n]) => `${ACTOR_LABEL[a]} ${n}단계`)
    .join(", ");
}

const PROVIDERS: Record<string, string> = {
  nvidia: "NVIDIA Nemotron",
  cloudflare: "Cloudflare Workers AI",
  groq: "Groq",
};

export type ExplainKind = "draft" | "judge" | "chat";

export interface Explain {
  title: string;
  what: string;
  model: string;
  basis: string | null;
  limit: string;
}

const LIMIT = "AI는 틀릴 수 있어요. 승인하기 전에 인용 근거를 직접 확인해 주세요.";

/** AI 라벨 팝오버 내용(AF-07). provider는 그 결과를 만든 단계나 답변의 공급자. */
export function aiExplain(
  kind: ExplainKind,
  opts: { providerKind: ProviderKind | string | null; providerName?: string | null; sent?: number; findings?: number; edited?: boolean },
): Explain {
  const replay = opts.providerKind === "replay";
  const model = replay
    ? "녹화해 둔 AI 응답을 다시 보여 주는 재현 예시예요"
    : opts.providerKind && PROVIDERS[opts.providerKind]
      ? `${PROVIDERS[opts.providerKind]}${opts.providerName && opts.providerName !== opts.providerKind ? ` (${opts.providerName})` : ""}`
      : "기록된 모델 정보가 없어요";
  if (kind === "draft")
    return {
      title: opts.edited ? "AI 초안, 변호사 수정" : "AI가 쓴 초안",
      what: opts.edited
        ? "AI 에이전트가 신구조문과 검색한 근거로 초안을 쓰고, 변호사가 문장을 고쳤어요."
        : "AI 에이전트가 신구조문과 검색한 근거로 문장마다 인용을 달아 초안을 썼어요.",
      model,
      basis: opts.sent !== undefined ? `AI에 보낸 근거 ${opts.sent}건` : null,
      limit: LIMIT,
    };
  if (kind === "judge")
    return {
      title: "AI 판정",
      what: "다른 AI가 초안이 근거보다 과하게 해석했는지 검사했어요. 인용과 숫자 검사는 규칙이 해요.",
      model,
      basis: opts.findings !== undefined ? `이 초안에서 AI가 찾은 적발 ${opts.findings}건` : null,
      limit: "판정도 틀릴 수 있어요. 적발된 문장은 근거와 함께 직접 판단해 주세요.",
    };
  return {
    title: "AI 답변",
    what: "검토 보조 AI가 이 초안의 근거 안에서만 답했어요. 질문은 보내기 전에 민감정보를 가렸어요.",
    model,
    basis: null,
    limit: "답변은 법률 자문이 아니에요. 인용된 근거를 직접 확인해 주세요.",
  };
}

/** AI 라벨을 붙일 대상인가. 대체 구현(규칙)과 모델 정보 없음은 AI가 아니다. */
export function isAiProvider(kind: string | null | undefined): boolean {
  return !!kind && kind !== "none" && kind !== "fallback";
}

export function findStage(stages: TrailStage[] | undefined, key: TrailStage["key"]): TrailStage | undefined {
  return stages?.find((s) => s.key === key);
}
