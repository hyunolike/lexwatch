import { readFile, readdir } from 'node:fs/promises';
import path from 'node:path';
import { pathToFileURL } from 'node:url';

const TOKEN_FILE = new URL('../src/styles/tokens.css', import.meta.url);
const COLORS = ['canvas', 'surface', 'ink', 'brand', 'danger', 'warning', 'focus'];
const hex = (value) => value.length === 4 ? '#' + [...value.slice(1)].map((c) => c + c).join('') : value;

/** 소스 파일 검사가 아니라, 브라우저에 제공할 최종 CSS의 최소 계약 검사. */
export async function verifyDesignCss(css) {
  const source = await readFile(TOKEN_FILE, 'utf8');
  const errors = [];
  const clean = css.replace(/\/\*[\s\S]*?\*\//g, '');
  for (const name of COLORS) {
    const key = `--color-${name}`;
    const expected = new RegExp(`${key}:\\s*(#[a-f0-9]+)`, 'i').exec(source)?.[1];
    const values = [...clean.matchAll(new RegExp(`${key}:\\s*(#[a-f0-9]+)\\s*[;}]`, 'gi'))];
    if (!values.length || values.some((m) => hex(m[1].toLowerCase()) !== hex(expected.toLowerCase()))) {
      errors.push(`${key}: 기대값 ${expected} 누락 또는 불일치`);
    }
  }
  const compact = clean.replace(/\s+/g, '');
  const rules = [
    ['.bg-surface', /\.bg-surface\{([^{}]*)\}/g, ['background-color:var(--color-surface)']],
    ['.bg-brand', /\.bg-brand\{([^{}]*)\}/g, ['background-color:var(--color-brand)']],
    ['.text-ink', /\.text-ink\{([^{}]*)\}/g, ['color:var(--color-ink)']],
    ['.text-danger', /\.text-danger\{([^{}]*)\}/g, ['color:var(--color-danger)']],
    ['.text-warning', /\.text-warning\{([^{}]*)\}/g, ['color:var(--color-warning)']],
    ['.text-title-lg', /\.text-title-lg\{([^{}]*)\}/g, ['font-size:var(--text-title-lg)']],
    [':focus-visible', /:focus-visible\{([^{}]*)\}/g, ['outline-color:var(--color-focus)']],
    ['body', /(?:^|[{}])body\{([^{}]*)\}/g, ['background-color:var(--color-canvas)', 'font-family:var(--font-sans)', 'color:var(--color-ink)']],
    ['html', /(?:^|[{},])html(?:,[^{}]+)?\{([^{}]*)\}/g, ['color-scheme:light']],
  ];
  for (const [label, pattern, declarations] of rules) {
    const blocks = [...compact.matchAll(pattern)].map((m) => m[1]);
    for (const declaration of declarations) {
      if (!blocks.some((b) => b.split(';').includes(declaration))) errors.push(`${label}: ${declaration} 누락`);
    }
  }
  if (!/--font-sans:[^;}]*PretendardVariable/.test(compact)) errors.push('Pretendard 글꼴 정의 누락');
  if (!/--text-title-lg:1\.5rem[;}]/.test(compact)) errors.push('페이지 제목 크기 누락');
  if (/--(?:background|foreground):|prefers-color-scheme:dark|body\{[^}]*font-family:Arial/.test(compact)) {
    errors.push('이전 배경/다크 모드/Arial 전역 스타일이 남아 있음');
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
  console.log('디자인 CSS 검사 통과: 토큰 값, 본문, 제목, 표면, 버튼, 경고, 포커스, 밝은 모드');
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => { console.error(error.message); process.exitCode = 1; });
}
