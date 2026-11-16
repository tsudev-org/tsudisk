// Chuẩn hóa URL cho disk.tsudev.com - hàm THUẦN (không I/O), kiểm thử bằng `node --test worker/`.
//
// Worker chỉ được gọi khi đường dẫn KHÔNG khớp tệp tĩnh nào (tệp tĩnh khớp thì Cloudflare phục vụ thẳng, không tốn
// lượt Worker). Vì vậy hàm này trả lời câu hỏi: "đường dẫn hỏng này có một dạng chuẩn tồn tại không?".
//
// Quy tắc (theo thứ tự):
//   1. Chỉ GET/HEAD                         -> khác: 405 + Allow
//   2. URL dài quá 2048 ký tự               -> 414
//   3. Host khác tên miền chuẩn              -> 301 về tên miền chuẩn (cùng đường dẫn đã chuẩn hóa)
//   4. Đường dẫn: gộp "//", chữ thường, bỏ "index.html", bỏ ".html", thêm "/" cuối cho trang
//   5. Bỏ tham số theo dõi (utm_*, fbclid, gclid, ...) khi đã phải chuyển hướng
//   6. Dạng chuẩn khác dạng gốc            -> 301 tới dạng chuẩn; trùng -> 404 thật
//
// Chống open redirect: đích chuyển hướng LUÔN là ORIGIN cố định + đường dẫn bắt đầu bằng đúng một "/". Không bao giờ
// dùng host, scheme hay đường dẫn kiểu "//evil.example" do người dùng gửi lên.

export const ORIGIN = 'https://disk.tsudev.com';
export const MAX_URL = 2048;
const THAM_SO_THEO_DOI = /^(utm_[a-z]+|fbclid|gclid|dclid|msclkid|yclid|mc_eid|mc_cid|_ga|ref)$/i;
const CO_DUOI_TEP = /\/[^/]+\.[a-z0-9]{1,8}$/i;

/**
 * @param {string} method
 * @param {string} rawUrl  URL đầy đủ của yêu cầu
 * @returns {{ kind: 'method', status: 405 } | { kind: 'too-long', status: 414 } |
 *           { kind: 'redirect', status: 301, location: string } | { kind: 'not-found', status: 404 }}
 */
export function chuanHoa(method, rawUrl) {
  if (method !== 'GET' && method !== 'HEAD') return { kind: 'method', status: 405 };
  if (rawUrl.length > MAX_URL) return { kind: 'too-long', status: 414 };

  let url;
  try {
    url = new URL(rawUrl);
  } catch {
    return { kind: 'not-found', status: 404 };
  }

  const duongGoc = url.pathname;
  let duong = duongGoc.replace(/\/{2,}/g, '/');
  duong = duong.toLowerCase();
  duong = duong.replace(/\/index\.html?$/, '/');
  if (/\.html?$/.test(duong) && !duong.endsWith('/404.html')) duong = duong.replace(/\.html?$/, '/');
  if (!duong.endsWith('/') && !CO_DUOI_TEP.test(duong)) duong += '/';
  if (!duong.startsWith('/')) duong = '/' + duong;
  // Sau khi gộp, "//" đầu đã thành "/" - không thể ra URL tương đối giao thức (//evil.example).

  const thamSo = new URLSearchParams();
  for (const [k, v] of url.searchParams) if (!THAM_SO_THEO_DOI.test(k)) thamSo.append(k, v);
  const chuoiThamSo = thamSo.toString();

  const sai = url.origin !== ORIGIN || duong !== duongGoc;
  if (!sai) return { kind: 'not-found', status: 404 };

  const dich = new URL(duong, ORIGIN);
  if (dich.origin !== ORIGIN) return { kind: 'not-found', status: 404 };   // phòng thủ chiều sâu
  return { kind: 'redirect', status: 301, location: dich.href + (chuoiThamSo ? `?${chuoiThamSo}` : '') };
}

/** Header bảo mật cho phản hồi do Worker tự sinh (tệp tĩnh đã có public/_headers). Giữ khớp với _headers. */
export const HEADER_BAO_MAT = {
  'Strict-Transport-Security': 'max-age=31536000; includeSubDomains; preload',
  'Content-Security-Policy': "default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; manifest-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'; object-src 'none'; upgrade-insecure-requests",
  'X-Content-Type-Options': 'nosniff',
  'X-Frame-Options': 'DENY',
  'Referrer-Policy': 'strict-origin-when-cross-origin',
  'Permissions-Policy': 'accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=(), interest-cohort=()',
  'Cross-Origin-Opener-Policy': 'same-origin',
  'Cross-Origin-Resource-Policy': 'same-origin',
  'Cross-Origin-Embedder-Policy': 'require-corp',
  'X-Permitted-Cross-Domain-Policies': 'none',
  'Origin-Agent-Cluster': '?1',
};
