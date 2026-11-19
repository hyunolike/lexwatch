export type Role = "lawyer" | "admin";
export type ProviderKind = "nvidia" | "fallback" | "replay" | "none";

export interface ReviewSummary {
  id: string;
  runId: string;
  groupCode: string;
  groupName: string;
  lawName: string;
  promulgationDate: string;
  status: string;
  confidence: number | null;
  findingCount: number;
  evidenceCount: number;
  providerKinds: ProviderKind[];
  isReplay: boolean;
}

export interface Finding {
  tier: number;
  ruleId: string;
  message: string;
}

export interface DraftSentence {
  sentence: string;
  citations: string[];
  findings: Finding[];
}

export interface Draft {
  version: number;
  createdBy: string;
  sentences: DraftSentence[];
  openQuestions: string[];
}

/** 변경 구간 [시작, 끝). 법제처 공식 표시를 지운 글 기준. */
export type MarkRange = [number, number];

export interface OldNew {
  articleNo: string;
  before: string;
  after: string;
  /** 해당 칸이 "(없음)"이었는지(신설, 삭제 조문) */
  beforeMissing: boolean;
  afterMissing: boolean;
  /** 법제처 공식 변경 구간. 배포 전 수집분과 재현 데이터는 null */
  oldMarks: MarkRange[] | null;
  newMarks: MarkRange[] | null;
}

export interface EvidenceItem {
  id: string;
  kind: string;
  title: string;
  sourceUrl: string | null;
  /** 근거 본문 앞부분(최대 240자) */
  excerpt?: string;
}

export interface ReviewDetail extends ReviewSummary {
  oldNew: OldNew[];
  draft: Draft | null;
  evidence: EvidenceItem[];
  version: number;
}

export interface EditedSentence {
  sentence: string;
  citations: string[];
}

export interface ApiError {
  code: string;
  message: string;
}

export type ApiResult<T> = { waking: true } | { waking: false; status: number; data: T };

export const WAKING_STATUSES = [502, 503, 504];

/** http(s) 주소만 링크로 허용한다. */
export function safeHttpUrl(url: string | null | undefined): string | null {
  return url && /^https?:\/\//i.test(url) ? url : null;
}

export const RETRY_INTERVAL_MS = 5000;
export const MAX_RETRIES = 24;

const BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5080";

export function getRole(): Role {
  if (typeof document === "undefined") return "lawyer";
  const m = /(?:^|;\s*)lw_role=([^;]*)/.exec(document.cookie);
  return m?.[1] === "admin" ? "admin" : "lawyer";
}

export function setRole(role: Role) {
  document.cookie = `lw_role=${role}; path=/; max-age=31536000; SameSite=Lax`;
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<ApiResult<T>> {
  const headers = new Headers(init.headers);
  headers.set("X-Demo-Role", getRole());
  if (init.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  let res: Response;
  try {
    res = await fetch(`${BASE}${path}`, { ...init, headers });
  } catch {
    return { waking: true };
  }
  if (WAKING_STATUSES.includes(res.status)) return { waking: true };
  let data: unknown = null;
  try {
    const text = await res.text();
    data = text ? JSON.parse(text) : null;
  } catch {
    data = { code: "NON_JSON_RESPONSE", message: `서버가 올바르지 않은 응답을 보냈어요. (${res.status})` } satisfies ApiError;
  }
  return { waking: false, status: res.status, data: data as T };
}

export type DecisionOutcome =
  | { kind: "waking" }
  | { kind: "conflict" }
  | { kind: "error"; message: string }
  | { kind: "ok" };

/** 승인·반려 요청을 보내고 화면이 다룰 결과로 접는다. */
export async function submitDecision(
  id: string,
  kind: "approve" | "reject",
  version: number,
  opts: { editedSentences?: EditedSentence[]; comment?: string },
): Promise<DecisionOutcome> {
  const body =
    kind === "approve"
      ? { version, editedSentences: opts.editedSentences }
      : { version, comment: opts.comment };
  const res = await apiFetch<ApiError | { status: string }>(`/api/reviews/${id}/${kind}`, {
    method: "POST",
    body: JSON.stringify(body),
  });
  if (res.waking) return { kind: "waking" };
  if (res.status === 409 && (res.data as ApiError | null)?.code === "NO_DRAFT")
    return { kind: "error", message: "초안이 없어 승인할 수 없어요. 반려만 할 수 있어요." };
  if (res.status === 409) return { kind: "conflict" };
  if (res.status >= 400) return { kind: "error", message: (res.data as ApiError | null)?.message ?? "처리하지 못했어요." };
  return { kind: "ok" };
}

const DECIDABLE_STATUSES = ["WaitingApproval", "NeedsReview"];

/**
 * 검토 화면에서 보일 결정 버튼. 백엔드와 같은 규칙이다: 결정 대기 상태에서 변호사만,
 * 초안이 없으면 승인·수정은 못 하고(409 NO_DRAFT) 반려만 할 수 있다.
 */
export function decisionActions(role: string | null | undefined, status: string, hasDraft: boolean) {
  const canDecide = role === "lawyer" && DECIDABLE_STATUSES.includes(status);
  return { approve: canDecide && hasDraft, edit: canDecide && hasDraft, reject: canDecide };
}

export interface RunSummary {
  id: string;
  lawName: string;
  status: string;
  isReplay: boolean;
  createdAt: string;
  groupStatuses: { groupCode: string; status: string }[];
}

export type TimelineItem =
  | {
      kind: "step";
      at: string;
      step: string;
      attempt: number;
      status: string;
      durationMs: number | null;
      errorCode: string | null;
      errorMessage: string | null;
      providerKind: ProviderKind;
      providerName: string | null;
      groupCode: string | null;
      summary: string | null;
    }
  | {
      kind: "tool_call";
      at: string;
      step: string;
      attempt: number;
      tool: string;
      groupCode: string | null;
      resultSummary: string;
      evidenceIds: string[];
    }
  | { kind: "finding"; at: string; groupCode: string | null; tier: number; ruleId: string; message: string }
  | { kind: "approval"; at: string; groupCode: string | null; decision: string; actorRole: string; comment: string | null }
  | { kind: "notification"; at: string; groupCode: string | null; status: string; message: string };

export interface RunDetail {
  run: { id: string; status: string; isReplay: boolean; traceId: string | null; createdAt: string; finishedAt: string | null };
  lawChange: { id: string; lawId: string; lawName: string; promulgationDate: string; effectiveDate: string | null };
  groups: { groupCode: string; status: string }[];
  timeline: TimelineItem[];
}

export interface InboxItem {
  id: string;
  groupReviewId: string | null;
  channel: string;
  status: string;
  body: string;
  sentAt: string | null;
  isReplay?: boolean;
}

export type RetryOutcome =
  | { kind: "waking" }
  | { kind: "ok"; requeued: string[] }
  | { kind: "nothing"; message: string }
  | { kind: "forbidden"; message: string }
  | { kind: "error"; message: string };

/** 실패 단계 재실행 요청을 보내고 화면이 다룰 결과로 접는다. */
export async function retryRun(runId: string): Promise<RetryOutcome> {
  const res = await apiFetch<ApiError | { requeued: string[] }>(`/api/runs/${runId}/retry`, { method: "POST" });
  if (res.waking) return { kind: "waking" };
  const msg = (res.data as ApiError | null)?.message;
  if (res.status === 403) return { kind: "forbidden", message: "다시 실행은 관리자만 할 수 있어요. 관리자로 보기로 바꿔 주세요." };
  if (res.status === 409) return { kind: "nothing", message: msg ?? "다시 실행할 실패 단계가 없어요." };
  if (res.status >= 400) return { kind: "error", message: msg ?? "다시 실행하지 못했어요." };
  return { kind: "ok", requeued: (res.data as { requeued?: string[] } | null)?.requeued ?? [] };
}

/** 소요 시간 표시. 끝나지 않았으면 "—". */
export function formatDuration(ms: number | null | undefined): string {
  if (ms == null) return "—";
  return ms < 1000 ? `${ms}ms` : `${(ms / 1000).toFixed(1)}초`;
}

export { stepLabel } from "./labels";

/**
 * 백엔드 RunEndpoints.Retryable과 같은 규칙으로 다시 실행할 수 있는 실패 단계가 있는지 본다.
 * (단계, 그룹)에 Failed가 아닌 행이 없어야 하고, 결정된 그룹은 Approved의 notify만 허용한다.
 */
export function hasRetryableStep(timeline: TimelineItem[], groups: { groupCode: string; status: string }[]): boolean {
  const steps = timeline.filter((it): it is Extract<TimelineItem, { kind: "step" }> => it.kind === "step");
  // 백엔드와 같다: 같은 (단계, 그룹)에 Failed가 아닌 행이 하나라도 있으면 이미 재큐된 것이다.
  const superseded = (s: (typeof steps)[number]) =>
    steps.some((o) => o.status !== "Failed" && o.step === s.step && o.groupCode === s.groupCode);
  const status = new Map(groups.map((g) => [g.groupCode, g.status]));
  return steps.some((s) => {
    if (s.status !== "Failed" || superseded(s)) return false;
    const g = s.groupCode ? status.get(s.groupCode) : undefined;
    if (g === undefined) return true;
    if (g === "Approved") return s.step === "notify";
    return !["Rejected", "Notified", "ClosedIrrelevant"].includes(g);
  });
}
