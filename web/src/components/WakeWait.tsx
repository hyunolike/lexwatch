"use client";
import { useEffect, useState, type ReactNode } from "react";
import { ChevronLeft, ChevronRight, Circle, CircleCheck, LoaderCircle, Pause, Play } from "lucide-react";
import { Mascot, CARD } from "@/components/ds/layout";
import { cx } from "@/lib/cx";
import {
  TIPS,
  TIP_INTERVAL_MS,
  elapsedText,
  isSlow,
  nextTip,
  snoreFor,
  steps,
  timeFraction,
  waitHint,
  type StepState,
} from "@/lib/wake";

const STEP_TEXT: Record<StepState, string> = { done: "완료", active: "진행 중", todo: "남음" };
const BOX = "min-h-[17rem]";

/**
 * 서버를 깨우는 동안의 대기 화면(wake-wait). startedAt이 null이면 같은 높이의 빈 자리와
 * 비어 있는 낭독 영역만 그린다(서버 렌더링과 첫 클라이언트 렌더링이 같아 하이드레이션 차이가 없다).
 */
export function WakeWait({
  label,
  startedAt,
  apiUp,
  loaded,
}: {
  label: string;
  startedAt: number | null;
  apiUp: boolean;
  loaded: boolean;
}) {
  if (startedAt == null)
    return (
      <div className={BOX}>
        <div role="status" aria-live="polite" />
      </div>
    );
  return <Waiting label={label} startedAt={startedAt} apiUp={apiUp} loaded={loaded} />;
}

function Waiting({ label, startedAt, apiUp, loaded }: { label: string; startedAt: number; apiUp: boolean; loaded: boolean }) {
  const [now, setNow] = useState(startedAt);
  useEffect(() => {
    const tick = () => setNow(Date.now());
    const id = setInterval(tick, 1000);
    const first = setTimeout(tick, 0);
    return () => {
      clearInterval(id);
      clearTimeout(first);
    };
  }, []);
  const elapsed = now - startedAt;
  const slow = isSlow(elapsed);
  const [s1, s2] = steps(apiUp, loaded);

  return (
    <section aria-labelledby="wake-title" className={cx(CARD, BOX, "p-4 sm:p-6")}>
      <div className="flex flex-col gap-5 sm:flex-row sm:gap-6">
        <Rex />
        <div className="min-w-0 flex-1">
          <div role="status" aria-live="polite">
            <div className="flex items-baseline gap-3">
              <h2 id="wake-title" className="min-w-0 flex-1 text-title-sm text-ink">
                {loaded ? "준비됐어요" : "서버를 깨우는 중이에요"}
              </h2>
              <span aria-hidden className="shrink-0 text-ui text-ink-muted tabular-nums">
                {elapsedText(elapsed)}
              </span>
            </div>
            <p className="mt-1 text-ui text-ink-muted">이 데모는 무료 서버에서 돌아가서, 15분 동안 방문이 없으면 잠들어요.</p>
            <p className={cx("text-ui", slow ? "font-semibold text-ink" : "text-ink-muted")}>{waitHint(elapsed)}</p>
            <TimeBar elapsed={elapsed} slow={slow} />
            <ol className="mt-3 flex flex-wrap gap-x-5 gap-y-1 text-ui">
              <Step state={s1}>서버 깨우기</Step>
              <Step state={s2}>{label} 불러오기</Step>
            </ol>
          </div>
          <Tips />
        </div>
      </div>
    </section>
  );
}

function TimeBar({ elapsed, slow }: { elapsed: number; slow: boolean }) {
  return (
    <div aria-hidden className="mt-3 flex items-center gap-2 text-caption text-ink-subtle tabular-nums">
      <span>0초</span>
      <div className="relative h-2 flex-1 overflow-hidden rounded-full bg-surface-sunken">
        <div
          className={cx("h-full rounded-full bg-info transition-[width] duration-1000 ease-linear", slow && "lw-stripes")}
          style={{ width: `${(slow ? 1 : timeFraction(elapsed)) * 100}%` }}
        />
      </div>
      <span>1분</span>
    </div>
  );
}

function Step({ state, children }: { state: StepState; children: ReactNode }) {
  const Icon = state === "done" ? CircleCheck : state === "active" ? LoaderCircle : Circle;
  return (
    <li className={cx("flex items-center gap-1.5", state === "todo" ? "text-ink-subtle" : "text-ink")}>
      <Icon
        aria-hidden
        className={cx(
          "size-4 shrink-0",
          state === "done" && "text-success",
          state === "active" && "animate-spin text-info",
        )}
      />
      {children}
      <span className="sr-only"> ({STEP_TEXT[state]})</span>
    </li>
  );
}

/** 렉스 흔들기. 기다리는 동안 하는 놀이일 뿐 서버를 빨리 깨우지 않는다. */
function Rex() {
  const [taps, setTaps] = useState(0);
  return (
    <div className="flex shrink-0 flex-row items-center gap-4 sm:w-40 sm:flex-col sm:items-center sm:text-center">
      <span className="lw-breathe inline-block">
        <button
          type="button"
          aria-label="렉스 흔들기, 기다리는 동안 하는 놀이"
          onClick={() => setTaps((n) => n + 1)}
          className="block rounded-full transition-transform duration-[120ms] active:scale-95"
        >
          <span key={taps} className={cx("block", taps > 0 && "lw-wiggle")}>
            <Mascot pose="waking" size={112} />
          </span>
        </button>
      </span>
      <div className="min-w-0 text-label">
        <p aria-live="polite" className="text-ink">
          {snoreFor(taps)}
          {taps > 0 && <span className="block text-ink-muted tabular-nums">렉스를 {taps}번 흔들었어요</span>}
        </p>
        <p className="mt-1 text-caption text-ink-subtle">눌러도 빨라지지는 않아요</p>
      </div>
    </div>
  );
}

/** 기다리는 동안 읽을거리. 7초마다 넘어가고 멈출 수 있다(WCAG 2.2.2, APG Carousel). */
function Tips() {
  const [i, setI] = useState(0);
  const [userPaused, setUserPaused] = useState(false);
  const [hover, setHover] = useState(false);
  const [focusIn, setFocusIn] = useState(false);
  const [round, setRound] = useState(0);
  const paused = userPaused || hover || focusIn;

  useEffect(() => {
    if (paused) return;
    const id = setInterval(() => setI((n) => nextTip(n)), TIP_INTERVAL_MS);
    return () => clearInterval(id);
  }, [paused, round]);

  const go = (dir: 1 | -1) => {
    setI((n) => nextTip(n, dir));
    setRound((r) => r + 1);
  };
  const tip = TIPS[i];
  const btn =
    "grid size-8 place-items-center rounded-md text-ink-muted transition-colors duration-[120ms] hover:bg-surface-sunken hover:text-ink";

  return (
    <section
      aria-label="기다리는 동안 읽을거리"
      aria-roledescription="carousel"
      className="mt-4 border-t border-line pt-3"
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      onFocus={() => setFocusIn(true)}
      onBlur={(e) => {
        if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setFocusIn(false);
      }}
    >
      <div className="flex items-center gap-1">
        <p className="min-w-0 flex-1 text-caption font-semibold text-ink-muted">
          기다리는 동안 읽을거리{" "}
          <span className="font-normal tabular-nums">
            {i + 1}/{TIPS.length}
          </span>
        </p>
        <button type="button" aria-label="이전 이야기" onClick={() => go(-1)} className={btn}>
          <ChevronLeft aria-hidden className="size-4" />
        </button>
        <button
          type="button"
          aria-label="이야기 자동 넘김 멈추기"
          aria-pressed={userPaused}
          onClick={() => setUserPaused((p) => !p)}
          className={btn}
        >
          {userPaused ? <Play aria-hidden className="size-4" /> : <Pause aria-hidden className="size-4" />}
        </button>
        <button type="button" aria-label="다음 이야기" onClick={() => go(1)} className={btn}>
          <ChevronRight aria-hidden className="size-4" />
        </button>
      </div>
      <div aria-live={paused ? "polite" : "off"} className="mt-1 min-h-[3.25rem]">
        <p className="text-ui font-semibold text-ink">{tip.title}</p>
        <p className="text-label text-ink-muted">{tip.body}</p>
      </div>
      <a
        href="/intro.mp4"
        target="_blank"
        rel="noopener"
        className="mt-2 inline-block rounded-sm text-label font-semibold text-ink underline underline-offset-2 hover:text-brand"
      >
        36초 소개 영상 보기
        <span className="sr-only"> (새 탭에서 열림)</span>
      </a>
    </section>
  );
}
