/** 서버 깨우는 동안의 대기 화면(wake-wait)에서 쓰는 순수 함수와 문구. */

/** 시간 막대의 끝. 화면 문구 "보통 30초에서 1분"과 맞춘다. */
export const EXPECTED_MS = 60_000;
/** 이보다 빨리 응답이 오면 대기 화면을 보이지 않는다(깜박임 방지). */
export const SHOW_DELAY_MS = 400;
/** 이야기 카드가 넘어가는 간격. */
export const TIP_INTERVAL_MS = 7_000;
/** /health를 다시 부르는 간격. */
export const HEALTH_RETRY_MS = 3_000;
/** 데이터가 오면 두 단계가 모두 완료된 모습을 이만큼 보인 뒤 넘어간다. */
export const DONE_HOLD_MS = 600;

export type StepState = "done" | "active" | "todo";

/** 두 단계(서버 깨우기, 데이터 불러오기)의 상태. 완료는 실제 응답으로만 정한다. */
export function steps(apiUp: boolean, loaded: boolean): [StepState, StepState] {
  if (loaded) return ["done", "done"];
  if (apiUp) return ["done", "active"];
  return ["active", "todo"];
}

/** 시간 막대 비율(0~1). 일의 양이 아니라 경과 시간이며 EXPECTED_MS에서 1로 멈춘다. */
export function timeFraction(elapsedMs: number): number {
  if (!(elapsedMs > 0)) return 0;
  return Math.min(1, elapsedMs / EXPECTED_MS);
}

export function isSlow(elapsedMs: number): boolean {
  return elapsedMs >= EXPECTED_MS;
}

/** 경과 시간 문구. "12초째", "1분째", "1분 15초째". */
export function elapsedText(elapsedMs: number): string {
  const s = Math.max(0, Math.floor(elapsedMs / 1000));
  if (s < 60) return `${s}초째`;
  const m = Math.floor(s / 60);
  const r = s % 60;
  return r ? `${m}분 ${r}초째` : `${m}분째`;
}

/** 안내 문장. 60초가 지나면 바뀌고, 화면 읽기 프로그램에도 이 문장으로 알린다. */
export function waitHint(elapsedMs: number): string {
  return isSlow(elapsedMs) ? "평소보다 오래 걸리고 있어요. 조금만 더 기다려 주세요." : "보통 30초에서 1분 걸려요.";
}

/** 렉스 잠꼬대. 진행 상황이나 남은 시간처럼 읽히는 말은 넣지 않는다. */
export const SNORES: readonly string[] = [
  "음냐… 조금만 더…",
  "법전 베개가 너무 푹신해요",
  "제1조… 목적… 쿨쿨…",
  "꿈에서 판례를 읽고 있어요",
  "커피 향이 나는 것 같아요",
  "누가 저를 부르나요… 쿨쿨",
];

/** 누른 횟수에 맞는 잠꼬대. 0이면 아직 누르지 않은 상태의 안내 문구. */
export function snoreFor(taps: number): string {
  if (taps <= 0) return "렉스를 눌러 보세요";
  return SNORES[(taps - 1) % SNORES.length];
}

/** 이야기 카드. README와 코드로 확인되는 사실만 쓴다(WW-07). */
export const TIPS: readonly { title: string; body: string }[] = [
  {
    title: "LexWatch가 하는 일",
    body: "매일 아침 국가법령정보센터에서 새로 공포된 법령 개정을 모아 노동, 개인정보, 금융 분야별로 나눠요.",
  },
  {
    title: "승인해야 나가요",
    body: "AI 초안은 변호사가 승인해야만 팀에 알림으로 나가요. 승인 직전에 인용과 숫자를 한 번 더 확인해요.",
  },
  {
    title: "자동 검사는 세 번",
    body: "AI가 쓴 초안은 문장마다 근거를 달았는지, 숫자와 날짜가 근거와 맞는지, 근거에 없는 내용을 단정하지 않았는지 차례로 검사해요.",
  },
  {
    title: "AI가 막히면",
    body: "NVIDIA, Cloudflare, Groq 순서로 불러요. 한 곳이 한도에 걸리거나 멈추면 다음 곳이 같은 요청을 이어받아요.",
  },
  { title: "이 화면의 체크 표시", body: "단계 옆 체크는 서버가 실제로 답했을 때만 켜져요." },
];

export function nextTip(i: number, dir: 1 | -1 = 1): number {
  return (i + dir + TIPS.length) % TIPS.length;
}

/** 서버 상태(design.md 7절 M9). 첫 /health 응답 전은 unknown. */
export type Health = "unknown" | "up" | "waking";

/**
 * 데이터를 기다리는 동안 무엇을 보일지. 스켈레톤(fallback)을 준 화면은 서버가 잠들었다고 확인될 때만
 * 렉스 대기 화면을 보이고, 서버가 깨어 있거나 아직 모를 때는 스켈레톤을 보인다. 그래서 렉스는 잠든 서버에만 나온다.
 * 스켈레톤이 없는 화면은 지금처럼 SHOW_DELAY_MS 뒤 대기 화면, 그 전에는 아무것도 보이지 않는다.
 */
export function gateView(input: { hasFallback: boolean; health: Health; waitShown: boolean }): "skeleton" | "wake" | "blank" {
  if (input.hasFallback) return input.health === "waking" ? "wake" : "skeleton";
  return input.waitShown ? "wake" : "blank";
}
