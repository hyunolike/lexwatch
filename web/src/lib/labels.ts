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
