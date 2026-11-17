// Nop manifest winget (do winget-manifest.mjs sinh) vao microsoft/winget-pkgs bang GitHub REST API - khong clone kho
// winget-pkgs (rat lon). Dung cho ca goi MOI (lan dau) lan phien ban moi. .standards/docs/PACKAGING.md muc 7.
//
//   WINGET_TOKEN=... node packaging/tools/winget-submit.mjs --root <thu-muc-goc-manifest> --version 26.11.1802 [--dry-run]
//
// <thu-muc-goc-manifest> chua `manifests/t/tsudev/TsuDisk/<phien-ban>/*.yaml` (giai nen tu
// tsudisk_<ver>_winget-manifest.zip). WINGET_TOKEN: PAT classic `public_repo` cua tai khoan dong goi; chua co fork
// `<tai-khoan>/winget-pkgs` thi tao. Da co PR (dang mo hoac da gop) cho dung phien ban thi bo qua. Khong in token.
import { appendFileSync, readFileSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const argv = process.argv.slice(2);
const opt = (k) => { const i = argv.indexOf(`--${k}`); return i >= 0 ? argv[i + 1] : undefined; };
const fail = (m) => { console.error(`[winget-submit] ${m}`); process.exit(1); };
const root = opt('root') ?? fail('thieu --root');
const version = opt('version') ?? fail('thieu --version');
const dry = argv.includes('--dry-run');
const token = process.env.WINGET_TOKEN || fail('thieu WINGET_TOKEN');
const UP = 'microsoft/winget-pkgs';

const pkg = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', 'package.json'), 'utf8'));
const id = pkg.winget;
const pkgDir = `manifests/${id[0].toLowerCase()}/${id.split('.').join('/')}`;
const rel = `${pkgDir}/${version}`;
const names = readdirSync(join(root, rel)).filter((n) => n.endsWith('.yaml')).sort();
if (names.length < 3) fail(`khong thay manifest trong ${join(root, rel)}`);
const files = names.map((n) => ({ path: `${rel}/${n}`, body: readFileSync(join(root, rel, n), 'utf8') }));
for (const f of files) if (!f.body.includes(`\nPackageVersion: ${version}\n`)) fail(`${f.path} lech phien ban ${version}`);

async function gh(method, path, body, okMissing = false) {
  const r = await fetch(`https://api.github.com/${path}`, {
    method,
    headers: {
      authorization: `Bearer ${token}`, accept: 'application/vnd.github+json', 'x-github-api-version': '2022-11-28',
      'user-agent': 'tsudisk-winget-submit', ...(body ? { 'content-type': 'application/json' } : {}),
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  if (okMissing && r.status === 404) return null;
  const text = await r.text();
  if (!r.ok) fail(`${method} ${path} -> ${r.status} ${text.slice(0, 300)}`);
  return text ? JSON.parse(text) : {};
}
const output = (k, v) => { if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, `${k}=${v}\n`); };
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const login = (await gh('GET', 'user')).login;
const fork = `${login}/winget-pkgs`;
const branch = `${id}-${version}`;

const q = encodeURIComponent(`repo:${UP} is:pr in:title "${id} version ${version}"`);
const found = (await gh('GET', `search/issues?q=${q}`)).items
  .find((i) => i.title.endsWith(`${id} version ${version}`) && (i.state === 'open' || i.pull_request?.merged_at));
if (found) { console.log(`[winget-submit] da co PR: ${found.html_url} - bo qua`); output('pr', found.html_url); process.exit(0); }

const isNew = (await gh('GET', `repos/${UP}/contents/${pkgDir}`, undefined, true)) === null;
const title = `${isNew ? 'New package' : 'New version'}: ${id} version ${version}`;
console.log(`[winget-submit] ${title} (tai khoan nop: ${login}, nhanh: ${branch})`);
if (dry) { for (const f of files) console.log(`  ${f.path}`); process.exit(0); }

if (!(await gh('GET', `repos/${fork}`, undefined, true))) {
  await gh('POST', `repos/${UP}/forks`, { default_branch_only: true });
  for (let i = 0; i < 30 && !(await gh('GET', `repos/${fork}/branches/master`, undefined, true)); i++) await sleep(10_000);
}
await gh('POST', `repos/${fork}/merge-upstream`, { branch: 'master' });
const base = (await gh('GET', `repos/${fork}/git/ref/heads/master`)).object.sha;

// Mot commit chua du cac tep manifest: blob -> tree -> commit -> nhanh.
const tree = [];
for (const f of files) {
  const blob = await gh('POST', `repos/${fork}/git/blobs`, { content: Buffer.from(f.body).toString('base64'), encoding: 'base64' });
  tree.push({ path: f.path, mode: '100644', type: 'blob', sha: blob.sha });
}
const t = await gh('POST', `repos/${fork}/git/trees`, { base_tree: base, tree });
const c = await gh('POST', `repos/${fork}/git/commits`, { message: title, tree: t.sha, parents: [base] });
if (await gh('GET', `repos/${fork}/git/ref/heads/${branch}`, undefined, true)) {
  await gh('PATCH', `repos/${fork}/git/refs/heads/${branch}`, { sha: c.sha, force: true });
} else {
  await gh('POST', `repos/${fork}/git/refs`, { ref: `refs/heads/${branch}`, sha: c.sha });
}

const body = [
  `Release: https://github.com/${pkg.release.repo}/releases/tag/v${version}`,
  '',
  "Manifests are generated from the publisher's `packaging/package.json` and validated against the 1.12.0 JSON schemas; "
    + '`InstallerSha256` comes from the `SHA256SUMS.txt` published with the release (signed with Sigstore, build provenance '
    + 'attested by GitHub).',
  '',
  'TsuDisk is a portable command-line tool (`tsudisk`). Commands that change disks require an elevated terminal; '
    + '`tsudisk danh-sach` (list disks) and `tsudisk phien-ban` (version) run without elevation.',
].join('\n');
const pr = await gh('POST', `repos/${UP}/pulls`, { title, head: `${login}:${branch}`, base: 'master', body });
console.log(`[winget-submit] PR: ${pr.html_url}`);
output('pr', pr.html_url);
