# 🚀 Hướng Dẫn Chạy Ứng Dụng

## ✅ Trạng Thái Hiện Tại

Ứng dụng đã được build và đang chạy thành công!

- **Backend API**: http://localhost:5000
- **Frontend**: http://localhost:5173
- **Swagger UI**: http://localhost:5000/swagger

## 🌐 Truy Cập Ứng Dụng

Mở trình duyệt và truy cập: **http://localhost:5173**

## 👤 Đăng Nhập

Sử dụng một trong hai tài khoản sau:

| Tên đăng nhập | Mật khẩu |
|--------------|----------|
| **Hlinh**    | 123@abc  |
| **Pthao**    | 123@abc  |

## ✨ Tính Năng

1. **Đăng nhập** - Sử dụng tài khoản Hlinh hoặc Pthao
2. **Đăng bài** - Chia sẻ kỉ niệm với nội dung và hình ảnh
3. **Xem bài viết** - Xem tất cả bài viết của cả hai người
4. **Thích bài viết** - Click vào nút ❤️ để thích
5. **Bình luận** - Click vào nút 💬 để xem và thêm bình luận
6. **Thông báo** - Click vào 🔔 để xem thông báo mới

## 🛠️ Dừng Ứng Dụng

### Dừng Backend:
```powershell
# Tìm process ID
netstat -ano | findstr ":5000"
# Dừng process (thay PID bằng số process ID)
taskkill /PID <PID> /F
```

### Dừng Frontend:
```powershell
# Tìm process ID
netstat -ano | findstr ":5173"
# Dừng process (thay PID bằng số process ID)
taskkill /PID <PID> /F
```

## 🔄 Chạy Lại Ứng Dụng

### Backend:
```powershell
cd MemoriesApp.Api
dotnet run
```

### Frontend:
```powershell
cd MemoriesApp.Frontend
npm run dev
```

## 📝 Lưu Ý

- Backend cần chạy trước khi frontend kết nối
- Đảm bảo MongoDB và Cloudinary đã được cấu hình đúng
- Nếu có lỗi, kiểm tra console của cả backend và frontend

---

💕 Chúc bạn có những khoảnh khắc đáng nhớ!

