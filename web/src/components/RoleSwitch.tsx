"use client";
import { useSyncExternalStore } from "react";
import { getRole, setRole, type Role } from "@/lib/api";

const listeners = new Set<() => void>();
const subscribe = (cb: () => void) => {
  listeners.add(cb);
  return () => listeners.delete(cb);
};

export function useRole(): Role {
  return useSyncExternalStore(subscribe, getRole, () => "lawyer" as Role);
}

export function RoleSwitch() {
  const role = useRole();
  const pick = (r: Role) => {
    setRole(r);
    listeners.forEach((l) => l());
  };
  const btn = (r: Role, label: string) => (
    <button
      onClick={() => pick(r)}
      className={`rounded border px-3 py-1 text-sm ${role === r ? "bg-slate-800 text-white" : "bg-white text-slate-700"}`}
    >
      {label}
    </button>
  );
  return (
    <div className="flex gap-2">
      {btn("lawyer", "변호사로 보기")}
      {btn("admin", "관리자로 보기")}
    </div>
  );
}
