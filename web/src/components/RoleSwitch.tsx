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

/** 역할을 바꾸고 구독자에게 알린다. 역할 전환 버튼과 처음 방문 안내 3단계가 같이 쓴다. */
export function switchRole(r: Role) {
  setRole(r);
  listeners.forEach((l) => l());
}

const ROLES: [Role, string, string][] = [
  ["lawyer", "변호사로 보기", "변호사"],
  ["partner", "파트너로 보기", "파트너"],
  ["admin", "관리자로 보기", "관리자"],
];

/** 역할 전환(로그인 없음). 세 칸짜리 선택 버튼. 파트너는 적발이 남은 승인을 최종 승인한다(ai-governance). */
export function RoleSwitch() {
  const role = useRole();
  return (
    <div role="group" aria-label="보기 역할" className="inline-flex rounded-md border border-line bg-surface-sunken p-0.5">
      {ROLES.map(([r, label, short]) => (
        <button
          key={r}
          type="button"
          aria-pressed={role === r}
          aria-label={label}
          onClick={() => switchRole(r)}
          className={cx(
            "h-7 rounded-[6px] px-3 text-label transition-colors duration-[120ms]",
            role === r ? "bg-surface text-ink shadow-card" : "text-ink-muted hover:text-ink",
          )}
        >
          <span className="sm:hidden">{short}</span>
          <span className="hidden sm:inline">{label}</span>
        </button>
      ))}
    </div>
  );
}
