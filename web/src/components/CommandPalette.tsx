"use client";
import { useEffect, useMemo, useState, useSyncExternalStore } from "react";
import { useRouter } from "next/navigation";
import { Autocomplete } from "@base-ui/react/autocomplete";
import { Dialog } from "@base-ui/react/dialog";
import { Search } from "lucide-react";
import { inboxCache } from "@/lib/inboxCache";
import { isComposingEvent, isPaletteKey } from "@/lib/shortcuts";
import { applyTheme, THEME_LABEL, type ThemeChoice } from "@/lib/theme";
import type { ReviewSummary, Role } from "@/lib/api";
import { switchRole, useRole } from "@/components/RoleSwitch";

type Item = { id: string; label: string; hint?: string; run: () => void };
type Group = { value: string; items: Item[] };

const PAGES: { href: string; label: string; admin?: boolean }[] = [
  { href: "/", label: "승인 대기함" },
  { href: "/runs", label: "실행 이력" },
  { href: "/inbox", label: "알림함" },
  { href: "/radar", label: "법안 레이더" },
  { href: "/ops", label: "운영 현황", admin: true },
  { href: "/governance", label: "AI 사용 기록", admin: true },
  { href: "/design-system", label: "디자인 시스템" },
];
const ROLES: [Role, string][] = [
  ["lawyer", "변호사로 보기"],
  ["partner", "파트너로 보기"],
  ["admin", "관리자로 보기"],
];
const THEMES: ThemeChoice[] = ["system", "light", "dark"];
/** 서버 스냅샷은 같은 참조여야 한다. 새 배열을 매번 만들면 React가 무한 반복으로 본다. */
const NO_REVIEWS: ReviewSummary[] = [];

/** 팔레트를 여는 버튼(헤더)과 키 입력이 같은 상태를 연다. */
const openers = new Set<(v: boolean) => void>();
export const openCommandPalette = () => openers.forEach((f) => f(true));

/**
 * 명령 팔레트(DS2-10, design.md 3.7). ⌘K / Ctrl+K로 연다. 이동, 검토, 역할, 보기 네 묶음.
 * 검토 묶음은 대기함을 한 번 받은 결과(inboxCache)만 쓴다. 비어 있으면 그리지 않는다.
 */
export function CommandPalette() {
  const [open, setOpen] = useState(false);
  const router = useRouter();
  const role = useRole();
  const reviews = useSyncExternalStore(inboxCache.subscribe, inboxCache.get, () => NO_REVIEWS);

  useEffect(() => {
    openers.add(setOpen);
    const onKey = (e: KeyboardEvent) => {
      if (isPaletteKey(e)) {
        e.preventDefault();
        setOpen((v) => !v);
      }
    };
    window.addEventListener("keydown", onKey);
    return () => {
      openers.delete(setOpen);
      window.removeEventListener("keydown", onKey);
    };
  }, []);

  const groups = useMemo<Group[]>(() => {
    const go = (href: string) => () => router.push(href);
    const out: Group[] = [
      {
        value: "이동",
        items: PAGES.filter((p) => !p.admin || role === "admin").map((p) => ({ id: `page:${p.href}`, label: p.label, run: go(p.href) })),
      },
    ];
    if (reviews.length > 0)
      out.push({
        value: "검토",
        items: reviews.map((r) => ({ id: `review:${r.id}`, label: r.lawName, hint: r.groupName, run: go(`/reviews/${r.id}`) })),
      });
    out.push(
      { value: "역할", items: ROLES.map(([r, label]) => ({ id: `role:${r}`, label, run: () => switchRole(r) })) },
      { value: "보기", items: THEMES.map((t) => ({ id: `theme:${t}`, label: `테마: ${THEME_LABEL[t]}`, run: () => applyTheme(t) })) },
    );
    return out;
  }, [reviews, role, router]);

  const runItem = (item: Item) => {
    setOpen(false);
    item.run();
  };

  return (
    <Dialog.Root open={open} onOpenChange={setOpen}>
      <Dialog.Portal>
        <Dialog.Backdrop className="fixed inset-0 z-(--layer-modal) bg-scrim transition-opacity duration-[200ms] data-[ending-style]:opacity-0 data-[starting-style]:opacity-0" />
        <Dialog.Popup
          aria-label="명령 팔레트"
          className="fixed top-[12vh] left-1/2 z-(--layer-modal) w-[min(36rem,calc(100vw-2rem))] -translate-x-1/2 overflow-hidden rounded-lg border border-line bg-surface-raised shadow-dialog transition-[opacity,scale] duration-[200ms] ease-out data-[ending-style]:scale-95 data-[ending-style]:opacity-0 data-[starting-style]:scale-95 data-[starting-style]:opacity-0"
        >
          <Autocomplete.Root inline open autoHighlight items={groups} itemToStringValue={(i: Item) => `${i.label} ${i.hint ?? ""}`}>
            <label className="flex items-center gap-2 border-b border-line px-4">
              <Search aria-hidden className="size-4 text-ink-subtle" />
              <Autocomplete.Input
                placeholder="화면, 검토, 역할, 테마 찾기"
                aria-label="명령 검색"
                onKeyDown={(e) => {
                  // 한글 조합 중 Enter는 글자를 확정할 뿐 항목을 실행하지 않는다.
                  if (e.key === "Enter" && isComposingEvent(e)) (e as unknown as { preventBaseUIHandler?: () => void }).preventBaseUIHandler?.();
                }}
                className="h-12 w-full bg-transparent text-ui text-ink outline-none placeholder:text-ink-subtle"
              />
            </label>
            <Autocomplete.Empty className="px-4 py-6 text-center text-ui text-ink-subtle">찾는 항목이 없어요</Autocomplete.Empty>
            <Autocomplete.List className="max-h-[min(24rem,60vh)] overflow-y-auto p-2 outline-none">
              {(group: Group) => (
                <Autocomplete.Group key={group.value} items={group.items} className="mb-1 [&:not(:has([role=option]))]:hidden">
                  <Autocomplete.GroupLabel className="px-2 pt-2 pb-1 text-micro text-ink-subtle">{group.value}</Autocomplete.GroupLabel>
                  <Autocomplete.Collection>
                    {(item: Item) => (
                      <Autocomplete.Item
                        key={item.id}
                        value={item}
                        onClick={() => runItem(item)}
                        className="flex cursor-default items-center justify-between gap-3 rounded-md px-2 py-2 text-ui text-ink data-[highlighted]:bg-brand-subtle"
                      >
                        <span className="truncate">{item.label}</span>
                        {item.hint && <span className="shrink-0 text-caption text-ink-subtle">{item.hint}</span>}
                      </Autocomplete.Item>
                    )}
                  </Autocomplete.Collection>
                </Autocomplete.Group>
              )}
            </Autocomplete.List>
          </Autocomplete.Root>
          <p className="border-t border-line px-4 py-2 text-caption text-ink-subtle">
            ↑↓ 이동 · Enter 실행 · Esc 닫기 · <kbd className="font-mono">?</kbd> 단축키 도움말
          </p>
        </Dialog.Popup>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
