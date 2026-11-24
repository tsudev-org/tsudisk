#!/usr/bin/env node
/**
 * Dây chuyền thương hiệu của sản phẩm - MỘT lệnh sinh mọi bản logo / icon / favicon từ
 * MỘT ảnh gốc. Đổi logo sau này = thay `assets/brand/source/<tệp>` + chạy lại lệnh này.
 *
 *   node website/scripts/brand-assets.mjs           # sinh lại, ghi tệp
 *   node website/scripts/brand-assets.mjs --check   # CI: báo lỗi nếu tệp đã commit lệch nguồn
 *
 * Cấu hình: `assets/brand/brand.json` (đường dẫn ảnh gốc, chế độ xử lý nền, màu nền đặc,
 * chữ trên ảnh OG). Cần `sharp` (Astro đã kéo sẵn trong website/node_modules).
 *
 * Chế độ xử lý ảnh gốc (`mode`):
 *   - "tile"    : logo vẽ sẵn trên NỀN ĐẶC (ô biểu tượng ứng dụng). Cắt vuông chính giữa, bo
 *                 góc theo `tileRadius`. KHÔNG xoá nền - nét trắng trên nền tối sẽ biến mất
 *                 trên nền Sáng nếu xoá.
 *   - "no-white": logo trên nền TRẮNG. Xoá vùng trắng NỐI LIỀN mép ảnh bằng flood fill (lòng
 *                 logo khép kín giữ nguyên), làm mềm biên + khử nhân với trắng (không quầng xám
 *                 trên nền tối), cắt sát, chừa lề `padding`.
 *
 * Định dạng đầu ra (website/public/ và assets/brand/app/):
 *   - brand/logo-<id>-{48,96,144,192}.{avif,webp} + brand/logo-<id>.png (512): <picture> ở
 *     header - AVIF/WebP nhỏ hơn PNG 5-20 lần, PNG chỉ là đường lui.
 *   - favicon.ico 7 lớp THẬT (16-256), favicon-{16,32,96}.png, android-chrome-{192,512}.png,
 *     maskable riêng (nền đặc, vùng an toàn 80%), apple-touch-icon 180 nền đặc, og-image 1200x630.
 *   - assets/brand/app/: icon-{16..1024}.png + app.ico 7 lớp cho ứng dụng desktop.
 */
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const WEB = path.resolve(HERE, '..');
const ROOT = path.resolve(WEB, '..');
const require = createRequire(path.join(WEB, 'package.json'));
const sharp = require('sharp');

const CFG = JSON.parse(fs.readFileSync(path.join(ROOT, 'assets/brand/brand.json'), 'utf8'));
const CHECK = process.argv.includes('--check');
const outputs = new Map(); // đường dẫn tuyệt đối -> Buffer

const emit = (abs, buf) => outputs.set(abs, buf);
const pub = (rel) => path.join(WEB, 'public', rel);
const app = (rel) => path.join(ROOT, 'assets/brand/app', rel);
const hex = (h) => ({
  r: parseInt(h.slice(1, 3), 16),
  g: parseInt(h.slice(3, 5), 16),
  b: parseInt(h.slice(5, 7), 16),
  alpha: 1,
});
const CLEAR = { r: 0, g: 0, b: 0, alpha: 0 };

/**
 * Ô biểu tượng, bo góc (mặt nạ SVG). Có `crop` [trái, trên, rộng, cao]: chỉ lấy vùng biểu tượng của ảnh gốc (bỏ chữ),
 * đặt giữa ô vuông nền `bg` với lề `pad` - favicon 16px vẫn đọc được. Không có `crop`: cắt vuông chính giữa ảnh.
 */
async function tile(buf, { crop, pad = 0.14, bg, radius = 0.22 } = {}) {
  const big = 1024;
  let src = buf;
  if (crop) {
    const [left, top, width, height] = crop;
    const side = Math.round(Math.max(width, height) * (1 + 2 * pad));
    const region = await sharp(buf).extract({ left, top, width, height }).png().toBuffer();
    src = await sharp({ create: { width: side, height: side, channels: 4, background: bg } })
      .composite([{ input: region, left: Math.round((side - width) / 2), top: Math.round((side - height) / 2) }])
      .png()
      .toBuffer();
  }
  const m = await sharp(src).metadata();
  const side = Math.min(m.width, m.height);
  const sq = await sharp(src)
    .extract({ left: Math.floor((m.width - side) / 2), top: Math.floor((m.height - side) / 2), width: side, height: side })
    .resize(big, big, { kernel: 'lanczos3' })
    .png()
    .toBuffer();
  const r = Math.round(big * radius);
  const mask = Buffer.from(
    `<svg width="${big}" height="${big}"><rect width="${big}" height="${big}" rx="${r}" ry="${r}"/></svg>`
  );
  return { rounded: await sharp(sq).composite([{ input: mask, blend: 'dest-in' }]).png().toBuffer(), square: sq };
}

/**
 * Logo VECTOR xoá nền (BRAND_ASSETS 15.4, 24/11/2026): `vector.dark` (nền Tối), `vector.light` (nền Sáng),
 * `vector.adaptive` (theo prefers-color-scheme) sinh bởi tsudev `packages/brand/build-vector-marks.js` từ ảnh gốc
 * PNG ở trên. Ô nền đặc (favicon.ico dự phòng, icon ứng dụng, apple-touch, maskable) dựng từ SVG nên sắc ở mọi cỡ;
 * `mark` là logo trong suốt cho og-image.
 */
async function vectorTile(v) {
  const big = 1024;
  const read = (f) => fs.readFileSync(path.join(ROOT, 'assets/brand', f));
  const svg = { dark: read(v.dark), light: read(v.light), adaptive: read(v.adaptive) };
  const vb = /viewBox="0 0 ([\d.]+) ([\d.]+)"/.exec(svg.dark.toString())
    .slice(1)
    .map(Number);
  const render = (side) =>
    sharp(svg.dark, { density: Math.min(2400, (72 * side * 2) / Math.max(...vb)) })
      .resize(side, side, { fit: 'contain', background: CLEAR, kernel: 'lanczos3' })
      .png()
      .toBuffer();
  const solid = hex(CFG.solidBg);
  const inner = Math.round(big * (v.tileScale ?? 0.84));
  const onTile = async () =>
    sharp({ create: { width: big, height: big, channels: 4, background: solid } })
      .composite([{ input: await render(inner), gravity: 'center' }])
      .png()
      .toBuffer();
  const square = await onTile();
  const r = Math.round(big * (CFG.tileRadius ?? 0.22));
  const mask = Buffer.from(
    `<svg width="${big}" height="${big}"><rect width="${big}" height="${big}" rx="${r}" ry="${r}"/></svg>`
  );
  const rounded = await sharp(square).composite([{ input: mask, blend: 'dest-in' }]).png().toBuffer();
  return { rounded, square, mark: await render(big), svg };
}

/** Xoá nền trắng nối liền mép; giữ mọi mảng trắng nằm TRONG logo. */
async function noWhite(buf, { inner = 12, outer = 40 } = {}) {
  const { data, info } = await sharp(buf).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const { width: W, height: H } = info;
  const C = 4;
  const dev = (p) => 255 - Math.min(data[p * C], data[p * C + 1], data[p * C + 2]);
  const bg = new Uint8Array(W * H);
  const st = [];
  const push = (x, y) => {
    if (x < 0 || y < 0 || x >= W || y >= H) return;
    const p = y * W + x;
    if (bg[p] || dev(p) > outer) return;
    bg[p] = 1;
    st.push(x, y);
  };
  for (let x = 0; x < W; x++) {
    push(x, 0);
    push(x, H - 1);
  }
  for (let y = 0; y < H; y++) {
    push(0, y);
    push(W - 1, y);
  }
  while (st.length) {
    const y = st.pop();
    const x = st.pop();
    push(x + 1, y);
    push(x - 1, y);
    push(x, y + 1);
    push(x, y - 1);
  }
  for (let p = 0; p < W * H; p++) {
    if (!bg[p]) continue;
    const d = dev(p);
    const a = d <= inner ? 0 : Math.round(((d - inner) / (outer - inner)) * 255);
    if (a > 0 && a < 255) {
      const k = a / 255;
      for (let c = 0; c < 3; c++) {
        const v = (data[p * C + c] - (1 - k) * 255) / k;
        data[p * C + c] = Math.max(0, Math.min(255, Math.round(v)));
      }
    }
    data[p * C + 3] = a;
  }
  const png = await sharp(data, { raw: { width: W, height: H, channels: C } }).png().toBuffer();
  const trimmed = await sharp(png).trim({ threshold: 1 }).png().toBuffer();
  const m = await sharp(trimmed).metadata();
  const side = Math.round(Math.max(m.width, m.height) * (1 + 2 * (CFG.padding ?? 0.06)));
  const sq = await sharp({ create: { width: side, height: side, channels: 4, background: CLEAR } })
    .composite([{ input: trimmed, left: Math.round((side - m.width) / 2), top: Math.round((side - m.height) / 2) }])
    .png()
    .toBuffer();
  return { rounded: sq, square: sq };
}

/** ICO thật nhiều lớp (PNG bên trong mỗi lớp). */
function buildIco(entries) {
  const header = Buffer.alloc(6);
  header.writeUInt16LE(0, 0);
  header.writeUInt16LE(1, 2);
  header.writeUInt16LE(entries.length, 4);
  const dir = Buffer.alloc(16 * entries.length);
  let offset = header.length + dir.length;
  entries.forEach((e, i) => {
    const b = i * 16;
    dir.writeUInt8(e.size >= 256 ? 0 : e.size, b);
    dir.writeUInt8(e.size >= 256 ? 0 : e.size, b + 1);
    dir.writeUInt16LE(1, b + 4);
    dir.writeUInt16LE(32, b + 6);
    dir.writeUInt32LE(e.data.length, b + 8);
    dir.writeUInt32LE(offset, b + 12);
    offset += e.data.length;
  });
  return Buffer.concat([header, dir, ...entries.map((e) => e.data)]);
}

const esc = (s) => String(s).replace(/[<>&'"]/g, (c) => `&#${c.charCodeAt(0)};`);

async function main() {
  const raw = fs.readFileSync(path.join(ROOT, 'assets/brand', CFG.source));
  const SOLID = hex(CFG.solidBg);
  const V = CFG.vector ? await vectorTile(CFG.vector) : null;
  const { rounded, square } = V ?? (CFG.mode === 'tile'
    ? await tile(raw, { crop: CFG.iconCrop, pad: CFG.iconPadding, bg: SOLID, radius: CFG.tileRadius })
    : await noWhite(raw));
  const at = (size, src = rounded) =>
    sharp(src).resize(size, size, { fit: 'contain', background: CLEAR, kernel: 'lanczos3' }).png({ compressionLevel: 9 }).toBuffer();
  const png = async (size, src) => sharp(await at(size, src)).png({ palette: true, quality: 92, effort: 10, compressionLevel: 9 }).toBuffer();
  const onSolid = async (size, scale, src = rounded) => {
    const inner = Math.round(size * scale);
    const pad = Math.round((size - inner) / 2);
    return sharp({ create: { width: size, height: size, channels: 4, background: SOLID } })
      .composite([{ input: await at(inner, src), top: pad, left: pad }])
      .flatten({ background: SOLID })
      .removeAlpha()
      .png({ compressionLevel: 9 })
      .toBuffer();
  };
  const ico = async (src) => {
    const entries = [];
    for (const size of [16, 24, 32, 48, 64, 128, 256]) entries.push({ size, data: await at(size, src) });
    return buildIco(entries);
  };

  // --- Logo cho giao diện website (<picture>).
  if (V) {
    emit(pub(`brand/logo-${CFG.id}.svg`), V.svg.dark);
    emit(pub(`brand/logo-${CFG.id}-light.svg`), V.svg.light);
    emit(pub('favicon.svg'), V.svg.adaptive);
  } else {
    for (const s of [48, 96, 144, 192]) {
      const base = sharp(await at(s));
      emit(pub(`brand/logo-${CFG.id}-${s}.avif`), await base.clone().avif({ quality: 70, effort: 9 }).toBuffer());
      emit(pub(`brand/logo-${CFG.id}-${s}.webp`), await base.clone().webp({ quality: 90, effort: 6 }).toBuffer());
    }
  }
  emit(pub(`brand/logo-${CFG.id}.png`), await png(512));

  // --- Logo đầy đủ (biểu tượng + chữ) cho chỗ hiển thị lớn: AVIF/WebP theo bề rộng, PNG dự phòng.


  // --- Dấu hiệu tsudev cho chân trang / khối "nhà làm" (BRAND_ASSETS.md mục 15 quy tắc 5), cũng sinh từ bản gốc.
  if (CFG.ecosystem) {
    const eco = await tile(fs.readFileSync(path.join(ROOT, 'assets/brand', CFG.ecosystem.source)), { bg: CLEAR });
    for (const s of [48, 96, 144]) {
      const base = sharp(await sharp(eco.rounded).resize(s, s, { kernel: 'lanczos3' }).png().toBuffer());
      emit(pub(`brand/logo-${CFG.ecosystem.id}-${s}.avif`), await base.clone().avif({ quality: 70, effort: 9 }).toBuffer());
      emit(pub(`brand/logo-${CFG.ecosystem.id}-${s}.webp`), await base.clone().webp({ quality: 90, effort: 6 }).toBuffer());
    }
  }

  // --- Favicon + PWA.
  emit(pub('favicon.ico'), await ico(rounded));
  for (const s of [16, 32, 96]) emit(pub(`favicon-${s}x${s}.png`), await png(s));
  for (const s of [192, 512]) {
    emit(pub(`android-chrome-${s}x${s}.png`), await png(s));
    emit(pub(`android-chrome-maskable-${s}x${s}.png`), await onSolid(s, 0.8, square));
  }
  // iOS tô đen chỗ trong suốt và tự bo góc: nền đặc, ô vuông đầy (tile) hoặc logo 86% (no-white).
  emit(pub('apple-touch-icon.png'), await onSolid(180, CFG.mode === 'tile' ? 1 : 0.86, square));

  // --- Ảnh chia sẻ 1200x630 (nền tối là hằng thương hiệu, không theo chế độ người xem).
  const W = 1200;
  const H = 630;
  const L = 300;
  const back = Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}">
    <defs><radialGradient id="g" cx="0.28" cy="0.5" r="0.75"><stop offset="0%" stop-color="${CFG.ogGlow}"/><stop offset="100%" stop-color="#0a0a0a"/></radialGradient></defs>
    <rect width="${W}" height="${H}" fill="url(#g)"/>
    ${(CFG.ogMotif ?? []).map(([x, w, trong]) => trong
      ? `<rect x="${x + 2}" y="560" width="${w - 4}" height="34" rx="8" fill="none" stroke="${CFG.ogAccent}" stroke-opacity="0.4" stroke-width="2" stroke-dasharray="10 7"/>`
      : `<rect x="${x}" y="558" width="${w}" height="38" rx="8" fill="${CFG.ogAccent}" fill-opacity="0.15"/>`).join('')}
    <text x="500" y="270" font-family="DejaVu Sans, Verdana, sans-serif" font-size="68" font-weight="700" fill="#ededed">${esc(CFG.name)}</text>
    ${CFG.ogLines.map((l, i) => `<text x="500" y="${340 + i * 44}" font-family="DejaVu Sans, Verdana, sans-serif" font-size="30" fill="#b4b4b4">${esc(l)}</text>`).join('')}
    <text x="500" y="${360 + CFG.ogLines.length * 44 + 20}" font-family="DejaVu Sans, Verdana, sans-serif" font-size="26" fill="${CFG.ogAccent}">${esc(CFG.domain)}</text>
  </svg>`);
  emit(
    pub('og-image.png'),
    await sharp(back)
      .composite([{ input: await at(L, V ? V.mark : rounded), left: 140, top: Math.round((H - L) / 2) }])
      .png({ palette: true, quality: 92, effort: 10, compressionLevel: 9 })
      .toBuffer()
  );

  // --- Icon ứng dụng desktop (exe, bộ cài, lối tắt).
  // Cỡ theo `appSizes` (mặc định tới 512; Tauri cần thêm 1024 làm đầu vào `tauri icon`).
  for (const s of CFG.appSizes ?? [16, 24, 32, 48, 64, 128, 256, 512]) emit(app(`icon-${s}.png`), await at(s));
  emit(app('app.ico'), await ico(rounded));

  // --- Ghi hoặc kiểm.
  const bad = [];
  for (const [abs, buf] of outputs) {
    const rel = path.relative(ROOT, abs);
    if (CHECK) {
      if (!fs.existsSync(abs) || !fs.readFileSync(abs).equals(buf)) bad.push(rel);
      continue;
    }
    fs.mkdirSync(path.dirname(abs), { recursive: true });
    fs.writeFileSync(abs, buf);
    console.log(`  ${rel.padEnd(52)} ${(buf.length / 1024).toFixed(1)} kB`);
  }
  if (CHECK) {
    if (bad.length) {
      console.error(`brand-assets: ${bad.length} tệp lệch nguồn - chạy lại không có --check:\n  ${bad.join('\n  ')}`);
      process.exit(1);
    }
    console.log(`brand-assets: khớp nguồn (${outputs.size} tệp).`);
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
