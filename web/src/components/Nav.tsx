import Link from "next/link";

export function Nav() {
  return (
    <nav className="mx-auto flex max-w-6xl gap-4 px-4 pt-3 text-sm font-medium text-slate-700">
      <Link href="/" className="hover:underline">승인 대기함</Link>
      <Link href="/runs" className="hover:underline">실행 이력</Link>
      <Link href="/inbox" className="hover:underline">알림함</Link>
    </nav>
  );
}
