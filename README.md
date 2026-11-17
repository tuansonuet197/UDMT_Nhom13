# UDMT - Nhóm 13: Thiết Kế Mạch Đo Điện Năng Tiêu Thụ Của Thiết Bị

- **Môn học:** Ứng dụng máy tính trong đo lường và điều khiển
- **Lớp:** EMEA3071E 2
- **Giảng viên hướng dẫn:** Hoàng Văn Mạnh
- **Phần cứng:** HLW8032 + ESP32-S3-WROOM-1 (`92.40 × 97.90 mm`), Màn hình TFT **2.8" 240×320 V1.0** (`50.0 × 81.70 mm`, 4 lỗ vít tâm `44.60 × 76.00 mm`)

## Thành viên Nhóm 13
| STT | Họ và Tên | MSSV |
| :-: | :--- | :---: |
| 1 | Vũ Hữu Hoạt | `23021273` |
| 2 | Nguyễn Tuấn Sơn | `23021335` |
| 3 | Nguyễn Văn Thắng | `23021359` |

---

## Danh sách file in 3D Vỏ Hộp (`3D_Enclosure_HLW8032/`)
Thiết kế vỏ hộp 3D khóa bằng **4 ốc vít M3 ở 4 tai góc** (tỷ lệ in thành công 100%, không dùng ngàm dễ gãy):
- `1_Enclosure_Bottom_Case.stl` — Đáy hộp (có gờ bậc thang đỡ PCB cao `5.5 mm`, 4 tai bắt ốc M3 trước/sau, mở hở toàn khoang đấu dây `J4/J5` trái & `TFT1/UART0/UART2/RESET1/BOOT1` phải, mặt sau phẳng vuông vức).
- `2_Enclosure_Top_Cover.stl` — Nắp hộp trên (4 tai bắt ốc M3 đồng tâm, gờ định tâm trượt êm, 4 trụ đè giữ chặt 4 góc PCB, **đục sẵn 4 trụ bắt vít M2.5/M3 tâm `44.6 × 76.0 mm` cao `3.5 mm`** + khe luồn 14 chân màn hình `2.8"`).
- `4_Full_Assembly_Preview.stl` — Bản ghép tổng thể Đáy + Nắp để kiểm tra trước khi in.
- `5_Enclosure_Parametric.scad` — File nguồn tham số OpenSCAD.

## Thông số in 3D & Lắp ráp khuyến nghị
- **Nhựa:** PLA+ hoặc PETG
- **Chiều cao lớp (Layer Height):** `0.20 mm` (Nozzle `0.4 mm`)
- **Infill:** `15% – 20%` (Grid / Gyroid)
- **Ốc vít lắp ráp:** Dùng **4 bu-lông `M3 × 35mm` hoặc `M3 × 40mm` + 4 đai ốc M3** xỏ qua 4 tai ngoài ở mặt trước & sau; **4 vít `M2.5 / M3 × 6–8mm`** cho màn hình `2.8"`.
- **Khối lượng ước tính:** ~`57 – 60 gram` cho cả bộ Đáy + Nắp.
