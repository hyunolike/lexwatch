import type { OldNew } from "@/lib/api";

export function OldNewDiff({ items }: { items: OldNew[] }) {
  if (items.length === 0) return <p className="text-sm text-slate-500">비교할 조문이 없어요.</p>;
  return (
    <div className="space-y-4">
      {items.map((it) => (
        <div key={it.articleNo} className="rounded border bg-white">
          <div className="border-b bg-slate-50 px-3 py-1 text-sm font-semibold">{it.articleNo}</div>
          <div className="grid grid-cols-2 gap-px bg-slate-200 text-sm">
            <div className="bg-white p-3">
              <div className="mb-1 text-xs font-semibold text-slate-500">구조문</div>
              <p className="whitespace-pre-wrap">{it.before || "—"}</p>
            </div>
            <div className="bg-green-50 p-3">
              <div className="mb-1 text-xs font-semibold text-slate-500">신조문</div>
              <p className="whitespace-pre-wrap">{it.after || "—"}</p>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
