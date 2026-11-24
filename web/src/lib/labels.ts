/**
 * 백엔드 값 → 한국어 라벨과 의미 색. 설계: docs/features/design-system/design.md 5.2절
 * 값 출처: backend/src/LexWatch.Api/Data/Entities/Enums.cs, Radar/BillRules.cs, NotifyStep.cs, ReviewEndpoints.cs, LexWatchDb.cs
 */

export type Tone = "success" | "danger" | "warning" | "info" | "neutral";
export type Domain = "run" | "review" | "step" | "notification" | "decision" | "role" | "channel" | "usage" | "billStage";
export type StatusLabel = { label: string; tone: Tone; known: boolean };

const TABLE: Record<Domain, Record<string, [string, Tone]>> = {
  run: {
    Queued: ["대기", "neutral"],
    Running: ["진행 중", "info"],
    InReview: ["검토 중", "info"],
    Completed: ["완료", "success"],
    Failed: ["실패", "danger"],
    ClosedIrrelevant: ["관련 없음", "neutral"],
  },
  review: {
    Analyzing: ["분석 중", "info"],
    NeedsReview: ["검토 필요", "warning"],
    WaitingApproval: ["승인 대기", "info"],
    Approved: ["승인", "success"],
    Rejected: ["반려", "danger"],
    Notified: ["알림 발송", "success"],
    ClosedIrrelevant: ["관련 없음", "neutral"],
    AwaitingPartner: ["파트너 승인 대기", "warning"],
  },
  step: {
    Pending: ["대기", "neutral"],
    Running: ["진행 중", "info"],
    Succeeded: ["성공", "success"],
    RetryScheduled: ["재시도 예정", "warning"],
    Failed: ["실패", "danger"],
    NeedsReview: ["검토 필요", "warning"],
  },
  notification: {
    pending: ["발송 대기", "neutral"],
    sent: ["발송 완료", "success"],
  },
  decision: {
    approve: ["승인", "success"],
    reject: ["반려", "danger"],
    confirm: ["파트너 최종 승인", "success"],
    return: ["파트너가 돌려보냄", "warning"],
  },
  role: {
    lawyer: ["변호사", "neutral"],
    partner: ["파트너", "neutral"],
    admin: ["관리자", "neutral"],
  },
  channel: {
    demo_inbox: ["그룹 알림함", "neutral"],
    admin: ["관리자 알림", "neutral"],
  },
  usage: {
    answered: ["답변", "success"],
    refused: ["거절", "warning"],
    unavailable: ["모델 없음", "neutral"],
    failed: ["실패", "danger"],
  },
  billStage: {
    Received: ["접수", "neutral"],
    Committee: ["위원회 심사", "info"],
    Judiciary: ["법사위 심사", "info"],
    Plenary: ["본회의 대기", "warning"],
    Passed: ["본회의 의결", "success"],
    Merged: ["대안에 반영", "success"],
    Closed: ["종료", "neutral"],
  },
};

/** 영역(domain)과 원래 값으로 라벨을 얻는다. 모르는 값은 원문을 neutral로 돌려준다(정보를 숨기지 않음). */
export function statusLabel(domain: Domain, value: string): StatusLabel {
  const hit = TABLE[domain][value];
  if (hit) return { label: hit[0], tone: hit[1], known: true };
  if (process.env.NODE_ENV === "development") console.warn(`[labels] 모르는 ${domain} 값: ${value}`);
  return { label: value, tone: "neutral", known: false };
}

const GROUPS: Record<string, string> = { labor: "노동", privacy: "개인정보", finance: "금융" };

/** 실무그룹 코드 → 이름. 모르면 원래 코드. */
export const groupLabel = (code: string) => GROUPS[code] ?? code;

const STEP_LABELS: Record<string, string> = {
  fetch_articles: "조문 수집",
  classify: "분류",
  index_articles: "조문 색인",
  analyze: "분석",
  critic_citation: "인용 검증",
  critic_source: "출처 검증",
  critic_judge: "크리틱 판정",
  notify: "알림",
};

/** 백엔드가 내는 단계 이름을 한국어로. 모르면 원래 이름. */
export const stepLabel = (s: string) => STEP_LABELS[s] ?? s;

/** 재현 데이터임을 알리는 문구. 정직성 표시라 화면에서 빼지 않는다. */
export const REPLAY_NOTE = "재현 예시예요. 실제 공포 법령이 아니에요.";

/** 출처 네 종류(design-system-v2 design.md 3.2). 누가 만들었나이고 품질 판정이 아니다. */
export type ProvKind = "ai" | "rule" | "human" | "replay" | "neutral";
export type Provenance = { kind: ProvKind; label: string; help: string };

const AI_VENDORS: Record<string, { name: string; help: string }> = {
  nvidia: { name: "NVIDIA", help: "AI가 만들었어요. NVIDIA Nemotron이 실제로 처리했어요." },
  cloudflare: { name: "Cloudflare", help: "AI가 만들었어요. Cloudflare Workers AI의 공개 모델(gpt-oss)이 처리했어요." },
  groq: { name: "Groq", help: "AI가 만들었어요. Cloudflare 무료 한도가 끝나 Groq의 공개 모델(gpt-oss)이 대신 처리했어요." },
};
const HUMAN_ROLES: Record<string, string> = { lawyer: "변호사", partner: "파트너", admin: "관리자" };

/**
 * 출처를 한 곳에서 정한다. SourceLabel, AiLabel, AiTrail, RunTimeline이 모두 이 함수를 쓴다.
 * - providerKind: API의 공급자 종류. none이나 빈 값은 그리지 않는다(null).
 * - providerName "guardrail": 입력 규칙이 모델 호출 전에 거절한 경우라 규칙이다.
 * - actor/role: AI 흔적 단계나 승인 기록. 사람은 역할 이름으로 적는다.
 */
export function provenanceOf(input: {
  providerKind?: string | null;
  providerName?: string | null;
  actor?: string | null;
  role?: string | null;
}): Provenance | null {
  const { providerKind: k, providerName, actor, role } = input;
  if (k && k !== "none") {
    if (k === "replay") return { kind: "replay", label: "재현", help: "녹화한 예시를 다시 보여 주는 데모 데이터예요." };
    if (k === "fallback")
      return providerName === "guardrail"
        ? { kind: "rule", label: "규칙", help: "입력 규칙이 모델에 보내기 전에 거절했어요." }
        : { kind: "rule", label: "규칙", help: "모델 대신 규칙 기반 대체 구현이 만들었어요. 품질 판정이 아니에요." };
    const v = AI_VENDORS[k];
    if (v) return { kind: "ai", label: `AI ${v.name}`, help: v.help };
    return { kind: "neutral", label: k, help: k };
  }
  if (actor === "replay") return { kind: "replay", label: "재현", help: "녹화한 예시를 다시 보여 주는 데모 데이터예요." };
  if (actor === "human") {
    const name = (role && HUMAN_ROLES[role]) || "사람";
    return { kind: "human", label: name, help: `${name}이 결정했어요.` };
  }
  if (actor === "ai") return { kind: "ai", label: "AI", help: "AI가 처리한 단계예요." };
  if (actor === "rule") return { kind: "rule", label: "규칙", help: "규칙으로 처리한 단계예요." };
  if (actor) return { kind: "neutral", label: actor, help: actor };
  return null;
}
