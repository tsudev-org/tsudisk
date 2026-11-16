// Chạy: node --test worker/   (không phụ thuộc thư viện nào)
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'node:test';
import { chuanHoa, HEADER_BAO_MAT, MAX_URL, ORIGIN } from './chuan-hoa-url.mjs';
import worker from './index.mjs';

const chuyen = (url) => {
  const kq = chuanHoa('GET', url);
  assert.equal(kq.kind, 'redirect', `${url} phải chuyển hướng, nhận ${kq.kind}`);
  assert.equal(kq.status, 301);
  return kq.location;
};

test('chữ hoa chuyển về chữ thường, có / cuối', () => {
  assert.equal(chuyen(`${ORIGIN}/Tai-Ve`), `${ORIGIN}/tai-ve/`);
  assert.equal(chuyen(`${ORIGIN}/HUONG-DAN/Thu-Cong/`), `${ORIGIN}/huong-dan/thu-cong/`);
});

test('gộp nhiều gạch chéo', () => {
  assert.equal(chuyen(`${ORIGIN}/huong-dan//thu-cong`), `${ORIGIN}/huong-dan/thu-cong/`);
});

test('bỏ index.html và đuôi .html', () => {
  assert.equal(chuyen(`${ORIGIN}/tai-ve/index.html`), `${ORIGIN}/tai-ve/`);
  assert.equal(chuyen(`${ORIGIN}/an-toan.html`), `${ORIGIN}/an-toan/`);
  assert.equal(chuyen(`${ORIGIN}/Index.HTML`), `${ORIGIN}/`);
});

test('host lạ chuyển về tên miền chuẩn', () => {
  assert.equal(chuyen('https://tsudisk.example.workers.dev/tai-ve/'), `${ORIGIN}/tai-ve/`);
  assert.equal(chuyen('http://disk.tsudev.com/tai-ve/'), `${ORIGIN}/tai-ve/`);
});

test('bỏ tham số theo dõi, giữ tham số khác', () => {
  assert.equal(chuyen(`${ORIGIN}/Tai-Ve?utm_source=fb&fbclid=x&v=2`), `${ORIGIN}/tai-ve/?v=2`);
  assert.equal(chuyen(`${ORIGIN}/TAI-VE?gclid=1`), `${ORIGIN}/tai-ve/`);
});

test('không mở chuyển hướng ra ngoài (open redirect)', () => {
  for (const url of [
    `${ORIGIN}//evil.example/`,
    `${ORIGIN}///evil.example`,
    `${ORIGIN}/%2F%2Fevil.example`,
    `${ORIGIN}/\\evil.example`,
    `${ORIGIN}/https://evil.example`,
  ]) {
    const kq = chuanHoa('GET', url);
    if (kq.kind === 'redirect') assert.ok(kq.location.startsWith(`${ORIGIN}/`), `${url} -> ${kq.location}`);
    if (kq.kind === 'redirect') assert.equal(new URL(kq.location).origin, ORIGIN);
  }
});

test('đường dẫn đã chuẩn mà không có tệp -> 404 thật, không vòng lặp chuyển hướng', () => {
  assert.equal(chuanHoa('GET', `${ORIGIN}/khong-ton-tai/`).kind, 'not-found');
  assert.equal(chuanHoa('GET', `${ORIGIN}/anh.png`).kind, 'not-found');
  // Đích của mọi chuyển hướng phải là điểm dừng: chuẩn hóa lần hai không chuyển tiếp nữa.
  for (const url of [`${ORIGIN}/Tai-Ve`, `${ORIGIN}//a//B`, `${ORIGIN}/x/index.html?utm_x=1`]) {
    const dich = chuyen(url);
    assert.notEqual(chuanHoa('GET', dich).kind, 'redirect', `${url} -> ${dich} lại chuyển tiếp`);
  }
});

test('phương thức khác GET/HEAD bị từ chối', () => {
  for (const m of ['POST', 'PUT', 'DELETE', 'PATCH', 'OPTIONS', 'TRACE']) assert.equal(chuanHoa(m, `${ORIGIN}/`).status, 405);
  assert.notEqual(chuanHoa('HEAD', `${ORIGIN}/Tai-Ve`).status, 405);
});

test('URL quá dài -> 414', () => {
  assert.equal(chuanHoa('GET', `${ORIGIN}/${'a'.repeat(MAX_URL)}`).status, 414);
});

// --- Worker đầy đủ với ASSETS giả ---
const env = { ASSETS: { fetch: async () => new Response('<h1>404</h1>', { status: 200, headers: { 'Content-Type': 'text/html' } }) } };

test('worker: chuyển hướng có Location + header bảo mật', async () => {
  const res = await worker.fetch(new Request(`${ORIGIN}/Tai-Ve`), env);
  assert.equal(res.status, 301);
  assert.equal(res.headers.get('Location'), `${ORIGIN}/tai-ve/`);
  for (const k of Object.keys(HEADER_BAO_MAT)) assert.ok(res.headers.get(k), `thiếu ${k}`);
});

test('worker: 404 trả trang 404 với mã 404 thật', async () => {
  const res = await worker.fetch(new Request(`${ORIGIN}/khong-co/`), env);
  assert.equal(res.status, 404);
  assert.match(await res.text(), /404/);
  assert.equal(res.headers.get('X-Frame-Options'), 'DENY');
});

test('worker: POST -> 405 kèm Allow', async () => {
  const res = await worker.fetch(new Request(`${ORIGIN}/`, { method: 'POST', body: 'x' }), env);
  assert.equal(res.status, 405);
  assert.equal(res.headers.get('Allow'), 'GET, HEAD');
});

test('header bảo mật của Worker khớp từng dòng với khối /* của public/_headers', () => {
  const khoi = readFileSync(new URL('../public/_headers', import.meta.url), 'utf8').split(/\n(?=\/)/)[0];
  const tuTep = Object.fromEntries([...khoi.matchAll(/^\s+([A-Za-z-]+):\s*(.+)$/gm)].map((m) => [m[1], m[2].trim()]));
  assert.deepEqual(tuTep, HEADER_BAO_MAT);
});
