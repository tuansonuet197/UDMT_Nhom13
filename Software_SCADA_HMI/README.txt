PHẦN MỀM GIÁM SÁT ĐIỆN NĂNG (SCADA HMI) — NHÓM 13 (EMA3071E 2)
==============================================================

1. Chạy trực tiếp ứng dụng (không cần cài đặt):
   - Mở thư mục: App_ChayNgay\
   - Nhấp đúp file: BTL_UDMT.exe
   - Yêu cầu hệ thống: Windows với .NET Framework 4.7.2 trở lên (có sẵn trên Windows 10/11).

2. Mở và chỉnh sửa mã nguồn:
   - Mở thư mục: SourceCode\
   - Mở file BTL_UDMT.sln bằng Visual Studio (2019/2022) và nhấn Build / Start.

3. Kết nối phần cứng:
   - Cắm cáp USB-to-UART từ mạch ESP32-S3 vào máy tính (chỉ đấu 3 dây GND, TXD, RXD; không đấu VCC khi bo đang cắm nguồn 220 VAC).
   - Chọn cổng COM tương ứng, cấu hình 115200 bps, 8N1 và nhấn "KẾT NỐI UART".
   - Phần mềm nhận dữ liệu đo một chiều định dạng: DATA,<U>,<I>,<P>,<CosPhi>,<kWh>\n
   - Mọi ngưỡng cảnh báo (Umin, Umax, Imax, Pmax, Emax) được cấu hình ở Tab "Cấu hình ngưỡng" và kiểm tra cục bộ trên máy tính.
