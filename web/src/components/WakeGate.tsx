"use client";
import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { MAX_RETRIES, RETRY_INTERVAL_MS, type ApiResult } from "@/lib/api";

type Ready<T> = { status: number; data: T };

export function WakeGate<T>({
  load,
  children,
}: {
  load: () => Promise<ApiResult<T>>;
  children: (res: Ready<T>, reload: () => void) => ReactNode;
}) {
  const [res, setRes] = useState<Ready<T> | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [gaveUp, setGaveUp] = useState(false);
  const [failed, setFailed] = useState(false);
  const [nonce, setNonce] = useState(0);
  const loadRef = useRef(load);
  useEffect(() => {
    loadRef.current = load;
  });

  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
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
        setRes({ status: r.status, data: r.data });
        setGaveUp(false);
        return;
      }
      setAttempt(n + 1);
      if (n + 1 >= MAX_RETRIES) {
        setGaveUp(true);
        return;
      }
      timer = setTimeout(() => run(n + 1), RETRY_INTERVAL_MS);
    };
    run(0);
    return () => {
      cancelled = true;
      if (timer) clearTimeout(timer);
    };
  }, [nonce]);

  const reload = useCallback(() => {
    setGaveUp(false);
    setFailed(false);
    setNonce((n) => n + 1);
  }, []);

  if (res) return <>{children(res, reload)}</>;
  if (failed)
    return (
      <div className="p-8 text-center">
        <p>화면을 불러오지 못했어요.</p>
        <button onClick={reload} className="mt-3 rounded border px-3 py-1">다시 시도</button>
      </div>
    );
  if (gaveUp)
    return (
      <div className="p-8 text-center">
        <p>서버가 아직 깨어나지 않았어요. 잠시 뒤에 다시 시도해 주세요.</p>
        <button onClick={reload} className="mt-3 rounded border px-3 py-1">다시 시도</button>
      </div>
    );
  return (
    <div className="p-8 text-center text-slate-600">
      <p className="text-lg font-medium">서버를 깨우는 중이에요</p>
      <p className="mt-1 text-sm">무료 서버라 첫 접속은 1분쯤 걸릴 수 있어요. ({attempt}/{MAX_RETRIES})</p>
    </div>
  );
}
