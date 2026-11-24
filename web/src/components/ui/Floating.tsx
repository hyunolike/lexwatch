"use client";
import { Popover as BasePopover } from "@base-ui/react/popover";
import { Tooltip as BaseTooltip } from "@base-ui/react/tooltip";
import type { ReactElement, ReactNode } from "react";

const POPUP =
  "origin-[var(--transform-origin)] transition-[opacity,scale] duration-[200ms] ease-out data-[ending-style]:scale-95 data-[ending-style]:opacity-0 data-[starting-style]:scale-95 data-[starting-style]:opacity-0";

/**
 * 클릭이나 Enter로 열리는 팝오버. Esc로 닫히고 포커스가 트리거로 돌아온다.
 * hover가 참이면 마우스를 올려도 잠깐 열린다(인용 미리보기, DS2-08). 클릭하면 고정된다.
 */
export function Popover({
  trigger,
  label,
  children,
  hover = false,
}: {
  trigger: ReactElement;
  label: string;
  children: ReactNode;
  hover?: boolean;
}) {
  return (
    <BasePopover.Root>
      <BasePopover.Trigger render={trigger} openOnHover={hover} delay={300} closeDelay={150} />
      <BasePopover.Portal>
        <BasePopover.Positioner sideOffset={6} className="z-40">
          <BasePopover.Popup
            aria-label={label}
            className={`w-[min(22rem,calc(100vw-2rem))] rounded-lg border border-line bg-surface-raised p-4 shadow-pop ${POPUP}`}
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
        <BaseTooltip.Positioner sideOffset={6} className="z-(--layer-tooltip)">
          <BaseTooltip.Popup className={`max-w-xs rounded-md bg-ink px-3 py-2 text-caption text-canvas shadow-pop ${POPUP}`}>
            {children}
          </BaseTooltip.Popup>
        </BaseTooltip.Positioner>
      </BaseTooltip.Portal>
    </BaseTooltip.Root>
  );
}
