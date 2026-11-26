"use client";
import { useEffect, useState } from "react";
import { apiFetch, type ApiError, type ApiResult, type EvidenceItem } from "@/lib/api";
import { aiWaker } from "@/lib/aiWaker";
import { ArrowUp, ShieldCheck, Sparkles } from "lucide-react";
import {
  MAX_QUESTION_CHARS,
  isAiWaking,
  mapChatResult,
  maskNotice,
  maskedUserTurn,
  toHistory,
  type ChatMessage,
  type AiState,
  type ChatResponse,
} from "@/lib/reviewChat";
import { SourceLabel } from "@/components/ds/badges";
import { CitationChip } from "@/components/ds/CitationChip";
import { AiLabel } from "@/components/ds/AiLabel";
import { aiExplain, isAiProvider } from "@/lib/aiTrail";
import { CARD } from "@/components/ds/layout";
import { Button } from "@/components/ui/Button";
import { cx } from "@/lib/cx";

/**
 * 검토 보조 채팅. 대화는 이 화면의 상태에만 있고 서버에 저장되지 않는다.
 * suggestions는 대화를 시작할 추천 질문(RC-02), onCitation이 있으면(분할 화면) 답의 인용 칩이 근거 패널을 바꾼다(RC-04).
 */
export function ReviewChat({
  reviewId,
  evidence,
  suggestions = [],
  onCitation,
  selectedEvidence = null,
}: {
  reviewId: string;
  evidence: EvidenceItem[];
  suggestions?: string[];
  onCitation?: (id: string) => void;
  selectedEvidence?: string | null;
}) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [question, setQuestion] = useState("");
  const [busy, setBusy] = useState(false);
  const [aiState, setAiState] = useState<AiState>(() => aiWaker.state());

  // 앱이 연 깨우기(AiWarmup)를 함께 쓴다. 이미 깨어 있으면 다시 묻지 않는다(chat-wake CW-02).
  useEffect(() => {
    const off = aiWaker.subscribe(setAiState);
    void aiWaker.warm();
    return off;
  }, []);

  async function ask(q: string, history: ReturnType<typeof toHistory>) {
    return apiFetch<ChatResponse | ApiError>(`/api/reviews/${reviewId}/chat`, {
      method: "POST",
      body: JSON.stringify({ question: q, history }),
    });
  }

  async function send(preset?: string) {
    const q = (preset ?? question).trim();
    if (!q || busy) return;
    const history = toHistory(messages);
    const mine: ChatMessage = { role: "user", text: q, citations: [], refused: false, providerKind: null, error: false };
    setMessages((m) => [...m, mine]);
    if (preset === undefined) setQuestion("");
    setBusy(true);
    let res: ApiResult<ChatResponse | ApiError>;
    // 잠든 AI에 바로 보내면 서버의 90초 재시도가 깨는 시간(길면 2~3분)보다 먼저 끝난다.
    // 깰 때까지 질문을 들고 기다렸다가 보낸다(chat-wake CW-03). 사용자가 다시 쓰지 않아도 된다.
    if (!(await aiWaker.warm())) res = { waking: true };
    else {
      res = await ask(q, history);
      // 깨어 있다고 본 뒤 다시 잠든 경우: 깨기를 다시 기다렸다가 한 번 더 보낸다(CW-04).
      if (isAiWaking(res) && (await aiWaker.warm({ force: true }))) res = await ask(q, history);
    }
    setBusy(false);
    // 서버가 가린 질문으로 내 말풍선을 바꾼다. 화면과 다음 대화 기록에 원문이 남지 않는다(GV-04).
    setMessages((m) => [...m.map((x) => (x === mine ? maskedUserTurn(mine, res) : x)), mapChatResult(res, evidence)]);
  }

  const byId = new Map(evidence.map((e) => [e.id, e]));
  return (
    <section
      className={`${CARD} space-y-4 border-l-4 border-l-prov-ai p-4 sm:p-5`}
      aria-labelledby="review-chat-title"
    >
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex items-center gap-2.5">
          <span aria-hidden className="grid size-8 place-items-center rounded-md bg-prov-ai-bg text-prov-ai">
            <Sparkles className="size-4" />
          </span>
          <div>
            <h2 id="review-chat-title" className="text-title-sm text-ink">검토 보조</h2>
            <p className="text-caption text-ink-muted">AI가 이 개정의 근거 {evidence.length}건 안에서만 답해요</p>
          </div>
        </div>
        <AiStatus state={aiState} />
      </div>
      <p className="text-caption text-ink-subtle">
        법률 자문이 아니고, 대화는 저장되지 않아요. 주민등록번호, 연락처, 등록된 고객사 이름 같은 민감정보는 AI로 보내기 전에
        가려요. 사람 이름이나 주소는 가리지 못하니 넣지 말아 주세요.
      </p>

      {messages.length === 0 && suggestions.length > 0 && (
        <div className="space-y-2">
          <p className="text-caption font-medium text-ink-muted">이런 걸 물어볼 수 있어요</p>
          <ul className="flex flex-wrap gap-2">
            {suggestions.map((s) => (
              <li key={s}>
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => void send(s)}
                  className="rounded-full border border-prov-ai/30 bg-prov-ai-bg px-3 py-1.5 text-left text-label text-ink transition-colors duration-[120ms] hover:border-prov-ai disabled:opacity-60"
                >
                  {s}
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {(messages.length > 0 || busy) && (
        <ul className="space-y-3" aria-live="polite">
          {messages.map((m, i) => (
            <li key={i} className={cx("lw-rise", m.role === "user" ? "text-right" : "")}>
              <div
                className={cx(
                  "inline-block max-w-full whitespace-pre-wrap rounded-lg px-3 py-2 text-left text-reading text-ink xl:max-w-reading",
                  m.role === "user"
                    ? "bg-info-bg"
                    : m.refused
                      ? "border border-warning/40 bg-warning-bg"
                      : m.error
                        ? "bg-danger-bg"
                        : "border border-line bg-surface-sunken",
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
                <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                  {!m.refused && !m.error && isAiProvider(m.providerKind) && (
                    <AiLabel text="AI 답변" context={`${i + 1}번째 메시지`} explain={aiExplain("chat", { providerKind: m.providerKind })} />
                  )}
                  {m.providerKind && <SourceLabel kind={m.providerKind} />}
                  {m.citations.map((c) => (
                    <CitationChip
                      key={c.id}
                      id={c.id}
                      evidence={byId.get(c.id)}
                      onSelect={onCitation}
                      selected={c.id === selectedEvidence}
                    />
                  ))}
                </div>
              )}
            </li>
          ))}
          {busy && (
            <li className="lw-rise flex items-start gap-2 text-caption text-ink-muted" role="status">
              <span aria-hidden className="mt-1 inline-flex gap-1 rounded-lg bg-surface-sunken px-2.5 py-2">
                <span className="lw-typing-dot size-1.5 rounded-full bg-prov-ai" />
                <span className="lw-typing-dot size-1.5 rounded-full bg-prov-ai" />
                <span className="lw-typing-dot size-1.5 rounded-full bg-prov-ai" />
              </span>
              <span className="pt-1">
                {aiState === "waking"
                  ? "AI 서버를 깨우는 중이에요. 길면 2~3분 걸려요. 깨어나면 이 질문을 자동으로 보낼게요."
                  : `근거 ${evidence.length}건을 읽고 답을 쓰는 중이에요`}
              </span>
            </li>
          )}
        </ul>
      )}

      <form
        onSubmit={(e) => {
          e.preventDefault();
          void send();
        }}
        className="rounded-lg border border-line-strong bg-surface focus-within:border-prov-ai"
      >
        <textarea
          value={question}
          maxLength={MAX_QUESTION_CHARS}
          onChange={(e) => setQuestion(e.target.value)}
          onKeyDown={(e) => {
            // Enter로 보내고 Shift+Enter로 줄을 바꾼다. 한글 조합 중 Enter는 보내지 않는다.
            if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing && e.keyCode !== 229) {
              e.preventDefault();
              void send();
            }
          }}
          rows={2}
          placeholder="이 초안의 근거에 대해 물어보세요"
          aria-label="검토 보조 질문"
          className="block w-full resize-none rounded-t-lg bg-transparent p-3 text-ui text-ink"
        />
        <div className="flex items-center justify-between px-3 pb-2 text-caption text-ink-subtle">
          <span className="tabular-nums">
            {question.length}/{MAX_QUESTION_CHARS} · Enter로 보내기
          </span>
          <Button type="submit" variant="primary" size="sm" loading={busy} disabled={!question.trim()}>
            <ArrowUp aria-hidden className="size-4" />
            보내기
          </Button>
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
