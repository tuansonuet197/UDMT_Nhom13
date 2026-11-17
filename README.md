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
Thiết kế vỏ hộp 3D lắp ghép khớp **Lego (Snap-Fit)** không cần ốc vít:
- `1_Enclosure_Bottom_Case.stl` — Đáy hộp (có gờ bậc thang đỡ PCB cao `5.5 mm`, mở hở toàn khoang đấu dây `J4/J5` bên trái & `TFT1/UART0/UART2/RESET1/BOOT1` bên phải, hốc ăng-ten ESP32-S3).
- `2_Enclosure_Top_Cover.stl` — Nắp hộp trên (4 lưỡi gà Snap-Fit cửa sổ chìm chống gãy, 4 trụ đè giữ chặt 4 góc PCB, mở thông 2 khoang đấu dây & bấm nút hai bên, **đục sẵn 4 trụ bắt vít M2.5/M3 tâm `44.6 × 76.0 mm` cao `3.5 mm`** + khe luồn 14 chân màn hình `2.8"`).
- `4_Full_Assembly_Preview.stl` — Bản ghép tổng thể Đáy + Nắp để kiểm tra trước khi in.
- `5_Enclosure_Parametric.scad` — File nguồn tham số OpenSCAD.

## Thông số in 3D khuyến nghị
- **Nhựa:** PLA+ hoặc PETG
- **Chiều cao lớp (Layer Height):** `0.20 mm` (Nozzle `0.4 mm`)
- **Infill:** `15% – 20%` (Grid / Gyroid)
- **Dung sai thiết kế:** Khe kẹp PCB `+0.15 mm` mỗi cạnh, khớp Lego `0.12 mm`.
- **Khối lượng ước tính:** ~`55 – 60 gram` cho cả bộ Đáy + Nắp.
