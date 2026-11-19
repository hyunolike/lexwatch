"use client";
import type { ReactNode } from "react";
import { Tabs } from "@/components/ui/Tabs";
import type { OldNew } from "@/lib/api";
import { redline, type Part, type Redline } from "@/lib/redline";

function Text({ parts }: { parts: Part[] }) {
  return (
    <p className="text-reading max-w-[40em] whitespace-pre-wrap text-ink">
      {parts.map((p, i) =>
        p.kind === "del" ? (
          <del key={i} className="bg-diff-del-bg text-diff-del decoration-diff-del">
            <span className="sr-only">삭제: </span>
            {p.text}
          </del>
        ) : p.kind === "ins" ? (
          <ins key={i} className="bg-diff-ins-bg text-diff-ins underline decoration-diff-ins decoration-2 underline-offset-2">
            <span className="sr-only">추가: </span>
            {p.text}
          </ins>
        ) : (
          <span key={i}>{p.text}</span>
        ),
      )}
    </p>
  );
}

const plain = (text: string): Part[] => [{ kind: "same", text }];

function Columns({ before, after, oldLabel, newLabel }: { before: ReactNode; after: ReactNode; oldLabel: ReactNode; newLabel: ReactNode }) {
  return (
    <>
      {/* 768px 이상: 두 칸. 서버와 브라우저 결과가 같도록 CSS로만 전환한다. */}
      <div className="hidden gap-px bg-line md:grid md:grid-cols-2">
        <div className="bg-surface-sunken p-4">
          <p className="mb-2 text-micro text-ink-subtle">{oldLabel}</p>
          {before}
        </div>
        <div className="bg-surface p-4">
          <p className="mb-2 text-micro text-ink-subtle">{newLabel}</p>
          {after}
        </div>
      </div>
      <div className="p-3 md:hidden">
        <Tabs
          label="구조문과 신조문"
          tabs={[
            { value: "new", title: newLabel, panel: after },
            { value: "old", title: oldLabel, panel: before },
          ]}
        />
      </div>
    </>
  );
}

function Status({ r }: { r: Redline }) {
  if (r.status !== "diff") return null;
  return (
    <span className="text-caption text-ink-subtle">
      변경 <span className="tabular-nums">{r.changes}</span>곳 · {r.source === "official" ? "법제처 표시" : "화면에서 비교"}
    </span>
  );
}

function Article({ item }: { item: OldNew }) {
  const r = redline(item);
  let body: ReactNode;
  switch (r.status) {
    case "diff":
      body = <Columns oldLabel="구조문" newLabel="신조문" before={<Text parts={r.old} />} after={<Text parts={r.new} />} />;
      break;
    case "added":
      body = <Columns oldLabel="구조문" newLabel="신조문 · 신설" before={<p className="text-ui text-ink-subtle">없음</p>} after={<Text parts={plain(item.after)} />} />;
      break;
    case "removed":
      body = <Columns oldLabel="구조문 · 삭제" newLabel="신조문" before={<Text parts={[{ kind: "del", text: item.before }]} />} after={<p className="text-ui text-ink-subtle">없음</p>} />;
      break;
    case "tooLong":
      body = (
        <>
          <p className="px-4 pt-3 text-caption text-ink-subtle">변경이 많아 강조하지 않았어요.</p>
          <Columns oldLabel="구조문" newLabel="신조문" before={<Text parts={plain(item.before)} />} after={<Text parts={plain(item.after)} />} />
        </>
      );
      break;
    case "same":
      body = <p className="p-4 text-ui text-ink-subtle">변경 없음</p>;
      break;
    case "empty":
      body = <p className="p-4 text-ui text-ink-subtle">비교할 내용이 없어요.</p>;
      break;
  }
  return (
    <article className="overflow-hidden rounded-lg border border-line bg-surface shadow-card">
      <header className="flex items-center justify-between gap-2 border-b border-line px-4 py-2">
        <h3 className="text-title-sm text-ink">{item.articleNo}</h3>
        <Status r={r} />
      </header>
      {body}
    </article>
  );
}

/** 신구조문 대조(DS-08). 조문마다 카드 하나. */
export function RedlineDiff({ items }: { items: OldNew[] }) {
  if (items.length === 0) return <p className="text-ui text-ink-subtle">비교할 조문이 없어요.</p>;
  return (
    <div className="space-y-4">
      {items.map((it) => (
        <Article key={it.articleNo} item={it} />
      ))}
    </div>
  );
}
