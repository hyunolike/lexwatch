"use client";
import { useState, useSyncExternalStore, type ReactNode } from "react";
import type { EvidenceItem, OldNew } from "@/lib/api";
import { ConfidenceMeter, CountBadge, SourceLabel, StatusLozenge } from "@/components/ds/badges";
import { Callout } from "@/components/ds/Callout";
import { CitationChip } from "@/components/ds/CitationChip";
import { RedlineDiff } from "@/components/ds/RedlineDiff";
import { CARD } from "@/components/ds/layout";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";

const EVIDENCE: EvidenceItem = {
  id: "law:005698:제7조",
  kind: "law",
  title: "가정보호심판규칙 제7조(검사에 대한 결정의 통지)",
  sourceUrl: "https://www.law.go.kr",
  excerpt: "제7조(검사에 대한 결정의 통지)\n① 법 제12조에 따라 송치 결정을 하였을 때에는 해당 사건을 기소한 공소청의 검사에게 그 결정을 통지하여야 한다.",
};

/** 실제 녹화본(가정보호심판규칙 제7조)의 공식 변경 표시를 쓴 예시 */
const REDLINE: OldNew[] = [
  {
    articleNo: "제7조 · 법제처 표시",
    before: "① 법 제12조에 따라 송치 결정을 하였을 때에는 해당 사건을 기소한 검찰청의 검사에게 그 결정을 통지하여야 한다.",
    after: "① 법 제12조에 따라 송치 결정을 하였을 때에는 해당 사건을 기소한 공소청의 검사에게 그 결정을 통지하여야 한다.",
    beforeMissing: false,
    afterMissing: false,
    oldMarks: [[39, 42]],
    newMarks: [[39, 42]],
  },
  {
    articleNo: "제50조 · 화면에서 비교",
    before: "근로자는 1주 40시간을 초과할 수 없다.",
    after: "근로자가 1주 52시간을 초과할 수 없다.",
    beforeMissing: false,
    afterMissing: false,
    oldMarks: null,
    newMarks: null,
  },
];

function Block({ title, note, children }: { title: string; note?: string; children: ReactNode }) {
  return (
    <div className={`${CARD} space-y-3 p-4`}>
      <div>
        <h3 className="text-title-sm text-ink">{title}</h3>
        {note && <p className="text-caption text-ink-subtle">{note}</p>}
      </div>
      {children}
    </div>
  );
}

const noop = () => () => {};
/** 브라우저에 붙은 뒤에만 참. jsdiff가 현재 시각을 읽어 미리 그리기(cacheComponents)에서는 계산하지 않는다. */
const useMounted = () => useSyncExternalStore(noop, () => true, () => false);

/** 부품과 상태 예시. 직접 눌러 볼 수 있다. */
export function Showcase() {
  const [result, setResult] = useState<string | null>(null);
  const mounted = useMounted();
  return (
    <section aria-labelledby="components" className="space-y-4">
      <h2 id="components" className="text-title-md text-ink">부품</h2>
      <div className="grid gap-4 lg:grid-cols-2">
        <Block title="Button" note="primary는 화면에 하나. 비활성은 opacity 50%, 로딩은 회전 아이콘과 aria-busy.">
          <div className="flex flex-wrap gap-2">
            <Button variant="primary">승인</Button>
            <Button>수정</Button>
            <Button variant="danger">반려 확정</Button>
            <Button variant="ghost">취소</Button>
            <Button variant="primary" disabled>
              비활성
            </Button>
            <Button variant="primary" loading>
              처리 중
            </Button>
          </div>
        </Block>

        <Block title="ConfirmDialog" note="되돌릴 수 없는 승인 전에 한 번 더. 열리면 포커스가 취소에, Esc로 닫힘.">
          <ConfirmDialog
            trigger={<Button variant="primary">승인 (예시)</Button>}
            title="승인할까요?"
            description="노동 그룹 알림함으로 안내문이 발송돼요. 발송 뒤에는 되돌릴 수 없어요."
            confirmLabel="승인하고 알림 보내기"
            onConfirm={async () => {
              await new Promise((r) => setTimeout(r, 600));
              setResult("승인했어요. 노동 그룹 알림함으로 발송을 시작했어요.");
            }}
          />
          {result && (
            <Callout tone="success" live>
              {result}
            </Callout>
          )}
        </Block>

        <Block title="StatusLozenge" note="색 점과 한국어 라벨. 백엔드 열거형 값을 그대로 넣는다.">
          <div className="flex flex-wrap gap-2">
            {["Analyzing", "NeedsReview", "WaitingApproval", "Approved", "Rejected", "ClosedIrrelevant"].map((v) => (
              <StatusLozenge key={v} domain="review" value={v} />
            ))}
            <StatusLozenge domain="review" value="UnknownValue" />
          </div>
        </Block>

        <Block title="SourceLabel" note="누가 만들었는지 알리는 분류. 품질 판정이 아니다. 마우스를 올리거나 Tab으로 설명을 본다.">
          <div className="flex flex-wrap gap-2">
            <SourceLabel kind="nvidia" />
            <SourceLabel kind="fallback" />
            <SourceLabel kind="replay" />
          </div>
        </Block>

        <Block title="CountBadge와 ConfidenceMeter" note="확인 필요 수만 danger. 그룹 관련성은 숫자와 5칸 막대이며 초안 정확도가 아니다.">
          <div className="flex flex-wrap items-center gap-3">
            <CountBadge count={2} label="확인 필요" tone="danger" />
            <CountBadge count={0} label="확인 필요" tone="danger" />
            <ConfidenceMeter value={0.92} />
            <ConfidenceMeter value={0.41} />
            <ConfidenceMeter value={null} />
            <ConfidenceMeter value={0} />
            <ConfidenceMeter value={1} />
          </div>
        </Block>

        <Block title="CitationChip" note="누르거나 Enter로 근거 본문 앞부분을 본다. 근거가 없으면 경고 모양.">
          <div className="flex flex-wrap gap-2">
            <CitationChip id={EVIDENCE.id} evidence={EVIDENCE} />
            <CitationChip id="law:999:제1조" />
          </div>
        </Block>

        <Block title="Callout">
          <div className="space-y-2">
            <Callout tone="info">재현 예시예요. 실제 공포 법령이 아니에요.</Callout>
            <Callout tone="warning" title="열린 질문">시행일 이전 계약에 적용되는지 확인이 필요해요.</Callout>
            <Callout tone="danger">
              <p>경과규정 없이 시행일 이전 계약에 적용된다고 단정했어요.</p>
              <details className="mt-2 text-caption text-ink-muted">
                <summary className="w-fit cursor-pointer rounded-sm">검사 정보</summary>
                <p className="mt-1">크리틱 3단 · OC-01</p>
              </details>
            </Callout>
          </div>
        </Block>
      </div>

      <Block title="RedlineDiff" note="삭제는 취소선, 추가는 두꺼운 밑줄. 768px보다 좁으면 탭으로 바뀐다.">
        {mounted ? <RedlineDiff items={REDLINE} /> : <p className="text-ui text-ink-subtle">불러오는 중이에요</p>}
      </Block>
    </section>
  );
}
