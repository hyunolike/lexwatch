"use client";
import { ExternalLink, FileText, TriangleAlert, X } from "lucide-react";
import { Popover } from "@/components/ui/Floating";
import { safeHttpUrl, type EvidenceItem } from "@/lib/api";
import { cx } from "@/lib/cx";

/**
 * 인용 칩(DS-09). 누르거나 Enter를 누르면 근거 제목, 본문 앞부분, 원문 링크가 팝오버로 뜬다.
 * 근거가 없으면 경고 모양이 된다(크리틱 1단과 같은 판단). 편집 중에는 제거 버튼이 따로 붙는다.
 */
export function CitationChip({ id, evidence, onRemove }: { id: string; evidence?: EvidenceItem; onRemove?: () => void }) {
  const missing = !evidence;
  const href = safeHttpUrl(evidence?.sourceUrl);
  return (
    <span className="inline-flex items-center">
      <Popover
        label={missing ? "근거 없음" : `근거: ${evidence.title}`}
        trigger={
          <button
            type="button"
            className={cx(
              "inline-flex max-w-[16rem] items-center gap-1 rounded-sm px-2 py-0.5 text-micro transition-colors duration-[120ms]",
              missing ? "bg-warning-bg text-warning" : "bg-info-bg text-info hover:brightness-95",
              onRemove && "rounded-r-none",
            )}
          >
            {missing ? <TriangleAlert aria-hidden className="size-3" /> : <FileText aria-hidden className="size-3" />}
            <span className="truncate">{evidence?.title ?? id}</span>
          </button>
        }
      >
        {missing ? (
          <p className="text-ui text-ink">
            근거를 찾을 수 없어요. <span className="font-mono text-caption text-ink-subtle">{id}</span>
          </p>
        ) : (
          <div className="space-y-2">
            <p className="text-title-sm text-ink">{evidence.title}</p>
            {evidence.excerpt && <p className="text-reading whitespace-pre-wrap text-ink-muted">{evidence.excerpt}</p>}
            {href && (
              <a href={href} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-label text-info underline">
                원문 보기 <ExternalLink aria-hidden className="size-3.5" />
              </a>
            )}
          </div>
        )}
      </Popover>
      {onRemove && (
        <button
          type="button"
          onClick={onRemove}
          aria-label={`인용 제거: ${evidence?.title ?? id}`}
          className="inline-flex h-[22px] w-6 items-center justify-center rounded-r-sm bg-info-bg text-info hover:bg-danger-bg hover:text-danger"
        >
          <X aria-hidden className="size-3" />
        </button>
      )}
    </span>
  );
}
