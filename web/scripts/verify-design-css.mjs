import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const TOKEN_FILE = new URL('../src/styles/tokens.css', import.meta.url);
/** 두 테마 값을 확인할 대표 색. 소스 tokens.css의 :root와 [data-theme="dark"] 블록에서 기대값을 읽는다. */
const COLORS = ['canvas', 'surface', 'ink', 'brand', 'danger', 'warning', 'focus', 'prov-ai'];
const hex = (value) => (value.length === 4 ? '#' + [...value.slice(1)].map((c) => c + c).join('') : value).toLowerCase();

/** 선택자 목록에 selector가 들어 있는 블록들의 본문. 압축된 CSS는 [data-theme=dark]처럼 따옴표가 빠질 수 있다. */
function blocks(compact, selector) {
  const re = new RegExp(`^${selector}$`);
  return [...compact.matchAll(/([^{}]+)\{([^{}]*)\}/g)]
    .filter((m) => m[1].split(',').some((x) => re.test(x.trim())))
    .map((m) => m[2]);
}

function sourceValues(source, selector) {
  const m = new RegExp(`${selector}\\s*\\{([^}]*)\\}`).exec(source);
  const out = {};
  for (const d of (m?.[1] ?? '').matchAll(/--lw-([a-z0-9-]+)\s*:\s*(#[0-9a-fA-F]{6})/g)) out[d[1]] = hex(d[2]);
  return out;
}

/**
 * 소스 파일 검사가 아니라, 브라우저에 제공할 최종 CSS의 최소 계약 검사(design-system-v2 design.md 2.7).
 * 클래스는 테마를 모르고 var(--lw-*)를 가리키며, 값은 :root(밝은 테마)와 [data-theme=dark] 블록에만 있다.
 */
export async function verifyDesignCss(css) {
  const source = await readFile(TOKEN_FILE, 'utf8');
  const errors = [];
  const compact = css.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\s+/g, '');
  const light = sourceValues(source, '(?:^|\\n):root(?:,\\s*\\[data-theme="light"\\])?');
  const dark = sourceValues(source, '\\[data-theme="dark"\\]');
  const lightBlocks = blocks(compact, ':root');
  const darkBlocks = blocks(compact, '\\[data-theme=(?:"dark"|dark)\\]');
  const themedLight = blocks(compact, '\\[data-theme=(?:"light"|light)\\]');
  if (!themedLight.length) errors.push('[data-theme=light] 블록 누락(견본 상자를 밝게 그릴 수 없음)');
  const has = (bodies, decl) => bodies.some((b) => b.split(';').map((d) => d.replace(/#[0-9a-f]{3,6}\b/gi, hex)).includes(decl));

  for (const name of COLORS) {
    if (!has(lightBlocks, `--lw-${name}:${light[name]}`)) errors.push(`밝은 테마 --lw-${name}: 기대값 ${light[name]} 누락 또는 불일치`);
    if (!has(darkBlocks, `--lw-${name}:${dark[name]}`)) errors.push(`어두운 테마 --lw-${name}: 기대값 ${dark[name]} 누락 또는 불일치`);
  }
  if (!has(lightBlocks, 'color-scheme:light')) errors.push(':root: color-scheme:light 누락');
  if (!has(darkBlocks, 'color-scheme:dark')) errors.push('[data-theme=dark]: color-scheme:dark 누락');

  const rules = [
    ['.bg-surface', /\.bg-surface\{([^{}]*)\}/g, ['background-color:var(--lw-surface)']],
    ['.bg-brand', /\.bg-brand\{([^{}]*)\}/g, ['background-color:var(--lw-brand)']],
    ['.text-ink', /\.text-ink\{([^{}]*)\}/g, ['color:var(--lw-ink)']],
    ['.text-danger', /\.text-danger\{([^{}]*)\}/g, ['color:var(--lw-danger)']],
    ['.text-warning', /\.text-warning\{([^{}]*)\}/g, ['color:var(--lw-warning)']],
    ['.text-title-lg', /\.text-title-lg\{([^{}]*)\}/g, ['font-size:var(--text-title-lg)']],
    [':focus-visible', /:focus-visible\{([^{}]*)\}/g, ['outline-color:var(--lw-focus)']],
    [':focus-visible offset', /(?:^|[}{,]):focus-visible\{([^{}]*)\}/g, ['outline-offset:2px']],
    ['body', /(?:^|[{}])body\{([^{}]*)\}/g, ['background-color:var(--lw-canvas)', 'font-family:var(--font-sans)', 'color:var(--lw-ink)']],
  ];
  for (const [label, pattern, declarations] of rules) {
    const found = [...compact.matchAll(pattern)].map((m) => m[1]);
    for (const declaration of declarations) {
      if (!found.some((b) => b.split(';').includes(declaration))) errors.push(`${label}: ${declaration} 누락`);
    }
  }
  if (!/--font-sans:[^;}]*PretendardVariable/.test(compact)) errors.push('Pretendard 글꼴 정의 누락');
  if (!/--font-serif:[^;}]*NotoSerifKRVariable/.test(compact)) errors.push('Noto Serif KR 글꼴 정의 누락');
  if (!/--text-title-lg:1\.625rem[;}]/.test(compact)) errors.push('페이지 제목 크기 누락');
  if (/--(?:background|foreground):|body\{[^}]*font-family:Arial/.test(compact)) {
    errors.push('이전 배경/Arial 전역 스타일이 남아 있음');
  }
  return errors;
}

async function localCss(root) {
  const files = await readdir(path.join(root, 'static'), { recursive: true });
  const styles = files.filter((f) => f.endsWith('.css'));
  if (!styles.length) throw new Error('빌드 산출물에 CSS 파일이 없어요.');
  return (await Promise.all(styles.map((f) => readFile(path.join(root, 'static', f), 'utf8')))).join('\n');
}

async function fetchText(url) {
  const response = await fetch(url, { signal: AbortSignal.timeout(15_000) });
  if (!response.ok) throw new Error(`HTTP ${response.status}: ${url}`);
  return response.text();
}

export async function publishedCss(url) {
  const html = await fetchText(url);
  const links = [...html.matchAll(/<link\b[^>]*>/gi)].map((m) => m[0]);
  const hrefs = links.filter((l) => /\brel=["']stylesheet["']/i.test(l))
    .map((l) => /\bhref=["']([^"']+)["']/i.exec(l)?.[1]).filter(Boolean);
  if (!hrefs.length) throw new Error('HTML에 연결된 CSS가 없어요. 로그인/오류 화면인지 확인하세요.');
  const urls = [...new Set(hrefs.map((href) => new URL(href.replaceAll('&amp;', '&'), url).href))];
  return (await Promise.all(urls.map(fetchText))).join('\n');
}

async function main() {
  const args = process.argv.slice(2);
  if (args.length && (args.length !== 2 || !['--url', '--dir'].includes(args[0]))) {
    throw new Error('사용법: node scripts/verify-design-css.mjs [--url https://... | --dir .next]');
  }
  const css = args[0] === '--url' ? await publishedCss(args[1]) : await localCss(args[1] ?? '.next');
  const errors = await verifyDesignCss(css);
  if (errors.length) throw new Error(`디자인 CSS 검사 실패:\n- ${errors.join('\n- ')}`);
  console.log('디자인 CSS 검사 통과: 두 테마 토큰 값, 본문, 제목, 표면, 버튼, 경고, 포커스');
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => { console.error(error.message); process.exitCode = 1; });
}
