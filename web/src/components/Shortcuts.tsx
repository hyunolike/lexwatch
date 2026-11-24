"use client";
import { useEffect, useRef, useState } from "react";
import { Dialog } from "@base-ui/react/dialog";
import { focusSpot, shortcutOf, shouldHandle, SHORTCUT_HELP, type Shortcut } from "@/lib/shortcuts";

/**
 * 화면 하나의 단축키를 건다(DS2-09). 보이는 버튼과 같은 처리 함수를 넘긴다. 명령 등록소는 두지 않는다.
 * 처리 함수가 없는 단축키(역할상 할 수 없는 동작)는 아무 일도 하지 않는다.
 */
export function useShortcuts(handlers: Partial<Record<Shortcut, () => void>>) {
  const ref = useRef(handlers);
  useEffect(() => {
    ref.current = handlers;
  });
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const s = shortcutOf(e);
      if (!s || s === "help") return;
      const run = ref.current[s];
      if (!run || !shouldHandle(e, focusSpot(document.activeElement))) return;
      e.preventDefault();
      run();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);
}

const POPUP =
  "fixed top-1/2 left-1/2 z-(--layer-modal) w-[min(32rem,calc(100vw-2rem))] -translate-x-1/2 -translate-y-1/2 rounded-lg border border-line bg-surface-raised p-6 shadow-dialog transition-[opacity,scale] duration-[200ms] ease-out data-[ending-style]:scale-95 data-[ending-style]:opacity-0 data-[starting-style]:scale-95 data-[starting-style]:opacity-0";

/** `?`로 여는 단축키 도움말. 모든 화면에 하나. */
export function ShortcutHelp() {
  const [open, setOpen] = useState(false);
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (shortcutOf(e) === "help" && shouldHandle(e, focusSpot(document.activeElement))) {
        e.preventDefault();
        setOpen(true);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);
  return (
    <Dialog.Root open={open} onOpenChange={setOpen}>
      <Dialog.Portal>
        <Dialog.Backdrop className="fixed inset-0 z-(--layer-modal) bg-scrim" />
        <Dialog.Popup className={POPUP}>
          <Dialog.Title className="font-serif text-title-md text-ink">키보드 단축키</Dialog.Title>
          <Dialog.Description className="mt-1 text-caption text-ink-muted">
            입력창에 글을 쓰는 동안과 한글을 조합하는 동안에는 단축키가 동작하지 않아요.
          </Dialog.Description>
          <table className="mt-4 w-full text-label">
            <tbody className="divide-y divide-line">
              {SHORTCUT_HELP.map((r) => (
                <tr key={r.keys}>
                  <td className="py-2 pr-4 whitespace-nowrap">
                    <kbd className="rounded-sm border border-line-strong bg-surface px-1.5 py-0.5 font-mono text-caption text-ink">{r.keys}</kbd>
                  </td>
                  <td className="py-2 text-ink">{r.what}</td>
                  <td className="py-2 pl-3 text-right text-caption text-ink-subtle">{r.where}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <Dialog.Close className="mt-5 inline-flex h-9 items-center rounded-md border border-line-strong bg-surface px-4 text-label text-ink hover:bg-surface-sunken">
            닫기
          </Dialog.Close>
        </Dialog.Popup>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
