"use client";
import { Toast } from "@base-ui/react/toast";
import { X } from "lucide-react";
import type { ReactNode } from "react";

/**
 * 토스트(DS2-11). 화면에 바로 보이지 않는 결과(재실행 접수)만 알린다. 5초, 최대 3개, 화면 읽기 프로그램이 읽는다.
 * 사용자가 고쳐야 하는 결과(실패, 권한 없음)는 화면 위 Callout으로 남긴다.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  return (
    <Toast.Provider limit={3} timeout={5000}>
      {children}
      <Toast.Portal>
        <Toast.Viewport className="fixed right-4 bottom-4 z-(--layer-tooltip) flex w-[min(22rem,calc(100vw-2rem))] flex-col gap-2 outline-none">
          <ToastList />
        </Toast.Viewport>
      </Toast.Portal>
    </Toast.Provider>
  );
}

function ToastList() {
  const { toasts } = Toast.useToastManager();
  return toasts.map((t) => (
    <Toast.Root
      key={t.id}
      toast={t}
      className="rounded-lg border border-line bg-surface-raised p-3 pr-9 shadow-pop transition-[opacity,translate] duration-[200ms] ease-out data-[ending-style]:translate-y-2 data-[ending-style]:opacity-0 data-[starting-style]:translate-y-2 data-[starting-style]:opacity-0"
    >
      <Toast.Content>
        <Toast.Title className="text-label font-semibold text-ink" />
        <Toast.Description className="mt-0.5 text-caption text-ink-muted" />
      </Toast.Content>
      <Toast.Close aria-label="알림 닫기" className="absolute top-2 right-2 inline-flex size-6 items-center justify-center rounded-sm text-ink-subtle hover:bg-surface-sunken hover:text-ink">
        <X aria-hidden className="size-3.5" />
      </Toast.Close>
    </Toast.Root>
  ));
}

/** 토스트 하나를 띄운다. 우선순위를 높여 화면 읽기 프로그램이 바로 읽게 한다. */
export function useToast() {
  const m = Toast.useToastManager();
  return { add: (title: string, description?: string) => m.add({ title, description, priority: "high" }) };
}
