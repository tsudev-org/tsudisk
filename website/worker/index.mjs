// Worker của disk.tsudev.com. Chỉ chạy khi yêu cầu KHÔNG khớp tệp tĩnh (wrangler.jsonc: not_found_handling "none",
// không bật run_worker_first) - trang thật được phục vụ thẳng từ biên, không tốn lượt Worker, khó bị làm cạn hạn mức.
// Không có trạng thái, không gọi mạng ra ngoài, không đọc cookie hay nội dung yêu cầu.
import { chuanHoa, HEADER_BAO_MAT } from './chuan-hoa-url.mjs';

const kemHeader = (res, them = {}) => {
  const h = new Headers(res.headers);
  for (const [k, v] of Object.entries({ ...HEADER_BAO_MAT, ...them })) h.set(k, v);
  return new Response(res.body, { status: res.status, headers: h });
};

const chu = (status, text, them = {}) =>
  kemHeader(new Response(text, { status, headers: { 'Content-Type': 'text/plain; charset=utf-8' } }), {
    'Cache-Control': 'no-store',
    ...them,
  });

export default {
  /** @param {Request} request @param {{ ASSETS: { fetch: (r: Request | string) => Promise<Response> } }} env */
  async fetch(request, env) {
    const kq = chuanHoa(request.method, request.url);
    switch (kq.kind) {
      case 'method':
        return chu(405, 'Phương thức không được hỗ trợ.', { Allow: 'GET, HEAD' });
      case 'too-long':
        return chu(414, 'Đường dẫn quá dài.');
      case 'redirect':
        return kemHeader(new Response(null, { status: 301, headers: { Location: kq.location } }), {
          'Cache-Control': 'public, max-age=86400',
        });
      default: {
        const trang = await env.ASSETS.fetch(new URL('/404.html', request.url));
        return kemHeader(new Response(request.method === 'HEAD' ? null : trang.body, { status: 404, headers: trang.headers }), {
          'Cache-Control': 'public, max-age=300',
        });
      }
    }
  },
};
