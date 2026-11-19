"use client";
import { Tabs as BaseTabs } from "@base-ui/react/tabs";
import type { ReactNode } from "react";

/** 탭. 좌우 화살표로 이동한다. */
export function Tabs({ label, tabs }: { label: string; tabs: { value: string; title: ReactNode; panel: ReactNode }[] }) {
  return (
    <BaseTabs.Root defaultValue={tabs[0]?.value}>
      <BaseTabs.List aria-label={label} className="relative flex gap-1 border-b border-line">
        {tabs.map((t) => (
          <BaseTabs.Tab
            key={t.value}
            value={t.value}
            className="h-9 px-3 text-label text-ink-muted transition-colors duration-[120ms] hover:text-ink data-[active]:text-ink"
          >
            {t.title}
          </BaseTabs.Tab>
        ))}
        <BaseTabs.Indicator className="absolute bottom-0 left-[var(--active-tab-left)] h-0.5 w-[var(--active-tab-width)] bg-brand transition-[left,width] duration-[200ms] ease-out" />
      </BaseTabs.List>
      {tabs.map((t) => (
        <BaseTabs.Panel key={t.value} value={t.value} className="pt-3">
          {t.panel}
        </BaseTabs.Panel>
      ))}
    </BaseTabs.Root>
  );
}
