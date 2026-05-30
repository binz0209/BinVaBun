# Memories App - Backend API

API backend cho ứng dụng chia sẻ kỉ niệm, sử dụng .NET 8, MongoDB và Cloudinary.

## Yêu cầu

- .NET 8 SDK
- MongoDB (đã cấu hình trong appsettings.json)
- Cloudinary account (đã cấu hình trong appsettings.json)

## Chạy ứng dụng

```bash
dotnet restore
dotnet run
```

API sẽ chạy tại http://localhost:5000

Swagger UI: http://localhost:5000/swagger

## Tài khoản mặc định

Ứng dụng sẽ tự động tạo 2 tài khoản khi khởi động lần đầu:
- Hlinh / 123@abc
- Pthao / 123@abc

