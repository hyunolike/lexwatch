import { ListChecks, Sparkles, UserCheck, type LucideIcon } from "lucide-react";
import type { TrailActor, TrailStage } from "@/lib/api";
import { ACTOR_LABEL, STAGE_NAMES, actorCounts, stageState, stageSummary } from "@/lib/aiTrail";
import type { Tone } from "@/lib/labels";
import { SourceLabel } from "@/components/ds/badges";
import { cx } from "@/lib/cx";

const ACTOR: Record<TrailActor, { icon: LucideIcon; cls: string }> = {
  ai: { icon: Sparkles, cls: "border-info/40 bg-info-bg text-info" },
  rule: { icon: ListChecks, cls: "border-line-strong bg-surface-sunken text-ink-muted" },
  human: { icon: UserCheck, cls: "border-success/40 bg-success-bg text-success" },
};

const TONE_TEXT: Record<Tone, string> = {
  success: "text-success",
  danger: "text-danger",
  warning: "text-warning",
  info: "text-info",
  neutral: "text-ink-subtle",
};

function ActorBadge({ actor }: { actor: TrailActor }) {
  const a = ACTOR[actor];
  const Icon = a.icon;
  return (
    <span className={cx("inline-flex h-[22px] shrink-0 items-center gap-1 rounded-sm border px-1.5 text-micro font-semibold", a.cls)}>
      <Icon aria-hidden className="size-3" />
      {ACTOR_LABEL[actor]}
    </span>
  );
}

/**
 * "이 초안이 만들어진 과정"(ai-footprints AF-05, Shape of AI Footprints 참고). 단계마다 AI, 규칙, 사람을 아이콘과 글로 구분한다.
 * 기본은 한 줄 칩 목록이고, 펼치면 단계별 요약이 보인다. 확인 안내 아래에 두어 초안을 밀어내지 않는다.
 */
export function AiTrail({ stages }: { stages: TrailStage[] }) {
  const running = stages.some((s) => s.status === "running");
  return (
    <section aria-labelledby="ai-trail" className="space-y-2 rounded-lg border border-line bg-surface p-3">
      <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
        <h2 id="ai-trail" className="text-label font-semibold text-ink">이 초안이 만들어진 과정</h2>
        <p className="text-caption text-ink-muted">{actorCounts(stages)}</p>
      </div>
      <ol className="flex flex-wrap items-center gap-1.5" aria-label="단계와 담당">
        {stages.map((s, i) => {
          const off = s.status === "not_run" || s.status === "skipped";
          const st = stageState(s);
          return (
            <li key={s.key} className={cx("flex items-center gap-1.5", off && "opacity-50")}>
              {i > 0 && <span aria-hidden className="text-ink-subtle">›</span>}
              <span className="inline-flex items-center gap-1 rounded-md border border-line px-1.5 py-0.5">
                <ActorBadge actor={s.actor} />
                <span className="text-caption text-ink">{STAGE_NAMES[s.key] ?? s.key}</span>
                {st.tone !== "success" && st.tone !== "neutral" && (
                  <span className={cx("text-micro font-semibold", TONE_TEXT[st.tone])}>{st.label}</span>
                )}
              </span>
            </li>
          );
        })}
      </ol>
      <details className="group">
        <summary className="w-fit cursor-pointer rounded-sm text-caption text-ink-muted hover:text-ink">단계별 내용 보기</summary>
        <ol className="mt-2 divide-y divide-line">
          {stages.map((s) => {
            const st = stageState(s);
            const summary = stageSummary(s);
            return (
              <li key={s.key} className="flex flex-wrap items-start gap-x-3 gap-y-1 py-2">
                <ActorBadge actor={s.actor} />
                <div className="min-w-0 flex-1">
                  <p className="text-label text-ink">
                    {STAGE_NAMES[s.key] ?? s.key}
                    <span className={cx("ml-2 text-caption", TONE_TEXT[st.tone])}>{st.label}</span>
                  </p>
                  {summary && <p className="text-caption text-ink-muted">{summary}</p>}
                </div>
                {s.actor === "ai" && s.providerKind && <SourceLabel kind={s.providerKind} />}
              </li>
            );
          })}
        </ol>
        {running && <p className="mt-1 text-caption text-ink-subtle">진행 중인 단계가 있어요. 새로고침하면 갱신돼요. 분석이 끝난 뒤에 결정할 수 있어요.</p>}
      </details>
    </section>
  );
}
