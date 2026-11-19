"use client";
import { useState } from "react";
import { Plus } from "lucide-react";
import type { Draft, EditedSentence, EvidenceItem } from "@/lib/api";
import { Callout } from "@/components/ds/Callout";
import { CitationChip } from "@/components/ds/CitationChip";
import { CARD } from "@/components/ds/layout";

/** 영향 검토 초안. 문장마다 인용 칩과 크리틱 적발(정적 안내)을 붙인다. 편집 중에는 문장과 인용을 고친다. */
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
    <div className="space-y-3">
      {draft.sentences.map((s, i) => {
        const citations = editing ? (edited[i]?.citations ?? s.citations) : s.citations;
        return (
          <div key={i} className={`${CARD} p-4`}>
            {editing ? (
              <textarea
                value={edited[i]?.sentence ?? s.sentence}
                onChange={(e) => update(i, { sentence: e.target.value })}
                rows={3}
                aria-label={`${i + 1}번째 문장`}
                className="text-reading w-full rounded-md border border-line-strong bg-surface p-2 text-ink"
              />
            ) : (
              <p className="text-reading text-ink">{s.sentence}</p>
            )}
            <div className="mt-2 flex flex-wrap items-center gap-1.5">
              {citations.map((c) => (
                <CitationChip
                  key={c}
                  id={c}
                  evidence={byId.get(c)}
                  onRemove={editing ? () => update(i, { citations: citations.filter((x) => x !== c) }) : undefined}
                />
              ))}
              {editing &&
                (adding === i ? (
                  <select
                    autoFocus
                    aria-label="추가할 근거"
                    className="h-7 rounded-md border border-line-strong bg-surface px-2 text-caption text-ink"
                    defaultValue=""
                    onBlur={() => setAdding(null)}
                    onChange={(e) => {
                      if (e.target.value && !citations.includes(e.target.value))
                        update(i, { citations: [...citations, e.target.value] });
                      setAdding(null);
                    }}
                  >
                    <option value="">근거 선택</option>
                    {evidence.map((ev) => (
                      <option key={ev.id} value={ev.id}>
                        {ev.title}
                      </option>
                    ))}
                  </select>
                ) : (
                  <button
                    type="button"
                    onClick={() => setAdding(i)}
                    className="inline-flex h-[22px] items-center gap-0.5 rounded-sm border border-dashed border-line-strong px-2 text-micro text-ink-muted hover:text-ink"
                  >
                    <Plus aria-hidden className="size-3" /> 인용
                  </button>
                ))}
            </div>
            {s.findings.length > 0 && (
              <div className="mt-3 space-y-1.5">
                {s.findings.map((f, k) => (
                  <Callout key={k} tone="danger" className="py-2">
                    <span className="font-semibold">
                      크리틱 {f.tier}단 · <span className="font-mono">{f.ruleId}</span>
                    </span>{" "}
                    {f.message}
                  </Callout>
                ))}
              </div>
            )}
          </div>
        );
      })}
      {draft.openQuestions.length > 0 && (
        <Callout tone="warning" title="열린 질문">
          <ul className="mt-1 list-disc pl-5">
            {draft.openQuestions.map((q, i) => (
              <li key={i}>{q}</li>
            ))}
          </ul>
        </Callout>
      )}
    </div>
  );
}
