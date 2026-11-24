# ⚡ Hệ Thống Giám Sát Điện Năng AC 1 Pha — SCADA HMI (ISA-101 & ISA-18.2)

Phần mềm giám sát điện năng xoay chiều 1 pha thời gian thực xây dựng trên **C# WinForms (.NET Framework 4.7.2)**, kết nối trực tiếp với phần cứng đo lường cách ly quang **ESP32-S3-WROOM-1 + IC HLW8032** qua giao tiếp nối tiếp **UART**.

---

## ✨ 1. Tính Năng Nổi Bật

* **Thiết kế giao diện chuẩn ISA-101 (High-Performance HMI):**
  * Tông màu sáng trung tính dịu mắt, tối giản các chi tiết gây nhiễu.
  * Màu sắc chỉ được dùng có chủ đích cho **đường đồ thị (Trend)**, **đường giới hạn ngưỡng (StripLines)** và **trạng thái báo động**.
  * Hiển thị giá trị đo bằng **LED 7 đoạn vẽ bằng GDI+** sắc nét, không cần cài font ngoài.
* **Quản lý báo động chuẩn ISA-18.2 (Alarm Management):**
  * Cụm 3 đèn trạng thái hình chữ nhật: `NORMAL` (Xanh lá), `ACKED` (Vàng hổ phách), `ALARM` (Đỏ nhấp nháy kèm âm thanh bíp cảnh báo).
  * Hỗ trợ xác nhận lỗi (**Acknowledge - ACK**) và ghi vết toàn bộ sự cố vào bảng **ISA-18.2 Alarm & Event Log**.
  * Bộ đếm Watchdog tự động báo động khi mất tín hiệu UART quá `3.5 giây`.
* **5 Tab giám sát chuyên biệt:**
  1. **Điện áp (V):** LED 7 đoạn, đồ thị thời gian thực, cài đặt ngưỡng `Umin` / `Umax`.
  2. **Dòng điện (A):** LED 7 đoạn, đồ thị thời gian thực, cài đặt ngưỡng `Imax`.
  3. **Công suất (W):** LED 7 đoạn, đồ thị công suất tác dụng ($P$), cài đặt ngưỡng `Pmax`.
  4. **Điện năng (kWh):** LED 7 đoạn, đồ thị điện năng tích lũy, cài đặt ngưỡng `Emax`.
  5. **Data Monitor & Logging:** Bảng nhật ký dữ liệu theo thời gian thực, bảng lịch sử sự cố ISA-18.2 và chức năng **Xuất báo cáo CSV (Excel)**.
* **Tương tác đồ thị mượt mà (Interactive Trend):**
  * **Lăn chuột:** Phóng to / thu nhỏ tại đúng điểm con trỏ chuột.
  * **`Ctrl` + Lăn chuột** *(hoặc lăn trên trục X)*: Zoom đơn lẻ trục thời gian (X).
  * **`Shift` + Lăn chuột** *(hoặc lăn trên trục Y)*: Zoom đơn lẻ trục giá trị (Y).
  * **Giữ chuột trái/phải + Kéo:** Pan (di chuyển đồ thị tự do).
  * **Nhấn đúp chuột trái (Double-Click):** Quay về chế độ bám dữ liệu thời gian thực (Realtime Tracking).

---

## 🔌 2. Kết Nối Phần Cứng (PC ↔ ESP32-S3-WROOM-1)

Sử dụng mạch chuyển đổi **USB-to-UART (3.3V)** (CP2102 / CH340G / FT232RL) kết nối vào header **`UART0`** trên board mạch:

| Chân USB-to-UART | Kết nối | Header `UART0` (Board) | Chân ESP32-S3 | Chức năng |
| :--- | :---: | :--- | :---: | :--- |
| **VCC (3.3V / 5V)** | ❌ | *Không nối* | — | Bỏ trống khi mạch đã cấp nguồn 220V AC |
| **GND** | ━━━ | **GND** | `GND2` (Pin 1) | Nối chung Mass cách ly |
| **RXD** | ◀━━ | **TX** | `TXD0` (Pin 37) | ESP32-S3 gửi dữ liệu đo lên PC |
| **TXD** | ━━▶ | **RX** | `RXD0` (Pin 36) | PC gửi lệnh cấu hình xuống ESP32-S3 |

---

## ⚙️ 3. Cấu Hình UART & Giao Thức Truyền Thông

### Thông số cổng Serial đồng bộ
* **Baudrate:** `115200 bps`
* **Data Bits:** `8` | **Parity:** `None` | **Stop Bits:** `One` (`8N1`)
* **Encoding:** `ASCII` | **Ký tự kết thúc dòng:** `\n`

### Định dạng bản tin
1. **ESP32-S3 gửi lên PC (chu kỳ `500 ms – 1000 ms`):**
   ```text
   DATA,<U>,<I>,<P>,<CosPhi>,<kWh>\n
   ```
   *Ví dụ:* `DATA,220.5,1.85,401.2,0.98,1.245\n`

2. **PC gửi lệnh xuống ESP32-S3:**
   * Đặt lại chỉ số điện năng về 0: `CMD,RESET_KWH\n`
   * Đồng bộ ngưỡng bảo vệ: `CMD,SET_LIMIT,<Umax>,<Imax>\n`

---

## 💻 4. Hướng Dẫn Chạy Ứng Dụng

1. Mở file **`BTL_UDMT.sln`** bằng **Visual Studio 2022**.
2. Kiểm tra Target Framework là **.NET Framework 4.7.2** và đã có Reference `System.Windows.Forms.DataVisualization`.
3. Nhấn **`F5`** để biên dịch và khởi chạy phần mềm.
4. Cắm cáp USB-to-UART → Bấm **Quét** để chọn đúng cổng **COM** → Giữ mặc định `115200, 8, None, One` → Bấm **KẾT NỐI UART**.