"use client";
import { ExternalLink, FileText } from "lucide-react";
import { safeHttpUrl, type EvidenceItem, type OldNew } from "@/lib/api";
import { oldNewFor, uncitedOldNew } from "@/lib/evidenceLink";
import { CitationChip } from "@/components/ds/CitationChip";
import { RedlineArticle, RedlineDiff } from "@/components/ds/RedlineDiff";
import { CARD } from "@/components/ds/layout";

/**
 * 근거 패널(DS2-07, design.md 3.3). 지금 API만 쓴다: 근거 앞부분(최대 240자)과 이어지는 신구조문 한 조.
 * - sentence가 있으면 그 문장의 인용을, 없으면 초안의 모든 인용을 위에 보인다.
 * - selected 근거를 가운데에, 이어지는 신구조문을 아래에, 인용하지 않은 변경 조문은 맨 아래 접힘 구역에 둔다.
 * 1280px 미만에서는 초안 아래에 같은 패널이 온다(SplitPane이 쌓는다).
 */
export function EvidencePanel({
  sentence,
  cited,
  allCited,
  selected,
  onSelect,
  evidence,
  oldNew,
}: {
  sentence: number | null;
  cited: string[];
  allCited: string[];
  selected: string | null;
  onSelect: (id: string) => void;
  evidence: EvidenceItem[];
  oldNew: OldNew[];
}) {
  const byId = new Map(evidence.map((e) => [e.id, e]));
  const list = sentence === null ? allCited : cited;
  const current = selected ?? list[0] ?? null;
  const ev = current ? byId.get(current) : undefined;
  const linked = current ? oldNewFor(current, evidence, oldNew) : null;
  const rest = uncitedOldNew(allCited, evidence, oldNew);
  const href = safeHttpUrl(ev?.sourceUrl);

  return (
    <section aria-labelledby="evidence-panel" className="space-y-4">
      <div className="space-y-2">
        <h2 id="evidence-panel" className="text-title-sm text-ink">
          {sentence === null ? "초안의 인용" : `문장 ${sentence + 1}의 인용`}
        </h2>
        {list.length === 0 ? (
          <p className="text-ui text-ink-subtle">인용한 근거가 없어요.</p>
        ) : (
          <ul className="flex flex-wrap gap-1.5">
            {list.map((id) => (
              <li key={id}>
                <CitationChip id={id} evidence={byId.get(id)} onSelect={onSelect} selected={id === current} />
              </li>
            ))}
          </ul>
        )}
      </div>

      {current && (
        <article aria-live="polite" className={`${CARD} space-y-2 p-4`}>
          {ev ? (
            <>
              <p className="flex items-start gap-2 text-title-sm text-ink">
                <FileText aria-hidden className="mt-1 size-4 shrink-0 text-ink-subtle" />
                {ev.title}
              </p>
              {ev.excerpt ? (
                <p className="text-reading whitespace-pre-wrap text-ink-muted">
                  {/(…|\.{3})$/.test(ev.excerpt.trim()) ? ev.excerpt : `${ev.excerpt}…`}
                </p>
              ) : (
                <p className="text-ui text-ink-subtle">근거 앞부분을 받지 못했어요.</p>
              )}
              {href && (
                <a href={href} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 rounded-sm text-label text-brand-fg underline underline-offset-2">
                  법제처 원문 보기 <ExternalLink aria-hidden className="size-3.5" />
                </a>
              )}
            </>
          ) : (
            <p className="text-ui text-ink">
              근거를 찾을 수 없어요. <span className="font-mono text-caption text-ink-subtle">{current}</span>
            </p>
          )}
        </article>
      )}

      {current && (linked ? <RedlineArticle item={linked} stacked /> : <p className="text-caption text-ink-subtle">이 근거에는 신구조문이 없어요.</p>)}

      {rest.length > 0 && (
        <details className={`${CARD} p-4`}>
          <summary className="w-fit cursor-pointer rounded-sm text-label font-medium text-ink-muted">
            인용하지 않은 변경 조문 {rest.length}개
          </summary>
          <div className="mt-4">
            <RedlineDiff items={rest} />
          </div>
        </details>
      )}
    </section>
  );
}
