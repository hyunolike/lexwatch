"use client";
import { useState } from "react";
import { apiFetch, type ApiError, type EvidenceItem } from "@/lib/api";
import { MAX_QUESTION_CHARS, mapChatResult, toHistory, type ChatMessage, type ChatResponse } from "@/lib/reviewChat";
import { ProviderBadge } from "@/components/ProviderBadge";

/** 검토 보조 채팅. 대화는 이 화면의 상태에만 있고 서버에 저장되지 않는다. */
export function ReviewChat({ reviewId, evidence }: { reviewId: string; evidence: EvidenceItem[] }) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [question, setQuestion] = useState("");
  const [busy, setBusy] = useState(false);

  async function send() {
    const q = question.trim();
    if (!q || busy) return;
    const history = toHistory(messages);
    setMessages((m) => [...m, { role: "user", text: q, citations: [], refused: false, providerKind: null, error: false }]);
    setQuestion("");
    setBusy(true);
    const res = await apiFetch<ChatResponse | ApiError>(`/api/reviews/${reviewId}/chat`, {
      method: "POST",
      body: JSON.stringify({ question: q, history }),
    });
    setBusy(false);
    setMessages((m) => [...m, mapChatResult(res, evidence)]);
  }

  return (
    <section className="space-y-2 rounded border bg-white p-3" aria-label="검토 보조">
      <h2 className="font-semibold">검토 보조</h2>
      <p className="text-xs text-slate-500">이 개정안의 근거 안에서만 답해요. 법률 자문이 아니고, 대화는 저장되지 않아요.</p>
      <ul className="space-y-2" aria-live="polite">
        {messages.map((m, i) => (
          <li key={i} className={m.role === "user" ? "text-right" : ""}>
            <div
              className={`inline-block max-w-full whitespace-pre-wrap rounded px-3 py-2 text-left text-sm ${
                m.role === "user" ? "bg-blue-50" : m.refused ? "border border-orange-300 bg-orange-50" : m.error ? "bg-yellow-50" : "bg-slate-100"
              }`}
            >
              {m.text}
            </div>
            {m.role === "assistant" && (m.providerKind || m.citations.length > 0) && (
              <div className="mt-1 flex flex-wrap items-center gap-1">
                {m.providerKind && <ProviderBadge kind={m.providerKind} />}
                {m.citations.map((c) => (
                  <span key={c.id} title={c.id} className="rounded bg-slate-200 px-2 py-0.5 text-xs">{c.title}</span>
                ))}
              </div>
            )}
          </li>
        ))}
        {busy && <li className="text-sm text-slate-500">답을 찾는 중이에요…</li>}
      </ul>
      <form onSubmit={(e) => { e.preventDefault(); void send(); }} className="space-y-1">
        <textarea
          value={question}
          maxLength={MAX_QUESTION_CHARS}
          onChange={(e) => setQuestion(e.target.value)}
          rows={2}
          placeholder="이 초안의 근거에 대해 물어보세요"
          aria-label="검토 보조 질문"
          className="w-full rounded border p-2 text-sm"
        />
        <div className="flex items-center justify-between text-xs text-slate-500">
          <span>{question.length}/{MAX_QUESTION_CHARS}</span>
          <button type="submit" disabled={busy || !question.trim()} className="rounded bg-slate-800 px-3 py-1 text-sm text-white disabled:opacity-40">보내기</button>
        </div>
      </form>
    </section>
  );
}
