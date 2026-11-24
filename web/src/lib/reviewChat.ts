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
