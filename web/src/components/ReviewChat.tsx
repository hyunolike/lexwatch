"use client";
import { useEffect, useRef, useState } from "react";
import { apiFetch, type ApiError, type EvidenceItem } from "@/lib/api";
import { ShieldCheck } from "lucide-react";
import {
  MAX_QUESTION_CHARS,
  aiStateOf,
  isAiWaking,
  mapChatResult,
  waitForAi,
  maskNotice,
  maskedUserTurn,
  toHistory,
  type ChatMessage,
  type AiState,
  type ChatResponse,
} from "@/lib/reviewChat";
import { SourceLabel } from "@/components/ds/badges";
import { AiLabel } from "@/components/ds/AiLabel";
import { aiExplain, isAiProvider } from "@/lib/aiTrail";
import { CARD } from "@/components/ds/layout";
import { Button } from "@/components/ui/Button";
import { cx } from "@/lib/cx";

/** 검토 보조 채팅. 대화는 이 화면의 상태에만 있고 서버에 저장되지 않는다. */
export function ReviewChat({ reviewId, evidence }: { reviewId: string; evidence: EvidenceItem[] }) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [question, setQuestion] = useState("");
  const [busy, setBusy] = useState(false);
  const [aiState, setAiState] = useState<AiState>("checking");
  /** 질문을 들고 AI 서비스가 깨기를 기다리는 중(자동으로 다시 보낸다). */
  const [waitingWake, setWaitingWake] = useState(false);
  const abort = useRef<AbortController | null>(null);

  const checkAi = async () => {
    const s = aiStateOf(await apiFetch<{ state?: string } | ApiError>("/api/ai/status"));
    setAiState(s);
    return s;
  };

  // 화면이 열리면 AI 서비스를 미리 깨운다. 질문을 쓰는 동안 깨어난다.
  useEffect(() => {
    const ac = new AbortController();
    abort.current = ac;
    void waitForAi(checkAi, { signal: ac.signal });
    return () => ac.abort();
  }, []);

  async function ask(q: string, history: ReturnType<typeof toHistory>) {
    return apiFetch<ChatResponse | ApiError>(`/api/reviews/${reviewId}/chat`, {
      method: "POST",
      body: JSON.stringify({ question: q, history }),
    });
  }

  async function send() {
    const q = question.trim();
    if (!q || busy) return;
    const history = toHistory(messages);
    const mine: ChatMessage = { role: "user", text: q, citations: [], refused: false, providerKind: null, error: false };
    setMessages((m) => [...m, mine]);
    setQuestion("");
    setBusy(true);
    let res = await ask(q, history);
    // AI 서비스가 깨는 중이면 깰 때까지 기다렸다가 같은 질문을 한 번 더 보낸다. 사용자가 다시 쓰지 않아도 된다.
    if (isAiWaking(res)) {
      setAiState("waking");
      setWaitingWake(true);
      const woke = await waitForAi(checkAi, { signal: abort.current?.signal });
      setWaitingWake(false);
      if (woke) res = await ask(q, history);
    }
    setBusy(false);
    // 서버가 가린 질문으로 내 말풍선을 바꾼다. 화면과 다음 대화 기록에 원문이 남지 않는다(GV-04).
    setMessages((m) => [...m.map((x) => (x === mine ? maskedUserTurn(mine, res) : x)), mapChatResult(res, evidence)]);
  }

  return (
    <section className={`${CARD} space-y-3 p-4`} aria-label="검토 보조">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-title-sm text-ink">검토 보조</h2>
        <AiStatus state={aiState} />
      </div>
      <p className="text-caption text-ink-subtle">
        이 개정안의 근거 안에서만 답해요. 법률 자문이 아니고, 대화는 저장되지 않아요. 주민등록번호, 연락처, 등록된 고객사 이름 같은
        민감정보는 AI로 보내기 전에 가려요. 사람 이름이나 주소는 가리지 못하니 넣지 말아 주세요.
      </p>
      <ul className="space-y-2" aria-live="polite">
        {messages.map((m, i) => (
          <li key={i} className={m.role === "user" ? "text-right" : ""}>
            <div
              className={cx(
                "inline-block max-w-full whitespace-pre-wrap rounded-lg px-3 py-2 text-left text-reading text-ink",
                m.role === "user" ? "bg-info-bg" : m.refused ? "border border-warning/40 bg-warning-bg" : m.error ? "bg-danger-bg" : "bg-surface-sunken",
              )}
            >
              {m.text}
            </div>
            {m.role === "user" && maskNotice(m.masked) && (
              <p className="mt-1 inline-flex items-center gap-1 text-caption text-ink-muted">
                <ShieldCheck aria-hidden className="size-3.5 text-success" />
                {maskNotice(m.masked)}
              </p>
            )}
            {m.role === "assistant" && (m.providerKind || m.citations.length > 0) && (
              <div className="mt-1 flex flex-wrap items-center gap-1">
                {!m.refused && !m.error && isAiProvider(m.providerKind) && (
                  <AiLabel text="AI 답변" context={`${i + 1}번째 메시지`} explain={aiExplain("chat", { providerKind: m.providerKind })} />
                )}
                {m.providerKind && <SourceLabel kind={m.providerKind} />}
                {m.citations.map((c) => (
                  <span key={c.id} title={c.id} className="rounded-sm bg-neutral-bg px-2 py-0.5 text-micro text-neutral">{c.title}</span>
                ))}
              </div>
            )}
          </li>
        ))}
        {busy && (
          <li className="text-caption text-ink-subtle" role="status">
            {waitingWake
              ? "AI 서버를 깨우는 중이에요. 길면 2~3분 걸려요. 깨어나면 이 질문을 자동으로 다시 보낼게요."
              : `근거 ${evidence.length}건 안에서 답을 찾는 중이에요…`}
          </li>
        )}
      </ul>
      <form onSubmit={(e) => { e.preventDefault(); void send(); }} className="space-y-1">
        <textarea
          value={question}
          maxLength={MAX_QUESTION_CHARS}
          onChange={(e) => setQuestion(e.target.value)}
          rows={2}
          placeholder="이 초안의 근거에 대해 물어보세요"
          aria-label="검토 보조 질문"
          className="w-full rounded-md border border-line-strong bg-surface p-2 text-ui text-ink"
        />
        <div className="flex items-center justify-between text-caption text-ink-subtle">
          <span className="tabular-nums">{question.length}/{MAX_QUESTION_CHARS}</span>
          <Button type="submit" variant="primary" size="sm" loading={busy} disabled={!question.trim()}>보내기</Button>
        </div>
      </form>
    </section>
  );
}

/** AI 서비스가 깨어 있는지. 잠든 무료 서버라 첫 질문 전에 알려 준다. */
function AiStatus({ state }: { state: AiState }) {
  const text = state === "up" ? "AI 준비됨" : state === "waking" ? "AI 깨우는 중" : "AI 확인 중";
  return (
    <span className="inline-flex items-center gap-1.5 text-caption text-ink-muted" role="status">
      <span
        aria-hidden
        className={cx(
          "size-2 rounded-full",
          state === "up" ? "bg-success" : "animate-pulse bg-warning motion-reduce:animate-none",
        )}
      />
      {text}
    </span>
  );
}
