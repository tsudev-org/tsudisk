// Ngân sách tốc độ web (LANGUAGE_SELECTION mục 5.1, phiếu QR1 STD-SYNC): mọi trang trong dist/ phải đạt
//   LCP <= 2,5 giây (máy tầm trung: CPU chậm 4x; mạng 4G chậm: 150 ms RTT, 1,6 Mb/s xuống, 750 kb/s lên),
//   CLS <= 0,1, JavaScript tải ban đầu (gzip) <= 170 KB. Vượt => mã thoát 1 (chặn CI).
// Chạy sau `npm run build`: `npm run check:toc-do`.
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { gzipSync } from 'node:zlib';
import { chromium } from 'playwright';
import { danhSachTrang, moMayChu } from './may-chu-tinh.mjs';

const NGAN_SACH = { lcpMs: 2500, cls: 0.1, jsGzipKb: 170 };
const DIST = new URL('../dist/', import.meta.url).pathname;
const { base, dong } = await moMayChu();
const browser = await chromium.launch();
const loi = [];
const bang = [];

function jsBanDau(duong) {
  const tep = join(DIST, duong.endsWith('/') ? duong + 'index.html' : duong);
  const html = readFileSync(tep, 'utf8');
  let tong = 0;
  for (const m of html.matchAll(/<script[^>]*\bsrc="(\/[^"]+)"/g)) tong += gzipSync(readFileSync(join(DIST, m[1]))).length;
  return tong / 1024;
}

for (const duong of danhSachTrang()) {
  const ctx = await browser.newContext({ viewport: { width: 412, height: 823 }, deviceScaleFactor: 2, isMobile: true });
  const page = await ctx.newPage();
  const cdp = await ctx.newCDPSession(page);
  await cdp.send('Network.enable');
  await cdp.send('Network.emulateNetworkConditions', {
    offline: false, latency: 150, downloadThroughput: (1.6 * 1024 * 1024) / 8, uploadThroughput: (750 * 1024) / 8,
  });
  await cdp.send('Emulation.setCPUThrottlingRate', { rate: 4 });
  await page.addInitScript(() => {
    window.__lcp = 0;
    window.__cls = 0;
    new PerformanceObserver((l) => { for (const e of l.getEntries()) window.__lcp = e.renderTime || e.loadTime || e.startTime; })
      .observe({ type: 'largest-contentful-paint', buffered: true });
    new PerformanceObserver((l) => { for (const e of l.getEntries()) if (!e.hadRecentInput) window.__cls += e.value; })
      .observe({ type: 'layout-shift', buffered: true });
  });
  await page.goto(base + duong, { waitUntil: 'load' });
  await page.waitForTimeout(800);   // để LCP/CLS cuối cùng kịp ghi
  const { lcp, cls } = await page.evaluate(() => ({ lcp: window.__lcp, cls: window.__cls }));
  const js = jsBanDau(duong);
  bang.push(`${duong.padEnd(28)} LCP ${(lcp / 1000).toFixed(2)} s  CLS ${cls.toFixed(3)}  JS ${js.toFixed(1)} KB`);
  if (!(lcp > 0)) loi.push(`${duong}: không đo được LCP`);
  if (lcp > NGAN_SACH.lcpMs) loi.push(`${duong}: LCP ${(lcp / 1000).toFixed(2)} s > 2,5 s`);
  if (cls > NGAN_SACH.cls) loi.push(`${duong}: CLS ${cls.toFixed(3)} > 0,1`);
  if (js > NGAN_SACH.jsGzipKb) loi.push(`${duong}: JS ban đầu ${js.toFixed(1)} KB gzip > 170 KB`);
  await ctx.close();
}

await browser.close();
await dong();
console.log(bang.join('\n'));
if (loi.length) {
  console.error(`check-toc-do: vượt ngân sách\n  ${loi.join('\n  ')}`);
  process.exit(1);
}
console.log(`check-toc-do: ${bang.length} trang đạt ngân sách (LCP <= 2,5 s, CLS <= 0,1, JS <= 170 KB gzip).`);
