// Gan the deploy website `deploy-YYYY.MM.DD.N` (VERSIONING.md muc 2.6) vao commit vua deploy va day len origin.
// Chay sau `wrangler deploy` trong `npm run deploy`. N dem trong ngay (gio Viet Nam), bat dau 1.
// Bo qua (khong loi) khi cay lam viec co thay doi chua commit: the phai tro dung ma nguon da deploy.
import { execFileSync } from 'node:child_process';

const git = (...args) => execFileSync('git', args, { encoding: 'utf8' }).trim();

if (git('status', '--porcelain', '--untracked-files=no')) {
  console.warn('[the-deploy] Cay lam viec co thay doi chua commit - khong gan the deploy.');
  process.exit(0);
}
const ngay = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' })
  .format(new Date()).replaceAll('-', '.');
const head = git('rev-parse', 'HEAD');
const coSan = git('tag', '--points-at', head).split('\n').find((t) => t.startsWith(`deploy-${ngay}.`));
if (coSan) {
  console.log(`[the-deploy] HEAD da co the ${coSan}.`);
  process.exit(0);
}
git('fetch', '--quiet', '--tags', 'origin');
const n = git('tag', '--list', `deploy-${ngay}.*`).split('\n').filter(Boolean)
  .map((t) => Number(t.slice(`deploy-${ngay}.`.length))).filter(Number.isInteger)
  .reduce((a, b) => Math.max(a, b), 0) + 1;
const the = `deploy-${ngay}.${n}`;
git('tag', '-a', the, '-m', `Website disk.tsudev.com ${the}`);
git('push', '--quiet', 'origin', the);
console.log(`[the-deploy] Da gan va day the ${the} (${head.slice(0, 7)}).`);
