"use client";
import { Popover as BasePopover } from "@base-ui/react/popover";
import { Tooltip as BaseTooltip } from "@base-ui/react/tooltip";
import type { ReactElement, ReactNode } from "react";

const POPUP =
  "origin-[var(--transform-origin)] transition-[opacity,scale] duration-[200ms] ease-out data-[ending-style]:scale-95 data-[ending-style]:opacity-0 data-[starting-style]:scale-95 data-[starting-style]:opacity-0";

/** 클릭이나 Enter로 열리는 팝오버. Esc로 닫히고 포커스가 트리거로 돌아온다. */
export function Popover({ trigger, label, children }: { trigger: ReactElement; label: string; children: ReactNode }) {
  return (
    <BasePopover.Root>
      <BasePopover.Trigger render={trigger} />
      <BasePopover.Portal>
        <BasePopover.Positioner sideOffset={6} className="z-40">
          <BasePopover.Popup
            aria-label={label}
            className={`w-[min(22rem,calc(100vw-2rem))] rounded-lg border border-line bg-surface p-4 shadow-pop ${POPUP}`}
          >
            {children}
          </BasePopover.Popup>
        </BasePopover.Positioner>
      </BasePopover.Portal>
    </BasePopover.Root>
  );
}

/** 마우스를 올리거나 포커스하면 뜨는 설명. 설명만 담고 동작은 담지 않는다. */
export function Tooltip({ trigger, children }: { trigger: ReactElement; children: ReactNode }) {
  return (
    <BaseTooltip.Root>
      <BaseTooltip.Trigger render={trigger} delay={300} />
      <BaseTooltip.Portal>
        <BaseTooltip.Positioner sideOffset={6} className="z-50">
          <BaseTooltip.Popup className={`max-w-xs rounded-md bg-ink px-3 py-2 text-caption text-on-brand shadow-pop ${POPUP}`}>
            {children}
          </BaseTooltip.Popup>
        </BaseTooltip.Positioner>
      </BaseTooltip.Portal>
    </BaseTooltip.Root>
  );
}
