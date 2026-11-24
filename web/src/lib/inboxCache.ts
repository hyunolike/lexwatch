import type { ReviewSummary } from "./api";

/**
 * 대기함 응답을 담아 두는 저장소(DS2-10). 대기함 화면이 받은 결과를 넣고 명령 팔레트가 검토 검색에 쓴다.
 * 서버를 따로 부르지 않으므로, 대기함을 아직 안 열었으면 비어 있다.
 */
let items: ReviewSummary[] = [];
const listeners = new Set<() => void>();

export const inboxCache = {
  get: () => items,
  set(next: ReviewSummary[]) {
    items = next;
    listeners.forEach((l) => l());
  },
  subscribe(l: () => void) {
    listeners.add(l);
    return () => listeners.delete(l);
  },
};
