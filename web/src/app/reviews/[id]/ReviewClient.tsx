"use client";
import Link from "next/link";
import { useState } from "react";
import { ArrowLeft } from "lucide-react";
import { apiFetch, decisionActions, submitDecision, type ApiError, type EditedSentence, type ReviewDetail } from "@/lib/api";
import { REPLAY_NOTE } from "@/lib/labels";
import { DraftEditor } from "@/components/DraftEditor";
import { ReviewChat } from "@/components/ReviewChat";
import { SourceLabel, StatusLozenge } from "@/components/ds/badges";
import { Callout } from "@/components/ds/Callout";
import { RedlineDiff } from "@/components/ds/RedlineDiff";
import { CARD, SummaryList } from "@/components/ds/layout";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { useRole } from "@/components/RoleSwitch";
import { WakeGate } from "@/components/WakeGate";

type Notice = { tone: "warning" | "danger"; text: string; conflict?: boolean } | null;

function Detail({ review, reload }: { review: ReviewDetail; reload: () => void }) {
  const role = useRole();
  const draft = review.draft;
  const initial = () => draft?.sentences.map((s) => ({ sentence: s.sentence, citations: s.citations })) ?? [];
  const [editing, setEditing] = useState(false);
  const [edited, setEdited] = useState<EditedSentence[]>(initial);
  const [rejecting, setRejecting] = useState(false);
  const [comment, setComment] = useState("");
  const [notice, setNotice] = useState<Notice>(null);
  const [busy, setBusy] = useState(false);

  const actions = decisionActions(role, review.status, draft != null);
  const kinds =
    review.isReplay && !review.providerKinds.includes("replay") ? [...review.providerKinds, "replay" as const] : review.providerKinds;

  async function decide(kind: "approve" | "reject") {
    setBusy(true);
    setNotice(null);
    const out = await submitDecision(review.id, kind, review.version, {
      editedSentences: editing ? edited : undefined,
      comment,
    });
    setBusy(false);
    if (out.kind === "waking") return setNotice({ tone: "warning", text: "서버를 깨우는 중이에요. 잠시 뒤에 다시 눌러 주세요." });
    if (out.kind === "conflict") return setNotice({ tone: "danger", text: "다른 사람이 먼저 결정했어요.", conflict: true });
    if (out.kind === "error") return setNotice({ tone: "danger", text: out.message });
    reload();
  }

  return (
    <div className="space-y-5">
      <Link href="/" className="inline-flex items-center gap-1 rounded-md text-label text-ink-muted hover:text-ink">
        <ArrowLeft aria-hidden className="size-4" /> 승인 대기함
      </Link>

      <header className="space-y-3">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-title-md text-ink">{review.lawName}</h1>
          <StatusLozenge domain="review" value={review.status} />
          {kinds.map((k) => (
            <SourceLabel key={k} kind={k} />
          ))}
        </div>
        {review.isReplay && <Callout tone="info">{REPLAY_NOTE}</Callout>}
        <div className={`${CARD} p-4`}>
          <SummaryList
            rows={[
              { term: "실무그룹", value: review.groupName },
              { term: "공포일", value: review.isReplay ? "재현 예시" : review.promulgationDate },
              { term: "초안", value: draft ? `v${draft.version} · ${draft.createdBy === "agent" ? "에이전트 작성" : draft.createdBy}` : "없음" },
              { term: "근거", value: `${review.evidence.length}건` },
            ]}
          />
        </div>
      </header>

      {notice && (
        <Callout tone={notice.tone} live>
          <span className="flex flex-wrap items-center gap-3">
            {notice.text}
            {notice.conflict && (
              <Button size="sm" onClick={() => { setNotice(null); reload(); }}>
                새로고침
              </Button>
            )}
          </span>
        </Callout>
      )}

      <div className="grid gap-5 lg:grid-cols-2">
        <section aria-labelledby="oldnew" className="space-y-3">
          <h2 id="oldnew" className="text-title-sm text-ink">신구조문 비교</h2>
          <RedlineDiff items={review.oldNew} />
        </section>
        <section aria-labelledby="draft" className="space-y-3">
          <h2 id="draft" className="text-title-sm text-ink">영향 검토 초안</h2>
          {draft ? (
            <DraftEditor draft={draft} evidence={review.evidence} editing={editing} edited={edited} onChange={setEdited} />
          ) : (
            <Callout tone="warning">아직 초안이 없어요. 근거가 부족하면 사유를 적어 반려할 수 있어요.</Callout>
          )}
        </section>
      </div>

      {actions.reject && (
        <section aria-label="결정" className={`${CARD} sticky bottom-4 space-y-3 p-4 shadow-pop`}>
          <div className="flex flex-wrap items-center gap-2">
            {actions.approve && !editing && (
              <ConfirmDialog
                trigger={<Button variant="primary" disabled={busy}>승인</Button>}
                title="승인할까요?"
                description={`${review.groupName} 그룹 알림함으로 안내문이 발송돼요. 발송 뒤에는 되돌릴 수 없어요.`}
                confirmLabel="승인하고 알림 보내기"
                onConfirm={() => decide("approve")}
              />
            )}
            {actions.edit && editing && (
              <ConfirmDialog
                trigger={<Button variant="primary" disabled={busy}>수정 후 승인</Button>}
                title="수정한 내용으로 승인할까요?"
                description={`수정 전과 후가 함께 기록되고, ${review.groupName} 그룹 알림함으로 안내문이 발송돼요.`}
                confirmLabel="수정 내용으로 승인"
                onConfirm={() => decide("approve")}
              />
            )}
            {actions.edit && (
              <Button
                disabled={busy}
                onClick={() => {
                  if (editing) setEdited(initial());
                  setEditing((e) => !e);
                }}
              >
                {editing ? "수정 취소" : "수정"}
              </Button>
            )}
            <Button variant="danger-ghost" disabled={busy} aria-expanded={rejecting} onClick={() => setRejecting((r) => !r)}>
              반려
            </Button>
          </div>
          {rejecting && (
            <div className="space-y-2">
              <label htmlFor="reject-reason" className="text-label text-ink">
                반려 사유 <span className="text-danger">(필수)</span>
              </label>
              <textarea
                id="reject-reason"
                value={comment}
                onChange={(e) => setComment(e.target.value)}
                rows={2}
                className="w-full rounded-md border border-line-strong bg-surface p-2 text-ui text-ink"
              />
              <Button variant="danger" loading={busy} disabled={!comment.trim()} onClick={() => decide("reject")}>
                반려 확정
              </Button>
            </div>
          )}
        </section>
      )}
      {role === "admin" && <Callout tone="info">관리자는 결과를 볼 수만 있어요. 승인은 변호사로 보기에서 할 수 있어요.</Callout>}

      {role === "lawyer" && <ReviewChat reviewId={review.id} evidence={review.evidence} />}
    </div>
  );
}

export function ReviewClient({ id }: { id: string }) {
  const role = useRole();
  return (
    <WakeGate<ReviewDetail | ApiError> key={`${id}-${role}`} load={() => apiFetch<ReviewDetail | ApiError>(`/api/reviews/${id}`)}>
      {(res, reload) =>
        res.status === 200 ? (
          <Detail
            key={(res.data as ReviewDetail).version + (res.data as ReviewDetail).status}
            review={res.data as ReviewDetail}
            reload={reload}
          />
        ) : (
          <Callout tone="danger">검토를 불러오지 못했어요.</Callout>
        )
      }
    </WakeGate>
  );
}
