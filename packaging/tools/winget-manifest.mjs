// Sinh manifest winget (ManifestVersion 1.12.0, ba tep: version, installer, locale en-US + vi-VN) tu
// packaging/package.json va SHA256SUMS.txt cua ban phat hanh. Nguon duy nhat: .standards/docs/PACKAGING.md muc 3.
// Chay: node packaging/tools/winget-manifest.mjs --version 26.11.1801 --sums dist/SHA256SUMS.txt --out winget-out
//        [--date 2026-11-18]
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const args = Object.fromEntries(process.argv.slice(2).reduce((acc, a, i, all) => (a.startsWith('--') ? [...acc, [a.slice(2), all[i + 1]]] : acc), []));
const fail = (m) => { console.error(`[winget-manifest] ${m}`); process.exit(1); };
const version = args.version ?? fail('thieu --version');
if (!/^[0-9]{2}\.[1-9][0-9]?\.[0-3][0-9][0-9]{2}$/.test(version)) fail(`phien ban ${version} lech khuon YY.M.DDNN`);
const pkg = JSON.parse(readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', 'package.json'), 'utf8'));
const asset = pkg.assets['windows-x64'];
const file = asset.file.replace('{version}', version);
const sums = readFileSync(args.sums ?? fail('thieu --sums'), 'utf8');
const line = sums.split('\n').map((l) => l.trim().split(/\s+\*?/)).find(([, name]) => name === file);
const sha = line?.[0];
if (!sha || !/^[0-9a-f]{64}$/i.test(sha)) fail(`khong thay SHA-256 cua ${file} trong ${args.sums}`);
const date = args.date ?? new Date().toISOString().slice(0, 10);
const id = pkg.winget;
const url = `https://github.com/${pkg.release.repo}/releases/download/v${version}/${file}`;
const MV = '1.12.0';
const head = (kind) => `# yaml-language-server: $schema=https://aka.ms/winget-manifest.${kind}.${MV}.schema.json\n\n`;
const q = (s) => `'${String(s).replaceAll("'", "''")}'`;
const dir = join(args.out ?? 'winget-out', 'manifests', id[0].toLowerCase(), ...id.split('.'), version);
mkdirSync(dir, { recursive: true });
const files = {
  [`${id}.yaml`]: head('version') + `PackageIdentifier: ${id}\nPackageVersion: ${version}\nDefaultLocale: en-US\nManifestType: version\nManifestVersion: ${MV}\n`,
  [`${id}.installer.yaml`]: head('installer') + [
    `PackageIdentifier: ${id}`, `PackageVersion: ${version}`, `MinimumOSVersion: ${asset.minimumOs}`, 'InstallerType: portable',
    'Commands:', `- ${asset.command}`, `ReleaseDate: ${date}`, 'Installers:', '- Architecture: x64', `  InstallerUrl: ${url}`,
    `  InstallerSha256: ${sha.toUpperCase()}`, 'ManifestType: installer', `ManifestVersion: ${MV}`, '',
  ].join('\n'),
  [`${id}.locale.en-US.yaml`]: head('defaultLocale') + [
    `PackageIdentifier: ${id}`, `PackageVersion: ${version}`, 'PackageLocale: en-US', `Publisher: ${pkg.publisher}`,
    'PublisherUrl: https://tsudev.com', `PublisherSupportUrl: https://github.com/${pkg.release.repo}/issues`,
    `PrivacyUrl: ${pkg.homepage}/quyen-rieng-tu/`, `PackageName: TsuDisk`, `PackageUrl: ${pkg.homepage}`, `License: ${pkg.license}`,
    `LicenseUrl: ${pkg.homepage}/ban-quyen/`, `ShortDescription: ${q(pkg.description.en)}`,
    `Moniker: ${asset.command}`, 'Tags:', '- disk', '- partition', '- recovery', '- winre', `ReleaseNotesUrl: https://github.com/${pkg.release.repo}/releases/tag/v${version}`,
    'ManifestType: defaultLocale', `ManifestVersion: ${MV}`, '',
  ].join('\n'),
  [`${id}.locale.vi-VN.yaml`]: head('locale') + [
    `PackageIdentifier: ${id}`, `PackageVersion: ${version}`, 'PackageLocale: vi-VN', `Publisher: ${pkg.publisher}`,
    `PackageName: TsuDisk`, `PackageUrl: ${pkg.homepage}`, `License: ${pkg.license}`, `ShortDescription: ${q(pkg.description.vi)}`,
    'ManifestType: locale', `ManifestVersion: ${MV}`, '',
  ].join('\n'),
};
for (const [name, body] of Object.entries(files)) writeFileSync(join(dir, name), body, 'utf8');
console.log(`[winget-manifest] ${id} ${version} -> ${dir}`);
