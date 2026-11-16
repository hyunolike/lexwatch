"use client";
import Link from "next/link";
import { useState } from "react";
import { apiFetch, decisionActions, submitDecision, type ApiError, type EditedSentence, type ReviewDetail } from "@/lib/api";
import { DraftEditor } from "@/components/DraftEditor";
import { OldNewDiff } from "@/components/OldNewDiff";
import { ReplayNote } from "@/components/ReplayNote";
import { ReviewChat } from "@/components/ReviewChat";
import { ProviderBadge } from "@/components/ProviderBadge";
import { RoleSwitch, useRole } from "@/components/RoleSwitch";
import { WakeGate } from "@/components/WakeGate";

function Detail({ review, reload }: { review: ReviewDetail; reload: () => void }) {
  const role = useRole();
  const draft = review.draft;
  const [editing, setEditing] = useState(false);
  const [edited, setEdited] = useState<EditedSentence[]>(
    () => draft?.sentences.map((s) => ({ sentence: s.sentence, citations: s.citations })) ?? [],
  );
  const [rejecting, setRejecting] = useState(false);
  const [comment, setComment] = useState("");
  const [msg, setMsg] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [busy, setBusy] = useState(false);

  const actions = decisionActions(role, review.status, draft != null);
  const kinds = review.isReplay && !review.providerKinds.includes("replay") ? [...review.providerKinds, "replay" as const] : review.providerKinds;

  async function decide(kind: "approve" | "reject") {
    setBusy(true);
    setMsg(null);
    const out = await submitDecision(review.id, kind, review.version, {
      editedSentences: editing ? edited : undefined,
      comment,
    });
    setBusy(false);
    if (out.kind === "waking") return setMsg("서버를 깨우는 중이에요. 잠시 뒤에 다시 눌러 주세요.");
    if (out.kind === "conflict") return setConflict(true);
    if (out.kind === "error") return setMsg(out.message);
    reload();
  }

  const resetEdits = () =>
    setEdited(draft?.sentences.map((s) => ({ sentence: s.sentence, citations: s.citations })) ?? []);

  return (
    <div className="space-y-4">
      <Link href="/" className="text-sm text-blue-700 underline">← 승인 대기함</Link>
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-xl font-bold">{review.lawName}</h1>
        <span className="text-sm text-slate-500">{review.groupName}{review.isReplay ? "" : ` · ${review.promulgationDate} 공포`}</span>
        <span className="rounded bg-slate-200 px-2 py-0.5 text-xs">{review.status}</span>
        {kinds.map((k) => <ProviderBadge key={k} kind={k} />)}
        <div className="ml-auto"><RoleSwitch /></div>
      </div>
      {review.isReplay && <ReplayNote />}

      {conflict && (
        <div className="flex items-center gap-3 rounded border border-red-300 bg-red-50 p-3 text-sm">
          다른 사람이 먼저 결정했어요.
          <button onClick={() => { setConflict(false); reload(); }} className="rounded border bg-white px-2 py-0.5">새로고침</button>
        </div>
      )}
      {msg && <div className="rounded border bg-yellow-50 p-3 text-sm">{msg}</div>}

      <div className="grid gap-4 md:grid-cols-2">
        <section>
          <h2 className="mb-2 font-semibold">신구조문 비교</h2>
          <OldNewDiff items={review.oldNew} />
        </section>
        <section>
          <h2 className="mb-2 font-semibold">영향 검토 초안{draft && ` (v${draft.version}, ${draft.createdBy})`}</h2>
          {draft ? (
            <DraftEditor draft={draft} evidence={review.evidence} editing={editing} edited={edited} onChange={setEdited} />
          ) : (
            <p className="text-sm text-slate-500">아직 초안이 없어요.</p>
          )}
        </section>
      </div>

      {role === "lawyer" && <ReviewChat reviewId={review.id} evidence={review.evidence} />}

      {actions.reject && (
        <div className="space-y-3 rounded border bg-white p-3">
          {!actions.approve && (
            <p className="text-sm text-slate-600">초안이 없어 승인하거나 수정할 수 없어요. 근거가 부족하면 사유를 적어 반려할 수 있어요.</p>
          )}
          <div className="flex flex-wrap gap-2">
            {actions.approve && (
              <button disabled={busy || editing} onClick={() => decide("approve")} className="rounded bg-green-700 px-3 py-1 text-white disabled:opacity-40">승인</button>
            )}
            {actions.edit && (
              <button disabled={busy} onClick={() => {
                if (editing) resetEdits();
                setEditing((e) => !e);
              }} className="rounded border px-3 py-1">
                {editing ? "수정 취소" : "수정"}
              </button>
            )}
            {actions.edit && editing && (
              <button disabled={busy} onClick={() => decide("approve")} className="rounded bg-green-700 px-3 py-1 text-white disabled:opacity-40">수정 후 승인</button>
            )}
            <button disabled={busy} onClick={() => setRejecting((r) => !r)} className="rounded border border-red-400 px-3 py-1 text-red-700">반려</button>
          </div>
          {rejecting && (
            <div className="space-y-2">
              <textarea
                value={comment}
                onChange={(e) => setComment(e.target.value)}
                placeholder="반려 사유 (필수)"
                rows={2}
                className="w-full rounded border p-2 text-sm"
              />
              <button disabled={busy || !comment.trim()} onClick={() => decide("reject")} className="rounded bg-red-700 px-3 py-1 text-white disabled:opacity-40">
                반려 확정
              </button>
            </div>
          )}
        </div>
      )}
      {role === "admin" && <p className="text-sm text-slate-500">관리자는 결과를 볼 수만 있어요. 승인은 변호사로 보기에서 할 수 있어요.</p>}
    </div>
  );
}

export function ReviewClient({ id }: { id: string }) {
  const role = useRole();
  return (
    <WakeGate<ReviewDetail | ApiError> key={`${id}-${role}`} load={() => apiFetch<ReviewDetail | ApiError>(`/api/reviews/${id}`)}>
      {(res, reload) =>
        res.status === 200 ? <Detail key={(res.data as ReviewDetail).version + (res.data as ReviewDetail).status} review={res.data as ReviewDetail} reload={reload} /> : <p>검토를 불러오지 못했어요.</p>
      }
    </WakeGate>
  );
}
