/**
 * 평면 로고 마크(DS2-14). 접힌 문서 위에 저울 기둥. 색은 currentColor라 두 테마에서 글자색을 따른다.
 * 장식이라 aria-hidden이고, 옆의 "LexWatch" 글자가 이름을 말한다.
 */
export function LogoMark({ size = 24, className }: { size?: number; className?: string }) {
  return (
    <svg
      aria-hidden
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.6}
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
    >
      <path d="M6 2.75h8.5L19 7.25v14H6z" />
      <path d="M14.5 2.75v4.5H19" />
      <path d="M12.5 10v8M9.5 18h6" />
      <path d="M9 11.5h7" />
      <path d="M9 11.5l-1.5 3.2a1.6 1.6 0 0 0 3 0zM16 11.5l-1.5 3.2a1.6 1.6 0 0 0 3 0z" />
    </svg>
  );
}
