"use client";
import { useLayoutEffect, useSyncExternalStore } from "react";
import { Menu } from "@base-ui/react/menu";
import { Check, Monitor, Moon, Sun, type LucideIcon } from "lucide-react";
import { applyTheme, readChoice, subscribeTheme, THEME_CHOICES, THEME_LABEL, watchSystemTheme, type ThemeChoice } from "@/lib/theme";

const ICON: Record<ThemeChoice, LucideIcon> = { system: Monitor, light: Sun, dark: Moon };

export function useThemeChoice(): ThemeChoice {
  return useSyncExternalStore(subscribeTheme, readChoice, () => "system");
}

/** 헤더의 테마 메뉴(DS2-01). 시스템, 밝게, 어둡게. 고르면 저장하고 바로 바꾼다. */
export function ThemeMenu() {
  const choice = useThemeChoice();
  // 첫 그림은 <head> 스크립트가 맞춰 두었다. 여기서는 Strict Mode 재적용과 시스템 설정 감시만 한다.
  useLayoutEffect(() => watchSystemTheme(), []);
  const Icon = ICON[choice];
  return (
    <Menu.Root>
      <Menu.Trigger
        aria-label={`화면 테마: ${THEME_LABEL[choice]}`}
        className="inline-flex size-8 items-center justify-center rounded-md text-ink-muted transition-colors duration-[120ms] hover:bg-surface-sunken hover:text-ink"
      >
        <Icon aria-hidden className="size-4" />
      </Menu.Trigger>
      <Menu.Portal>
        <Menu.Positioner sideOffset={6} align="end" className="z-40">
          <Menu.Popup className="min-w-36 rounded-lg border border-line bg-surface-raised p-1 shadow-pop">
            <Menu.RadioGroup value={choice} onValueChange={(v) => applyTheme(v as ThemeChoice)}>
              {THEME_CHOICES.map((c) => {
                const I = ICON[c];
                return (
                  <Menu.RadioItem
                    key={c}
                    value={c}
                    className="flex cursor-default items-center gap-2 rounded-md px-2 py-1.5 text-label text-ink outline-none data-[highlighted]:bg-surface-sunken"
                  >
                    <I aria-hidden className="size-4 text-ink-muted" />
                    <span className="flex-1">{THEME_LABEL[c]}</span>
                    <Menu.RadioItemIndicator>
                      <Check aria-hidden className="size-4 text-brand-fg" />
                    </Menu.RadioItemIndicator>
                  </Menu.RadioItem>
                );
              })}
            </Menu.RadioGroup>
          </Menu.Popup>
        </Menu.Positioner>
      </Menu.Portal>
    </Menu.Root>
  );
}
