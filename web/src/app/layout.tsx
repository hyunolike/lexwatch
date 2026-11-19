import { Suspense } from "react";
import type { Metadata } from "next";
import Link from "next/link";
import "pretendard/dist/web/variable/pretendardvariable-dynamic-subset.css";
import "./globals.css";
import { AppHeader } from "@/components/ds/AppHeader";

export const metadata: Metadata = {
  title: "LexWatch",
  description: "공포된 법령 개정의 영향 검토 초안을 확인하고 승인해요.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="ko" className="h-full">
      <body className="min-h-full">
        <Suspense fallback={<div className="h-[77px]" />}>
          <AppHeader />
        </Suspense>
        <main id="main" tabIndex={-1} className="mx-auto max-w-workspace px-4 py-6 outline-none sm:py-8">
          {children}
        </main>
        <footer className="mx-auto max-w-workspace px-4 pb-8 text-caption text-ink-subtle">
          <Link href="/design-system" className="rounded-sm underline underline-offset-2 hover:text-ink">
            디자인 시스템
          </Link>
        </footer>
      </body>
    </html>
  );
}
