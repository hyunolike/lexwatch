"use client";
import { useState } from "react";
import { Plus } from "lucide-react";
import type { Draft, EditedSentence, EvidenceItem, TrailStage } from "@/lib/api";
import { checkState } from "@/lib/checkState";
import { CheckMark } from "@/components/ds/CheckMark";
import { AiLabel } from "@/components/ds/AiLabel";
import { Callout } from "@/components/ds/Callout";
import type { Explain } from "@/lib/aiTrail";
import { CitationChip } from "@/components/ds/CitationChip";
import { CARD } from "@/components/ds/layout";
import { cx } from "@/lib/cx";

/** 영향 검토 초안. 문장마다 인용 칩과 크리틱 적발(정적 안내)을 붙인다. 편집 중에는 문장과 인용을 고친다. */
export function DraftEditor({
  draft,
  evidence,
  editing,
  edited,
  onChange,
  judgeExplain,
  stages,
  selected = null,
  onSelect,
}: {
  /** 선택한 문장(분할 화면, DS2-07). 강조하고 근거 패널이 이 문장의 인용을 보인다. */
  selected?: number | null;
  /** 문장이나 인용 칩을 고르면 부른다. 인용 칩이면 id도 넘긴다. 없으면 고르기를 하지 않는다(1280px 미만). */
  onSelect?: (sentence: number, evidenceId?: string) => void;
  /** AI 흔적 단계. 크리틱이 끝났는지 보고 "검사 통과"와 "검사 전"을 가른다(DS2-06). */
  stages?: TrailStage[] | null;
  /** 크리틱 3단(AI 판정) 적발에 붙일 AI 라벨 설명. AI가 판정하지 않았으면 없다. */
  judgeExplain?: Explain;
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
          <article
            key={i}
            id={`draft-sentence-${i + 1}`}
            tabIndex={-1}
            aria-label={`${i + 1}번째 문장`}
            aria-current={selected === i ? "true" : undefined}
            onClick={onSelect && !editing ? (e) => !(e.target as HTMLElement).closest("button,a,textarea") && onSelect(i) : undefined}
            className={cx(
              CARD,
              "p-4 transition-colors duration-[120ms] sm:p-6",
              onSelect && !editing && "cursor-pointer hover:border-line-strong",
              selected === i && "border-l-[3px] border-l-brand bg-brand-subtle/40",
            )}
          >
            <div className="mb-3 flex items-center justify-between gap-3">
              <span className="text-caption text-ink-subtle">문장 {i + 1}</span>
              <CheckMark
                state={checkState({
                  findings: s.findings.length,
                  stages,
                  edited:
                    editing &&
                    ((edited[i]?.sentence ?? s.sentence) !== s.sentence ||
                      (edited[i]?.citations ?? s.citations).join("|") !== s.citations.join("|")),
                })}
              />
            </div>
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
                  onSelect={onSelect && !editing ? (id) => onSelect(i, id) : undefined}
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
                    <p className="flex flex-wrap items-start gap-2">
                      <span className="min-w-0 flex-1">{f.message}</span>
                      {f.tier === 3 && judgeExplain && <AiLabel explain={judgeExplain} text="AI 판정" context={`문장 ${i + 1}`} />}
                    </p>
                    <details className="mt-2 text-caption text-ink-muted">
                      <summary className="w-fit cursor-pointer rounded-sm">검사 정보</summary>
                      <p className="mt-1">크리틱 {f.tier}단 · <span className="font-mono">{f.ruleId}</span></p>
                    </details>
                  </Callout>
                ))}
              </div>
            )}
          </article>
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
