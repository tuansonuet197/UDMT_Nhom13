// ============================================================================
// HỘP BẢO VỆ 3D KHỚP LEGO (SNAP-FIT) - MẠCH HLW8032 + ESP32-S3 + KHAY TFT ST7735S
// Kích thước PCB: 92.4 x 97.9 x 1.6 mm | Dung sai máy in: 0.12 mm
// - Chiều dọc rộng rãi (gầm dưới 5.5mm, khoảng trên 22.5mm) không lo cấn mối hàn
// - Đã cắt bỏ hoàn toàn thanh chắn bên phải chỗ 2 nút RESET1 & BOOT1
// - Tích hợp khay gá màn hình TFT ST7735S + khe cắm thẳng xuống chân tft1
// ============================================================================
$fn = 48;
pcb_w = 92.4; pcb_l = 97.9; pcb_t = 1.6;
tol_xy = 0.15; snap_tol = 0.12;
wall = 2.0; floor_t = 1.8; roof_t = 1.8;
under_pcb_h = 5.5; above_pcb_h = 22.5;
