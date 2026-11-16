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

export interface OldNew {
  articleNo: string;
  before: string;
  after: string;
}

export interface EvidenceItem {
  id: string;
  kind: string;
  title: string;
  sourceUrl: string | null;
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
  if (res.status === 409) return { kind: "conflict" };
  if (res.status >= 400) return { kind: "error", message: (res.data as ApiError | null)?.message ?? "처리하지 못했어요." };
  return { kind: "ok" };
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
  timeline: TimelineItem[];
}

export interface InboxItem {
  id: string;
  groupReviewId: string | null;
  channel: string;
  status: string;
  body: string;
  sentAt: string | null;
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
