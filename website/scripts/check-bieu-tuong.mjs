// UI1 (phiếu 20261124-UI1, DESIGN_SYSTEM mục 1 "Biểu tượng trên nền trạng thái"): mọi biểu tượng SVG hiện trên mọi trang
// đạt >= 3:1 so với NỀN THẬT phía sau (đi ngược cây DOM tới nền đặc, trộn lớp nền bán trong suốt) ở MỌI trạng thái:
// thường, di chuột (nút/liên kết chứa nó), đang chọn (aria-checked/aria-current/aria-pressed = true) - cả Sáng lẫn Tối.
// Hoạ tiết trang trí lớn (> 48px, aria-hidden) không phải biểu tượng nên bỏ qua.
// Chạy sau `npm run build`: `npm run check:bieu-tuong`.
import { chromium } from 'playwright';
import { danhSachTrang, moMayChu } from './may-chu-tinh.mjs';

const NGUONG = 3;
const { base, dong } = await moMayChu();
const browser = await chromium.launch();
const loi = [];
let soDo = 0;

// Chạy trong trang: đo mọi biểu tượng đang hiện, trả về [{ten, mau, nen, tiLe}].
function doTrongTrang() {
  const rgba = (s) => {
    const m = s.match(/rgba?\(([^)]+)\)/);
    if (!m) return null;
    const [r, g, b, a = 1] = m[1].split(/[\s,/]+/).filter(Boolean).map(Number);
    return [r, g, b, a];
  };
  const tron = (tren, duoi) => tren.slice(0, 3).map((c, i) => c * tren[3] + duoi[i] * (1 - tren[3])).concat(1);
  const nenThat = (el) => {
    const lop = [];
    for (let e = el; e; e = e.parentElement) {
      const c = rgba(getComputedStyle(e).backgroundColor);
      if (c && c[3] > 0) { lop.push(c); if (c[3] >= 1) break; }
    }
    let nen = [255, 255, 255, 1];
    for (const c of lop.reverse()) nen = tron(c, nen);
    return nen;
  };
  const doSang = ([r, g, b]) => {
    const k = (v) => { v /= 255; return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; };
    return 0.2126 * k(r) + 0.7152 * k(g) + 0.0722 * k(b);
  };
  const out = [];
  for (const svg of document.querySelectorAll('svg')) {
    const hop = svg.getBoundingClientRect();
    if (hop.width === 0 || hop.height === 0 || hop.width > 48 || hop.height > 48) continue;
    const st = getComputedStyle(svg);
    if (st.visibility === 'hidden' || st.display === 'none') continue;
    const net = st.stroke && st.stroke !== 'none' ? st.stroke : st.fill;
    const c = rgba(net);
    if (!c) continue;
    const nen = nenThat(svg.parentElement);
    const mau = tron(c, nen);
    const [x, y] = [doSang(mau), doSang(nen)];
    const tiLe = (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
    const chu = svg.closest('button,a,[role]');
    const ten = chu?.getAttribute('aria-label') || chu?.getAttribute('title') || chu?.textContent?.trim().slice(0, 30) || svg.outerHTML.slice(0, 50);
    out.push({ ten, mau: net, nen: `rgb(${nen.slice(0, 3).map(Math.round).join(',')})`, tiLe });
  }
  return out;
}

async function kiem(page, nhan) {
  for (const d of await page.evaluate(doTrongTrang)) {
    soDo++;
    if (d.tiLe < NGUONG) loi.push(`${nhan} "${d.ten}": ${d.mau} trên ${d.nen} = ${d.tiLe.toFixed(2)}:1`);
  }
}

for (const scheme of ['light', 'dark']) {
  const ctx = await browser.newContext({ colorScheme: scheme, reducedMotion: 'reduce' });
  await ctx.addInitScript((t) => { try { localStorage.setItem('tsudev-theme', t); } catch { /* bị chặn */ } }, scheme);
  const page = await ctx.newPage();
  for (const duong of danhSachTrang()) {
    await page.goto(base + duong);
    await kiem(page, `${scheme} ${duong} thường`);
    // Đang chọn: lần lượt bật trạng thái chọn cho TỪNG phần tử chọn được có biểu tượng (chỉ đổi thuộc tính ARIA để
    // CSS vẽ trạng thái đó, không đổi giao diện trang).
    const chon = page.locator('[aria-checked]:has(svg), [aria-pressed]:has(svg), a:has(svg)');
    const n = await chon.count();
    for (let i = 0; i < n; i++) {
      const el = chon.nth(i);
      if (!(await el.isVisible())) continue;
      const truoc = await el.evaluate((e) => ['aria-checked', 'aria-pressed', 'aria-current'].map((a) => [a, e.getAttribute(a)]));
      await el.evaluate((e) => {
        if (e.hasAttribute('aria-checked')) e.setAttribute('aria-checked', 'true');
        else if (e.hasAttribute('aria-pressed')) e.setAttribute('aria-pressed', 'true');
        else e.setAttribute('aria-current', 'page');
      });
      await kiem(page, `${scheme} ${duong} đang chọn #${i}`);
      await el.hover();
      await kiem(page, `${scheme} ${duong} đang chọn + di chuột #${i}`);
      await el.evaluate((e, ds) => { for (const [a, v] of ds) { if (v === null) e.removeAttribute(a); else e.setAttribute(a, v); } }, truoc);
      await el.hover();
      await kiem(page, `${scheme} ${duong} di chuột #${i}`);
      await page.mouse.move(0, 0);
    }
  }
  await ctx.close();
}

await browser.close();
await dong();
if (soDo === 0) loi.push('không đo được biểu tượng nào - bộ chọn hỏng?');
if (loi.length) {
  console.error(`check-bieu-tuong: ${loi.length} biểu tượng dưới ${NGUONG}:1\n  ${[...new Set(loi)].join('\n  ')}`);
  process.exit(1);
}
console.log(`check-bieu-tuong: ${soDo} lượt đo, mọi biểu tượng >= ${NGUONG}:1 ở Sáng/Tối, thường/di chuột/đang chọn.`);
