Khoa Công nghệ Thông tin Trường Đại học Mỏ - Địa chất
Bộ môn Tin học Kinh tế
Mở Terminal (trên macOS/Linux) hoặc Git Bash (trên Windows) và trỏ tới thư mục chứa mã nguồn dự án của bạn.
Khởi tạo kho lưu trữ Git cục bộ (nếu chưa làm) bằng lệnh:git init
Thêm toàn bộ các tệp thay đổi vào vùng chờ (staging area):git add .
Lưu (commit) các thay đổi kèm theo thông điệp mô tả:git commit -m "Nhập nội dung commit của bạn ở đây"
Liên kết kho lưu trữ dưới máy với kho lưu trữ trên GitHub (chỉ cần làm lần đầu):git remote add origin <URL_Repository_Cua_Ban>
Đẩy mã nguồn lên GitHub (thường ở nhánh main hoặc master):git push -u origin main
//su dung command J để thực hiện với Git