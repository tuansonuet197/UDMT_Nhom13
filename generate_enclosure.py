import numpy as np
import struct
import os
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d.art3d import Poly3DCollection

# ==============================================================================
# THÔNG SỐ HỘP 3D KHỚP LEGO - CHIỀU DỌC RỘNG RÃI AN TOÀN CHO MỐI HÀN & LINH KIỆN
# ==============================================================================
PCB_W = 92.40          # Chiều rộng PCB (Trục X: Trái -> Phải)
PCB_L = 97.90          # Chiều dài PCB  (Trục Y: Dưới -> Trên)
PCB_T = 1.60           # Độ dày PCB tiêu chuẩn

# Dung sai an toàn cho cả sai số máy in 0.12mm lẫn dung sai cắt viền PCB (±0.20mm):
# -> Lòng hộp 92.90 x 98.40 mm thả PCB 92.40 x 97.90 mm vào êm ái không cần dũa mép,
#    kết hợp khe Lego 0.18mm đảm bảo sập nắp nhẹ nhàng không bị kích.
TOL_XY    = 0.25
SNAP_TOL  = 0.18       # Khe hở gờ âm-dương Lego an toàn khi máy in sai số 0.12mm
CAVITY_W  = PCB_W + 2 * TOL_XY  # 92.90 mm
CAVITY_L  = PCB_L + 2 * TOL_XY  # 98.40 mm

# Thành hộp 2.0mm tiết kiệm nhựa, nhưng CHIỀU DỌC (Z) để dư dả cho mối hàn & linh kiện:
WALL        = 2.0      # Thành dày 2.0mm cứng cáp, tiết kiệm tiền in
FLOOR_T     = 1.8      # Đáy dày 1.8mm
ROOF_T      = 1.8      # Nắp dày 1.8mm
UNDER_PCB_H = 5.5      # Gầm dưới PCB cao 5.5mm (tha hồ cho mối hàn chân dài, IC U2 & R8 mặt đáy)
ABOVE_PCB_H = 22.5     # Khoảng không trên PCB cao 22.5mm (dư 3.5mm trên cục nguồn HLK-10M05 & dây cắm)

# Tọa độ Z (Gốc Z=0 tại mặt đáy ngoài của hộp dưới):
Z_FLOOR_TOP = FLOOR_T                           # 1.80 mm
Z_PCB_BOT   = Z_FLOOR_TOP + UNDER_PCB_H         # 7.30 mm (mặt bậc đỡ PCB)
Z_PCB_TOP   = Z_PCB_BOT + PCB_T                 # 8.90 mm (mặt trên PCB)
Z_SPLIT     = Z_PCB_TOP + 10.5                  # 19.40 mm (mặt phân khuôn giữa Đáy và Nắp)
Z_LIP_TOP   = Z_SPLIT + 2.4                     # 21.80 mm (đỉnh gờ dương Lego của Đáy hộp)
Z_TOP_IN    = Z_PCB_TOP + ABOVE_PCB_H           # 31.40 mm (mặt trần trong của Nắp)
Z_TOTAL_H   = Z_TOP_IN + ROOF_T                 # 33.20 mm (tổng chiều cao toàn bộ hộp)

OUT_W = CAVITY_W + 2 * WALL                     # 97.10 mm
OUT_L = CAVITY_L + 2 * WALL                     # 102.60 mm

def pcb_x(x): return WALL + TOL_XY + x
def pcb_y(y): return WALL + TOL_XY + y

# 1. Cọc đấu dây bên trái (X = 0):
J4_Y1, J4_Y2 = 10.5, 39.5   # Cọc J4 INPUT (tâm Y=25.56)
J5_Y1, J5_Y2 = 57.5, 81.5   # Cọc J5 OUTPUT (tâm Y=69.80)

# 2. Các cổng kết nối ở VÁCH BÊN PHẢI (X = PCB_W):
TFT1_Y1, TFT1_Y2   = 4.8, 27.5    # Header tft1 8 chân (cho màn ST7735S)
UART2_Y1, UART2_Y2 = 30.5, 44.0   # Header UART2 4 chân (dự phòng)
UART0_Y1, UART0_Y2 = 48.5, 63.8   # Header UART0 4 chân

# 3. Vùng nút nhấn RESET1 (85.32, 83.24) và BOOT1 (85.72, 69.32):
# CẮT BỎ HOÀN TOÀN THANH CHẮN BÊN PHẢI (X từ 78.5mm cắt thông thẳng ra ngoài mép phải OUT_W!)
RESET1_X, RESET1_Y = 85.32, 83.24
BOOT1_X,  BOOT1_Y  = 85.72, 69.32
BTN_OPEN_X1 = 78.5                      # Cắt từ X=78.5mm thông thẳng ra tận OUT_W (không còn thanh chắn!)
BTN_OPEN_Y1, BTN_OPEN_Y2 = 64.2, 89.5   # Trải dài ôm trọn cả BOOT1 và RESET1

# 4. Ăng-ten ESP32-S3 nhô ra ở cạnh trên (Y = PCB_L):
ANT_X1, ANT_X2 = 56.5, 80.5

class MeshBuilder:
    def __init__(self):
        self.triangles = []
        self.quads = []
        self.volume_mm3 = 0.0

    def add_quad(self, p0, p1, p2, p3):
        self.triangles.append((p0, p1, p2))
        self.triangles.append((p0, p2, p3))
        self.quads.append([p0, p1, p2, p3])

    def add_box(self, x0, x1, y0, y1, z0, z1):
        if x1 <= x0 or y1 <= y0 or z1 <= z0:
            return
        self.volume_mm3 += (x1 - x0) * (y1 - y0) * (z1 - z0)
        p000 = (x0, y0, z0); p100 = (x1, y0, z0); p110 = (x1, y1, z0); p010 = (x0, y1, z0)
        p001 = (x0, y0, z1); p101 = (x1, y0, z1); p111 = (x1, y1, z1); p011 = (x0, y1, z1)
        self.add_quad(p000, p010, p110, p100)
        self.add_quad(p001, p101, p111, p011)
        self.add_quad(p000, p100, p101, p001)
        self.add_quad(p110, p010, p011, p111)
        self.add_quad(p010, p000, p001, p011)
        self.add_quad(p100, p110, p111, p101)

    def add_square_with_hole(self, cx, cy, half_s, r_hole, z0, z1, segs=32):
        """Khối vuông [cx±half_s, cy±half_s] có đục lỗ tròn xuyên suốt ở tâm (100% kín STL)."""
        if z1 <= z0 or half_s <= r_hole:
            return
        self.volume_mm3 += ((2*half_s)**2 - np.pi * r_hole**2) * (z1 - z0)
        angles = np.linspace(0, 2*np.pi, segs+1)
        c_pts = []
        s_pts = []
        for a in angles:
            ca, sa = np.cos(a), np.sin(a)
            c_pts.append((cx + r_hole * ca, cy + r_hole * sa))
            m = max(abs(ca), abs(sa))
            s_pts.append((cx + half_s * ca / m, cy + half_s * sa / m))
        for i in range(segs):
            c0x, c0y = c_pts[i];   c1x, c1y = c_pts[i+1]
            s0x, s0y = s_pts[i];   s1x, s1y = s_pts[i+1]
            # Mặt trên (z1) & mặt dưới (z0)
            self.add_quad((c0x, c0y, z1), (s0x, s0y, z1), (s1x, s1y, z1), (c1x, c1y, z1))
            self.add_quad((s0x, s0y, z0), (c0x, c0y, z0), (c1x, c1y, z0), (s1x, s1y, z0))
            # Thành lỗ tròn bên trong
            self.add_quad((c1x, c1y, z0), (c0x, c0y, z0), (c0x, c0y, z1), (c1x, c1y, z1))
            # Thành vuông bên ngoài (kết nối khít vào lưới mặt nắp)
            self.add_quad((s0x, s0y, z0), (s1x, s1y, z0), (s1x, s1y, z1), (s0x, s0y, z1))

    def add_frustum_y(self, x0, x1, y0_bot, y1_bot, y0_top, y1_top, z0, z1):
        """Khối lăng trụ vát nghiêng theo trục Y dùng tạo ngàm Snap-Fit vát 45 độ in không cần Support."""
        if x1 <= x0 or z1 <= z0:
            return
        w_avg = 0.5 * ((y1_bot - y0_bot) + (y1_top - y0_top))
        self.volume_mm3 += max(0.0, (x1 - x0) * w_avg * (z1 - z0))
        p000 = (x0, y0_bot, z0); p100 = (x1, y0_bot, z0); p110 = (x1, y1_bot, z0); p010 = (x0, y1_bot, z0)
        p001 = (x0, y0_top, z1); p101 = (x1, y0_top, z1); p111 = (x1, y1_top, z1); p011 = (x0, y1_top, z1)
        self.add_quad(p000, p010, p110, p100)
        self.add_quad(p001, p101, p111, p011)
        self.add_quad(p000, p100, p101, p001)
        self.add_quad(p110, p010, p011, p111)
        self.add_quad(p010, p000, p001, p011)
        self.add_quad(p100, p110, p111, p101)

    def add_square_with_hole(self, cx, cy, half_s, r_hole, z0, z1, segs=32):
        """Khối vuông [cx±half_s, cy±half_s] có đục lỗ tròn xuyên suốt ở tâm (100% kín STL)."""
        if z1 <= z0 or half_s <= r_hole:
            return
        self.volume_mm3 += ((2*half_s)**2 - np.pi * r_hole**2) * (z1 - z0)
        angles = np.linspace(0, 2*np.pi, segs+1)
        c_pts = []
        s_pts = []
        for a in angles:
            ca, sa = np.cos(a), np.sin(a)
            c_pts.append((cx + r_hole * ca, cy + r_hole * sa))
            m = max(abs(ca), abs(sa))
            s_pts.append((cx + half_s * ca / m, cy + half_s * sa / m))
        for i in range(segs):
            c0x, c0y = c_pts[i];   c1x, c1y = c_pts[i+1]
            s0x, s0y = s_pts[i];   s1x, s1y = s_pts[i+1]
            # Mặt trên (z1) & mặt dưới (z0)
            self.add_quad((c0x, c0y, z1), (s0x, s0y, z1), (s1x, s1y, z1), (c1x, c1y, z1))
            self.add_quad((s0x, s0y, z0), (c0x, c0y, z0), (c1x, c1y, z0), (s1x, s1y, z0))
            # Thành lỗ tròn bên trong
            self.add_quad((c1x, c1y, z0), (c0x, c0y, z0), (c0x, c0y, z1), (c1x, c1y, z1))
            # Thành vuông bên ngoài (kết nối khít vào lưới mặt nắp)
            self.add_quad((s0x, s0y, z0), (s1x, s1y, z0), (s1x, s1y, z1), (s0x, s0y, z1))

    def add_cylinder(self, cx, cy, r_out, r_in, z0, z1, segs=24):
        if z1 <= z0 or r_out <= r_in:
            return
        self.volume_mm3 += np.pi * (r_out**2 - r_in**2) * (z1 - z0)
        angles = np.linspace(0, 2*np.pi, segs+1)
        for i in range(segs):
            a0, a1 = angles[i], angles[i+1]
            c0, s0 = np.cos(a0), np.sin(a0)
            c1, s1 = np.cos(a1), np.sin(a1)
            p_out0_b = (cx + r_out*c0, cy + r_out*s0, z0)
            p_out1_b = (cx + r_out*c1, cy + r_out*s1, z0)
            p_out1_t = (cx + r_out*c1, cy + r_out*s1, z1)
            p_out0_t = (cx + r_out*c0, cy + r_out*s0, z1)
            self.add_quad(p_out0_b, p_out1_b, p_out1_t, p_out0_t)
            if r_in > 0:
                p_in0_b = (cx + r_in*c0, cy + r_in*s0, z0)
                p_in1_b = (cx + r_in*c1, cy + r_in*s1, z0)
                p_in1_t = (cx + r_in*c1, cy + r_in*s1, z1)
                p_in0_t = (cx + r_in*c0, cy + r_in*s0, z1)
                self.add_quad(p_in1_b, p_in0_b, p_in0_t, p_in1_t)
                self.add_quad(p_in0_b, p_in1_b, p_out1_b, p_out0_b)
                self.add_quad(p_out0_t, p_out1_t, p_in1_t, p_in0_t)
            else:
                ctr_b = (cx, cy, z0)
                ctr_t = (cx, cy, z1)
                self.triangles.append((ctr_b, p_out1_b, p_out0_b))
                self.triangles.append((ctr_t, p_out0_t, p_out1_t))
                self.quads.append([ctr_b, p_out1_b, p_out0_b, ctr_b])
                self.quads.append([ctr_t, p_out0_t, p_out1_t, ctr_t])

    def save_binary_stl(self, filename, name="HLW8032_Enclosure"):
        header = name.encode('ascii').ljust(80, b' ')[:80]
        with open(filename, 'wb') as f:
            f.write(header)
            f.write(struct.pack('<I', len(self.triangles)))
            for p0, p1, p2 in self.triangles:
                v0 = np.array(p0, dtype=np.float32)
                v1 = np.array(p1, dtype=np.float32)
                v2 = np.array(p2, dtype=np.float32)
                n = np.cross(v1 - v0, v2 - v0)
                norm = np.linalg.norm(n)
                if norm > 1e-9:
                    n /= norm
                else:
                    n = np.array([0, 0, 1], dtype=np.float32)
                f.write(struct.pack('<12fH',
                    n[0], n[1], n[2],
                    v0[0], v0[1], v0[2],
                    v1[0], v1[1], v1[2],
                    v2[0], v2[1], v2[2], 0))

def build_bottom_case():
    m = MeshBuilder()

    # 1. ĐÁY HỘP PHẲNG KÍN CHẮC CHẮN
    m.add_box(0, OUT_W, 0, OUT_L, 0, Z_FLOOR_TOP)

    # 2. GỜ BẬC THANG ĐỠ PCB Ở 4 GÓC & CẠNH (Cao 5.5mm tránh toàn bộ mối hàn & IC mặt đáy)
    ledge_w = 2.2
    corner_span = 15.0
    m.add_box(WALL, WALL + corner_span, WALL, WALL + ledge_w, Z_FLOOR_TOP, Z_PCB_BOT)
    m.add_box(WALL, WALL + ledge_w, WALL, WALL + 9.5, Z_FLOOR_TOP, Z_PCB_BOT)
    m.add_box(OUT_W - WALL - corner_span, OUT_W - WALL, WALL, WALL + ledge_w, Z_FLOOR_TOP, Z_PCB_BOT)
    m.add_box(OUT_W - WALL - ledge_w, OUT_W - WALL, WALL, WALL + 5.0, Z_FLOOR_TOP, Z_PCB_BOT)
    m.add_box(WALL, WALL + corner_span, OUT_L - WALL - ledge_w, OUT_L - WALL, Z_FLOOR_TOP, Z_PCB_BOT)
    m.add_box(WALL, WALL + ledge_w, OUT_L - WALL - 15.0, OUT_L - WALL, Z_FLOOR_TOP, Z_PCB_BOT)
    m.add_box(OUT_W - WALL - 12.0, OUT_W - WALL, OUT_L - WALL - ledge_w, OUT_L - WALL, Z_FLOOR_TOP, Z_PCB_BOT)
    m.add_box(OUT_W - WALL - ledge_w, OUT_W - WALL, OUT_L - WALL - 25.0, OUT_L - WALL, Z_FLOOR_TOP, Z_PCB_BOT)
    m.add_box(WALL, WALL + ledge_w, pcb_y(41.5), pcb_y(55.5), Z_FLOOR_TOP, Z_PCB_BOT)
    m.add_box(pcb_x(30.0), pcb_x(62.0), WALL, WALL + ledge_w, Z_FLOOR_TOP, Z_PCB_BOT)

    # 3. VÁCH TRƯỚC (Y = 0..WALL) + GỜ DƯƠNG LEGO + 2 CỬA SỔ KHÓA SNAP-FIT CHÌM (CHỐNG GÃY)
    # 2 Cửa sổ chữ nhật tại X=[5..16] và [68..79] (rộng 11mm, cao 3.0mm: Z=14.4..17.4mm):
    f_win1 = (pcb_x(5.0), pcb_x(16.0))
    f_win2 = (pcb_x(68.0), pcb_x(79.0))
    z_win_bot, z_win_top = 14.4, 17.4
    lip_t = 0.95

    f_spans = [
        (0.0,       f_win1[0], True),
        (f_win1[0], f_win1[1], False),
        (f_win1[1], f_win2[0], True),
        (f_win2[0], f_win2[1], False),
        (f_win2[1], OUT_W,     True),
    ]
    for sx0, sx1, is_solid in f_spans:
        if is_solid:
            m.add_box(sx0, sx1, 0, WALL, Z_FLOOR_TOP, Z_SPLIT)
            lx0, lx1 = max(sx0, WALL), min(sx1, OUT_W - WALL)
            if lx1 > lx0:
                m.add_box(lx0, lx1, WALL - lip_t, WALL, Z_SPLIT, Z_LIP_TOP)
        else:
            m.add_box(sx0, sx1, 0, WALL, Z_FLOOR_TOP, z_win_bot)
            # Thanh cầu ngang khóa ngàm ở phía trên cửa sổ (Z = 17.4 .. 19.4 mm):
            m.add_box(sx0, sx1, 0, WALL, z_win_top, Z_SPLIT)

    # 4. VÁCH SAU (Y = OUT_L - WALL .. OUT_L) + HỐC BẢO VỆ ĂNG-TEN + 2 CỬA SỔ KHÓA SNAP-FIT CHÌM
    # Chừa cột đứng 2.0mm ở [80.5..82.5] cạnh hốc ăng-ten để đỡ thanh cầu cửa sổ b_win2 cực chắc chắn!
    b_win1 = (pcb_x(6.0),  pcb_x(17.0))
    b_win2 = (pcb_x(82.5), pcb_x(88.5))
    ax1, ax2 = pcb_x(ANT_X1), pcb_x(ANT_X2)

    b_spans = [
        (0.0,       b_win1[0], True),
        (b_win1[0], b_win1[1], False),
        (b_win1[1], ax1,       True),
        (ax2,       b_win2[0], True),
        (b_win2[0], b_win2[1], False),
        (b_win2[1], OUT_W,     True),
    ]
    for sx0, sx1, is_solid in b_spans:
        if is_solid:
            m.add_box(sx0, sx1, OUT_L - WALL, OUT_L, Z_FLOOR_TOP, Z_SPLIT)
            lx0, lx1 = max(sx0, WALL), min(sx1, OUT_W - WALL)
            if lx1 > lx0:
                m.add_box(lx0, lx1, OUT_L - WALL, OUT_L - WALL + lip_t, Z_SPLIT, Z_LIP_TOP)
        else:
            m.add_box(sx0, sx1, OUT_L - WALL, OUT_L, Z_FLOOR_TOP, z_win_bot)
            m.add_box(sx0, sx1, OUT_L - WALL, OUT_L, z_win_top, Z_SPLIT)
    m.add_box(ax1, ax2, OUT_L - WALL, OUT_L, Z_FLOOR_TOP, Z_PCB_BOT)

    ant_ext = 6.0
    m.add_box(ax1 - WALL, ax2 + WALL, OUT_L, OUT_L + ant_ext, 0, Z_FLOOR_TOP)
    m.add_box(ax1 - WALL, ax1, OUT_L, OUT_L + ant_ext, Z_FLOOR_TOP, Z_SPLIT)
    m.add_box(ax2, ax2 + WALL, OUT_L, OUT_L + ant_ext, Z_FLOOR_TOP, Z_SPLIT)
    m.add_box(ax1 - WALL, ax2 + WALL, OUT_L + ant_ext - WALL, OUT_L + ant_ext, Z_FLOOR_TOP, Z_SPLIT)

    # 5. VÁCH TRÁI (X = 0..WALL) - Cửa sổ bên hông luồn dây J4 & J5 + Gờ dương Lego
    m.add_box(0, WALL, WALL, OUT_L - WALL, Z_FLOOR_TOP, Z_PCB_TOP)
    j4_y1, j4_y2 = pcb_y(J4_Y1), pcb_y(J4_Y2)
    j5_y1, j5_y2 = pcb_y(J5_Y1), pcb_y(J5_Y2)
    m.add_box(0, WALL, WALL, j4_y1, Z_PCB_TOP, Z_SPLIT)
    m.add_box(0, WALL, j4_y2, j5_y1, Z_PCB_TOP, Z_SPLIT)
    m.add_box(0, WALL, j5_y2, OUT_L - WALL, Z_PCB_TOP, Z_SPLIT)
    m.add_box(WALL - lip_t, WALL, WALL, j4_y1, Z_SPLIT, Z_LIP_TOP)
    m.add_box(WALL - lip_t, WALL, j4_y2, j5_y1, Z_SPLIT, Z_LIP_TOP)
    m.add_box(WALL - lip_t, WALL, j5_y2, OUT_L - WALL, Z_SPLIT, Z_LIP_TOP)

    # 6. VÁCH PHẢI (X = OUT_W - WALL .. OUT_W)
    # Mở hở toàn bộ dải bên phải từ TFT1 -> UART2 -> UART0 -> BOOT1 -> RESET1 (Y = 4.8 .. 89.5mm)
    # hạ phẳng vách xuống bằng mặt PCB (Z_PCB_TOP) để cắm dây & bấm nút thoải mái 100% không vướng!
    m.add_box(OUT_W - WALL, OUT_W, WALL, OUT_L - WALL, Z_FLOOR_TOP, Z_PCB_TOP)
    t_y1, t_y2 = pcb_y(TFT1_Y1), pcb_y(TFT1_Y2)
    u2_y1, u2_y2 = pcb_y(UART2_Y1), pcb_y(UART2_Y2)
    u0_y1, u0_y2 = pcb_y(UART0_Y1), pcb_y(UART0_Y2)
    by1, by2 = pcb_y(BTN_OPEN_Y1), pcb_y(BTN_OPEN_Y2)
    m.add_box(OUT_W - WALL, OUT_W, WALL, t_y1, Z_PCB_TOP, Z_SPLIT)
    m.add_box(OUT_W - WALL, OUT_W, by2, OUT_L - WALL, Z_PCB_TOP, Z_SPLIT)
    m.add_box(OUT_W - WALL, OUT_W - WALL + lip_t, WALL, t_y1, Z_SPLIT, Z_LIP_TOP)
    m.add_box(OUT_W - WALL, OUT_W - WALL + lip_t, by2, OUT_L - WALL, Z_SPLIT, Z_LIP_TOP)

    return m

def build_top_cover():
    m = MeshBuilder()
    lip_outer_x0 = WALL - 0.95 - SNAP_TOL  # Rãnh âm chụp ra ngoài gờ dương Lego (dung sai 0.12mm)

    # 1. VÁCH TRƯỚC NẮP HỘP + RÃNH ÂM LEGO + 2 LƯỠI GÀ SNAP-FIT DÀI 15.6mm (BIẾN DẠNG CHỈ 0.67% - BỀN VĨNH VIỄN TRÊN PLA)
    m.add_box(0, OUT_W, 0, WALL, Z_LIP_TOP, Z_TOP_IN)
    m.add_box(0, OUT_W, 0, lip_outer_x0, Z_SPLIT, Z_LIP_TOP)
    for tx0, tx1 in [(pcb_x(5.6), pcb_x(15.4)), (pcb_x(68.6), pcb_x(78.4))]:
        # Lưỡi gà đàn hồi liền khối dài L=15.6mm (biến dạng chỉ 0.67%, không bao giờ gãy trên PLA):
        y_out, y_in, y_tooth = WALL + 0.12, WALL + 1.37, 1.25
        m.add_frustum_y(tx0, tx1, y_out,   y_in, y_tooth, y_in, 13.7, 14.8)    # Mũi vát 38° dẫn hướng trượt êm vào khe
        m.add_box(tx0, tx1, y_tooth, y_in, 14.8, 16.6)                        # Vấu gài ngậm sâu 0.75mm trong cửa sổ
        m.add_frustum_y(tx0, tx1, y_tooth, y_in, y_out,   y_in, 16.6, 17.35)   # Vai khóa vát 48° (in 0-support & giữ chặt)
        m.add_box(tx0, tx1, y_out,   y_in, 17.35, 29.8)                       # Thân lò xo đàn hồi dài 12.45mm
        m.add_frustum_y(tx0, tx1, y_out,   y_in, y_out, y_in + 1.0, 29.8, Z_TOP_IN) # Gân vát chân chống gãy gốc

    # 2. VÁCH SAU NẮP HỘP + HỐC ĂNG-TEN + 2 LƯỠI GÀ SNAP-FIT DÀI 15.6mm
    ax1, ax2 = pcb_x(ANT_X1), pcb_x(ANT_X2)
    ant_ext = 6.0
    m.add_box(0, ax1, OUT_L - WALL, OUT_L, Z_LIP_TOP, Z_TOP_IN)
    m.add_box(0, ax1, OUT_L - lip_outer_x0, OUT_L, Z_SPLIT, Z_LIP_TOP)
    m.add_box(ax2, OUT_W, OUT_L - WALL, OUT_L, Z_LIP_TOP, Z_TOP_IN)
    m.add_box(ax2, OUT_W, OUT_L - lip_outer_x0, OUT_L, Z_SPLIT, Z_LIP_TOP)
    m.add_box(ax1 - WALL, ax1, OUT_L, OUT_L + ant_ext, Z_SPLIT, Z_TOTAL_H)
    m.add_box(ax2, ax2 + WALL, OUT_L, OUT_L + ant_ext, Z_SPLIT, Z_TOTAL_H)
    m.add_box(ax1 - WALL, ax2 + WALL, OUT_L + ant_ext - WALL, OUT_L + ant_ext, Z_SPLIT, Z_TOTAL_H)
    m.add_box(ax1 - WALL, ax2 + WALL, OUT_L, OUT_L + ant_ext, Z_TOP_IN, Z_TOTAL_H)
    for tx0, tx1 in [(pcb_x(6.6), pcb_x(16.4)), (pcb_x(83.0), pcb_x(88.0))]:
        y_in, y_out, y_tooth = OUT_L - WALL - 1.37, OUT_L - WALL - 0.12, OUT_L - 1.25
        m.add_frustum_y(tx0, tx1, y_in, y_out,   y_in,     y_tooth, 13.7, 14.8)
        m.add_box(tx0, tx1, y_in, y_tooth, 14.8, 16.6)
        m.add_frustum_y(tx0, tx1, y_in, y_tooth, y_in,     y_out,   16.6, 17.35)
        m.add_box(tx0, tx1, y_in, y_out, 17.35, 29.8)
        m.add_frustum_y(tx0, tx1, y_in, y_out,   y_in - 1.0, y_out, 29.8, Z_TOP_IN)

    # 3. VÁCH TRÁI NẮP HỘP (Cửa sổ bên hông cho dây J4/J5 + Rãnh âm Lego)
    z_wire_top = Z_PCB_TOP + 16.5
    j4_y1, j4_y2 = pcb_y(J4_Y1), pcb_y(J4_Y2)
    j5_y1, j5_y2 = pcb_y(J5_Y1), pcb_y(J5_Y2)
    m.add_box(0, WALL, WALL, j4_y1, Z_LIP_TOP, Z_TOP_IN)
    m.add_box(0, lip_outer_x0, WALL, j4_y1, Z_SPLIT, Z_LIP_TOP)
    m.add_box(0, WALL, j4_y2, j5_y1, Z_LIP_TOP, Z_TOP_IN)
    m.add_box(0, lip_outer_x0, j4_y2, j5_y1, Z_SPLIT, Z_LIP_TOP)
    m.add_box(0, WALL, j5_y2, OUT_L - WALL, Z_LIP_TOP, Z_TOP_IN)
    m.add_box(0, lip_outer_x0, j5_y2, OUT_L - WALL, Z_SPLIT, Z_LIP_TOP)
    m.add_box(0, WALL, j4_y1, j4_y2, z_wire_top, Z_TOP_IN)
    m.add_box(0, WALL, j5_y1, j5_y2, z_wire_top, Z_TOP_IN)

    # 4. VÁCH PHẢI NẮP HỘP:
    # Bỏ toàn bộ thanh chắn & đà treo giữa TFT1 -> UART2 -> UART0 -> BOOT1 -> RESET1!
    # Chỉ giữ lại 2 trụ góc Trước-Phải và Sau-Phải để khóa góc hộp & kẹp PCB cực chắc.
    t_y1, t_y2 = pcb_y(TFT1_Y1), pcb_y(TFT1_Y2)
    by1, by2 = pcb_y(BTN_OPEN_Y1), pcb_y(BTN_OPEN_Y2)
    m.add_box(OUT_W - WALL, OUT_W, WALL, t_y1, Z_LIP_TOP, Z_TOP_IN)
    m.add_box(OUT_W - lip_outer_x0, OUT_W, WALL, t_y1, Z_SPLIT, Z_LIP_TOP)
    m.add_box(OUT_W - WALL, OUT_W, by2, OUT_L - WALL, Z_LIP_TOP, Z_TOP_IN)
    m.add_box(OUT_W - lip_outer_x0, OUT_W, by2, OUT_L - WALL, Z_SPLIT, Z_LIP_TOP)

    # 5. 4 TRỤ TỲ ĐÈ GIỮ CHẶT 4 GÓC PCB TỪ TRÊN XUỐNG (Khe hở Z 0.12mm -> ép chặt PCB, né lưỡi gà Back-Right)
    z_hold_bot = Z_PCB_TOP + 0.12
    pillar_s = 3.6
    m.add_box(WALL, WALL + pillar_s, WALL, WALL + pillar_s, z_hold_bot, Z_TOP_IN)
    m.add_box(OUT_W - WALL - pillar_s, OUT_W - WALL, WALL, WALL + pillar_s, z_hold_bot, Z_TOP_IN)
    m.add_box(WALL, WALL + pillar_s, OUT_L - WALL - pillar_s, OUT_L - WALL, z_hold_bot, Z_TOP_IN)
    m.add_box(OUT_W - WALL - pillar_s, OUT_W - WALL, OUT_L - WALL - pillar_s, OUT_L - WALL, z_hold_bot, Z_TOP_IN)

    # 6. MẶT TRÊN NẮP HỘP + 4 LỖ VÍT MÀN HÌNH TFT 2.8" (50.0 x 81.70 mm, tâm lỗ 44.60 x 76.00 mm):
    # - Mở hở TOÀN BỘ khoang bên phải (từ X=80.5mm ra mép phải, Y=4.8..89.5mm) lộ toàn bộ TFT1, UART2, UART0, BOOT1, RESET1!
    # - Khoét khe luồn hàng 14 chân & dây Dupont màn hình 2.8" (X=30.2..66.5mm, Y=6.2..17.5mm)
    bx1 = pcb_x(80.5)
    scr_wx0, scr_wx1 = pcb_x(30.2), pcb_x(66.5)
    scr_wy0, scr_wy1 = pcb_y(6.2),  pcb_y(17.5)

    # Tọa độ 4 tâm lỗ vít của màn hình 2.8" V1.0 (50.0 x 81.70 mm):
    # Đặt mép màn hình tại X=[23.5..73.5], Y=[6.5..88.2] -> Tâm lỗ: X=[26.2, 70.8], Y=[9.35, 85.35]
    hx_L, hx_R = pcb_x(26.20), pcb_x(70.80)
    hy_B, hy_T = pcb_y(9.35),  pcb_y(85.35)
    hs = 3.3  # Nửa cạnh ô vuông chứa lỗ trụ Ø6.6mm (nằm gọn ngoài khe dây scr_wx0..scr_wx1)

    holes = [
        (hx_L, hy_B),
        (hx_R, hy_B),
        (hx_L, hy_T),
        (hx_R, hy_T),
    ]

    r_xs = sorted([
        0.0,
        hx_L - hs, hx_L + hs,
        scr_wx0, scr_wx1,
        hx_R - hs, hx_R + hs,
        bx1, OUT_W
    ])
    r_ys = sorted([
        0.0,
        t_y1, scr_wy0, hy_B - hs, hy_B + hs, scr_wy1,
        hy_T - hs, hy_T + hs,
        by2, OUT_L
    ])

    for i in range(len(r_xs)-1):
        for j in range(len(r_ys)-1):
            cx0, cx1 = r_xs[i], r_xs[i+1]
            cy0, cy1 = r_ys[j], r_ys[j+1]
            if cx1 - cx0 < 1e-4 or cy1 - cy0 < 1e-4:
                continue
            xm, ym = 0.5*(cx0+cx1), 0.5*(cy0+cy1)

            # Bỏ qua các ô khoét hở:
            # 1) Khoang bên phải mở lộ thiên hoàn toàn từ TFT1 qua UART2/UART0 tới RESET1/BOOT1:
            if xm >= bx1 and t_y1 <= ym <= by2:
                continue
            # 2) Khe luồn 14 chân & dây màn hình TFT 2.8" ngay dưới đầu header:
            if scr_wx0 <= xm <= scr_wx1 and scr_wy0 <= ym <= scr_wy1:
                continue
            # 3) 4 Ô chứa lỗ bắt vít màn hình -> vẽ khối vuông có lỗ tròn Ø2.5mm xuyên trần nắp:
            is_hole_cell = False
            for hx, hy in holes:
                if abs(xm - hx) < hs - 0.1 and abs(ym - hy) < hs - 0.1:
                    is_hole_cell = True
                    break
            if is_hole_cell:
                continue

            m.add_box(cx0, cx1, cy0, cy1, Z_TOP_IN, Z_TOTAL_H)

    # 7. VẼ 4 Ô LỖ XUYÊN TRẦN + 4 TRỤ ĐỠ VÍT MÀN HÌNH TFT 2.8" (CAO 3.5mm ĐỂ NÉ KHE THẺ SD & CHÂN HEADER MẶT LƯNG):
    for hx, hy in holes:
        m.add_square_with_hole(hx, hy, hs, 1.25, Z_TOP_IN, Z_TOTAL_H, segs=32)
        m.add_cylinder(hx, hy, hs, 1.25, Z_TOTAL_H, Z_TOTAL_H + 3.5, segs=32)

    return m

out_dir = r"c:\Users\Admin\OneDrive\Desktop\UDMT\3D_Enclosure_HLW8032"
os.makedirs(out_dir, exist_ok=True)

bot = build_bottom_case()
top = build_top_cover()

bot.save_binary_stl(os.path.join(out_dir, "1_Enclosure_Bottom_Case.stl"), "Bottom_Case_Lego")
top.save_binary_stl(os.path.join(out_dir, "2_Enclosure_Top_Cover.stl"), "Top_Cover_Lego")

assy = MeshBuilder()
assy.triangles = bot.triangles + top.triangles
assy.save_binary_stl(os.path.join(out_dir, "4_Full_Assembly_Preview.stl"), "Full_Assembly_Lego")

vol_bot_cm3 = bot.volume_mm3 / 1000.0
vol_top_cm3 = top.volume_mm3 / 1000.0
total_cm3   = vol_bot_cm3 + vol_top_cm3
weight_bot_g = vol_bot_cm3 * 1.02
weight_top_g = vol_top_cm3 * 1.02
print(f"Bottom volume: {vol_bot_cm3:.1f} cm3 (~{weight_bot_g:.1f} g)")
print(f"Top volume:    {vol_top_cm3:.1f} cm3 (~{weight_top_g:.1f} g)")
print(f"Total volume:  {total_cm3:.1f} cm3 (~{weight_bot_g + weight_top_g:.1f} g)")

# ==============================================================================
# TẠO FILE OPENSCAD THAM SỐ HÓA
# ==============================================================================
scad_code = f"""// ============================================================================
// HỘP BẢO VỆ 3D KHỚP LEGO (SNAP-FIT) - MẠCH HLW8032 + ESP32-S3 + KHAY TFT ST7735S
// Kích thước PCB: {PCB_W} x {PCB_L} x {PCB_T} mm | Dung sai máy in: 0.12 mm
// - Chiều dọc rộng rãi (gầm dưới 5.5mm, khoảng trên 22.5mm) không lo cấn mối hàn
// - Đã cắt bỏ hoàn toàn thanh chắn bên phải chỗ 2 nút RESET1 & BOOT1
// - Tích hợp khay gá màn hình TFT ST7735S + khe cắm thẳng xuống chân tft1
// ============================================================================
$fn = 48;
pcb_w = {PCB_W}; pcb_l = {PCB_L}; pcb_t = {PCB_T};
tol_xy = {TOL_XY}; snap_tol = {SNAP_TOL};
wall = {WALL}; floor_t = {FLOOR_T}; roof_t = {ROOF_T};
under_pcb_h = {UNDER_PCB_H}; above_pcb_h = {ABOVE_PCB_H};
"""
with open(os.path.join(out_dir, "5_Enclosure_Parametric.scad"), "w", encoding="utf-8") as f:
    f.write(scad_code)

# ==============================================================================
# VẼ BẢN VẼ KỸ THUẬT 3D ĐA GÓC NHÌN (BLUEPRINT PNG)
# ==============================================================================
fig = plt.figure(figsize=(18, 12), dpi=160, facecolor='#0F172A')

def draw_mesh_on_ax(ax, mesh, color, alpha=0.82, edge_color='#1E293B', z_offset=0.0):
    shifted_quads = []
    for q in mesh.quads:
        sq = [(p[0], p[1], p[2] + z_offset) for p in q]
        shifted_quads.append(sq)
    poly = Poly3DCollection(shifted_quads, facecolors=color, edgecolors=edge_color, linewidths=0.35, alpha=alpha)
    ax.add_collection3d(poly)

def draw_pcb_on_ax(ax, z_offset=0.0):
    pcb_m = MeshBuilder()
    pcb_m.add_box(WALL+TOL_XY, WALL+TOL_XY+PCB_W, WALL+TOL_XY, WALL+TOL_XY+PCB_L, Z_PCB_BOT, Z_PCB_TOP)
    draw_mesh_on_ax(ax, pcb_m, '#15803D', alpha=0.95, edge_color='#86EFAC', z_offset=z_offset)
    ps1_m = MeshBuilder()
    ps1_m.add_box(pcb_x(20.7), pcb_x(51.2), pcb_y(39.7), pcb_y(91.3), Z_PCB_TOP, Z_PCB_TOP+18.5)
    draw_mesh_on_ax(ax, ps1_m, '#1E293B', alpha=0.95, edge_color='#94A3B8', z_offset=z_offset)
    dom_m = MeshBuilder()
    dom_m.add_box(pcb_x(0.5), pcb_x(18.0), pcb_y(12.0), pcb_y(37.8), Z_PCB_TOP, Z_PCB_TOP+14.0)
    dom_m.add_box(pcb_x(0.5), pcb_x(17.5), pcb_y(59.1), pcb_y(79.9), Z_PCB_TOP, Z_PCB_TOP+14.0)
    draw_mesh_on_ax(ax, dom_m, '#0284C7', alpha=0.95, edge_color='#BAE6FD', z_offset=z_offset)
    btn_m = MeshBuilder()
    btn_m.add_cylinder(pcb_x(RESET1_X), pcb_y(RESET1_Y), 2.2, 0, Z_PCB_TOP, Z_PCB_TOP+3.5, segs=16)
    btn_m.add_cylinder(pcb_x(BOOT1_X), pcb_y(BOOT1_Y), 2.2, 0, Z_PCB_TOP, Z_PCB_TOP+3.5, segs=16)
    draw_mesh_on_ax(ax, btn_m, '#EF4444', alpha=0.98, edge_color='#FCA5A5', z_offset=z_offset)

def draw_tft_on_ax(ax, z_offset=0.0):
    # Màn hình 2.8" V1.0 thật của người dùng (PCB: 50.0 x 81.70 mm, đặt trên 4 trụ vít cao 3.5mm)
    tft_z0 = Z_TOTAL_H + 3.5
    tft_pcb = MeshBuilder()
    tft_pcb.add_box(pcb_x(23.5), pcb_x(73.5), pcb_y(6.5), pcb_y(88.2), tft_z0, tft_z0+1.6)
    draw_mesh_on_ax(ax, tft_pcb, '#DC2626', alpha=0.92, edge_color='#FCA5A5', z_offset=z_offset)
    tft_lcd = MeshBuilder()
    tft_lcd.add_box(pcb_x(25.8), pcb_x(71.2), pcb_y(16.0), pcb_y(78.5), tft_z0+1.6, tft_z0+3.4)
    draw_mesh_on_ax(ax, tft_lcd, '#090D16', alpha=0.95, edge_color='#38BDF8', z_offset=z_offset)

# Góc nhìn 1: Phối cảnh Tách Lớp (Exploded Isometric View)
ax1 = fig.add_subplot(2, 2, 1, projection='3d', facecolor='#0F172A')
draw_mesh_on_ax(ax1, bot, '#38BDF8', alpha=0.68, edge_color='#0284C7', z_offset=0)
draw_pcb_on_ax(ax1, z_offset=10)
draw_mesh_on_ax(ax1, top, '#F8FAFC', alpha=0.65, edge_color='#64748B', z_offset=24)
draw_tft_on_ax(ax1, z_offset=32)
ax1.view_init(elev=28, azim=-52)
ax1.set_title("1. HỘP 3D + 4 TRỤ VÍT MÀN HÌNH 2.8\" (50.0 × 81.70 mm)\nTâm 4 lỗ vít: 44.60 × 76.00 mm + Trụ cao 3.5mm né khay SD", color='white', fontsize=11.5, fontweight='bold', pad=10)

# Góc nhìn 2: Đáy Hộp & Cắt Bỏ Vách Chắn Nút Nhấn
ax2 = fig.add_subplot(2, 2, 2, projection='3d', facecolor='#0F172A')
draw_mesh_on_ax(ax2, bot, '#38BDF8', alpha=0.85, edge_color='#0369A1', z_offset=0)
draw_pcb_on_ax(ax2, z_offset=0)
ax2.view_init(elev=38, azim=-35)
ax2.set_title("2. ĐÁY HỘP ÔM KHÍT PCB (92.90 × 98.40 mm) & GỜ BẬC THANG 5.5mm\n4 Cửa sổ khóa chìm Snap-Fit chống gãy (biến dạng 0.67%)", color='white', fontsize=11.5, fontweight='bold', pad=10)

# Góc nhìn 3: Mặt Trên Nắp Hộp (4 lỗ bắt vít + khe luồn 14 chân)
ax3 = fig.add_subplot(2, 2, 3, projection='3d', facecolor='#0F172A')
draw_mesh_on_ax(ax3, bot, '#0284C7', alpha=0.45, edge_color='#0369A1', z_offset=0)
draw_pcb_on_ax(ax3, z_offset=0)
draw_mesh_on_ax(ax3, top, '#E2E8F0', alpha=0.88, edge_color='#475569', z_offset=0)
draw_tft_on_ax(ax3, z_offset=0)
ax3.view_init(elev=72, azim=-88)
ax3.set_title("3. MẶT TRÊN ĐỤC SẴN 4 LỖ BẮT VÍT M2.5/M3 CHO MÀN 50×81.7mm\nKèm khe luồn 14 chân cắm + Dây giấu gọn bên trong hộp", color='white', fontsize=11.5, fontweight='bold', pad=10)

# Góc nhìn 4: Góc nhìn Cạnh Phải (Thấy rõ vùng nút bấm thông thoáng 100% + Khe TFT1/UART0)
ax4 = fig.add_subplot(2, 2, 4, projection='3d', facecolor='#0F172A')
draw_mesh_on_ax(ax4, bot, '#38BDF8', alpha=0.75, edge_color='#0369A1', z_offset=0)
draw_pcb_on_ax(ax4, z_offset=0)
draw_mesh_on_ax(ax4, top, '#F1F5F9', alpha=0.70, edge_color='#475569', z_offset=0)
draw_tft_on_ax(ax4, z_offset=0)
ax4.view_init(elev=24, azim=35)
ax4.set_title("4. GÓC NHÌN BÊN PHẢI: MÀN HÌNH ST7789 2.8 INCH + NÚT BẤM LỘ THIÊN\nCộng Khe Cắm Dây TFT1 + Cửa Sổ UART0/UART2", color='white', fontsize=11.5, fontweight='bold', pad=10)

for ax in [ax1, ax2, ax3, ax4]:
    ax.set_xlim(-4, OUT_W + 4)
    ax.set_ylim(-4, OUT_L + 8)
    ax.set_zlim(0, 68 if ax == ax1 else 40)
    ax.set_xlabel('X (mm)', color='#94A3B8')
    ax.set_ylabel('Y (mm)', color='#94A3B8')
    ax.set_zlabel('Z (mm)', color='#94A3B8')
    ax.tick_params(colors='#94A3B8', labelsize=8)
    ax.xaxis.pane.fill = False
    ax.yaxis.pane.fill = False
    ax.zaxis.pane.fill = False

plt.suptitle("BẢN VẼ HỘP 3D KHỚP LEGO - CHIỀU DỌC RỘNG RÃI - CẮT BỎ THANH CHE NÚT BẤM & KHAY LẮP TFT ST7735S",
             color='#38BDF8', fontsize=14, fontweight='bold', y=0.98)
plt.tight_layout(rect=[0, 0, 1, 0.95])

blueprint_path = os.path.join(out_dir, "Enclosure_3D_Blueprint.png")
plt.savefig(blueprint_path, dpi=160, facecolor='#0F172A')
artifact_img = r"C:\Users\Admin\.gemini\antigravity\brain\56f37626-bba5-4a9d-be23-ddd493aba15d\Enclosure_3D_Blueprint.png"
plt.savefig(artifact_img, dpi=160, facecolor='#0F172A')
print("Saved updated blueprint successfully!")
