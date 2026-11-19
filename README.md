**Môn học: Phát triển phần mềm quản lý**

Buổi 2: offline 
1. Su dung command J để thực hiện với Git
2. Tạo project.Net MVC: dotnet new mvc -o DemoMVC [tức tạo thư mục]
3. Chạy project
- cd tới thư mục chứa project
- chạy lệnh dotnet run/dotnet watch
4. lưu lên Github
- git add .
- git commit -m (Nội dung)
- git push
  
Buổi 3: nghỉ

Buổi 4: online
1. Tìm hiểu lại về định tuyến
2. Tạo một Controller (Lesson4) và Action (Index) -> truy cập: http://localhost:5164/Lesson4/Index
3. Tạo Views với yêu cầu 2
4. Gửi nhận dữ liệu giữa Controller và Views: Submit form và ViewBag/ViewData
5. Gửi nhận dữ liệu giữa Controller và Views thông qua Models

Buổi 5: vắng 
1. TempData
2. Tích hợp bootstrap 5 vào project
3. Tách files CSS và tích hợp Layout

Buổi 6: online 
1. Cài đặt package
2. Thiết lập kết nối CSDL trong appsetting.json\
3. Tạo file ApplicationDBContext
4. Cấu hình kết nối CSDL trong program.cs
5. Tạo class trong Models
6. Sử dụng Migrations để quản lý phiên bản CSDL
7. Cập nhật thay đổi CSDL

Buổi 7: offline
1. Xây dựng chức năng CRUD dữ liệu với Entity
2. Validate dữ liệu bằng Data Annotations

Buổi 8: offline
1. ôn tập lại Data Annotations
2. CRUD: Sinh code tự động
3. Sử dụng Docker _ chạy SQL Server

Buổi 9: offline 
1. Tạo liên kết khoá ngoại (Ví dụ: Bảng Sinh viên và Bảng Khoa liên kết thông qua mã Khoa) => Nhập dữ liệu là các khoa của trường Đại học Mỏ - Địa chất
2. Xây dựng chức năng với các bảng có khoá ngoại
3. Xây dựng chức năng sinh mã tự động, ví dụ mã sinh viên có dạng 2821050199, trong đó:
+ 28 => Năm nhập học (ví dụ 2028)
+ 21 => hệ đào tạo (ví dụ đại học chính quy)
+ 05 => Ngành học (Ví dụ Công nghệ thông tin)
+ 4 số cuối => Số thứ tự của sinh viên
=> Khi thêm thông tin của 1 sinh viên mới, sinh viên tiếp theo sẽ có mã 2821050200

Buổi 10: offline
- Thầy giáo hướng dẫn cài đặt Copilot Pro for Student 2 years
- Hướng dẫn làm đồ án tin kinh tế -> thực tập doanh nghiệp -> đồ án tốt nghiệp thì phần quan trong nhất chính là mô tả nghiệp vụ bài toán.

Buổi 11: offline
1. Sử dụng Ajax: Xây lại toàn bộ các chức năng CRUD sử dụng Modal kết hợp với Ajax
2. Xây dựng các chức năng xác thực và uỷ quyền người dùng áp dụng cho tất cả cho tất cả các trang.

Buổi 12: offline
1. Xây dựng chức năng phân trang và tìm kiếm dữ liệu

Buổi 13: offline
1. Xây dựng chức năng xác thực và uỷ quyền người dùng
- Tích hợp identity
- Khởi tạo tài khoản mặc định
- Cấu hình gửi email: Xác thực tài khoản, Quên mật khẩu,...

## Cổng Admin và cổng sinh viên

- Admin đăng nhập tại `/Account/Login` và quản lý hồ sơ ở `/Admin/Students`.
- Tài khoản User đăng ký/đăng nhập để tạo nhiều hồ sơ tại `/Student/Register`; User chỉ xem được hồ sơ do chính mình tạo.
- Hồ sơ User mới tạo ở trạng thái **Chờ duyệt**. Admin lọc theo trạng thái và chọn **Duyệt** để đưa hồ sơ lên danh sách công khai `/PublicStudents`.
- Danh sách công khai chỉ hiển thị mã sinh viên, họ tên, ngành học và năm nhập học. Hồ sơ cũ được giữ nguyên và xem là đã duyệt; hồ sơ cũ chưa liên kết tài khoản vẫn giữ `UserId` là `NULL`.
- Danh sách công khai hỗ trợ tìm kiếm theo mã sinh viên hoặc họ tên và phân trang, mỗi trang tối đa 10 sinh viên.
- Sau khi lấy mã nguồn mới, áp dụng migration bằng `dotnet ef database update --context ApplicationIdentityDbContext --project DemoMVC/DemoMVC.csproj --startup-project DemoMVC/DemoMVC.csproj`. Migration chỉ thêm trạng thái duyệt, giữ hồ sơ cũ chưa liên kết ở trạng thái đã duyệt và buộc hồ sơ có `UserId` ở trạng thái chờ duyệt.

## Xác thực, phân quyền và email

DemoMVC sử dụng ASP.NET Core Identity với EF Core/SQLite. Identity được lưu trong cùng file `DemoMVC/App.db`, qua `ApplicationIdentityDbContext` và bảng lịch sử migration riêng `__IdentityMigrationsHistory`; các bảng Student, Employee, Score và Major được giữ nguyên. Mật khẩu do Identity băm, ứng dụng không lưu mật khẩu dạng văn bản.

### Package

- `Microsoft.AspNetCore.Identity.EntityFrameworkCore`: Identity lưu tài khoản/role bằng EF Core.
- `Microsoft.EntityFrameworkCore.Sqlite`: provider SQLite hiện có.
- `Microsoft.EntityFrameworkCore.Design` và `Microsoft.EntityFrameworkCore.Tools`: tạo/áp dụng migration.
- `MailKit`: gửi email SMTP bất đồng bộ qua TLS.

Các package Identity, EF Core và SQLite đã được khai báo trong project; MailKit là package SMTP được thêm vào.

### Cấu hình tài khoản Admin đầu tiên

Không có mật khẩu Admin cố định trong mã hoặc `appsettings.json`. Trong terminal, chạy từ thư mục project:

```bash
cd DemoMVC
dotnet user-secrets set "AdminSeed:Email" "admin@example.com"
dotnet user-secrets set "AdminSeed:Password" "DienMatKhauManhCuaBan!"
```

Thay địa chỉ và mật khẩu mẫu bằng giá trị riêng của bạn; không gửi các giá trị thật lên GitHub. User Secrets được lưu ngoài thư mục mã nguồn. Khi deploy, dùng biến môi trường `AdminSeed__Email` và `AdminSeed__Password` từ nơi quản lý secrets của môi trường deploy.

Khi khởi chạy, ứng dụng tạo role `Admin` và `User` nếu thiếu. Nếu cấu hình Admin có đủ email/mật khẩu, ứng dụng tạo tài khoản Admin khi chưa có và gán role Admin. Nếu tài khoản đã tồn tại, ứng dụng không thay mật khẩu hay thông tin của tài khoản; mật khẩu cấu hình chỉ được dùng lúc tạo tài khoản mới. Admin ban đầu được đánh dấu email đã xác thực để có thể đăng nhập.

Sau khi đăng nhập Admin lần đầu, vào **Thông tin tài khoản / Đổi mật khẩu** hoặc `/Account/Manage`, chọn **Đổi mật khẩu** và thay mật khẩu ban đầu ngay.

### Cấu hình SMTP (Gmail)

Tạo Gmail App Password trong bảo mật tài khoản Google (yêu cầu bật xác minh 2 bước). Dùng App Password, không dùng mật khẩu Gmail thường. Từ thư mục `DemoMVC`, lưu cấu hình local bằng User Secrets:

```bash
dotnet user-secrets set "Email:Host" "smtp.gmail.com"
dotnet user-secrets set "Email:Port" "587"
dotnet user-secrets set "Email:Security" "StartTls"
dotnet user-secrets set "Email:UserName" "your-address@gmail.com"
dotnet user-secrets set "Email:Password" "YOUR_GMAIL_APP_PASSWORD"
dotnet user-secrets set "Email:FromAddress" "your-address@gmail.com"
dotnet user-secrets set "Email:FromName" "DemoMVC"
```

Thay tất cả giá trị mẫu bằng thông tin của bạn và không commit App Password. Khi deploy, dùng các biến môi trường tương ứng: `Email__Host`, `Email__Port`, `Email__Security`, `Email__UserName`, `Email__Password`, `Email__FromAddress`, `Email__FromName`. Nếu SMTP chưa cấu hình hoặc gửi thất bại, giao diện đăng ký báo email xác thực chưa gửi được; chi tiết lỗi chỉ ghi vào log máy chủ. Màn quên mật khẩu luôn hiển thị thông báo chung để không tiết lộ email nào có tài khoản.

### Luồng tài khoản và quyền truy cập

- Đăng ký tại `/Account/Register`: chỉ được cấp role `User`, không có trường chọn Admin. Tài khoản cần xác thực email qua liên kết Identity trước khi đăng nhập.
- Đăng nhập tại `/Account/Login`; tài khoản chưa xác thực, nhập sai mật khẩu và tài khoản bị khóa được xử lý riêng.
- Quên mật khẩu tại `/Account/ForgotPassword`; liên kết đặt lại và xác thực email dùng token Identity.
- Quản lý email/số điện thoại và đổi mật khẩu tại `/Account/Manage`.
- Admin quản lý vai trò và khóa/mở khóa tài khoản tại `/Admin/Users`. Không thể tự đổi role/khóa tài khoản đang đăng nhập hoặc hạ quyền Admin cuối cùng.
- Tại mục **Phân quyền quản lý tài khoản** trên thanh điều hướng Admin (hoặc `/Admin/Users`), Admin có thể cấp riêng quyền Xem/Thêm/Sửa/Xóa hồ sơ sinh viên cho từng User. Quyền lưu dưới dạng Identity User Claims, không cần migration; tài khoản User mới không có quyền hồ sơ cho đến khi Admin cấp.
- User chỉ xem, tạo, sửa hoặc xóa hồ sơ do chính tài khoản đó tạo. Khi User sửa hồ sơ đã duyệt, hồ sơ trở về trạng thái **Chờ Admin duyệt**. Khi Admin đổi quyền hoặc vai trò, phiên đăng nhập cũ bị thu hồi ở request tiếp theo; User đăng nhập lại để nhận quyền mới.
- Mặc định các trang yêu cầu đăng nhập. Trang chủ và danh sách sinh viên công khai được phép truy cập ẩn danh; Student portal chỉ role User; khu vực Admin và CRUD nhân viên chỉ role Admin.

### Migration và cập nhật database

Các migration Identity hiện có đã được áp dụng trong database của dự án, gồm các bảng `AspNetUsers`, `AspNetRoles` và liên kết trạng thái hồ sơ sinh viên. Các chức năng email/profile mới sử dụng trường Identity đã có nên **không tạo migration mới và không tự chạy migration khi ứng dụng khởi động**.

Trước khi cập nhật một bản database khác, sao lưu `DemoMVC/App.db`, sau đó kiểm tra migration:

```bash
dotnet ef migrations list --context ApplicationIdentityDbContext --project DemoMVC/DemoMVC.csproj --startup-project DemoMVC/DemoMVC.csproj
```

Chỉ khi xác nhận bản database cần cập nhật, chạy các migration Identity hiện có:

```bash
dotnet ef database update --context ApplicationIdentityDbContext --project DemoMVC/DemoMVC.csproj --startup-project DemoMVC/DemoMVC.csproj
```

Không xóa `App.db` và không áp dụng migration lên một database khác ngoài ý muốn. Các migration Identity có một số bước thêm cột `UserId`/`IsApproved` vào bảng Students nên cần sao lưu trước nếu database chưa có các migration đó.
