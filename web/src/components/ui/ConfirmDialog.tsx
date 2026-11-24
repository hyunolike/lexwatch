"use client";
import { AlertDialog } from "@base-ui/react/alert-dialog";
import { useRef, useState, type ReactElement } from "react";
import { Button } from "./Button";

type Props = {
  /** 대화상자를 여는 요소. Base UI가 이 요소를 트리거로 감싼다. */
  trigger: ReactElement;
  title: string;
  description: string;
  confirmLabel: string;
  cancelLabel?: string;
  /** 확정 동작. 끝나면 대화상자를 닫는다. */
  onConfirm: () => Promise<void> | void;
};

/**
 * 되돌릴 수 없는 동작을 한 번 더 확인한다(DS-10). 열리면 포커스가 "취소"에 있고 Esc로 닫힌다.
 */
export function ConfirmDialog({ trigger, title, description, confirmLabel, cancelLabel = "취소", onConfirm }: Props) {
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const cancelRef = useRef<HTMLButtonElement>(null);

  async function confirm() {
    setBusy(true);
    try {
      await onConfirm();
    } finally {
      setBusy(false);
      setOpen(false);
    }
  }

  return (
    <AlertDialog.Root open={open} onOpenChange={(o) => !busy && setOpen(o)}>
      <AlertDialog.Trigger render={trigger} />
      <AlertDialog.Portal>
        <AlertDialog.Backdrop className="fixed inset-0 z-(--layer-modal) bg-scrim transition-opacity duration-[240ms] ease-out data-[ending-style]:opacity-0 data-[starting-style]:opacity-0" />
        <AlertDialog.Popup
          initialFocus={cancelRef}
          className="fixed top-1/2 left-1/2 z-(--layer-modal) max-h-[calc(100dvh-2rem)] w-[min(28rem,calc(100vw-2rem))] -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-lg border border-line bg-surface-raised p-6 shadow-dialog transition-[opacity,scale] duration-[240ms] ease-out data-[ending-style]:scale-95 data-[ending-style]:opacity-0 data-[starting-style]:scale-95 data-[starting-style]:opacity-0"
        >
          <AlertDialog.Title className="text-title-sm text-ink">{title}</AlertDialog.Title>
          <AlertDialog.Description className="mt-2 text-ui text-ink-muted">{description}</AlertDialog.Description>
          <div className="mt-6 flex justify-end gap-2">
            <AlertDialog.Close render={<Button ref={cancelRef} variant="secondary" disabled={busy} />}>{cancelLabel}</AlertDialog.Close>
            <Button variant="primary" loading={busy} onClick={confirm}>
              {confirmLabel}
            </Button>
          </div>
        </AlertDialog.Popup>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  );
}
