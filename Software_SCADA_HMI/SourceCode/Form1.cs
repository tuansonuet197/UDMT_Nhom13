using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace BTL_UDMT
{
    public partial class Form1 : Form
    {
        // ================== BẢNG MÀU CHUẨN ISA-101 (TRUNG TÍNH - DỊU NHẸ) ==================
        private readonly Color IsaBgCanvas = Color.FromArgb(244, 246, 248);
        private readonly Color IsaBgSurface = Color.FromArgb(255, 255, 255);
        private readonly Color IsaBgSubtle = Color.FromArgb(237, 241, 245);
        private readonly Color IsaTabActive = Color.FromArgb(216, 224, 232);
        private readonly Color IsaBorder = Color.FromArgb(209, 217, 224);

        private readonly Color IsaTextPrimary = Color.FromArgb(30, 41, 59);
        private readonly Color IsaTextSecondary = Color.FromArgb(100, 116, 139);
        private readonly Color IsaButtonNeutral = Color.FromArgb(71, 85, 105);
        private readonly Color IsaButtonConnected = Color.FromArgb(51, 65, 85);

        private readonly Color IsaTrendLine = Color.FromArgb(2, 132, 199);
        private readonly Color IsaCardSelectedBg = Color.FromArgb(240, 247, 255);
        private readonly Color IsaCardFaultBg = Color.FromArgb(254, 242, 242);
        private readonly Color IsaNormalGreen = Color.FromArgb(46, 125, 91);
        private readonly Color IsaAckedAmber = Color.FromArgb(217, 119, 6);
        private readonly Color IsaAlarmRed = Color.FromArgb(220, 38, 38);

        // ================== MÁY TRẠNG THÁI CẢNH BÁO CHUẨN ISA-18.2 ==================
        private enum IsaAlarmState
        {
            Normal,
            UnackActive,
            AckedActive,
            UnackCleared
        }

        private IsaAlarmState _alarmState = IsaAlarmState.Normal;
        private bool _blinkToggle = false;
        private string _activeAlarmType = "";
        private string _activeAlarmMessage = "Hệ thống vận hành trong giới hạn an toàn.";

        // ================== BIẾN HỆ THỐNG & ĐO LƯỜNG ==================
        private SerialPort _serialPort;
        private Timer _clockTimer;
        private Timer _alarmBlinkTimer;
        private DateTime _lastDataTime;
        private bool _isConnected = false;

        private bool _hasLiveData = false;
        private double _lastU = 0.0, _lastI = 0.0, _lastP = 0.0, _valKwh = 0.000;
        private readonly bool[] _metricFaulted = new bool[4];
        private int _selectedMetricIndex = 0; // 0: U, 1: I, 2: P, 3: kWh
        private int _sampleIndex = 0;

        // Ngưỡng bảo vệ (kiểm tra cục bộ trên phần mềm PC, không gửi xuống ESP32)
        private double _uMin = 195.0, _uMax = 240.0;
        private double _iMax = 5.0;
        private double _pMax = 1100.0;
        private double _kwhMax = 100.0;

        // ================== CÁC CONTROL GIAO DIỆN ==================
        private Label lblHeaderStatus, lblClock;
        private ComboBox cbxPorts, cbxBaud, cbxParity, cbxDataBits, cbxStopBits;
        private Button btnRefreshPort, btnConnect;
        private Button btnClearCharts, btnAckTop;
        private Label lblSystemHealthBadge;

        // Cụm 3 Đèn hình chữ nhật báo động
        private Panel lampGreen, lampYellow, lampRed;
        private Label lblLampGreen, lblLampYellow, lblLampRed, lblAlarmBannerText;

        // 3 Tab chính: [0] Giám sát tổng quan | [1] Cấu hình ngưỡng | [2] Nhật ký & Lưu trữ dữ liệu
        private readonly Button[] _tabButtons = new Button[3];
        private readonly Panel[] _tabPages = new Panel[3];

        // 4 Thẻ thông số trên Tab 1 + 4 Đồ thị giữ lịch sử độc lập
        private readonly Panel[] _metricCards = new Panel[4];
        private readonly Panel[] _metricAccentBars = new Panel[4];
        private readonly Label[] _metricValueLabels = new Label[4];
        private readonly Label[] _metricLimitLabels = new Label[4];
        private Chart chartU, chartI, chartP, chartKwh;
        private readonly Chart[] _metricCharts = new Chart[4];

        private readonly Dictionary<Chart, SmoothChartState> _chartStates = new Dictionary<Chart, SmoothChartState>();

        // Control Tab 2 (Cấu hình ngưỡng)
        private NumericUpDown numUmin, numUmax, numImax, numPmax, numKwhMax;
        private Label lblActiveThresholdSummary;

        // Control Tab 3 (Nhật ký & Lưu trữ dữ liệu)
        private DataGridView dgvDataLog, dgvAlarmEvents;
        private Label lblSummaryKwh;

        private class SmoothChartState
        {
            public bool IsTrackingRealtime = true;
            public bool IsPanning = false;
            public Point PanStartPt;
            public double StartXMin, StartXMax, StartYMin, StartYMax;
        }

        public Form1()
        {
            this.Text = "Hệ thống giám sát điện năng";
            this.Size = new Size(1366, 820);
            this.MinimumSize = new Size(1150, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = IsaBgCanvas;
            this.ForeColor = IsaTextPrimary;
            this.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            this.DoubleBuffered = true;

            BuildWireframeInterface();
            InitializeLogic();
        }

        // =========================================================================
        // 1. DỰNG GIAO DIỆN 3 TAB CHUẨN ISA-101 (TỔNG QUAN / NGƯỠNG / NHẬT KÝ)
        // =========================================================================
        private void BuildWireframeInterface()
        {
            this.SuspendLayout();

            // --- A. THANH TIÊU ĐỀ TRÊN CÙNG ---
            var pnlTitleBar = new Panel { Dock = DockStyle.Top, Height = 44, Size = new Size(1366, 44), BackColor = IsaBgSurface, Padding = new Padding(14, 0, 14, 0) };
            pnlTitleBar.Paint += DrawBottomBorder;

            var lblMainTitle = new Label
            {
                Text = "HỆ THỐNG GIÁM SÁT ĐIỆN NĂNG",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = IsaTextPrimary,
                AutoSize = true,
                Location = new Point(12, 11)
            };

            lblClock = new Label
            {
                Text = "00:00:00  01/01/2026",
                Font = new Font("Consolas", 10.5F, FontStyle.Bold),
                ForeColor = IsaTextSecondary,
                Dock = DockStyle.Right,
                Width = 200,
                TextAlign = ContentAlignment.MiddleRight
            };

            lblHeaderStatus = new Label
            {
                Text = "● Chưa kết nối cổng COM",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = IsaTextSecondary,
                Dock = DockStyle.Right,
                Width = 310,
                TextAlign = ContentAlignment.MiddleRight
            };

            pnlTitleBar.Controls.AddRange(new Control[] { lblMainTitle, lblHeaderStatus, lblClock });

            // --- B. KHUNG CHÍNH CHIA 2 CỘT ---
            var pnlMainContainer = new Panel { Dock = DockStyle.Fill, Size = new Size(1366, 760), BackColor = IsaBgSurface };
            var pnlLeftCol = new Panel { Dock = DockStyle.Left, Width = 330, Size = new Size(330, 760), BackColor = IsaBgCanvas };
            pnlLeftCol.Paint += DrawRightBorder;

            var pnlRightCol = new Panel { Dock = DockStyle.Fill, Size = new Size(1036, 760), BackColor = IsaBgSurface };

            this.Controls.Add(pnlMainContainer);
            this.Controls.Add(pnlTitleBar);

            pnlMainContainer.Controls.Add(pnlRightCol);
            pnlMainContainer.Controls.Add(pnlLeftCol);

            // =========================================================================
            // CỘT TRÁI: [1] CẤU HÌNH KẾT NỐI UART (TRÊN) & [2] THAO TÁC VẬN HÀNH (DƯỚI)
            // =========================================================================
            var pnlOperatorBox = new Panel { Dock = DockStyle.Bottom, Height = 158, Size = new Size(330, 158), BackColor = IsaBgCanvas, Padding = new Padding(18) };
            pnlOperatorBox.Paint += DrawTopBorder;

            var pnlUartBox = new Panel { Dock = DockStyle.Fill, Size = new Size(330, 602), BackColor = IsaBgCanvas, Padding = new Padding(18) };

            pnlLeftCol.Controls.Add(pnlUartBox);
            pnlLeftCol.Controls.Add(pnlOperatorBox);

            // --- Ô TRÊN TRÁI: [Cấu hình kết nối UART] ---
            var lblUartHeader = new Label { Text = "CẤU HÌNH KẾT NỐI UART", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = IsaTextPrimary, AutoSize = true, Location = new Point(18, 18) };

            int yStart = 58, yStep = 44;
            pnlUartBox.Controls.Add(CreateFieldLabel("Cổng COM:", new Point(18, yStart + 4)));
            cbxPorts = CreateNeutralCombo(new string[] { }, new Point(118, yStart), 120);
            btnRefreshPort = CreateOutlineButton("Quét", new Point(244, yStart - 1), new Size(66, 30));
            btnRefreshPort.Click += (s, e) => ScanComPorts();

            pnlUartBox.Controls.Add(CreateFieldLabel("Baudrate:", new Point(18, yStart + yStep + 4)));
            cbxBaud = CreateNeutralCombo(new string[] { "4800", "9600", "19200", "38400", "57600", "115200", "230400" }, new Point(118, yStart + yStep), 192);
            cbxBaud.SelectedItem = "115200";

            pnlUartBox.Controls.Add(CreateFieldLabel("Parity:", new Point(18, yStart + yStep * 2 + 4)));
            cbxParity = CreateNeutralCombo(new string[] { "None", "Even", "Odd" }, new Point(118, yStart + yStep * 2), 192);
            cbxParity.SelectedIndex = 0;

            pnlUartBox.Controls.Add(CreateFieldLabel("Data Bits:", new Point(18, yStart + yStep * 3 + 4)));
            cbxDataBits = CreateNeutralCombo(new string[] { "8", "7" }, new Point(118, yStart + yStep * 3), 78);
            cbxDataBits.SelectedIndex = 0;

            pnlUartBox.Controls.Add(new Label { Text = "Stop:", ForeColor = IsaTextSecondary, AutoSize = true, Location = new Point(204, yStart + yStep * 3 + 4) });
            cbxStopBits = CreateNeutralCombo(new string[] { "One", "Two" }, new Point(244, yStart + yStep * 3), 66);
            cbxStopBits.SelectedIndex = 0;

            btnConnect = CreateSolidButton("KẾT NỐI UART", IsaButtonNeutral, new Point(18, yStart + yStep * 4 + 14), new Size(292, 42));
            btnConnect.Click += BtnConnect_Click;

            pnlUartBox.Controls.AddRange(new Control[] { lblUartHeader, cbxPorts, btnRefreshPort, cbxBaud, cbxParity, cbxDataBits, cbxStopBits, btnConnect });

            // --- Ô DƯỚI TRÁI: [Thao tác vận hành] ---
            var lblOpHeader = new Label { Text = "THAO TÁC VẬN HÀNH", Font = new Font("Segoe UI", 10.5F, FontStyle.Bold), ForeColor = IsaTextPrimary, AutoSize = true, Location = new Point(18, 14) };

            lblSystemHealthBadge = new Label
            {
                Text = "TRẠNG THÁI: ĐANG CHỜ KẾT NỐI",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = IsaBgSubtle,
                ForeColor = IsaTextSecondary,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(292, 36),
                Location = new Point(18, 44)
            };

            btnClearCharts = CreateOutlineButton("Làm mới phiên đồ thị && bộ đệm", new Point(18, 94), new Size(292, 38));
            btnClearCharts.Click += (s, e) => ClearAllChartData();

            pnlOperatorBox.Controls.AddRange(new Control[] { lblOpHeader, lblSystemHealthBadge, btnClearCharts });

            // =========================================================================
            // CỘT PHẢI: [1] THANH 3 TAB BẰNG NHAU -> [2] DẢI 3 ĐÈN BÁO ĐỘNG -> [3] NỘI DUNG
            // =========================================================================
            var pnlTabHeader = new TableLayoutPanel { Dock = DockStyle.Top, Height = 44, Size = new Size(1036, 44), ColumnCount = 3, RowCount = 1, BackColor = IsaBorder, Padding = new Padding(0, 0, 0, 1) };
            for (int i = 0; i < 3; i++) pnlTabHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));

            string[] tabNames = { "Giám sát tổng quan", "Cấu hình ngưỡng", "Nhật ký && Lưu trữ dữ liệu" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var btn = new Button
                {
                    Text = tabNames[i],
                    Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 10F, i == 0 ? FontStyle.Bold : FontStyle.Regular),
                    BackColor = i == 0 ? IsaTabActive : IsaBgSurface,
                    ForeColor = IsaTextPrimary,
                    Margin = new Padding(0, 0, i < 2 ? 1 : 0, 0),
                    Cursor = Cursors.Hand
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.Click += (s, e) => SwitchTab(idx);
                _tabButtons[i] = btn;
                pnlTabHeader.Controls.Add(btn, i, 0);
            }

            // --- DẢI 3 ĐÈN HÌNH CHỮ NHẬT BÁO ĐỘNG ---
            var pnlIsaAlarmBar = new Panel { Dock = DockStyle.Top, Height = 74, Size = new Size(1036, 74), BackColor = IsaBgCanvas, Padding = new Padding(16, 6, 16, 6) };
            pnlIsaAlarmBar.Paint += DrawBottomBorder;

            var pnlAlarmRow1 = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Color.Transparent };
            lampGreen = CreateAlarmRectangleLamp("NORMAL (BÌNH THƯỜNG)", new Point(0, 1), out lblLampGreen);
            lampYellow = CreateAlarmRectangleLamp("ACKED (ĐÃ XÁC NHẬN)", new Point(176, 1), out lblLampYellow);
            lampRed = CreateAlarmRectangleLamp("ALARM (BÁO ĐỘNG)", new Point(352, 1), out lblLampRed);

            btnAckTop = CreateSolidButton("XÁC NHẬN CẢNH BÁO (ACK)", IsaButtonNeutral, new Point(760, 1), new Size(216, 32));
            btnAckTop.Dock = DockStyle.Right;
            btnAckTop.Click += BtnAckAlarm_Click;

            pnlAlarmRow1.Controls.AddRange(new Control[] { lampGreen, lampYellow, lampRed, btnAckTop });

            lblAlarmBannerText = new Label
            {
                Text = "Trạng thái: Hệ thống vận hành trong giới hạn an toàn.",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = IsaTextSecondary,
                Dock = DockStyle.Bottom,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            };

            pnlIsaAlarmBar.Controls.Add(lblAlarmBannerText);
            pnlIsaAlarmBar.Controls.Add(pnlAlarmRow1);

            var pnlPagesHost = new Panel { Dock = DockStyle.Fill, Size = new Size(1036, 642), BackColor = IsaBgSurface };

            pnlRightCol.Controls.Add(pnlPagesHost);
            pnlRightCol.Controls.Add(pnlIsaAlarmBar);
            pnlRightCol.Controls.Add(pnlTabHeader);

            // Khởi tạo 3 trang nội dung
            _tabPages[0] = CreateOverviewTabPage();
            _tabPages[1] = CreateThresholdConfigTabPage();
            _tabPages[2] = CreateDataMonitorTabPage();

            for (int i = 0; i < 3; i++)
            {
                _tabPages[i].Dock = DockStyle.Fill;
                _tabPages[i].Visible = (i == 0);
                pnlPagesHost.Controls.Add(_tabPages[i]);
            }

            UpdateThresholdStripLines();
            UpdateThresholdLabels();
            SelectMetricChart(0);
            UpdateIsaAlarmLampsVisual();
            this.ResumeLayout(true);
        }

        private Panel CreateAlarmRectangleLamp(string text, Point location, out Label lblText)
        {
            var box = new Panel
            {
                Size = new Size(168, 32),
                Location = location,
                BorderStyle = BorderStyle.FixedSingle
            };
            lblText = new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            };
            box.Controls.Add(lblText);
            return box;
        }

        // =========================================================================
        // 2. TAB 1: GIÁM SÁT TỔNG QUAN (4 THẺ V/A/W/kWh + 1 ĐỒ THỊ LỚN BẤM CHỌN)
        // =========================================================================
        private Panel CreateOverviewTabPage()
        {
            var page = new Panel { Size = new Size(1036, 642), BackColor = IsaBgSurface, Padding = new Padding(16, 14, 16, 14) };

            // Hàng trên: 4 thẻ KPI song song
            var tblCards = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 148,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = IsaBgSurface,
                Margin = new Padding(0)
            };
            for (int i = 0; i < 4; i++) tblCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            tblCards.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            string[] titles = { "ĐIỆN ÁP (V)", "DÒNG ĐIỆN (A)", "CÔNG SUẤT (W)", "ĐIỆN NĂNG (kWh)" };
            string[] defaults = { "0.0 V", "0.00 A", "0.0 W", "0.000 kWh" };

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var card = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = IsaBgCanvas,
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(i == 0 ? 0 : 6, 0, i == 3 ? 0 : 6, 0),
                    Cursor = Cursors.Hand
                };

                var accentBar = new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 4,
                    BackColor = IsaBorder
                };

                var lblTitle = new Label
                {
                    Text = titles[i],
                    Dock = DockStyle.Top,
                    Height = 32,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    ForeColor = IsaTextSecondary,
                    TextAlign = ContentAlignment.BottomCenter,
                    Cursor = Cursors.Hand
                };

                var lblLimit = new Label
                {
                    Text = "",
                    Dock = DockStyle.Bottom,
                    Height = 30,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                    ForeColor = IsaTextSecondary,
                    TextAlign = ContentAlignment.TopCenter,
                    Cursor = Cursors.Hand
                };

                var lblVal = new Label
                {
                    Text = defaults[i],
                    Dock = DockStyle.Fill,
                    Font = new Font("Consolas", i == 3 ? 22F : 25F, FontStyle.Bold),
                    ForeColor = IsaTextPrimary,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand
                };

                EventHandler clickHandler = (s, e) => SelectMetricChart(idx);
                card.Click += clickHandler;
                accentBar.Click += clickHandler;
                lblTitle.Click += clickHandler;
                lblVal.Click += clickHandler;
                lblLimit.Click += clickHandler;

                card.Controls.Add(lblVal);
                card.Controls.Add(lblLimit);
                card.Controls.Add(lblTitle);
                card.Controls.Add(accentBar);

                _metricCards[i] = card;
                _metricAccentBars[i] = accentBar;
                _metricValueLabels[i] = lblVal;
                _metricLimitLabels[i] = lblLimit;
                tblCards.Controls.Add(card, i, 0);
            }

            // Thanh hướng dẫn chọn kênh đồ thị
            var pnlChartHint = new Panel { Dock = DockStyle.Top, Height = 30, BackColor = IsaBgSurface };
            var lblHint = new Label
            {
                Text = "Nhấp vào thẻ thông số phía trên để chuyển đồ thị  •  Cuộn chuột để thu phóng  •  Kéo chuột để di chuyển  •  Nhấp đúp để bám thời gian thực",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = IsaTextSecondary,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlChartHint.Controls.Add(lblHint);

            // Khu vực đồ thị lớn phía dưới
            var pnlChartHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = IsaBgSurface,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(8)
            };

            chartU = CreateSmoothInteractiveChart("Đồ thị điện áp theo thời gian thực", "Điện áp (V)", "V");
            chartI = CreateSmoothInteractiveChart("Đồ thị dòng điện theo thời gian thực", "Dòng điện (A)", "A");
            chartP = CreateSmoothInteractiveChart("Đồ thị công suất theo thời gian thực", "Công suất (W)", "W");
            chartKwh = CreateSmoothInteractiveChart("Đồ thị điện năng tiêu thụ tích lũy", "Điện năng (kWh)", "kWh");

            _metricCharts[0] = chartU;
            _metricCharts[1] = chartI;
            _metricCharts[2] = chartP;
            _metricCharts[3] = chartKwh;

            for (int i = 0; i < 4; i++)
            {
                _metricCharts[i].Visible = (i == 0);
                pnlChartHost.Controls.Add(_metricCharts[i]);
            }

            page.Controls.Add(pnlChartHost);
            page.Controls.Add(pnlChartHint);
            page.Controls.Add(tblCards);
            return page;
        }

        private void SelectMetricChart(int index)
        {
            if (index < 0 || index > 3) return;
            _selectedMetricIndex = index;
            for (int i = 0; i < 4; i++)
            {
                _metricCharts[i].Visible = (i == index);
            }
            RefreshMetricCardStyles();
        }

        private void RefreshMetricCardStyles()
        {
            for (int i = 0; i < 4; i++)
            {
                bool isSel = (i == _selectedMetricIndex);
                bool isFault = _metricFaulted[i];

                if (isFault)
                {
                    _metricCards[i].BackColor = IsaCardFaultBg;
                    _metricAccentBars[i].BackColor = IsaAlarmRed;
                    _metricValueLabels[i].ForeColor = IsaAlarmRed;
                }
                else if (isSel)
                {
                    _metricCards[i].BackColor = IsaCardSelectedBg;
                    _metricAccentBars[i].BackColor = IsaTrendLine;
                    _metricValueLabels[i].ForeColor = IsaTextPrimary;
                }
                else
                {
                    _metricCards[i].BackColor = IsaBgCanvas;
                    _metricAccentBars[i].BackColor = IsaBorder;
                    _metricValueLabels[i].ForeColor = IsaTextPrimary;
                }
            }
        }

        // =========================================================================
        // 3. TAB 2: CẤU HÌNH NGƯỠNG CẢNH BÁO TẬP TRUNG TRÊN PC
        // =========================================================================
        private Panel CreateThresholdConfigTabPage()
        {
            var page = new Panel { Size = new Size(1036, 642), BackColor = IsaBgSurface, Padding = new Padding(24) };

            var card = new Panel
            {
                Size = new Size(680, 440),
                BackColor = IsaBgCanvas,
                BorderStyle = BorderStyle.FixedSingle,
                Location = new Point(160, 40)
            };

            page.Resize += (s, e) =>
            {
                card.Left = Math.Max(20, (page.ClientSize.Width - card.Width) / 2);
                card.Top = Math.Max(20, (page.ClientSize.Height - card.Height) / 2 - 10);
            };

            var lblTitle = new Label
            {
                Text = "CẤU HÌNH NGƯỠNG CẢNH BÁO CỤC BỘ TRÊN MÁY TÍNH",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = IsaTextPrimary,
                Size = new Size(640, 28),
                Location = new Point(20, 22),
                TextAlign = ContentAlignment.MiddleCenter
            };

            var lblNote = new Label
            {
                Text = "Phần mềm nhận dữ liệu đo một chiều từ mạch đo và đối chiếu trực tiếp với các ngưỡng bên dưới để kích hoạt cảnh báo.",
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = IsaTextSecondary,
                Size = new Size(620, 36),
                Location = new Point(30, 52),
                TextAlign = ContentAlignment.MiddleCenter
            };

            int lx = 72, nx = 365, ux = 472, y = 112, step = 46;

            card.Controls.Add(new Label { Text = "Ngưỡng thấp áp (Umin):", Font = new Font("Segoe UI", 10F), ForeColor = IsaTextPrimary, AutoSize = true, Location = new Point(lx, y + 4) });
            numUmin = CreateNeutralNumeric(150, 215, (decimal)_uMin, 0, new Point(nx, y));
            card.Controls.Add(new Label { Text = "V", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = IsaTextSecondary, AutoSize = true, Location = new Point(ux, y + 4) });

            y += step;
            card.Controls.Add(new Label { Text = "Ngưỡng quá áp (Umax):", Font = new Font("Segoe UI", 10F), ForeColor = IsaTextPrimary, AutoSize = true, Location = new Point(lx, y + 4) });
            numUmax = CreateNeutralNumeric(220, 265, (decimal)_uMax, 0, new Point(nx, y));
            card.Controls.Add(new Label { Text = "V", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = IsaTextSecondary, AutoSize = true, Location = new Point(ux, y + 4) });

            y += step;
            card.Controls.Add(new Label { Text = "Ngưỡng quá dòng (Imax):", Font = new Font("Segoe UI", 10F), ForeColor = IsaTextPrimary, AutoSize = true, Location = new Point(lx, y + 4) });
            numImax = CreateNeutralNumeric(0.5m, 30.0m, (decimal)_iMax, 1, new Point(nx, y));
            card.Controls.Add(new Label { Text = "A", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = IsaTextSecondary, AutoSize = true, Location = new Point(ux, y + 4) });

            y += step;
            card.Controls.Add(new Label { Text = "Ngưỡng quá công suất (Pmax):", Font = new Font("Segoe UI", 10F), ForeColor = IsaTextPrimary, AutoSize = true, Location = new Point(lx, y + 4) });
            numPmax = CreateNeutralNumeric(50, 5000, (decimal)_pMax, 0, new Point(nx, y));
            numPmax.Increment = 50;
            card.Controls.Add(new Label { Text = "W", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = IsaTextSecondary, AutoSize = true, Location = new Point(ux, y + 4) });

            y += step;
            card.Controls.Add(new Label { Text = "Ngưỡng điện năng tích lũy (Emax):", Font = new Font("Segoe UI", 10F), ForeColor = IsaTextPrimary, AutoSize = true, Location = new Point(lx, y + 4) });
            numKwhMax = CreateNeutralNumeric(0.1m, 10000m, (decimal)_kwhMax, 1, new Point(nx, y));
            numKwhMax.Increment = 1.0m;
            card.Controls.Add(new Label { Text = "kWh", Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = IsaTextSecondary, AutoSize = true, Location = new Point(ux, y + 4) });

            var btnApplyAll = CreateSolidButton("Áp dụng toàn bộ ngưỡng", IsaButtonNeutral, new Point(155, 352), new Size(210, 40));
            btnApplyAll.Click += BtnApplyThresholds_Click;

            var btnDefault = CreateOutlineButton("Khôi phục mặc định", new Point(380, 352), new Size(155, 40));
            btnDefault.Click += (s, e) =>
            {
                numUmin.Value = 195;
                numUmax.Value = 240;
                numImax.Value = 5.0m;
                numPmax.Value = 1100;
                numKwhMax.Value = 100.0m;
                BtnApplyThresholds_Click(s, e);
            };

            lblActiveThresholdSummary = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = IsaTextSecondary,
                Size = new Size(640, 24),
                Location = new Point(20, 404),
                TextAlign = ContentAlignment.MiddleCenter
            };

            card.Controls.AddRange(new Control[]
            {
                lblTitle, lblNote,
                numUmin, numUmax, numImax, numPmax, numKwhMax,
                btnApplyAll, btnDefault, lblActiveThresholdSummary
            });

            page.Controls.Add(card);
            return page;
        }

        private void UpdateThresholdLabels()
        {
            if (_metricLimitLabels[0] != null) _metricLimitLabels[0].Text = $"Dải chuẩn: {_uMin:0} – {_uMax:0} V";
            if (_metricLimitLabels[1] != null) _metricLimitLabels[1].Text = $"Ngưỡng tối đa: ≤ {_iMax:0.#} A";
            if (_metricLimitLabels[2] != null) _metricLimitLabels[2].Text = $"Ngưỡng tối đa: ≤ {_pMax:0} W";
            if (_metricLimitLabels[3] != null) _metricLimitLabels[3].Text = $"Ngưỡng tối đa: ≤ {_kwhMax:0.#} kWh";

            if (lblActiveThresholdSummary != null)
            {
                lblActiveThresholdSummary.Text = $"Đang áp dụng: U = [{_uMin:0}..{_uMax:0}] V  |  I ≤ {_iMax:0.#} A  |  P ≤ {_pMax:0} W  |  E ≤ {_kwhMax:0.#} kWh";
            }
        }

        // =========================================================================
        // 4. ĐỒ THỊ CHUẨN ISA-101 (PAN/ZOOM MƯỢT, TRỤC Y KHÔNG ÂM & ĐƯỜNG NGƯỠNG)
        // =========================================================================
        private Chart CreateSmoothInteractiveChart(string titleText, string yAxisTitle, string unit)
        {
            var chart = new Chart { Dock = DockStyle.Fill, Size = new Size(980, 420), BackColor = IsaBgSurface };

            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(chart, true, null);

            var title = new Title(titleText.ToUpper(), Docking.Top, new Font("Segoe UI", 10.5F, FontStyle.Bold), IsaTextSecondary);
            chart.Titles.Add(title);

            var legend = new Legend("MainLegend")
            {
                Docking = Docking.Top,
                Alignment = StringAlignment.Far,
                BackColor = IsaBgSurface,
                ForeColor = IsaTextPrimary,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            chart.Legends.Add(legend);

            var area = new ChartArea("Area1") { BackColor = IsaBgCanvas };

            area.AxisX.Title = "Thời gian thực (HH:mm:ss - Đơn vị: Giây [s])";
            area.AxisX.TitleFont = new Font("Segoe UI", 9F, FontStyle.Regular);
            area.AxisX.TitleForeColor = IsaTextSecondary;
            area.AxisX.LabelStyle.ForeColor = IsaTextSecondary;
            area.AxisX.LineColor = IsaBorder;
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(224, 230, 237);
            area.AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dash;

            area.AxisY.Title = $"Giá trị {yAxisTitle} — Đơn vị: [{unit}]";
            area.AxisY.TitleFont = new Font("Segoe UI", 9F, FontStyle.Bold);
            area.AxisY.TitleForeColor = IsaTextPrimary;
            area.AxisY.LabelStyle.ForeColor = IsaTextSecondary;
            area.AxisY.LabelStyle.Format = unit == "kWh" ? "0.###" : "0.##";
            area.AxisY.Minimum = 0;
            area.AxisY.LineColor = IsaBorder;
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(224, 230, 237);

            chart.ChartAreas.Add(area);

            var series = new Series($"{yAxisTitle} [{unit}]")
            {
                ChartType = SeriesChartType.FastLine,
                BorderWidth = 3,
                Color = IsaTrendLine,
                XValueType = ChartValueType.Double,
                ToolTip = "Thời gian: #AXISLABEL\nGiá trị: #VALY{0.###} " + unit
            };
            chart.Series.Add(series);

            var state = new SmoothChartState();
            _chartStates[chart] = state;

            AttachSmoothChartEvents(chart, state);
            return chart;
        }

        private void UpdateThresholdStripLines()
        {
            SetChartStripLines(chartU, new[] { (_uMax, $"Umax ({_uMax}V)"), (_uMin, $"Umin ({_uMin}V)") });
            SetChartStripLines(chartI, new[] { (_iMax, $"Imax ({_iMax}A)") });
            SetChartStripLines(chartP, new[] { (_pMax, $"Pmax ({_pMax}W)") });
            SetChartStripLines(chartKwh, new[] { (_kwhMax, $"Emax ({_kwhMax}kWh)") });
        }

        private void SetChartStripLines(Chart chart, (double val, string label)[] limits)
        {
            if (chart == null) return;
            var stripLines = chart.ChartAreas[0].AxisY.StripLines;
            stripLines.Clear();

            foreach (var lim in limits)
            {
                var sl = new StripLine
                {
                    Interval = 0,
                    IntervalOffset = lim.val,
                    StripWidth = 0,
                    BorderColor = Color.FromArgb(180, IsaAlarmRed),
                    BorderDashStyle = ChartDashStyle.Dash,
                    BorderWidth = 2,
                    Text = lim.label,
                    TextAlignment = StringAlignment.Near,
                    ForeColor = IsaAlarmRed,
                    Font = new Font("Segoe UI", 8F, FontStyle.Bold)
                };
                stripLines.Add(sl);
            }
        }

        private void AttachSmoothChartEvents(Chart chart, SmoothChartState state)
        {
            ChartArea area = chart.ChartAreas[0];

            chart.MouseEnter += (s, e) => { if (!chart.Focused) chart.Focus(); };

            chart.MouseWheel += (s, e) =>
            {
                if (chart.Series[0].Points.Count < 2) return;

                try
                {
                    LockCurrentAxisBounds(area);
                    state.IsTrackingRealtime = false;

                    bool ctrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                    bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

                    HitTestResult hit = chart.HitTest(e.X, e.Y);
                    bool onXAxis = (hit.ChartElementType == ChartElementType.Axis || hit.ChartElementType == ChartElementType.AxisLabels || hit.ChartElementType == ChartElementType.AxisTitle) && hit.Axis == area.AxisX;
                    bool onYAxis = (hit.ChartElementType == ChartElementType.Axis || hit.ChartElementType == ChartElementType.AxisLabels || hit.ChartElementType == ChartElementType.AxisTitle) && hit.Axis == area.AxisY;

                    bool zoomX = true, zoomY = true;
                    if (ctrl || onXAxis) { zoomX = true; zoomY = false; }
                    else if (shift || onYAxis) { zoomX = false; zoomY = true; }

                    double factor = (e.Delta > 0) ? 0.85 : 1.18;

                    if (zoomX)
                    {
                        double xMin = area.AxisX.Minimum;
                        double xMax = area.AxisX.Maximum;
                        double pivotX = SafePixelToXValue(area, e.X, xMin, xMax);
                        double newXMin = pivotX - (pivotX - xMin) * factor;
                        double newXMax = pivotX + (xMax - pivotX) * factor;

                        if (newXMax - newXMin >= 2.0 && newXMax - newXMin <= 2000.0)
                        {
                            area.AxisX.Minimum = newXMin;
                            area.AxisX.Maximum = newXMax;
                        }
                    }

                    if (zoomY)
                    {
                        double yMin = area.AxisY.Minimum;
                        double yMax = area.AxisY.Maximum;
                        double pivotY = SafePixelToYValue(area, e.Y, yMin, yMax);
                        double newYMin = Math.Max(0.0, pivotY - (pivotY - yMin) * factor);
                        double newYMax = Math.Max(newYMin + 0.001, pivotY + (yMax - pivotY) * factor);

                        if (newYMax - newYMin >= 0.001 && newYMax - newYMin <= 50000.0)
                        {
                            area.AxisY.Minimum = newYMin;
                            area.AxisY.Maximum = newYMax;
                        }
                    }
                }
                catch { }
            };

            chart.MouseDown += (s, e) =>
            {
                if ((e.Button == MouseButtons.Left || e.Button == MouseButtons.Right) && chart.Series[0].Points.Count >= 2)
                {
                    LockCurrentAxisBounds(area);
                    state.IsTrackingRealtime = false;
                    state.IsPanning = true;
                    state.PanStartPt = e.Location;
                    state.StartXMin = area.AxisX.Minimum;
                    state.StartXMax = area.AxisX.Maximum;
                    state.StartYMin = area.AxisY.Minimum;
                    state.StartYMax = area.AxisY.Maximum;
                    chart.Cursor = Cursors.SizeAll;
                }
            };

            chart.MouseMove += (s, e) =>
            {
                if (!state.IsPanning) return;

                try
                {
                    double plotWidthPx = Math.Max(100.0, chart.ClientSize.Width * 0.80);
                    double plotHeightPx = Math.Max(80.0, chart.ClientSize.Height * 0.72);

                    int deltaPxX = e.X - state.PanStartPt.X;
                    int deltaPxY = e.Y - state.PanStartPt.Y;

                    double spanX = state.StartXMax - state.StartXMin;
                    double spanY = state.StartYMax - state.StartYMin;

                    double shiftX = -(deltaPxX / plotWidthPx) * spanX;
                    double shiftY = (deltaPxY / plotHeightPx) * spanY;

                    area.AxisX.Minimum = state.StartXMin + shiftX;
                    area.AxisX.Maximum = state.StartXMax + shiftX;
                    double pannedYMin = Math.Max(0.0, state.StartYMin + shiftY);
                    area.AxisY.Minimum = pannedYMin;
                    area.AxisY.Maximum = pannedYMin + spanY;
                }
                catch { }
            };

            chart.MouseUp += (s, e) =>
            {
                if (state.IsPanning)
                {
                    state.IsPanning = false;
                    chart.Cursor = Cursors.Default;
                }
            };

            chart.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    state.IsPanning = false;
                    state.IsTrackingRealtime = true;
                    UpdateChartRealtimeWindow(chart);
                }
            };
        }

        private void LockCurrentAxisBounds(ChartArea area)
        {
            if (double.IsNaN(area.AxisX.Minimum) || double.IsNaN(area.AxisX.Maximum))
            {
                area.AxisX.Minimum = area.AxisX.ScaleView.ViewMinimum;
                area.AxisX.Maximum = area.AxisX.ScaleView.ViewMaximum;
            }
            if (double.IsNaN(area.AxisY.Minimum) || double.IsNaN(area.AxisY.Maximum))
            {
                area.AxisY.Minimum = area.AxisY.ScaleView.ViewMinimum;
                area.AxisY.Maximum = area.AxisY.ScaleView.ViewMaximum;
            }
        }

        private double SafePixelToXValue(ChartArea area, int pixelX, double fallbackMin, double fallbackMax)
        {
            try
            {
                double val = area.AxisX.PixelPositionToValue(pixelX);
                return Math.Max(fallbackMin, Math.Min(fallbackMax, val));
            }
            catch { return (fallbackMin + fallbackMax) / 2.0; }
        }

        private double SafePixelToYValue(ChartArea area, int pixelY, double fallbackMin, double fallbackMax)
        {
            try
            {
                double val = area.AxisY.PixelPositionToValue(pixelY);
                return Math.Max(fallbackMin, Math.Min(fallbackMax, val));
            }
            catch { return (fallbackMin + fallbackMax) / 2.0; }
        }

        private void AppendChartPoint(Chart chart, string timeStr, double yVal)
        {
            var s = chart.Series[0];
            int ptIdx = s.Points.AddXY(_sampleIndex, yVal);
            s.Points[ptIdx].AxisLabel = timeStr;

            if (s.Points.Count > 400)
                s.Points.RemoveAt(0);

            if (_chartStates.TryGetValue(chart, out var state) && state.IsTrackingRealtime)
            {
                UpdateChartRealtimeWindow(chart);
            }
        }

        private void UpdateChartRealtimeWindow(Chart chart)
        {
            var area = chart.ChartAreas[0];
            var pts = chart.Series[0].Points;
            if (pts.Count == 0) return;

            double maxX = pts[pts.Count - 1].XValue;
            double minX = Math.Max(pts[0].XValue, maxX - 30);

            double minY = double.MaxValue, maxY = double.MinValue;
            for (int i = Math.Max(0, pts.Count - 31); i < pts.Count; i++)
            {
                double v = pts[i].YValues[0];
                if (v < minY) minY = v;
                if (v > maxY) maxY = v;
            }

            if (chart == chartKwh)
            {
                double marginKwh = Math.Max(0.01, (maxY - minY) * 0.25);
                double yMin = Math.Max(0.0, Math.Round(minY - marginKwh, 3));
                double yMax = Math.Max(yMin + 0.01, Math.Round(maxY + marginKwh, 3));
                area.AxisX.Minimum = minX;
                area.AxisX.Maximum = Math.Max(minX + 5, maxX);
                area.AxisY.Minimum = yMin;
                area.AxisY.Maximum = yMax;
            }
            else
            {
                double marginY = Math.Max(2.0, (maxY - minY) * 0.25);
                double yMin = Math.Max(0.0, Math.Floor(minY - marginY));
                double yMax = Math.Max(yMin + 1.0, Math.Ceiling(maxY + marginY));
                area.AxisX.Minimum = minX;
                area.AxisX.Maximum = Math.Max(minX + 5, maxX);
                area.AxisY.Minimum = yMin;
                area.AxisY.Maximum = yMax;
            }
        }

        private void ClearAllChartData()
        {
            _sampleIndex = 0;
            chartU.Series[0].Points.Clear();
            chartI.Series[0].Points.Clear();
            chartP.Series[0].Points.Clear();
            chartKwh.Series[0].Points.Clear();
        }

        // =========================================================================
        // 5. TAB 3: NHẬT KÝ & LƯU TRỮ DỮ LIỆU (ĐỒNG BỘ TIẾNG VIỆT 100%)
        // =========================================================================
        private Panel CreateDataMonitorTabPage()
        {
            var page = new Panel { Size = new Size(1036, 642), BackColor = IsaBgSurface, Padding = new Padding(18) };

            var pnlTopSummary = new Panel { Dock = DockStyle.Top, Height = 60, Size = new Size(1000, 60), BackColor = IsaBgCanvas, Padding = new Padding(14) };
            pnlTopSummary.Paint += DrawBottomBorder;

            lblSummaryKwh = new Label
            {
                Text = "ĐIỆN NĂNG TÍCH LŨY: 0.000 kWh   |   Tần số: 0 Hz",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = IsaTextPrimary,
                AutoSize = true,
                Location = new Point(14, 18)
            };

            var btnExportCsv = CreateSolidButton("Xuất Báo Cáo CSV", IsaButtonNeutral, new Point(640, 11), new Size(185, 36));
            btnExportCsv.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnExportCsv.Click += BtnExportCsv_Click;

            var btnClearLog = CreateOutlineButton("Xóa bảng Nhật ký", new Point(835, 11), new Size(140, 36));
            btnClearLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClearLog.Click += (s, e) => { dgvDataLog.Rows.Clear(); dgvAlarmEvents.Rows.Clear(); };

            pnlTopSummary.Controls.AddRange(new Control[] { lblSummaryKwh, btnExportCsv, btnClearLog });

            var pnlAlarmEventSection = new Panel { Dock = DockStyle.Bottom, Height = 210, Size = new Size(1000, 210), BackColor = IsaBgSurface, Padding = new Padding(0, 10, 0, 0) };
            var lblAlarmTableTitle = new Label
            {
                Text = "NHẬT KÝ SỰ KIỆN CẢNH BÁO",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = IsaTextSecondary,
                Dock = DockStyle.Top,
                Height = 24
            };

            dgvAlarmEvents = CreateStyledGrid();
            dgvAlarmEvents.Columns.Add("Time", "Thời gian xảy ra");
            dgvAlarmEvents.Columns.Add("Type", "Loại sự kiện");
            dgvAlarmEvents.Columns.Add("Detail", "Chi tiết thông số");
            dgvAlarmEvents.Columns.Add("AlarmState", "Trạng thái cảnh báo");

            pnlAlarmEventSection.Controls.Add(dgvAlarmEvents);
            pnlAlarmEventSection.Controls.Add(lblAlarmTableTitle);

            dgvDataLog = CreateStyledGrid();
            dgvDataLog.Columns.Add("Time", "Thời gian thực");
            dgvDataLog.Columns.Add("U", "Điện áp (V)");
            dgvDataLog.Columns.Add("I", "Dòng điện (A)");
            dgvDataLog.Columns.Add("P", "Công suất (W)");
            dgvDataLog.Columns.Add("Kwh", "Điện năng (kWh)");
            dgvDataLog.Columns.Add("Status", "Trạng thái vận hành");

            page.Controls.Add(dgvDataLog);
            page.Controls.Add(pnlAlarmEventSection);
            page.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 10, BackColor = IsaBgSurface });
            page.Controls.Add(pnlTopSummary);
            return page;
        }

        private DataGridView CreateStyledGrid()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = IsaBgSurface,
                BorderStyle = BorderStyle.FixedSingle,
                AllowUserToAddRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EnableHeadersVisualStyles = false,
                GridColor = Color.FromArgb(226, 232, 240),
                RowTemplate = { Height = 28 }
            };
            dgv.ColumnHeadersHeight = 34;
            dgv.ColumnHeadersDefaultCellStyle.BackColor = IsaBgCanvas;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = IsaTextPrimary;
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.DefaultCellStyle.BackColor = IsaBgSurface;
            dgv.DefaultCellStyle.ForeColor = IsaTextPrimary;
            dgv.DefaultCellStyle.SelectionBackColor = IsaBgSubtle;
            dgv.DefaultCellStyle.SelectionForeColor = IsaTextPrimary;
            return dgv;
        }

        private void SwitchTab(int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex >= _tabPages.Length) return;
            for (int i = 0; i < _tabPages.Length; i++)
            {
                _tabPages[i].Visible = (i == selectedIndex);
                _tabButtons[i].BackColor = (i == selectedIndex) ? IsaTabActive : IsaBgSurface;
                _tabButtons[i].Font = new Font("Segoe UI", 10F, (i == selectedIndex) ? FontStyle.Bold : FontStyle.Regular);
            }
        }

        // =========================================================================
        // 6. LOGIC BÁO ĐỘNG 3 ĐÈN HÌNH CHỮ NHẬT & ÂM THANH CHUẨN ISA-18.2
        // =========================================================================
        private void InitializeLogic()
        {
            _serialPort = new SerialPort { NewLine = "\n", Encoding = Encoding.ASCII };
            _serialPort.DataReceived += SerialPort_DataReceived;

            ScanComPorts();

            _clockTimer = new Timer { Interval = 1000 };
            _clockTimer.Tick += (s, e) =>
            {
                lblClock.Text = DateTime.Now.ToString("HH:mm:ss  dd/MM/yyyy");
                if (_isConnected && (DateTime.Now - _lastDataTime).TotalSeconds > 3.5)
                {
                    RaiseIsaAlarmEvent("MẤT TÍN HIỆU UART", "Quá 3.5 giây không nhận được dữ liệu đo");
                }
            };
            _clockTimer.Start();

            _alarmBlinkTimer = new Timer { Interval = 500 };
            _alarmBlinkTimer.Tick += AlarmBlinkTimer_Tick;
            _alarmBlinkTimer.Start();
        }

        private void AlarmBlinkTimer_Tick(object sender, EventArgs e)
        {
            _blinkToggle = !_blinkToggle;
            UpdateIsaAlarmLampsVisual();

            if (_alarmState == IsaAlarmState.UnackActive && _blinkToggle)
            {
                Task.Run(() =>
                {
                    try { Console.Beep(2200, 150); } catch { }
                });
            }
        }

        private void RaiseIsaAlarmEvent(string alarmType, string detail)
        {
            _activeAlarmMessage = $"{alarmType}: {detail}";

            // Chuẩn ISA-18.2: Giữ nguyên đèn vàng nếu cùng loại sự cố đã được xác nhận (AckedActive)
            if (_alarmState == IsaAlarmState.AckedActive && _activeAlarmType == alarmType)
            {
                UpdateIsaAlarmLampsVisual();
                return;
            }

            if (_alarmState != IsaAlarmState.UnackActive || _activeAlarmType != alarmType)
            {
                _activeAlarmType = alarmType;
                _alarmState = IsaAlarmState.UnackActive;
                LogAlarmEventRow(alarmType, detail, "UNACK ALARM (Chưa xác nhận)");
                UpdateIsaAlarmLampsVisual();
            }
        }

        private void EvaluateNormalRecovery(bool currentlyFaulted)
        {
            if (!currentlyFaulted)
            {
                if (_alarmState == IsaAlarmState.AckedActive)
                {
                    _alarmState = IsaAlarmState.Normal;
                    _activeAlarmType = "";
                    _activeAlarmMessage = "Hệ thống vận hành trong giới hạn an toàn.";
                    LogAlarmEventRow("PHỤC HỒI (CLEAR)", "Thông số đã trở về ngưỡng an toàn", "NORMAL (Bình thường)");
                    UpdateIsaAlarmLampsVisual();
                }
                else if (_alarmState == IsaAlarmState.UnackActive)
                {
                    _alarmState = IsaAlarmState.UnackCleared;
                    UpdateIsaAlarmLampsVisual();
                }
            }
        }

        private void BtnAckAlarm_Click(object sender, EventArgs e)
        {
            if (_alarmState == IsaAlarmState.UnackActive)
            {
                _alarmState = IsaAlarmState.AckedActive;
                LogAlarmEventRow("XÁC NHẬN (ACK)", _activeAlarmMessage, "ACKED ACTIVE (Đèn vàng sáng)");
            }
            else if (_alarmState == IsaAlarmState.UnackCleared)
            {
                _alarmState = IsaAlarmState.Normal;
                _activeAlarmType = "";
                _activeAlarmMessage = "Hệ thống vận hành trong giới hạn an toàn.";
                LogAlarmEventRow("XÁC NHẬN & HOÀN TẤT", "Người vận hành đã xác nhận sự cố đã qua", "NORMAL (Bình thường)");
            }
            UpdateIsaAlarmLampsVisual();
        }

        private void UpdateIsaAlarmLampsVisual()
        {
            Color offBg = Color.FromArgb(230, 235, 240);
            Color offText = Color.FromArgb(160, 174, 192);

            lampGreen.BackColor = offBg; lblLampGreen.ForeColor = offText;
            lampYellow.BackColor = offBg; lblLampYellow.ForeColor = offText;
            lampRed.BackColor = offBg; lblLampRed.ForeColor = offText;

            switch (_alarmState)
            {
                case IsaAlarmState.Normal:
                    lampGreen.BackColor = IsaNormalGreen;
                    lblLampGreen.ForeColor = Color.White;
                    lblAlarmBannerText.Text = "Trạng thái: BÌNH THƯỜNG — Các thông số nằm trong dải vận hành chuẩn.";
                    lblAlarmBannerText.ForeColor = IsaTextSecondary;
                    btnAckTop.BackColor = IsaButtonNeutral;
                    break;

                case IsaAlarmState.UnackActive:
                    if (_blinkToggle)
                    {
                        lampRed.BackColor = IsaAlarmRed;
                        lblLampRed.ForeColor = Color.White;
                    }
                    lblAlarmBannerText.Text = $"⚠ BÁO ĐỘNG CHƯA XÁC NHẬN: {_activeAlarmMessage}";
                    lblAlarmBannerText.ForeColor = IsaAlarmRed;
                    btnAckTop.BackColor = IsaAlarmRed;
                    break;

                case IsaAlarmState.AckedActive:
                    lampYellow.BackColor = IsaAckedAmber;
                    lblLampYellow.ForeColor = Color.White;
                    lblAlarmBannerText.Text = $"● ĐÃ XÁC NHẬN (CÒN NGOÀI NGƯỠNG): {_activeAlarmMessage}";
                    lblAlarmBannerText.ForeColor = IsaAckedAmber;
                    btnAckTop.BackColor = IsaButtonNeutral;
                    break;

                case IsaAlarmState.UnackCleared:
                    lampGreen.BackColor = IsaNormalGreen;
                    lblLampGreen.ForeColor = Color.White;
                    if (_blinkToggle)
                    {
                        lampYellow.BackColor = IsaAckedAmber;
                        lblLampYellow.ForeColor = Color.White;
                    }
                    lblAlarmBannerText.Text = $"🔔 Thông số đã tự phục hồi ({_activeAlarmMessage}) — Nhấn XÁC NHẬN CẢNH BÁO (ACK).";
                    lblAlarmBannerText.ForeColor = IsaAckedAmber;
                    btnAckTop.BackColor = IsaTrendLine;
                    break;
            }
        }

        private void LogAlarmEventRow(string type, string detail, string alarmStatus)
        {
            if (dgvAlarmEvents == null) return;
            int idx = dgvAlarmEvents.Rows.Add(DateTime.Now.ToString("HH:mm:ss.fff"), type, detail, alarmStatus);
            if (alarmStatus.Contains("UNACK"))
            {
                dgvAlarmEvents.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(254, 226, 226);
                dgvAlarmEvents.Rows[idx].DefaultCellStyle.ForeColor = IsaAlarmRed;
            }
            else if (alarmStatus.Contains("ACKED"))
            {
                dgvAlarmEvents.Rows[idx].DefaultCellStyle.BackColor = Color.FromArgb(254, 243, 199);
                dgvAlarmEvents.Rows[idx].DefaultCellStyle.ForeColor = IsaAckedAmber;
            }
            if (dgvAlarmEvents.Rows.Count > 0)
                dgvAlarmEvents.FirstDisplayedScrollingRowIndex = dgvAlarmEvents.Rows.Count - 1;
        }

        // =========================================================================
        // 7. XỬ LÝ KẾT NỐI UART (CHỈ NHẬN 1 CHIỀU) & DỮ LIỆU ĐO LƯỜNG
        // =========================================================================
        private void ScanComPorts()
        {
            string prev = cbxPorts.SelectedItem?.ToString();
            cbxPorts.Items.Clear();
            string[] ports = SerialPort.GetPortNames();
            cbxPorts.Items.AddRange(ports);
            if (ports.Length > 0)
                cbxPorts.SelectedIndex = (prev != null && cbxPorts.Items.Contains(prev)) ? cbxPorts.Items.IndexOf(prev) : 0;
        }

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (!_isConnected)
            {
                if (cbxPorts.SelectedItem == null)
                {
                    MessageBox.Show("Chưa tìm thấy cổng COM! Vui lòng cắm cáp USB-UART và nhấn nút 'Quét'.", "Thiếu cổng COM", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    _serialPort.PortName = cbxPorts.SelectedItem.ToString();
                    _serialPort.BaudRate = int.Parse(cbxBaud.SelectedItem.ToString());
                    _serialPort.DataBits = int.Parse(cbxDataBits.SelectedItem.ToString());
                    _serialPort.Parity = (Parity)Enum.Parse(typeof(Parity), cbxParity.SelectedItem.ToString());
                    _serialPort.StopBits = (StopBits)Enum.Parse(typeof(StopBits), cbxStopBits.SelectedItem.ToString());

                    _serialPort.Open();
                    _isConnected = true;
                    _lastDataTime = DateTime.Now;
                    UpdateConnectionState(true, $"● Đã kết nối {_serialPort.PortName} ({_serialPort.BaudRate} bps)");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Không thể mở cổng {cbxPorts.SelectedItem}.\nChi tiết: {ex.Message}", "Lỗi kết nối UART", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                DisconnectSerial();
            }
        }

        private void DisconnectSerial()
        {
            try { if (_serialPort.IsOpen) _serialPort.Close(); } catch { }
            _isConnected = false;
            UpdateConnectionState(false, "● Chưa kết nối cổng COM");
        }

        private void UpdateConnectionState(bool active, string headerText)
        {
            lblHeaderStatus.Text = headerText;
            lblHeaderStatus.ForeColor = active ? IsaNormalGreen : IsaTextSecondary;

            // Chuẩn ISA-101: Không dùng màu đỏ báo động cho nút thao tác ngắt kết nối thông thường
            btnConnect.Text = active ? "NGẮT KẾT NỐI UART" : "KẾT NỐI UART";
            btnConnect.BackColor = active ? IsaButtonConnected : IsaButtonNeutral;

            lblSystemHealthBadge.Text = active ? "TRẠNG THÁI: ĐANG NHẬN DỮ LIỆU ĐO" : "TRẠNG THÁI: ĐANG CHỜ KẾT NỐI";
            lblSystemHealthBadge.ForeColor = active ? IsaNormalGreen : IsaTextSecondary;

            cbxPorts.Enabled = !active;
            btnRefreshPort.Enabled = !active;
            cbxBaud.Enabled = !active;
            cbxParity.Enabled = !active;
            cbxDataBits.Enabled = !active;
            cbxStopBits.Enabled = !active;
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string line = _serialPort.ReadLine().Trim();
                if (!string.IsNullOrEmpty(line))
                    this.BeginInvoke(new Action(() => ParseIncomingData(line)));
            }
            catch { }
        }

        private void ParseIncomingData(string rawLine)
        {
            string clean = rawLine.StartsWith("DATA,", StringComparison.OrdinalIgnoreCase) ? rawLine.Substring(5) : rawLine;
            string[] parts = clean.Split(',');
            if (parts.Length < 5) return;

            if (double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double u) &&
                double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out double i) &&
                double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double p) &&
                double.TryParse(parts[4], NumberStyles.Any, CultureInfo.InvariantCulture, out double kwh))
            {
                _lastDataTime = DateTime.Now;
                ProcessAndRenderMetrics(u, i, p, kwh);
            }
        }

        private void ProcessAndRenderMetrics(double u, double i, double p, double kwh)
        {
            _hasLiveData = true;
            _lastU = u;
            _lastI = i;
            _lastP = p;
            _valKwh = kwh;
            _sampleIndex++;

            // 1. Cập nhật đồng thời cả 4 thẻ thông số trên Tab 1
            _metricValueLabels[0].Text = $"{u:F1} V";
            _metricValueLabels[1].Text = $"{i:F2} A";
            _metricValueLabels[2].Text = $"{p:F1} W";
            _metricValueLabels[3].Text = $"{kwh:F3} kWh";

            // 2. Kiểm tra ngưỡng cảnh báo cục bộ trên PC
            bool faulted = EvaluateThresholds(u, i, p, kwh, out string status);

            // 3. Cập nhật đồng thời cả 4 đồ thị U, I, P, kWh
            string timeLabel = DateTime.Now.ToString("HH:mm:ss");
            AppendChartPoint(chartU, timeLabel, u);
            AppendChartPoint(chartI, timeLabel, i);
            AppendChartPoint(chartP, timeLabel, p);
            AppendChartPoint(chartKwh, timeLabel, kwh);

            // 4. Cập nhật Tab 3 (Nhật ký & Lưu trữ dữ liệu)
            lblSummaryKwh.Text = $"ĐIỆN NĂNG TÍCH LŨY: {kwh:F3} kWh   |   Tần số: {(u > 50 ? 50 : 0)} Hz";

            int rowIdx = dgvDataLog.Rows.Add(timeLabel, u.ToString("F1"), i.ToString("F2"), p.ToString("F1"), kwh.ToString("F3"), status);
            if (faulted)
            {
                dgvDataLog.Rows[rowIdx].DefaultCellStyle.BackColor = Color.FromArgb(254, 226, 226);
                dgvDataLog.Rows[rowIdx].DefaultCellStyle.ForeColor = IsaAlarmRed;
            }
            if (dgvDataLog.Rows.Count > 200) dgvDataLog.Rows.RemoveAt(0);
            if (dgvDataLog.Rows.Count > 0) dgvDataLog.FirstDisplayedScrollingRowIndex = dgvDataLog.Rows.Count - 1;
        }

        private bool EvaluateThresholds(double u, double i, double p, double kwh, out string status)
        {
            status = "Bình thường";
            bool faulted = false;
            for (int k = 0; k < 4; k++) _metricFaulted[k] = false;

            if (u > _uMax)
            {
                status = $"Quá áp ({u:F1}V > {_uMax}V)";
                faulted = true;
                _metricFaulted[0] = true;
                RaiseIsaAlarmEvent("QUÁ ÁP (HIGH VOLTAGE)", $"U = {u:F1} V > {_uMax} V");
            }
            else if (u > 10 && u < _uMin)
            {
                status = $"Thấp áp ({u:F1}V < {_uMin}V)";
                faulted = true;
                _metricFaulted[0] = true;
                RaiseIsaAlarmEvent("THẤP ÁP (LOW VOLTAGE)", $"U = {u:F1} V < {_uMin} V");
            }

            if (i > _iMax)
            {
                status = $"Quá dòng ({i:F2}A > {_iMax}A)";
                faulted = true;
                _metricFaulted[1] = true;
                RaiseIsaAlarmEvent("QUÁ DÒNG (HIGH CURRENT)", $"I = {i:F2} A > {_iMax} A");
            }

            if (p > _pMax)
            {
                status = $"Quá công suất ({p:F1}W > {_pMax}W)";
                faulted = true;
                _metricFaulted[2] = true;
                RaiseIsaAlarmEvent("QUÁ CÔNG SUẤT (HIGH POWER)", $"P = {p:F1} W > {_pMax} W");
            }

            if (kwh > _kwhMax)
            {
                status = $"Vượt điện năng ({kwh:F3}kWh > {_kwhMax}kWh)";
                faulted = true;
                _metricFaulted[3] = true;
                RaiseIsaAlarmEvent("VƯỢT NGƯỠNG ĐIỆN NĂNG", $"E = {kwh:F3} kWh > {_kwhMax} kWh");
            }

            RefreshMetricCardStyles();
            EvaluateNormalRecovery(faulted);
            return faulted;
        }

        private void BtnApplyThresholds_Click(object sender, EventArgs e)
        {
            double newUmin = (double)numUmin.Value;
            double newUmax = (double)numUmax.Value;
            if (newUmin >= newUmax)
            {
                MessageBox.Show("Ngưỡng thấp áp (Umin) phải nhỏ hơn ngưỡng quá áp (Umax).", "Ngưỡng không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _uMin = newUmin;
            _uMax = newUmax;
            _iMax = (double)numImax.Value;
            _pMax = (double)numPmax.Value;
            _kwhMax = (double)numKwhMax.Value;

            UpdateThresholdStripLines();
            UpdateThresholdLabels();
            if (_hasLiveData)
            {
                EvaluateThresholds(_lastU, _lastI, _lastP, _valKwh, out _);
            }

            MessageBox.Show($"Đã cập nhật ngưỡng kiểm tra trên phần mềm:\n\n• Dải điện áp chuẩn: {_uMin} V – {_uMax} V\n• Ngưỡng dòng điện (Imax): {_iMax} A\n• Ngưỡng công suất (Pmax): {_pMax} W\n• Ngưỡng điện năng (Emax): {_kwhMax} kWh", "Cập nhật ngưỡng thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnExportCsv_Click(object sender, EventArgs e)
        {
            if (dgvDataLog.Rows.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu trong bảng Nhật ký để xuất file!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var sfd = new SaveFileDialog { Filter = "CSV File (*.csv)|*.csv", FileName = $"BaoCao_DienNang_{DateTime.Now:yyyyMMdd_HHmm}.csv" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    using (var sw = new StreamWriter(sfd.FileName, false, new UTF8Encoding(true)))
                    {
                        sw.WriteLine("Thoi Gian,Dien Ap (V),Dong Dien (A),Cong Suat (W),Dien Nang (kWh),Trang Thai");
                        foreach (DataGridViewRow r in dgvDataLog.Rows)
                            sw.WriteLine($"{r.Cells[0].Value},{r.Cells[1].Value},{r.Cells[2].Value},{r.Cells[3].Value},{r.Cells[4].Value},\"{r.Cells[5].Value}\"");
                    }
                    MessageBox.Show("Đã xuất file báo cáo CSV thành công!", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        // =========================================================================
        // 8. CÁC HÀM VẼ VIỀN & TẠO CONTROL CHUẨN ISA-101
        // =========================================================================
        private void DrawBottomBorder(object sender, PaintEventArgs e)
        {
            var c = (Control)sender;
            using (var pen = new Pen(IsaBorder, 1))
                e.Graphics.DrawLine(pen, 0, c.Height - 1, c.Width, c.Height - 1);
        }

        private void DrawTopBorder(object sender, PaintEventArgs e)
        {
            var c = (Control)sender;
            using (var pen = new Pen(IsaBorder, 1))
                e.Graphics.DrawLine(pen, 0, 0, c.Width, 0);
        }

        private void DrawRightBorder(object sender, PaintEventArgs e)
        {
            var c = (Control)sender;
            using (var pen = new Pen(IsaBorder, 1))
                e.Graphics.DrawLine(pen, c.Width - 1, 0, c.Width - 1, c.Height);
        }

        private Label CreateFieldLabel(string text, Point loc) =>
            new Label { Text = text, ForeColor = IsaTextPrimary, Font = new Font("Segoe UI", 9.5F), AutoSize = true, Location = loc };

        private ComboBox CreateNeutralCombo(string[] items, Point loc, int width)
        {
            var cbx = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = IsaBgSurface,
                ForeColor = IsaTextPrimary,
                FlatStyle = FlatStyle.Standard,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                Size = new Size(width, 28),
                Location = loc
            };
            cbx.Items.AddRange(items);
            return cbx;
        }

        private Button CreateSolidButton(string text, Color bg, Point loc, Size size)
        {
            var btn = new Button
            {
                Text = text,
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.2F, FontStyle.Bold),
                Location = loc,
                Size = size,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private Button CreateOutlineButton(string text, Point loc, Size size)
        {
            var btn = new Button
            {
                Text = text,
                BackColor = IsaBgSurface,
                ForeColor = IsaTextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Location = loc,
                Size = size,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = IsaBorder;
            btn.FlatAppearance.BorderSize = 1;
            return btn;
        }

        private NumericUpDown CreateNeutralNumeric(decimal min, decimal max, decimal val, int decimals, Point loc)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = val,
                DecimalPlaces = decimals,
                Increment = decimals == 0 ? 1m : 0.5m,
                BackColor = IsaBgSurface,
                ForeColor = IsaTextPrimary,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Size = new Size(96, 28),
                Location = loc,
                TextAlign = HorizontalAlignment.Center
            };
        }
    }
}
