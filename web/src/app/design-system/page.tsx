import { readFileSync } from "node:fs";
import path from "node:path";
import type { Metadata } from "next";
import { CONTRAST_PAIRS, contrastRatio, parseThemes } from "@/design/tokens";
import { CARD, Mascot } from "@/components/ds/layout";
import { Showcase } from "./Showcase";

export const metadata: Metadata = { title: "디자인 시스템 · LexWatch" };

/** 빌드 때 tokens.css를 읽어 그린다(정적 미리 그리기). 값의 원본은 그 파일 하나다. */
function readTokens() {
  return parseThemes(readFileSync(path.join(process.cwd(), "src/styles/tokens.css"), "utf8"));
}

const GROUPS: { title: string; prefix: RegExp }[] = [
  { title: "바탕과 글자", prefix: /^(canvas|surface|ink|line)/ },
  { title: "브랜드와 포커스", prefix: /^(brand|on-brand|focus)/ },
  { title: "상태", prefix: /^(success|danger|on-danger|warning|info|neutral)/ },
  { title: "출처: 누가 만들었나 (상태 색과 다른 계열)", prefix: /^prov-/ },
  { title: "신구 대조", prefix: /^diff-/ },
];

const TYPE = [
  ["text-display", "표지 32/40 세리프"],
  ["text-title-lg", "페이지 제목 26/36 세리프"],
  ["text-title-md", "법령명 21/30 세리프"],
  ["text-title-sm", "구역 제목 16/24"],
  ["text-reading", "읽는 글 17/30 — 근로자의 1주 근로시간은 휴게시간을 제외하고 40시간을 초과할 수 없다."],
  ["text-ui", "일반 UI 15/24"],
  ["text-label", "버튼과 라벨 14/20"],
  ["text-caption", "캡션과 시간 13/18 · 2026-11-19 07:00"],
  ["text-micro", "배지 12/16"],
] as const;

const POSES = [
  ["empty-queue", "승인 대기 없음"],
  ["empty-inbox", "알림 없음"],
  ["waking", "서버 깨우는 중"],
  ["error", "불러오기 실패"],
] as const;

export default function DesignSystemPage() {
  const themes = readTokens();
  const names = Object.keys(themes.light);
  return (
    <div className="space-y-10">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-serif text-display text-ink">디자인 시스템</h1>
          <p className="mt-1 max-w-[40em] text-ui text-ink-muted">
            로펌 직원이 긴 법령 문장을 오래 읽고, 근거를 확인한 뒤 승인하는 화면을 위한 규칙이에요. 값의 원본은{" "}
            <code className="font-mono text-caption">src/styles/tokens.css</code> 하나이고, 대비는 테스트가 WCAG 2.2 AA로 검사해요.
          </p>
        </div>
        <Mascot pose="master" size={96} />
      </div>

      <section aria-labelledby="colors" className="space-y-4">
        <h2 id="colors" className="text-title-md text-ink">색</h2>
        {GROUPS.map((g) => (
          <div key={g.title}>
            <h3 className="mb-2 text-title-sm text-ink">{g.title}</h3>
            <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
              {names
                .filter((n) => g.prefix.test(n))
                .map((n) => (
                  <li key={n} className={`${CARD} overflow-hidden`}>
                    <div className="grid h-12 grid-cols-2 border-b border-line">
                      <span title="밝은 테마" style={{ background: themes.light[n] }} />
                      <span title="어두운 테마" style={{ background: themes.dark[n] }} />
                    </div>
                    <div className="p-2">
                      <p className="truncate font-mono text-caption text-ink">{n}</p>
                      <p className="font-mono text-micro text-ink-subtle">
                        {themes.light[n]} / {themes.dark[n]}
                      </p>
                    </div>
                  </li>
                ))}
            </ul>
          </div>
        ))}
      </section>

      <section aria-labelledby="contrast" className="space-y-3">
        <h2 id="contrast" className="text-title-md text-ink">대비 검사</h2>
        <p className="text-ui text-ink-muted">글자는 4.5:1, 입력 테두리와 포커스 링은 3:1 이상이어야 해요.</p>
        <div className={`${CARD} overflow-x-auto`}>
          <table className="w-full text-ui">
            <thead className="bg-surface-sunken text-left text-label text-ink-muted">
              <tr>
                <th className="px-3 py-2">견본</th>
                <th className="px-3 py-2">앞</th>
                <th className="px-3 py-2">뒤</th>
                <th className="px-3 py-2 text-right">밝은 테마</th>
                <th className="px-3 py-2 text-right">어두운 테마</th>
                <th className="px-3 py-2 text-right">기준</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {CONTRAST_PAIRS.map(({ fg, bg, min }) => {
                const light = contrastRatio(themes.light[fg], themes.light[bg]);
                const dark = contrastRatio(themes.dark[fg], themes.dark[bg]);
                const ok = light >= min && dark >= min;
                return (
                  <tr key={`${fg}/${bg}`}>
                    <td className="space-x-1 px-3 py-1.5 whitespace-nowrap">
                      <span className="rounded-sm px-2 py-0.5 text-label" style={{ color: themes.light[fg], background: themes.light[bg] }}>
                        가나다
                      </span>
                      <span className="rounded-sm px-2 py-0.5 text-label" style={{ color: themes.dark[fg], background: themes.dark[bg] }}>
                        가나다
                      </span>
                    </td>
                    <td className="px-3 py-1.5 font-mono text-caption">{fg}</td>
                    <td className="px-3 py-1.5 font-mono text-caption">{bg}</td>
                    <td className="px-3 py-1.5 text-right tabular-nums">{light.toFixed(2)}</td>
                    <td className="px-3 py-1.5 text-right tabular-nums">{dark.toFixed(2)}</td>
                    <td className={`px-3 py-1.5 text-right tabular-nums ${ok ? "text-success" : "text-danger"}`}>
                      {ok ? "통과" : "미달"} {min}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </section>

      <section aria-labelledby="type" className="space-y-3">
        <h2 id="type" className="text-title-md text-ink">글자</h2>
        <p className="text-ui text-ink-muted">Pretendard Variable. 자간 0. 숫자 열은 고정폭 숫자(tabular-nums).</p>
        <ul className={`${CARD} divide-y divide-line`}>
          {TYPE.map(([cls, sample]) => (
            <li key={cls} className="flex flex-wrap items-baseline gap-x-6 gap-y-1 px-4 py-3">
              <code className="w-32 shrink-0 font-mono text-caption text-ink-subtle">{cls}</code>
              <span className={`${cls} ${/display|title-(lg|md)/.test(cls) ? "font-serif" : ""} text-ink`}>{sample}</span>
            </li>
          ))}
        </ul>
      </section>

      <Showcase />

      <section aria-labelledby="patterns" className="space-y-3">
        <h2 id="patterns" className="text-title-md text-ink">화면 구성 규칙</h2>
        <dl className={`${CARD} space-y-5 p-5 text-ui`}>
          <div><dt className="font-semibold">확인할 문제 → 근거 대조 → 결정</dt><dd className="mt-1 text-ink-muted">상세 상단에서 확인할 문장 수와 첫 문장 이동을 제공해요. 검사 메시지를 먼저 쓰고 단계와 규칙 코드는 접힌 검사 정보에 둬요. 지적이 없다는 사실을 정확도 보장으로 표현하지 않아요.</dd></div>
          <div><dt className="font-semibold">작업 폭과 읽기 폭</dt><dd className="mt-1 text-ink-muted">작업 영역은 최대 70rem, 초안은 최대 48rem이에요. 초안 아래에서 신구조문을 작업 영역 전체 폭으로 비교해요. 768px 미만에서는 조문을 탭으로 바꿔요. 간격은 4px 단위로, 같은 문장 안 8~12px, 카드 안 16~24px, 구역 사이 32px를 사용해요.</dd></div>
          <div><dt className="font-semibold">목록의 정보 순서</dt><dd className="mt-1 text-ink-muted">법령명, 확인 필요 수, 그룹 관련성, 근거 수, 출처 순서예요. 데스크톱에서는 행마다 같은 열에 정렬해요. 그룹 관련성은 초안 정확도가 아니에요. 재현 데이터라는 설명을 숨기지 않아요.</dd></div>
          <div><dt className="font-semibold">겹치는 화면의 순서</dt><dd className="mt-1 text-ink-muted">결정 영역 20, 헤더 30, 팝오버 40, 확인창과 뒤 배경 60, 툴팁 70 순서예요. 확인창은 불투명한 표면과 뒤 배경 가림을 함께 사용해요.</dd></div>
        </dl>
      </section>

      <section aria-labelledby="mascot" className="space-y-3">
        <h2 id="mascot" className="text-title-md text-ink">마스코트 렉스</h2>
        <p className="max-w-[40em] text-ui text-ink-muted">
          매일 아침 개정을 지켜보는 부엉이예요. v2부터는 빈 화면, 서버 깨우기 대기, 오류처럼 여유 있는 순간에만 120px 이하로 나와요. 헤더는 평면 로고 마크를 써요.{" "}
          <strong className="text-ink">승인과 반려 버튼 주변, 확인 대화상자, 초안 본문, 신구조문, 실행 타임라인에는 쓰지 않아요.</strong>
        </p>
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {POSES.map(([pose, label]) => (
            <li key={pose} className={`${CARD} flex flex-col items-center p-3 text-center`}>
              <Mascot pose={pose} size={120} />
              <p className="mt-2 font-mono text-caption text-ink">{pose}</p>
              <p className="text-micro text-ink-subtle">{label}</p>
            </li>
          ))}
        </ul>
      </section>
    </div>
  );
}
