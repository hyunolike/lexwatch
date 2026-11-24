"use client";
import { Sparkles } from "lucide-react";
import { Popover } from "@/components/ui/Floating";
import type { Explain } from "@/lib/aiTrail";

/**
 * AI가 만든 결과에 붙이는 라벨(ai-footprints AF-06~08, IBM Carbon AI label 참고). 장식으로 쓰지 않는다.
 * 접근 이름은 보이는 글을 앞에 두고 맥락을 붙인다(WCAG 2.5.3 Label in Name).
 */
export function AiLabel({ explain, text = "AI", context }: { explain: Explain; text?: string; context?: string }) {
  return (
    <Popover
      label={explain.title}
      trigger={
        <button
          type="button"
          aria-label={`${text}${context ? `, ${context}` : ""}, 설명 보기`}
          className="inline-flex h-[22px] items-center gap-1 rounded-sm border border-prov-ai/40 bg-prov-ai-bg px-1.5 text-micro font-semibold text-prov-ai hover:border-prov-ai"
        >
          <Sparkles aria-hidden className="size-3" />
          {text}
        </button>
      }
    >
      <div className="space-y-2 text-label">
        <p className="flex items-center gap-1.5 text-ui font-semibold text-ink">
          <Sparkles aria-hidden className="size-4 text-prov-ai" />
          {explain.title}
        </p>
        <p className="text-ink">{explain.what}</p>
        <dl className="space-y-1 text-caption">
          <div className="flex gap-2">
            <dt className="shrink-0 text-ink-subtle">모델</dt>
            <dd className="text-ink-muted">{explain.model}</dd>
          </div>
          {explain.basis && (
            <div className="flex gap-2">
              <dt className="shrink-0 text-ink-subtle">근거</dt>
              <dd className="text-ink-muted">{explain.basis}</dd>
            </div>
          )}
        </dl>
        <p className="rounded-sm bg-warning-bg px-2 py-1.5 text-caption text-warning">{explain.limit}</p>
      </div>
    </Popover>
  );
}
