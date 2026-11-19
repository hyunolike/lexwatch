"use client";
import { useSyncExternalStore } from "react";
import { getRole, setRole, type Role } from "@/lib/api";
import { cx } from "@/lib/cx";

const listeners = new Set<() => void>();
const subscribe = (cb: () => void) => {
  listeners.add(cb);
  return () => listeners.delete(cb);
};

export function useRole(): Role {
  return useSyncExternalStore(subscribe, getRole, () => "lawyer" as Role);
}

const ROLES: [Role, string][] = [
  ["lawyer", "변호사로 보기"],
  ["admin", "관리자로 보기"],
];

/** 역할 전환(로그인 없음). 두 칸짜리 선택 버튼. */
export function RoleSwitch() {
  const role = useRole();
  const pick = (r: Role) => {
    setRole(r);
    listeners.forEach((l) => l());
  };
  return (
    <div role="group" aria-label="보기 역할" className="inline-flex rounded-md border border-line bg-surface-sunken p-0.5">
      {ROLES.map(([r, label]) => (
        <button
          key={r}
          type="button"
          aria-pressed={role === r}
          aria-label={label}
          onClick={() => pick(r)}
          className={cx(
            "h-7 rounded-[6px] px-3 text-label transition-colors duration-[120ms]",
            role === r ? "bg-surface text-ink shadow-card" : "text-ink-muted hover:text-ink",
          )}
        >
          <span className="sm:hidden">{r === "lawyer" ? "변호사" : "관리자"}</span>
          <span className="hidden sm:inline">{label}</span>
        </button>
      ))}
    </div>
  );
}
