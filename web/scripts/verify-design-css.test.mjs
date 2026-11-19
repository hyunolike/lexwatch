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
const theme = ':root{--color-canvas:#f6f7f9;--color-surface:#fff;--color-ink:#1a2233;--color-brand:#1f2a44;--color-danger:#b3261e;--color-warning:#8a5a00;--color-focus:#1f6feb;--font-sans:"Pretendard Variable";--text-title-lg:1.5rem}';
const utilities = 'html{color-scheme:light}body{background-color:var(--color-canvas);font-family:var(--font-sans);color:var(--color-ink)}.bg-surface{background-color:var(--color-surface)}.bg-brand{background-color:var(--color-brand)}.text-ink{color:var(--color-ink)}.text-danger{color:var(--color-danger)}.text-warning{color:var(--color-warning)}.text-title-lg{font-size:var(--text-title-lg)}:where(button):focus-visible{outline-color:var(--color-focus)}';

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

test('현재 배포에서 관찰한 이전 다크 body와 토큰 누락은 실패한다', async () => {
  const result = await run({ 'old.css': stale });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /color-canvas/);
  assert.match(result.stderr, /이전 배경/);
});

test('토큰만 있고 버튼이나 모달 표면 스타일이 없으면 실패한다', async () => {
  const result = await run({ 'theme.css': theme });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /bg-surface/);
  assert.match(result.stderr, /bg-brand/);
});

test('색상 이름만 같고 실제 값이 다르면 실패한다', async () => {
  const result = await run({ 'app.css': (theme + utilities).replace('#1f2a44', '#000000') });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /color-brand/);
});

test('정상 CSS와 이전 전역 스타일이 혼재해도 실패한다', async () => {
  const result = await run({ 'app.css': theme + utilities + stale });
  assert.equal(result.status, 1);
  assert.match(result.stderr, /이전 배경/);
});

test('선택자 이름이 있어도 필요한 선언이 없으면 실패한다', async () => {
  const result = await run({ 'app.css': (theme + utilities).replace('background-color:var(--color-brand)', 'color:var(--color-brand)') });
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
