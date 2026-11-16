"use client";
import { useState } from "react";
import type { Draft, EditedSentence, EvidenceItem } from "@/lib/api";

function Chip({ ev, id, onRemove }: { ev?: EvidenceItem; id: string; onRemove?: () => void }) {
  const label = ev?.title ?? id;
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-blue-50 px-2 py-0.5 text-xs text-blue-800">
      {ev?.sourceUrl ? (
        <a href={ev.sourceUrl} target="_blank" rel="noreferrer" className="underline">{label}</a>
      ) : (
        <span>{label}</span>
      )}
      {onRemove && (
        <button onClick={onRemove} aria-label="인용 제거" className="font-bold">×</button>
      )}
    </span>
  );
}

export function DraftEditor({
  draft,
  evidence,
  editing,
  edited,
  onChange,
}: {
  draft: Draft;
  evidence: EvidenceItem[];
  editing: boolean;
  edited: EditedSentence[];
  onChange: (next: EditedSentence[]) => void;
}) {
  const byId = new Map(evidence.map((e) => [e.id, e]));
  const [adding, setAdding] = useState<number | null>(null);
  const update = (i: number, patch: Partial<EditedSentence>) =>
    onChange(edited.map((s, j) => (j === i ? { ...s, ...patch } : s)));

  return (
    <div className="space-y-4">
      {draft.sentences.map((s, i) => (
        <div key={i} className="rounded border bg-white p-3">
          {editing ? (
            <textarea
              value={edited[i]?.sentence ?? s.sentence}
              onChange={(e) => update(i, { sentence: e.target.value })}
              rows={3}
              className="w-full rounded border p-2 text-sm"
            />
          ) : (
            <p className="text-sm">{s.sentence}</p>
          )}
          <div className="mt-2 flex flex-wrap items-center gap-1">
            {(editing ? (edited[i]?.citations ?? s.citations) : s.citations).map((c) => (
              <Chip
                key={c}
                id={c}
                ev={byId.get(c)}
                onRemove={
                  editing
                    ? () => update(i, { citations: (edited[i]?.citations ?? s.citations).filter((x) => x !== c) })
                    : undefined
                }
              />
            ))}
            {editing && (
              adding === i ? (
                <select
                  autoFocus
                  className="rounded border text-xs"
                  defaultValue=""
                  onChange={(e) => {
                    const cur = edited[i]?.citations ?? s.citations;
                    if (e.target.value && !cur.includes(e.target.value))
                      update(i, { citations: [...cur, e.target.value] });
                    setAdding(null);
                  }}
                >
                  <option value="">근거 선택</option>
                  {evidence.map((ev) => (
                    <option key={ev.id} value={ev.id}>{ev.title}</option>
                  ))}
                </select>
              ) : (
                <button onClick={() => setAdding(i)} className="rounded border px-2 text-xs">+ 인용</button>
              )
            )}
          </div>
          {s.findings.length > 0 && (
            <ul className="mt-2 space-y-1">
              {s.findings.map((f, k) => (
                <li key={k} className="rounded bg-red-50 px-2 py-1 text-xs text-red-800">
                  {f.tier}단 · {f.ruleId} — {f.message}
                </li>
              ))}
            </ul>
          )}
        </div>
      ))}
      {draft.openQuestions.length > 0 && (
        <div className="rounded border bg-yellow-50 p-3 text-sm">
          <div className="mb-1 font-semibold">열린 질문</div>
          <ul className="list-disc pl-5">
            {draft.openQuestions.map((q, i) => (
              <li key={i}>{q}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
