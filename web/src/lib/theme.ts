/**
 * 테마 선택(design-system-v2 design.md 2.4). 저장 값은 system, light, dark 셋이고
 * <html data-theme>에는 늘 실제 테마(light 또는 dark)를 적는다. CSS는 이 속성 하나만 본다.
 */
export type ThemeChoice = "system" | "light" | "dark";
export type ThemeName = "light" | "dark";

export const THEME_KEY = "lw_theme";
export const THEME_CHOICES: readonly ThemeChoice[] = ["system", "light", "dark"];
export const THEME_LABEL: Record<ThemeChoice, string> = { system: "시스템", light: "밝게", dark: "어둡게" };
const QUERY = "(prefers-color-scheme: dark)";

export function parseChoice(raw: string | null | undefined): ThemeChoice {
  return raw === "light" || raw === "dark" || raw === "system" ? raw : "system";
}

export function resolveTheme(choice: ThemeChoice, systemDark: boolean): ThemeName {
  return choice === "system" ? (systemDark ? "dark" : "light") : choice;
}

/**
 * 첫 그림 전에 <head>에서 도는 스크립트. 위 두 함수와 같은 판정을 한다(theme.test.ts가 실제로 실행해 확인).
 * 저장소를 못 읽으면(사생활 보호 모드) 시스템 설정을 따른다. matchMedia까지 실패하면 서버가 그린 light가 남는다.
 */
export const THEME_SCRIPT = `(function(){try{var c=null;try{c=localStorage.getItem("${THEME_KEY}")}catch(e){}if(c!=="light"&&c!=="dark")c=matchMedia("${QUERY}").matches?"dark":"light";document.documentElement.setAttribute("data-theme",c)}catch(e){}})()`;

export function readChoice(): ThemeChoice {
  try {
    return parseChoice(localStorage.getItem(THEME_KEY));
  } catch {
    return "system";
  }
}

const listeners = new Set<() => void>();
let unwatch: (() => void) | null = null;

function paint(choice: ThemeChoice) {
  const mq = window.matchMedia(QUERY);
  document.documentElement.setAttribute("data-theme", resolveTheme(choice, mq.matches));
  unwatch?.();
  unwatch = null;
  if (choice === "system") {
    const onChange = () => document.documentElement.setAttribute("data-theme", resolveTheme("system", mq.matches));
    mq.addEventListener("change", onChange);
    unwatch = () => mq.removeEventListener("change", onChange);
  }
}

/** 고른 값을 저장하고 바로 적용한다. 저장이 안 되는 환경에서도 이번 화면에는 적용한다. */
export function applyTheme(choice: ThemeChoice) {
  try {
    localStorage.setItem(THEME_KEY, choice);
  } catch {
    /* 사생활 보호 모드 등: 저장 없이 적용만 */
  }
  paint(choice);
  listeners.forEach((l) => l());
}

/**
 * 처음 그린 뒤 한 번 부른다. 개발 모드 Strict Mode의 재마운트가 <html> 속성을 지울 수 있어 다시 적용하고
 * (Next.js 문서), 시스템 선택이면 운영체제 설정 변경을 따라가도록 감시를 건다.
 */
export function watchSystemTheme() {
  paint(readChoice());
}

export function subscribeTheme(l: () => void) {
  listeners.add(l);
  return () => listeners.delete(l);
}
