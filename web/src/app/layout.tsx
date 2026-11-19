import { Suspense } from "react";
import type { Metadata } from "next";
import "pretendard/dist/web/variable/pretendardvariable-dynamic-subset.css";
import "./globals.css";
import { DemoBanner } from "@/components/DemoBanner";
import { Nav } from "@/components/Nav";

export const metadata: Metadata = {
  title: "LexWatch",
  description: "공포된 법령 개정의 영향 검토 초안을 확인하고 승인해요.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="ko" className="h-full antialiased">
      <body className="min-h-full">
        <DemoBanner />
        <Suspense fallback={null}>
          <Nav />
        </Suspense>
        <main className="mx-auto max-w-6xl p-4">{children}</main>
      </body>
    </html>
  );
}
