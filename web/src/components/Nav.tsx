"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";

const LINKS = [
  { href: "/", label: "승인 대기함" },
  { href: "/runs", label: "실행 이력" },
  { href: "/inbox", label: "알림함" },
];

export function Nav() {
  const path = usePathname();
  const active = (href: string) => (href === "/" ? path === "/" || path.startsWith("/reviews") : path.startsWith(href));
  return (
    <nav className="mx-auto flex max-w-6xl gap-4 px-4 pt-3 text-sm font-medium text-slate-700">
      {LINKS.map((l) => (
        <Link
          key={l.href}
          href={l.href}
          aria-current={active(l.href) ? "page" : undefined}
          className={active(l.href) ? "border-b-2 border-slate-800 text-slate-900" : "hover:underline"}
        >
          {l.label}
        </Link>
      ))}
    </nav>
  );
}
