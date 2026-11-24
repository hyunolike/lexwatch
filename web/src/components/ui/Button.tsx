"use client";
import { Button as BaseButton } from "@base-ui/react/button";
import { LoaderCircle } from "lucide-react";
import type { ComponentProps } from "react";
import { cva } from "class-variance-authority";
import { cx } from "@/lib/cx";

export type ButtonVariant = "primary" | "secondary" | "danger" | "danger-ghost" | "ghost";

/** 버튼 변형 표(DS2-15). 위험 버튼 글자는 on-danger: 어두운 테마의 밝은 빨강 위에는 흰 글자가 안 된다. */
export const buttonVariants = cva(
  [
    "inline-flex items-center justify-center gap-1.5 rounded-md text-label whitespace-nowrap",
    "transition-colors duration-[120ms] ease-out disabled:cursor-not-allowed disabled:opacity-50",
  ],
  {
    variants: {
      variant: {
        primary: "bg-brand text-on-brand shadow-card hover:bg-brand-hover",
        secondary: "border border-line-strong bg-surface text-ink hover:bg-surface-sunken",
        danger: "bg-danger text-on-danger hover:bg-danger/90",
        "danger-ghost": "text-danger hover:bg-danger-bg",
        ghost: "text-ink-muted hover:bg-surface-sunken hover:text-ink",
      } satisfies Record<ButtonVariant, string>,
      size: { sm: "h-8 px-3", md: "h-9 px-4" },
    },
    defaultVariants: { variant: "secondary", size: "md" },
  },
);

type Props = Omit<ComponentProps<typeof BaseButton>, "className"> & {
  variant?: ButtonVariant;
  size?: "sm" | "md";
  loading?: boolean;
  className?: string;
};

/** 버튼. 설계 4.2: 높이 32/36px, loading이면 회전 아이콘과 aria-busy, 비활성은 opacity-50 하나. */
export function Button({ variant = "secondary", size = "md", loading = false, disabled, className, children, ...rest }: Props) {
  return (
    <BaseButton
      {...rest}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      className={cx(buttonVariants({ variant, size }), className)}
    >
      {loading && <LoaderCircle aria-hidden className="size-4 animate-spin" />}
      {children}
    </BaseButton>
  );
}
