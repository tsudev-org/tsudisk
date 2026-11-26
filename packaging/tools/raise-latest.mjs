// Nang `latest` cua mot kenh phat hanh tren ban ke phien ban tsudev.com (ADR-0012 muc 12, APP_INTEGRITY 2.1).
// Dung chung cho job `nang-latest` cua release.yml (kenh github) va workflow kenh-phat-hanh.yml (github/winget/scoop).
//
//   TSU_VERSIONS_TOKEN=... node packaging/tools/raise-latest.mjs --channel github|winget|scoop --version 26.11.2601 \
//     [--retries 3] [--retry-delay 120] [--dry-run]
//
// Chot chan (khong nang khi kenh chua co ban de tai - nguoi dung bi khoa ma khong co loi ra):
//   - github: url = trang Release `v<ban>`; may chu tu GET url do.
//   - winget: chi nang khi `manifests/t/tsudev/TsuDisk/<ban>` da co tren microsoft/winget-pkgs (PR da gop).
//   - scoop : chi nang khi bucket/tsudisk.json o tsudev-org/scoop-bucket co `version` = ban moi; url = URL tai exe.
// Ket qua: 2xx = da nang; 409 `version_not_newer` = da nang tu truoc (xong); 409 `download_unverified` = thu lai;
// kenh chua co ban = bo qua (ma thoat 0, in `skip`). KHONG BAO GIO in token: moi dong in ra deu qua `redact`.
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const ENDPOINT = 'https://tsudev.com/api/versions/raise';
export const VERSION_RE = /^[0-9]{2}\.[1-9][0-9]?\.[0-3][0-9][0-9]{2}$/;
export const CHANNELS = ['github', 'winget', 'scoop'];
const SCOOP_BUCKET = 'tsudev-org/scoop-bucket';
const WINGET_REPO = 'microsoft/winget-pkgs';

export function loadPackage() {
  return JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', 'package.json'), 'utf8'));
}

/** Thu muc manifest winget cua goi, vd `manifests/t/tsudev/TsuDisk`. */
export function wingetDir(pkg) {
  const id = pkg.winget;
  return `manifests/${id[0].toLowerCase()}/${id.split('.').join('/')}`;
}

export function raiseEndpoint(pkg, channel) {
  if (!CHANNELS.includes(channel)) throw new Error(`kenh khong ho tro: ${channel}`);
  return `${ENDPOINT}/${encodeURIComponent(pkg.name)}/${channel}`;
}

export function githubReleaseUrl(pkg, version) {
  return `https://github.com/${pkg.release.repo}/releases/tag/v${version}`;
}

export function wingetPageUrl(pkg, version) {
  return `https://github.com/${WINGET_REPO}/tree/master/${wingetDir(pkg)}/${version}`;
}

/** URL tai exe CLI trong manifest Scoop (bo `#/ten.exe`), hoac null neu manifest khong khop ban/khong hop le. */
export function scoopDownloadUrl(manifest, pkg, version) {
  if (!manifest || manifest.version !== version) return null;
  const arch = manifest.architecture?.['64bit']?.url ?? manifest.url;
  const urls = (Array.isArray(arch) ? arch : [arch]).filter((u) => typeof u === 'string');
  const prefix = `https://github.com/${pkg.release.repo}/releases/download/v${version}/`;
  const file = pkg.assets['windows-x64'].file.replace('{version}', version);
  const hit = urls.map((u) => u.split('#')[0]).find((u) => u === prefix + file);
  return hit ?? null;
}

export const redactor = (secret) => (s) =>
  secret && secret.length >= 8 ? String(s).split(secret).join('***') : String(s);

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/**
 * Xac dinh url cho kenh, kiem chot chan. Tra { url } hoac { skip: 'ly do' }.
 * `fetchImpl` tiem duoc de test; `ghToken` (tuy chon) chi de tranh gioi han tan suat API GitHub.
 */
export async function resolveChannelUrl({ pkg, channel, version, fetchImpl = fetch, ghToken }) {
  if (channel === 'github') return { url: githubReleaseUrl(pkg, version) };
  const headers = { 'user-agent': 'tsudisk-raise-latest', accept: 'application/vnd.github+json' };
  if (ghToken) headers.authorization = `Bearer ${ghToken}`;
  if (channel === 'winget') {
    const path = `${wingetDir(pkg)}/${version}`;
    const r = await fetchImpl(`https://api.github.com/repos/${WINGET_REPO}/contents/${path}`, { headers });
    if (r.status === 404) return { skip: `winget-pkgs chua co ${path} (PR chua gop)` };
    if (!r.ok) throw new Error(`GET winget-pkgs ${path} -> ${r.status}`);
    return { url: wingetPageUrl(pkg, version) };
  }
  if (channel === 'scoop') {
    const r = await fetchImpl(`https://api.github.com/repos/${SCOOP_BUCKET}/contents/bucket/${pkg.name}.json`, {
      headers: { ...headers, accept: 'application/vnd.github.raw+json' },
    });
    if (r.status === 404) return { skip: `${SCOOP_BUCKET} chua co bucket/${pkg.name}.json` };
    if (!r.ok) throw new Error(`GET ${SCOOP_BUCKET} bucket/${pkg.name}.json -> ${r.status}`);
    const manifest = JSON.parse(await r.text());
    const url = scoopDownloadUrl(manifest, pkg, version);
    if (!url) return { skip: `bucket Scoop dang o ${manifest?.version ?? '?'}, chua phai ${version}` };
    return { url };
  }
  throw new Error(`kenh khong ho tro: ${channel}`);
}

/**
 * POST nang `latest`. Tra { status: 'raised' | 'already', detail }. Nem loi khi that bai (sau khi het luot thu lai).
 */
export async function raise({
  pkg, channel, version, url, token, fetchImpl = fetch, retries = 3, retryDelayMs = 120_000, log = () => {},
}) {
  if (!VERSION_RE.test(version)) throw new Error(`phien ban sai khuon YY.M.DDNN: ${version}`);
  if (!token) throw new Error('thieu TSU_VERSIONS_TOKEN');
  const redact = redactor(token);
  const endpoint = raiseEndpoint(pkg, channel);
  for (let attempt = 0; ; attempt++) {
    const r = await fetchImpl(endpoint, {
      method: 'POST',
      headers: { 'x-versions-token': token, 'content-type': 'application/json', 'user-agent': 'tsudisk-raise-latest' },
      body: JSON.stringify({ latest: version, url }),
    });
    const text = await r.text();
    let body = {};
    try { body = text ? JSON.parse(text) : {}; } catch { /* than khong phai JSON */ }
    if (r.ok) return { status: 'raised', detail: redact(`${channel} -> ${version}`) };
    if (r.status === 409 && body.error === 'version_not_newer') {
      return { status: 'already', detail: redact(`${channel} dang o ${body.current ?? '?'} (>= ${version})`) };
    }
    const msg = redact(`POST ${endpoint} -> ${r.status} ${text.slice(0, 300)}`);
    if (r.status === 409 && body.error === 'download_unverified' && attempt < retries) {
      log(`${msg} - thu lai sau ${Math.round(retryDelayMs / 1000)} giay (${attempt + 1}/${retries})`);
      await sleep(retryDelayMs);
      continue;
    }
    throw new Error(msg);
  }
}

function parseArgs(argv) {
  const opt = (k) => { const i = argv.indexOf(`--${k}`); return i >= 0 ? argv[i + 1] : undefined; };
  return {
    channel: opt('channel'),
    version: opt('version'),
    retries: Number(opt('retries') ?? 3),
    retryDelayMs: Number(opt('retry-delay') ?? 120) * 1000,
    dry: argv.includes('--dry-run'),
  };
}

async function main() {
  const token = process.env.TSU_VERSIONS_TOKEN || '';
  const redact = redactor(token);
  const say = (m) => console.log(`[raise-latest] ${redact(m)}`);
  try {
    const a = parseArgs(process.argv.slice(2));
    if (!CHANNELS.includes(a.channel)) throw new Error(`--channel phai la mot trong: ${CHANNELS.join(', ')}`);
    if (!VERSION_RE.test(a.version ?? '')) throw new Error('--version phai theo khuon YY.M.DDNN (ban chinh thuc)');
    const pkg = loadPackage();
    const res = await resolveChannelUrl({ pkg, channel: a.channel, version: a.version, ghToken: process.env.GH_TOKEN });
    if (res.skip) { say(`skip ${a.channel}: ${res.skip}`); return; }
    say(`${a.channel} ${a.version} url=${res.url}`);
    if (a.dry) { say('dry-run - khong goi may chu'); return; }
    const out = await raise({
      pkg, channel: a.channel, version: a.version, url: res.url, token,
      retries: a.retries, retryDelayMs: a.retryDelayMs, log: say,
    });
    say(`${out.status === 'raised' ? 'da nang' : 'da nang tu truoc'}: ${out.detail}`);
  } catch (e) {
    console.error(`[raise-latest] ${redact(e instanceof Error ? e.message : e)}`);
    process.exitCode = 1;
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) await main();
