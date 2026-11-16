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

export const RETRY_INTERVAL_MS = 5000;
export const MAX_RETRIES = 24;

const BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5000";

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
  if (res.status === 502 || res.status === 503) return { waking: true };
  const text = await res.text();
  const data = (text ? JSON.parse(text) : null) as T;
  return { waking: false, status: res.status, data };
}
