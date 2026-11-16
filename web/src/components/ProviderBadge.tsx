import type { ProviderKind } from "@/lib/api";

const STYLES: Record<Exclude<ProviderKind, "none">, { label: string; cls: string }> = {
  nvidia: { label: "NVIDIA", cls: "bg-green-100 text-green-800" },
  fallback: { label: "대체 구현", cls: "bg-orange-100 text-orange-800" },
  replay: { label: "재현", cls: "bg-gray-200 text-gray-700" },
};

export function ProviderBadge({ kind }: { kind: ProviderKind }) {
  if (kind === "none" || !(kind in STYLES)) return null;
  const s = STYLES[kind];
  return <span className={`rounded px-2 py-0.5 text-xs font-semibold ${s.cls}`}>{s.label}</span>;
}
