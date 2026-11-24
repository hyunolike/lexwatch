"use client";
import Link from "next/link";
import { useEffect, useRef, useSyncExternalStore, type ReactNode } from "react";
import { Circle, CircleCheck, X } from "lucide-react";
import { progress, roleSentence, type StepId } from "@/lib/guide";
import { guideStore, type GuideView } from "@/lib/guideStore";
import { CARD } from "@/components/ds/layout";
import { switchRole, useRole } from "@/components/RoleSwitch";
import { cx } from "@/lib/cx";

/** 서버와 하이드레이션 중에는 null이라 안내를 그리지 않는다(FG-07). */
export function useGuide(): GuideView | null {
  return useSyncExternalStore(guideStore.subscribe, guideStore.getSnapshot, () => null);
}

const LINK = "rounded-sm font-semibold text-ink underline underline-offset-4 hover:text-brand";

const STEPS: { id: StepId; title: ReactNode; body: ReactNode }[] = [
  {
    id: "open-review",
    title: "확인 필요 초안 열기",
    body: "아래 목록에서 '확인 필요'가 붙은 초안을 열고, 표시된 문장을 근거 법령과 비교해 보세요.",
  },
  {
    id: "approve",
    title: "초안 승인하기",
    body: (
      <>
        1에서 연 화면에서 승인하면 완료돼요. 승인한 초안은{" "}
        <Link href="/inbox" className={LINK}>
          알림함
        </Link>
        에 팀 알림으로 쌓여요. 적발을 확인하고 승인한 초안은 파트너로 보기에서 최종 승인해야 나가요.
      </>
    ),
  },
  {
    id: "admin-run",
    title: (
      <Link href="/runs" className={LINK} onClick={() => switchRole("admin")}>
        관리자 화면에서 작업 기록 보기
      </Link>
    ),
    body: "누르면 관리자 역할로 바뀌어요. 작업 하나를 열면 법령 수집부터 승인까지 AI가 한 일이 단계별로 보여요.",
  },
];

/**
 * 처음 방문 안내(first-visit-guide). 승인 대기함 위에 두는 닫을 수 있는 영역이다.
 * 대화상자가 아니라 페이지 안 영역이라 포커스를 가두지 않는다(research.md, APG).
 */
export function FirstVisitGuide() {
  const view = useGuide();
  const role = useRole();
  const heading = useRef<HTMLHeadingElement>(null);
  const visible = view != null && !view.state.dismissed;
  const focusRequested = view?.focusRequested ?? false;

  useEffect(() => {
    if (visible && focusRequested) {
      heading.current?.focus();
      guideStore.consumeFocus();
    }
  }, [visible, focusRequested]);

  if (!visible) return null;
  const { done, total } = progress(view.state);

  return (
    <section aria-labelledby="guide-title" className={cx(CARD, "relative overflow-hidden border-l-4 border-l-info p-4 sm:p-5")}>
      <div className="flex items-start gap-3">
        <h2 id="guide-title" ref={heading} tabIndex={-1} className="min-w-0 flex-1 rounded-sm text-title-sm text-ink">
          처음 오셨나요? 이렇게 둘러보세요
        </h2>
        <p className="shrink-0 pt-0.5 text-caption text-ink-muted tabular-nums">
          {done}/{total} 완료{done === total && " · 다 둘러봤어요"}
        </p>
        <button
          type="button"
          aria-label="둘러보기 안내 닫기"
          onClick={() => {
            guideStore.dismiss();
            document.getElementById("main")?.focus();
          }}
          className="-mt-1 -mr-1 grid size-8 shrink-0 place-items-center rounded-md text-ink-muted transition-colors duration-[120ms] hover:bg-surface-sunken hover:text-ink"
        >
          <X aria-hidden className="size-4" />
        </button>
      </div>

      <p className="mt-2 max-w-reading text-ui text-ink-muted">
        매일 공포되는 법령 개정을 모아, AI가 각 담당 팀(실무그룹)에 미칠 영향을 정리한 초안을 써요. 자동 검사가 근거와 맞지
        않는 문장을 표시하고, 변호사가 근거를 대조해 승인해야 팀에 알림이 가요.{" "}
        <strong className="font-semibold text-ink">AI 초안은 틀릴 수 있고, 자동 검사도 모든 오류를 잡지는 못해요.</strong>{" "}
        {roleSentence(role)}
      </p>

      <ol className="mt-4 grid gap-3 md:grid-cols-3">
        {STEPS.map((s, i) => {
          const isDone = view.state.done.includes(s.id);
          const Icon = isDone ? CircleCheck : Circle;
          return (
            <li key={s.id} className="flex gap-2.5 rounded-md bg-surface-sunken p-3">
              <Icon aria-hidden className={cx("mt-0.5 size-4 shrink-0", isDone ? "text-success" : "text-ink-subtle")} />
              <div className="min-w-0 text-ui">
                <p className={cx("font-semibold", isDone ? "text-ink-muted" : "text-ink")}>
                  {i + 1}. {s.title}
                  <span className="sr-only"> ({isDone ? "완료" : "남음"})</span>
                </p>
                <p className="mt-0.5 text-label text-ink-muted">{s.body}</p>
              </div>
            </li>
          );
        })}
      </ol>

      <p className="mt-3 text-caption text-ink-subtle">
        첫 접속은 서버를 깨우느라 1분쯤 걸릴 수 있어요. 기다리는 동안{" "}
        <a href="/intro.mp4" target="_blank" rel="noopener" className="rounded-sm font-semibold text-ink-muted underline underline-offset-2 hover:text-ink">
          36초 소개 영상
          <span className="sr-only"> (새 탭에서 열림)</span>
        </a>
        을 볼 수 있어요. 데모 데이터는 매일 새벽 처음 상태로 돌아가요.
      </p>
    </section>
  );
}
