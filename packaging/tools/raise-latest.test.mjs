// node --test packaging/tools/*.test.mjs  - test raise-latest.mjs (khong goi mang: fetch duoc tiem).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import {
  loadPackage, validatePackage, raiseEndpoint, githubReleaseUrl, wingetPageUrl, scoopDownloadUrl, resolveChannelUrl, raise, redactor,
} from './raise-latest.mjs';

const pkg = loadPackage();
const V = '26.11.2601';
const TOKEN = 'test-token-not-real-0123456789abcdef0123456789';
const SCRIPT = fileURLToPath(new URL('./raise-latest.mjs', import.meta.url));

const res = (status, body) => ({
  ok: status >= 200 && status < 300,
  status,
  text: async () => (typeof body === 'string' ? body : JSON.stringify(body ?? {})),
});
/** fetch gia: tra lan luot cac phan hoi, ghi lai moi lan goi. */
function fakeFetch(...responses) {
  const calls = [];
  const f = async (url, init = {}) => { calls.push({ url, init }); return responses.shift() ?? res(500, 'het phan hoi'); };
  f.calls = calls;
  return f;
}

test('URL dung cho tung kenh', () => {
  assert.equal(raiseEndpoint(pkg, 'github'), 'https://tsudev.com/api/versions/raise/tsudisk/github');
  assert.equal(raiseEndpoint(pkg, 'scoop'), 'https://tsudev.com/api/versions/raise/tsudisk/scoop');
  assert.throws(() => raiseEndpoint(pkg, 'play'));
  assert.equal(githubReleaseUrl(pkg, V), `https://github.com/tsudev-org/tsudisk/releases/tag/v${V}`);
  assert.equal(wingetPageUrl(pkg, V), `https://github.com/microsoft/winget-pkgs/tree/master/manifests/t/tsudev/TsuDisk/${V}`);
});

test('Scoop: lay URL exe CLI dung ban, bo #/ten.exe; lech ban thi null', () => {
  const m = {
    version: V,
    architecture: { '64bit': { url: [
      `https://github.com/tsudev-org/tsudisk/releases/download/v${V}/tsudisk_${V}_windows-x64-portable.exe#/tsudisk.exe`,
      `https://github.com/tsudev-org/tsudisk/releases/download/v${V}/tsudisk-gui_${V}_windows-x64-portable.exe#/tsudisk-gui.exe`,
    ] } },
  };
  assert.equal(scoopDownloadUrl(m, pkg, V),
    `https://github.com/tsudev-org/tsudisk/releases/download/v${V}/tsudisk_${V}_windows-x64-portable.exe`);
  assert.equal(scoopDownloadUrl({ ...m, version: '26.11.2401' }, pkg, V), null);
  assert.equal(scoopDownloadUrl({ version: V, url: 'https://evil.example/tsudisk.exe' }, pkg, V), null);
});

test('github: POST dung endpoint, header token, body {latest,url}', async () => {
  const f = fakeFetch(res(200, { data: { latest: V } }));
  const out = await raise({ pkg, channel: 'github', version: V, url: githubReleaseUrl(pkg, V), token: TOKEN, fetchImpl: f });
  assert.equal(out.status, 'raised');
  assert.equal(f.calls.length, 1);
  const { url, init } = f.calls[0];
  assert.equal(url, 'https://tsudev.com/api/versions/raise/tsudisk/github');
  assert.equal(init.method, 'POST');
  assert.equal(init.headers['x-versions-token'], TOKEN);
  assert.deepEqual(JSON.parse(init.body), { latest: V, url: githubReleaseUrl(pkg, V) });
});

test('409 version_not_newer = xong, khong thu lai', async () => {
  const f = fakeFetch(res(409, { error: 'version_not_newer', current: V }));
  const out = await raise({ pkg, channel: 'github', version: V, url: githubReleaseUrl(pkg, V), token: TOKEN, fetchImpl: f, retryDelayMs: 0 });
  assert.equal(out.status, 'already');
  assert.equal(f.calls.length, 1);
});

test('409 download_unverified: thu lai toi da N lan roi bao loi', async () => {
  const bad = () => res(409, { error: 'download_unverified', reason: '404' });
  const f = fakeFetch(bad(), bad(), res(200, {}));
  const logs = [];
  const out = await raise({ pkg, channel: 'github', version: V, url: githubReleaseUrl(pkg, V), token: TOKEN, fetchImpl: f, retries: 3,
    retryDelayMs: 0, log: (m) => logs.push(m) });
  assert.equal(out.status, 'raised');
  assert.equal(f.calls.length, 3);
  assert.equal(logs.length, 2);

  const g = fakeFetch(bad(), bad(), bad(), bad(), bad());
  await assert.rejects(raise({ pkg, channel: 'github', version: V, url: githubReleaseUrl(pkg, V), token: TOKEN, fetchImpl: g, retries: 3,
    retryDelayMs: 0 }), /409/);
  assert.equal(g.calls.length, 4);
});

test('Loi khac (401, 400) khong thu lai; ban khong chinh thuc bi tu choi truoc khi goi mang', async () => {
  const f = fakeFetch(res(401, { error: 'invalid_versions_token' }));
  await assert.rejects(raise({ pkg, channel: 'github', version: V, url: githubReleaseUrl(pkg, V), token: TOKEN, fetchImpl: f, retryDelayMs: 0 }), /401/);
  assert.equal(f.calls.length, 1);
  const g = fakeFetch();
  await assert.rejects(raise({ pkg, channel: 'github', version: '26.11.2601-beta.1', url: githubReleaseUrl(pkg, V), token: TOKEN, fetchImpl: g }));
  await assert.rejects(raise({ pkg, channel: 'github', version: V, url: githubReleaseUrl(pkg, V), token: '', fetchImpl: g }), /TSU_VERSIONS_TOKEN/);
  assert.equal(g.calls.length, 0);
});

test('Khong in token: may chu doi lai token trong than loi van bi che', async () => {
  const f = fakeFetch(res(500, `loi: ${TOKEN}`));
  const err = await raise({ pkg, channel: 'github', version: V, url: githubReleaseUrl(pkg, V), token: TOKEN, fetchImpl: f, retryDelayMs: 0 })
    .catch((e) => e);
  assert.ok(err instanceof Error);
  assert.ok(!err.message.includes(TOKEN));
  assert.ok(err.message.includes('***'));
  assert.equal(redactor(TOKEN)(`a${TOKEN}b`), 'a***b');
});

test('winget: chua gop (404) thi bo qua; da gop thi url trang thu muc', async () => {
  const skip = await resolveChannelUrl({ pkg, channel: 'winget', version: V, fetchImpl: fakeFetch(res(404, {})) });
  assert.match(skip.skip, /chua gop/);
  const f = fakeFetch(res(200, []));
  const ok = await resolveChannelUrl({ pkg, channel: 'winget', version: V, fetchImpl: f });
  assert.equal(ok.url, wingetPageUrl(pkg, V));
  assert.equal(f.calls[0].url,
    `https://api.github.com/repos/microsoft/winget-pkgs/contents/manifests/t/tsudev/TsuDisk/${V}`);
});

test('scoop: bucket chua len ban thi bo qua; len roi thi url tai exe', async () => {
  const m = (v) => ({ version: v, architecture: { '64bit': { url: [
    `https://github.com/tsudev-org/tsudisk/releases/download/v${v}/tsudisk_${v}_windows-x64-portable.exe#/tsudisk.exe`,
  ] } } });
  const skip = await resolveChannelUrl({ pkg, channel: 'scoop', version: V, fetchImpl: fakeFetch(res(200, m('26.11.2401'))) });
  assert.match(skip.skip, /26\.11\.2401/);
  const ok = await resolveChannelUrl({ pkg, channel: 'scoop', version: V, fetchImpl: fakeFetch(res(200, m(V))) });
  assert.match(ok.url, new RegExp(`/v${V}/tsudisk_${V}_windows-x64-portable\\.exe$`));
});

test('CLI: dry-run kenh github in url, khong lo token; tham so sai -> ma thoat 1, khong lo token', () => {
  const env = { ...process.env, TSU_VERSIONS_TOKEN: TOKEN };
  const ok = spawnSync(process.execPath, [SCRIPT, '--channel', 'github', '--version', V, '--dry-run'], { env, encoding: 'utf8' });
  assert.equal(ok.status, 0, ok.stderr);
  assert.match(ok.stdout, /releases\/tag\/v26\.11\.2601/);
  assert.ok(!(ok.stdout + ok.stderr).includes(TOKEN));
  const bad = spawnSync(process.execPath, [SCRIPT, '--channel', 'github', '--version', TOKEN], { env, encoding: 'utf8' });
  assert.equal(bad.status, 1);
  assert.ok(!(bad.stdout + bad.stderr).includes(TOKEN));
});

test('package.json sai khuon bi tu choi truoc khi dung URL goi mang', () => {
  const ok = { name: 'tsudisk', winget: 'tsudev.TsuDisk', release: { repo: 'tsudev-org/tsudisk' },
    assets: { 'windows-x64': { file: 'tsudisk_{version}_windows-x64-portable.exe' } } };
  assert.deepEqual(validatePackage(ok), ok);
  for (const bad of [
    { ...ok, name: '../evil' },
    { ...ok, name: 'a/b' },
    { ...ok, winget: 'x/../y' },
    { ...ok, release: { repo: 'evil-org/tsudisk' } },
    { ...ok, release: { repo: 'tsudev-org/a/../b' } },
    { ...ok, assets: { 'windows-x64': { file: '../x_{version}.exe' } } },
    { ...ok, assets: { 'windows-x64': { file: 'khong-co-ban.exe' } } },
  ]) assert.throws(() => validatePackage(bad), /sai khuon/);
});

test('raise: url ngoai https://github.com bi tu choi, khong goi mang', async () => {
  let called = false;
  const f = async () => { called = true; return new Response('{}', { status: 200 }); };
  for (const url of ['http://github.com/x', 'https://evil.example/x', 'https://github.com.evil.example/x',
    'https://u:p@github.com/x', 'khong-phai-url']) {
    await assert.rejects(raise({ pkg, channel: 'github', version: '26.11.2601', url, token: 'test-token-not-real',
      fetchImpl: f }), /url/);
  }
  assert.equal(called, false);
});
