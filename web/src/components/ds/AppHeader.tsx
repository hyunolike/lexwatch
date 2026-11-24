"use client";
import Link from "next/link";
import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";
import { CircleHelp } from "lucide-react";
import { RoleSwitch, useRole } from "@/components/RoleSwitch";
import { stepForVisit } from "@/lib/guide";
import { guideStore } from "@/lib/guideStore";
import { cx } from "@/lib/cx";
import { Mascot } from "./layout";

const LINKS = [
  { href: "/", label: "승인 대기함" },
  { href: "/runs", label: "실행 이력" },
  { href: "/inbox", label: "알림함" },
  { href: "/radar", label: "법안 레이더" },
];
/** 관리자에게만 보이는 메뉴(ai-governance GV-06). */
const ADMIN_LINKS = [{ href: "/governance", label: "AI 사용 기록" }];

/**
 * 공통 머리: 데모 안내 띠, 로고, 내비게이션, 둘러보기, 역할 전환.
 * 경로와 역할이 바뀔 때 처음 방문 안내의 열람 단계를 완료로 기록한다(first-visit-guide FG-04).
 * 경로를 읽으므로 layout.tsx에서 <Suspense>로 감싼다(cacheComponents).
 */
export function AppHeader() {
  const path = usePathname();
  const role = useRole();
  const router = useRouter();
  useEffect(() => {
    const step = stepForVisit(path, role);
    if (step) guideStore.markStep(step);
  }, [path, role]);
  const active = (href: string) => (href === "/" ? path === "/" || path.startsWith("/reviews") : path.startsWith(href));
  return (
    <header className="sticky top-0 z-30">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-50 focus:rounded-md focus:bg-surface focus:px-3 focus:py-2 focus:text-label focus:shadow-pop"
      >
        본문으로 건너뛰기
      </a>
      <p className="flex h-7 items-center justify-center bg-warning-bg px-4 text-caption text-warning">
        데모 데이터는 매일 새벽 초기화됩니다.
      </p>
      <div className="border-b border-line bg-surface/95 backdrop-blur">
        <div className="mx-auto flex max-w-workspace flex-wrap items-center gap-x-6 gap-y-2 px-4 py-2">
          <Link href="/" className="flex items-center gap-2 rounded-md">
            <Mascot pose="avatar" size={32} className="rounded-full bg-surface-sunken" />
            <span className="text-title-sm text-brand">LexWatch</span>
          </Link>
          <nav aria-label="주 메뉴" className="order-3 flex w-full gap-1 sm:order-none sm:w-auto">
            {[...LINKS, ...(role === "admin" ? ADMIN_LINKS : [])].map((l) => (
              <Link
                key={l.href}
                href={l.href}
                aria-current={active(l.href) ? "page" : undefined}
                className={cx(
                  "relative rounded-md px-3 py-1.5 text-label transition-colors duration-[120ms]",
                  active(l.href)
                    ? "bg-surface-sunken text-ink after:absolute after:inset-x-3 after:-bottom-[9px] after:h-0.5 after:bg-brand"
                    : "text-ink-muted hover:bg-surface-sunken hover:text-ink",
                )}
              >
                {l.label}
              </Link>
            ))}
          </nav>
          <div className="ml-auto flex items-center gap-2">
            <button
              type="button"
              aria-label="둘러보기 안내 열기"
              onClick={() => {
                guideStore.reopen();
                if (path !== "/") router.push("/");
              }}
              className="inline-flex h-8 items-center gap-1.5 rounded-md px-2 text-label text-ink-muted transition-colors duration-[120ms] hover:bg-surface-sunken hover:text-ink"
            >
              <CircleHelp aria-hidden className="size-4" />
              <span aria-hidden className="hidden sm:inline">둘러보기</span>
            </button>
            <RoleSwitch />
          </div>
        </div>
      </div>
    </header>
  );
}
