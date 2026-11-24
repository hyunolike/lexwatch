/**
 * 키보드 단축키 판정(DS2-09, design.md 3.6). 글자 키는 e.code로 본다.
 * 한글 입력기가 켜져 있으면 e.key가 "ㅓ"나 "Process"로 와서 e.key로는 판정할 수 없다.
 */
export type KeyLike = Pick<KeyboardEvent, "code" | "key" | "shiftKey" | "metaKey" | "ctrlKey" | "altKey" | "repeat" | "defaultPrevented"> & {
  isComposing?: boolean;
  keyCode?: number;
};

/** 한글 조합 중인 키 입력인가. 조합 중 Enter가 항목을 실행하면 안 된다. */
export function isComposingEvent(e: { isComposing?: boolean; keyCode?: number; nativeEvent?: { isComposing?: boolean } }): boolean {
  return !!(e.isComposing || e.nativeEvent?.isComposing || e.keyCode === 229);
}

const BLOCKING_ROLES = "[role=menu],[role=listbox],[role=combobox],[role=dialog],[role=alertdialog],[role=separator]";

/** 포커스 위치를 단축키 판정에 필요한 만큼만 요약한다. 판정(shouldHandle)을 DOM 없이 테스트하려고 나눴다. */
export type FocusSpot = "body" | "main-target" | "blocked";

export function focusSpot(active: Element | null): FocusSpot {
  if (!active || active.tagName === "BODY") return "body";
  if (active.closest(BLOCKING_ROLES) || !active.closest("main")) return "blocked";
  return active.matches("a, button, article[tabindex], main") ? "main-target" : "blocked";
}

/**
 * 수정자 없는 글자 단축키를 받을지. 본문이나 <main> 안의 링크, 버튼, 문장 카드에 포커스가 있을 때만 받는다.
 * 입력창, 메뉴, 대화상자, 분리선, main 밖에서는 그 요소의 키 동작을 지킨다.
 */
export function shouldHandle(e: KeyLike, spot: FocusSpot): boolean {
  if (e.defaultPrevented || e.repeat || isComposingEvent(e)) return false;
  if (e.metaKey || e.ctrlKey || e.altKey) return false;
  return spot !== "blocked";
}

/** ⌘K / Ctrl+K. 입력창 안에서도 받는다(조합 중은 제외). */
export function isPaletteKey(e: KeyLike): boolean {
  return !isComposingEvent(e) && e.code === "KeyK" && (e.metaKey || e.ctrlKey) && !e.altKey;
}

/** 이 화면이 받는 단축키 이름. e.code와 Shift로만 정한다. */
export type Shortcut = "next" | "prev" | "nextFlag" | "prevFlag" | "approve" | "edit" | "reject" | "help";

export function shortcutOf(e: KeyLike): Shortcut | null {
  if (e.code === "Slash" && e.shiftKey) return "help";
  if (e.shiftKey) return null;
  switch (e.code) {
    case "KeyJ":
      return "next";
    case "KeyK":
      return "prev";
    case "BracketRight":
      return "nextFlag";
    case "BracketLeft":
      return "prevFlag";
    case "KeyA":
      return "approve";
    case "KeyE":
      return "edit";
    case "KeyR":
      return "reject";
    default:
      return null;
  }
}

/** 도움말 대화상자와 /design-system이 같이 쓰는 표. */
export const SHORTCUT_HELP: { keys: string; what: string; where: string }[] = [
  { keys: "j / k", what: "다음, 이전 검토로 이동", where: "승인 대기함" },
  { keys: "Enter", what: "고른 검토 열기", where: "승인 대기함" },
  { keys: "] / [", what: "다음, 이전 확인 필요 문장", where: "검토 상세" },
  { keys: "a", what: "승인 확인 창 열기(바로 승인하지 않아요)", where: "검토 상세, 변호사와 파트너" },
  { keys: "e", what: "수정 시작, 수정 취소", where: "검토 상세, 변호사" },
  { keys: "r", what: "반려(파트너는 돌려보내기) 사유 입력", where: "검토 상세" },
  { keys: "⌘K / Ctrl+K", what: "명령 팔레트", where: "모든 화면" },
  { keys: "?", what: "이 도움말", where: "모든 화면" },
];

/** 순환 이동: 목록에서 현재 위치 다음(또는 이전). 현재가 없으면 처음(또는 끝). */
export function cycle<T>(items: T[], current: T | null, dir: 1 | -1): T | null {
  if (items.length === 0) return null;
  const i = current === null ? -1 : items.indexOf(current);
  if (i < 0) return dir === 1 ? items[0] : items[items.length - 1];
  return items[(i + dir + items.length) % items.length];
}
