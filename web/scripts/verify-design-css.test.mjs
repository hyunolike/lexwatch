import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import test from 'node:test';
import { createServer } from 'node:http';
import { publishedCss } from './verify-design-css.mjs';

const script = fileURLToPath(new URL('./verify-design-css.mjs', import.meta.url));
// 관찰한 배포 오류: HTML만 새 의미 클래스로 바뀌고 CSS에는 이전 body가 남았다.
const stale = ':root{--background:#fff;--foreground:#171717}@media(prefers-color-scheme:dark){:root{--background:#0a0a0a}}body{background:var(--background);font-family:Arial,Helvetica,sans-serif}';
const theme =
  ':root{color-scheme:light;--lw-canvas:#f7f6f3;--lw-surface:#fff;--lw-ink:#1b1d22;--lw-brand:#1f3a68;--lw-danger:#b3261e;--lw-warning:#8a5300;--lw-focus:#2f6fdb;--lw-prov-ai:#5b3fc4}' +
  '[data-theme=dark]{color-scheme:dark;--lw-canvas:#111214;--lw-surface:#18191c;--lw-ink:#ecedef;--lw-brand:#8fa9e0;--lw-danger:#f08a80;--lw-warning:#e3b25a;--lw-focus:#6ea2f7;--lw-prov-ai:#b6a6f5}' +
  ':root,:host{--font-sans:"Pretendard Variable";--font-serif:"Noto Serif KR Variable";--text-title-lg:1.625rem}';
const utilities =
  'body{background-color:var(--lw-canvas);font-family:var(--font-sans);color:var(--lw-ink)}.bg-surface{background-color:var(--lw-surface)}.bg-brand{background-color:var(--lw-brand)}.text-ink{color:var(--lw-ink)}.text-danger{color:var(--lw-danger)}.text-warning{color:var(--lw-warning)}.text-title-lg{font-size:var(--text-title-lg)}:focus-visible{outline-offset:2px}:where(button):focus-visible{outline-color:var(--lw-focus)}';

async function run(files) {
  const root = await mkdtemp(path.join(os.tmpdir(), 'lexwatch-css-test-'));
  try {
    await mkdir(path.join(root, 'static/chunks'), { recursive: true });
    for (const [name, css] of Object.entries(files)) await writeFile(path.join(root, 'static/chunks', name), css);
    return spawnSync(process.execPath, [script, '--dir', root], { encoding: 'utf8' });
  } finally {
    await rm(root, { recursive: true, force: true });
  }
}

test('토큰과 스타일이 여러 CSS 파일로 나뉘어도 최종 산출물을 검사한다', async () => {
  const result = await run({ 'theme.css': theme, 'app.css': utilities });
  assert.equal(result.status, 0, result.stderr);
  assert.match(result.stdout, /검사 통과/);
});

test('현재 배포에서 관찰한 이전 body와 토큰 누락은 실패한다', async () => {
  const result = await run({ 'old.css': stale });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /lw-canvas/);
  assert.match(result.stderr, /이전 배경/);
});

test('어두운 테마 블록이 없으면 실패한다', async () => {
  const result = await run({ 'app.css': theme.replace(/\[data-theme=dark\]\{[^}]*\}/, '') + utilities });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /어두운 테마 --lw-canvas/);
  assert.match(result.stderr, /color-scheme:dark/);
});

test('클래스가 테마를 모르는 --lw- 변수 대신 --color-를 가리키면 실패한다', async () => {
  const result = await run({ 'app.css': theme + utilities.replace('background-color:var(--lw-surface)', 'background-color:var(--color-surface)') });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /bg-surface/);
});

test('포커스 링을 띄우지 않으면 실패한다', async () => {
  const result = await run({ 'app.css': theme + utilities.replace(':focus-visible{outline-offset:2px}', '') });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /outline-offset/);
});

test('토큰만 있고 버튼이나 모달 표면 스타일이 없으면 실패한다', async () => {
  const result = await run({ 'theme.css': theme });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /bg-surface/);
  assert.match(result.stderr, /bg-brand/);
});

test('색상 이름만 같고 실제 값이 다르면 실패한다', async () => {
  const result = await run({ 'app.css': (theme + utilities).replace('#1f3a68', '#000000') });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /lw-brand/);
});

test('정상 CSS와 이전 전역 스타일이 혼재해도 실패한다', async () => {
  const result = await run({ 'app.css': theme + utilities + stale });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /이전 배경/);
});

test('선택자 이름이 있어도 필요한 선언이 없으면 실패한다', async () => {
  const result = await run({ 'app.css': (theme + utilities).replace('background-color:var(--lw-brand)', 'color:var(--lw-brand)') });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /bg-brand/);
});

test('CSS 파일이 없는 빌드는 성공으로 처리하지 않는다', async () => {
  const result = await run({});
  assert.equal(result.status, 1);
  assert.match(result.stderr, /CSS/);
});

async function withServer(handler, check) {
  const server = createServer(handler);
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  try {
    await check(`http://127.0.0.1:${server.address().port}`);
  } finally {
    server.closeAllConnections();
    await new Promise((resolve, reject) => server.close((e) => e ? reject(e) : resolve()));
  }
}

test('공개 HTML에 연결된 immutable 경로의 CSS를 합친다', async () => {
  await withServer((req, res) => {
    if (req.url === '/') return res.end('<link href="/_next/static/immutable/theme.css" rel="stylesheet"><link rel="stylesheet" href="/_next/static/app.css">');
    res.end(req.url.endsWith('theme.css') ? theme : utilities);
  }, async (url) => assert.equal(await publishedCss(url), theme + '\n' + utilities));
});

test('CSS의 HTTP 오류를 정상 배포로 처리하지 않는다', async () => {
  await withServer((req, res) => {
    if (req.url === '/') return res.end('<link rel="stylesheet" href="/missing.css">');
    res.writeHead(404).end('not found');
  }, async (url) => assert.rejects(publishedCss(url), /HTTP 404/));
});

test('로그인 또는 오류 HTML에 CSS가 없으면 실패한다', async () => {
  await withServer((req, res) => res.end('<p>Sign in</p>'), async (url) => assert.rejects(publishedCss(url), /연결된 CSS/));
});
