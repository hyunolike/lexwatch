import type { ApiError, ApiResult, EvidenceItem, ProviderKind } from "./api";

export const MAX_QUESTION_CHARS = 500;
export const MAX_HISTORY_TURNS = 6;

/** 서버가 AI로 보내기 전에 가린 민감정보 한 종류(ai-governance GV-04). */
export interface MaskHit {
  kind: string;
  label: string;
  count: number;
}

export interface ChatResponse {
  answer: string;
  citations: string[];
  refused: boolean;
  provider: { name: string; kind: string };
  /** 서버가 가린 질문. 이 값이 실제로 AI에 간 글이다. */
  maskedQuestion?: string;
  masked?: MaskHit[];
}

export interface ChatCitation {
  id: string;
  title: string;
}

export interface ChatMessage {
  role: "user" | "assistant";
  text: string;
  citations: ChatCitation[];
  refused: boolean;
  providerKind: ProviderKind | null;
  /** 시스템 안내(한도·오류)라 대화 기록으로 서버에 보내지 않는다. */
  error: boolean;
  /** 사용자 말풍선: 서버가 가린 민감정보 */
  masked?: MaskHit[];
}

/**
 * 성공 응답이면 사용자 말풍선을 서버가 가린 질문으로 바꾼다. 다음 질문의 대화 기록에도 가린 글이 간다.
 * 오류 응답이면 그대로 둔다(서버가 다음 요청 때 기록을 다시 가린다).
 */
export function maskedUserTurn(original: ChatMessage, res: ApiResult<ChatResponse | ApiError>): ChatMessage {
  if (res.waking || res.status >= 400) return original;
  const d = res.data as ChatResponse;
  if (typeof d.maskedQuestion !== "string") return original;
  return { ...original, text: d.maskedQuestion, masked: d.masked ?? [] };
}

/** "민감정보를 가리고 보냈어요: 주민등록번호 1건, 고객사 2건". 가린 것이 없으면 null. */
export function maskNotice(masked: MaskHit[] | undefined): string | null {
  const hits = (masked ?? []).filter((h) => h.count > 0);
  if (hits.length === 0) return null;
  return `민감정보를 가리고 보냈어요: ${hits.map((h) => `${h.label} ${h.count}건`).join(", ")}`;
}

const note = (text: string): ChatMessage => ({ role: "assistant", text, citations: [], refused: false, providerKind: null, error: true });

/** 서버 응답을 화면 메시지로 접는다. 모르는 provider 종류는 fallback으로 보여 NVIDIA로 잘못 표시하지 않는다. */
export function mapChatResult(res: ApiResult<ChatResponse | ApiError>, evidence: EvidenceItem[]): ChatMessage {
  if (res.waking) return note("서버를 깨우는 중이에요. 잠시 뒤에 다시 물어봐 주세요.");
  if (res.status === 429) return note("오늘 사용 한도를 다 썼어요. 내일 다시 이용할 수 있어요.");
  if (res.status === 403) return note("검토 보조는 변호사로 보기에서만 쓸 수 있어요.");
  if (res.status >= 400) return note("검토 보조를 지금 쓸 수 없어요. 잠시 뒤에 다시 시도해 주세요.");
  const d = res.data as ChatResponse;
  const titles = new Map(evidence.map((e) => [e.id, e.title]));
  return {
    role: "assistant",
    text: d.answer,
    citations: d.citations.filter((c) => titles.has(c)).map((c) => ({ id: c, title: titles.get(c)! })),
    refused: d.refused,
    // 알려진 모델 공급자만 그대로 두고, 모르는 값은 대체 구현으로 본다(NVIDIA로 부풀리지 않는다).
    providerKind: d.provider.kind === "nvidia" || d.provider.kind === "cloudflare" || d.provider.kind === "groq" ? d.provider.kind : "fallback",
    error: false,
  };
}

/** 서버로 보낼 대화 기록: 오류 안내는 빼고 최근 6턴만. */
export function toHistory(messages: ChatMessage[]): { role: "user" | "assistant"; content: string }[] {
  return messages
    .filter((m) => !m.error)
    .slice(-MAX_HISTORY_TURNS)
    .map((m) => ({ role: m.role, content: m.text }));
}

/** AI 서비스 상태(검토 보조 머리의 표시). checking은 첫 확인 전. */
export type AiState = "checking" | "up" | "waking";

/** 잠든 AI 서비스를 기다리는 최대 시간. 운영에서 깨는 데 2분 넘게 걸린 적이 있다(2026-11-25). */
export const AI_WAKE_WAIT_MS = 180_000;
export const AI_POLL_MS = 5_000;

/** 서버(API)나 AI 서비스가 깨는 중이라 같은 질문을 나중에 다시 보내면 되는 응답인지. */
export function isAiWaking(res: ApiResult<ChatResponse | ApiError>): boolean {
  if (res.waking) return true;
  return res.status === 424 && (res.data as ApiError | null)?.code === "AI_WAKING";
}

/** `/api/ai/status` 응답을 상태로 접는다. API 자체가 잠들었거나 이상한 응답이면 waking으로 본다. */
export function aiStateOf(res: ApiResult<{ state?: string } | ApiError>): Exclude<AiState, "checking"> {
  if (res.waking || res.status !== 200) return "waking";
  return (res.data as { state?: string } | null)?.state === "up" ? "up" : "waking";
}

/**
 * AI 서비스가 깰 때까지 `poll` 간격으로 상태를 묻는다. 깨면 true, 마감을 넘기거나 취소되면 false.
 * 화면과 테스트가 시간과 요청을 주입한다.
 */
export async function waitForAi(
  check: () => Promise<Exclude<AiState, "checking">>,
  opts: { deadlineMs?: number; pollMs?: number; now?: () => number; sleep?: (ms: number) => Promise<void>; signal?: AbortSignal } = {},
): Promise<boolean> {
  const { deadlineMs = AI_WAKE_WAIT_MS, pollMs = AI_POLL_MS, now = Date.now, signal } = opts;
  const sleep = opts.sleep ?? ((ms: number) => new Promise<void>((r) => setTimeout(r, ms)));
  const start = now();
  while (!signal?.aborted) {
    if ((await check()) === "up") return true;
    if (now() - start + pollMs > deadlineMs) return false;
    await sleep(pollMs);
  }
  return false;
}

/**
 * 대화를 시작할 추천 질문(review-chat-ux RC-02). 모델을 부르지 않고 이 초안에서 만든다. 최대 3개.
 * 확인 필요 문장이 있으면 그것을, 신구조문이 있으면 첫 조문을, 그리고 시행일을 묻는다.
 */
export function suggestQuestions(input: { flaggedSentences: number; firstArticle: string | null; hasDraft: boolean }): string[] {
  const out: string[] = [];
  if (input.flaggedSentences > 0) out.push(`확인 필요 문장 ${input.flaggedSentences}개가 근거와 어떻게 다른지 알려 주세요`);
  if (input.firstArticle) out.push(`${input.firstArticle}에서 새로 생기거나 바뀐 의무는 무엇인가요?`);
  out.push("이 개정의 시행일과 경과규정이 근거에 있나요?");
  if (input.hasDraft) out.push("초안의 핵심 내용을 근거와 함께 세 줄로 요약해 주세요");
  return out.slice(0, 3);
}
