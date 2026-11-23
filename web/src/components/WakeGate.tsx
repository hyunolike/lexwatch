"use client";
import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { MAX_RETRIES, RETRY_INTERVAL_MS, pingHealth, type ApiResult } from "@/lib/api";
import { DONE_HOLD_MS, HEALTH_RETRY_MS, SHOW_DELAY_MS } from "@/lib/wake";
import { Mascot } from "@/components/ds/layout";
import { Button } from "@/components/ui/Button";
import { WakeWait } from "@/components/WakeWait";

type Ready<T> = { status: number; data: T };

const reducedMotion = () =>
  typeof window !== "undefined" && window.matchMedia?.("(prefers-reduced-motion: reduce)").matches === true;

/**
 * 잠든 API(Render 무료)를 깨우며 데이터를 불러온다. 0.4초 안에 오면 대기 화면 없이 바로 보이고,
 * 늦으면 대기 화면(wake-wait)을 띄워 경과 시간과 실제 단계를 보인다.
 */
export function WakeGate<T>({
  load,
  label,
  children,
}: {
  load: () => Promise<ApiResult<T>>;
  /** 단계 이름 "〈label〉 불러오기"에 쓴다. */
  label: string;
  children: (res: Ready<T>, reload: () => void) => ReactNode;
}) {
  const [res, setRes] = useState<Ready<T> | null>(null);
  const [gaveUp, setGaveUp] = useState(false);
  const [failed, setFailed] = useState(false);
  const [nonce, setNonce] = useState(0);
  const [wait, setWait] = useState<{ startedAt: number } | null>(null);
  const [apiUp, setApiUp] = useState(false);
  const [holding, setHolding] = useState(false);
  const loadRef = useRef(load);
  useEffect(() => {
    loadRef.current = load;
  });

  // 데이터 요청과 재시도. 대기 화면은 SHOW_DELAY_MS 뒤에만 띄운다.
  useEffect(() => {
    let cancelled = false;
    let shown = false;
    const timers: ReturnType<typeof setTimeout>[] = [];
    const t0 = Date.now();
    const showTimer = setTimeout(() => {
      shown = true;
      setWait({ startedAt: t0 });
    }, SHOW_DELAY_MS);
    timers.push(showTimer);

    const run = async (n: number) => {
      let r: ApiResult<T>;
      try {
        r = await loadRef.current();
      } catch {
        if (!cancelled) setFailed(true);
        return;
      }
      if (cancelled) return;
      if (!r.waking) {
        const ready = { status: r.status, data: r.data };
        clearTimeout(showTimer);
        if (!shown || reducedMotion()) {
          setRes(ready);
          return;
        }
        setHolding(true);
        timers.push(setTimeout(() => !cancelled && setRes(ready), DONE_HOLD_MS));
        return;
      }
      if (n + 1 >= MAX_RETRIES) {
        setGaveUp(true);
        return;
      }
      timers.push(setTimeout(() => run(n + 1), RETRY_INTERVAL_MS));
    };
    run(0);
    return () => {
      cancelled = true;
      timers.forEach(clearTimeout);
    };
  }, [nonce]);

  // 대기 화면이 떠 있는 동안 /health로 서버가 깼는지 본다. 한 번에 요청 하나.
  const pinging = wait != null && !apiUp && !res && !gaveUp && !failed && !holding;
  useEffect(() => {
    if (!pinging) return;
    const ctrl = new AbortController();
    let timer: ReturnType<typeof setTimeout> | undefined;
    const ping = async () => {
      try {
        const r = await pingHealth(ctrl.signal);
        if (r === "up") setApiUp(true);
        else if (r === "waking") timer = setTimeout(ping, HEALTH_RETRY_MS);
      } catch {
        // 취소됨
      }
    };
    ping();
    return () => {
      ctrl.abort();
      if (timer) clearTimeout(timer);
    };
  }, [pinging]);

  const reload = useCallback(() => {
    setRes(null);
    setWait(null);
    setApiUp(false);
    setHolding(false);
    setGaveUp(false);
    setFailed(false);
    setNonce((n) => n + 1);
  }, []);

  if (res) return <>{children(res, reload)}</>;
  if (failed || gaveUp)
    return (
      <div className="flex flex-col items-center py-10 text-center">
        <Mascot pose="error" size={180} />
        <p className="mt-4 text-title-sm text-ink">
          {failed ? "화면을 불러오지 못했어요" : "서버가 아직 깨어나지 않았어요"}
        </p>
        <p className="mt-1 text-ui text-ink-muted">잠시 뒤에 다시 시도해 주세요.</p>
        <Button className="mt-4" onClick={reload}>
          다시 시도
        </Button>
      </div>
    );
  return <WakeWait label={label} startedAt={wait?.startedAt ?? null} apiUp={apiUp} loaded={holding} />;
}
