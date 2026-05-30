# Hướng dẫn Deploy Frontend lên Vercel

## Bước 1: Chuẩn bị

1. Đảm bảo code đã được commit và push lên Git (GitHub, GitLab, hoặc Bitbucket)

## Bước 2: Deploy trên Vercel

### Cách 1: Deploy qua Vercel Dashboard

1. Truy cập [vercel.com](https://vercel.com) và đăng nhập
2. Click "Add New..." → "Project"
3. Import repository từ Git của bạn
4. Cấu hình project:
   - **Framework Preset**: Vite
   - **Root Directory**: `MemoriesApp.Frontend` (nếu repo có nhiều folder)
   - **Build Command**: `npm run build` (tự động)
   - **Output Directory**: `dist` (tự động)
   - **Install Command**: `npm install` (tự động)

5. **Environment Variables**:
   - Thêm biến môi trường:
     - `VITE_API_URL`: URL của backend API
     - Ví dụ: `https://your-backend-api.vercel.app` hoặc `https://api.yourdomain.com`

6. Click "Deploy"

### Cách 2: Deploy qua Vercel CLI

```bash
# Cài đặt Vercel CLI
npm i -g vercel

# Đăng nhập
vercel login

# Deploy (từ thư mục MemoriesApp.Frontend)
cd MemoriesApp.Frontend
vercel

# Deploy production
vercel --prod
```

## Bước 3: Cấu hình Environment Variables trên Vercel

1. Vào Project Settings → Environment Variables
2. Thêm:
   - **Key**: `VITE_API_URL`
   - **Value**: URL backend của bạn (ví dụ: `https://your-backend.vercel.app` hoặc `https://api.yourdomain.com`)
   - **Environment**: Production, Preview, Development

## Bước 4: Cấu hình Backend URL

Sau khi deploy backend, cập nhật `VITE_API_URL` trong Vercel:
- Vào Project Settings → Environment Variables
- Cập nhật `VITE_API_URL` với URL backend thực tế
- Redeploy để áp dụng thay đổi

## Lưu ý

- File `vercel.json` đã được tạo với cấu hình phù hợp cho React Router
- Frontend sẽ tự động redirect tất cả routes về `/index.html` để hỗ trợ React Router
- SignalR sẽ tự động sử dụng `VITE_API_URL` để kết nối với backend

## Troubleshooting

- Nếu gặp lỗi 404 khi navigate: Kiểm tra file `vercel.json` có đúng không
- Nếu SignalR không kết nối: Kiểm tra `VITE_API_URL` đã được set đúng chưa
- Nếu API calls fail: Kiểm tra CORS settings trên backend

