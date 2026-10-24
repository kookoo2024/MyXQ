using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using Yolov5Net;
using Yolov5Net.Scorer;
using Yolov5Net.Scorer.Models;
using System.Drawing.Drawing2D;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;


namespace VinXiangQi
{
    public partial class Mainform : Form
    {
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        // === Layered Window (per-pixel alpha) 相关 P/Invoke ===
        // 用于浮动分数窗的真半透明：离屏绘制 32bpp ARGB 位图后用 UpdateLayeredWindow 推送，
        // 使窗体可透出背后的游戏/桌面画面。普通 TransparencyKey 只支持单色 key 抠图，
        // 无法做半透明渐变和抗锯齿圆角，故必须走 layered window。
        [DllImport("user32.dll")]
        static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte alpha, uint dwFlags);
        [DllImport("user32.dll")]
        static extern bool UpdateLayeredWindow(IntPtr hWnd, IntPtr hdcDst, ref Point pptDst, ref Size psize,
            IntPtr hdcSrc, ref Point pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("gdi32.dll")]
        static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);
        [DllImport("gdi32.dll")]
        static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")]
        static extern bool DeleteDC(IntPtr hdc);
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x00080000;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const uint ULW_ALPHA = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        struct BLENDFUNCTION
        {
            public byte BlendOp;      // AC_SRC_OVER = 0
            public byte BlendFlags;   // 0
            public byte SourceConstant; // 整体不透明度（与位图 alpha 相乘）
            public byte AlphaFormat;  // AC_SRC_ALPHA = 1
        }
        const byte AC_SRC_OVER = 0;
        const byte AC_SRC_ALPHA = 1;

        private bool _isLargeSize = false;
        private const int SmallW = 225, SmallH = 208;
        private const int LargeW = 450, LargeH = 416;
        public Mainform()
        {
            InitializeComponent();
        }

        // 程序版本：统一从程序集版本读取（AssemblyInfo.cs），避免多处硬编码不一致
        // 显示时取 Major.Minor.Build 三段，与历史格式 "1.0.0" 一致
        public static string Version
        {
            get
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return v.Major + "." + v.Minor + "." + v.Build;
            }
        }

        // 用于截图的窗口句柄
        public static IntPtr GameHandle = IntPtr.Zero;
        // 用于点击的窗口句柄
        public static IntPtr ClickHandle = IntPtr.Zero;
        // 棋盘识别模型
        public static Dictionary<string, YoloScorer<YoloXiangQiModel>> ModelList = new Dictionary<string, YoloScorer<YoloXiangQiModel>>();
        public static YoloScorer<YoloXiangQiModel> Model;
        // UCI引擎封装
        public static EngineHelper Engine;
        // 开局库
        public static Dictionary<string, OpenBookHelper.OpenBook> OpenBookList = new Dictionary<string, OpenBookHelper.OpenBook>();
        // 用于储存方案供选择
        public static Dictionary<string, Solution> SolutionList = new Dictionary<string, Solution>();
        // 当前选择的方案
        public static Solution CurrentSolution;
        // 用于存放每个棋盘上的点对应图片上点击坐标
        public static Point[,] ClickPositionMap = new Point[9, 10];
        // 当GameHandle和ClickHandle不是同一个时，获取他们尺寸的插值作为图片上坐标和点击坐标转换的偏移量
        public static Size ClickOffset = new Size(0, 0);
        // 用于截图和识别的主线程
        Thread DetectionThread;
        // 用于自动点击的线程
        Thread AutoClickThread;
        // 用于在原始截图上画出检测框和分类
        Bitmap YoloDisplayBitmap;
        Graphics YoloGDI;
        // 用于绘制示意棋盘
        Bitmap BoardDisplayBitmap = new Bitmap(400, 440);
        Graphics BoardGDI;
        // 用于在示意棋盘上显示箭头
        ChessMove BestMove = new ChessMove(new Point(-1, -1), new Point(-1, -1));
        ChessMove PonderMove = new ChessMove(new Point(-1, -1), new Point(-1, -1));
        public bool ShowTwoSteps = false;
        ChessMove Move3 = new ChessMove(new Point(-1, -1), new Point(-1, -1));
        ChessMove Move4 = new ChessMove(new Point(-1, -1), new Point(-1, -1));
        ChessMove BackgroundAnalysisMove = new ChessMove(new Point(-1, -1), new Point(-1, -1));
        string[] CandidateMoves = new string[3] { "", "", "" };
        int[] CandidateScores = new int[3] { 0, 0, 0 };
        // Flag 用于在程序退出时示意其他子线程退出
        public static bool Running = true;
        // 用于判断引擎当前是否存在别的分析任务，是否需要给引擎发送stop，是否需要跳过发送stop产生的bestmove
        public static int EngineAnalyzeCount = 0;
        // 全局设置
        public static ProgramSettings Settings = new ProgramSettings();
        public static string SettingsPath = @".\settings.json";
        public static string MODEL_FOLDER = @".\Models\";
        public static string OPENBOOK_FOLDER = @".\OpenBooks\";
        public static string SOLUTION_FOLDER = @".\Solutions\";
        public static string ENGINE_FOLDER = @".\Engines\";
        // 是否开始连线
        public static bool DetectEnabled = false;
        // 自动点击的图片(按语义角色分类)
        // 旧的单列表保留为向后兼容：若方案目录下只有平级 AutoClick 图而无 Close/Confirm
        // 子目录，则全部当作 Close 类加载。AutoClickLoop 优先用 Close/Confirm 两列表。
        public static List<Bitmap> AutoClickImageList = new List<Bitmap>();
        // 关闭类：红叉/"不需要"/"取消"等，点了让上层弹窗消失。处理时最高优先级。
        public static List<Bitmap> AutoClickCloseList = new List<Bitmap>();
        // 确认类："再来一局"/"开始下一盘"等，点了开始新局。仅在无关闭类弹窗时处理。
        public static List<Bitmap> AutoClickConfirmList = new List<Bitmap>();
        public static Random Rand = new Random();

        public class Solution
        {
            public string ScreenshotCaption;
            public string ScreenshotClass;
            public string ClickCaption;
            public string ClickClass;
            public Solution(string ScreenshotCaption, string ScreenshotClass, string ClickCaption, string ClickClass)
            {
                this.ScreenshotCaption = ScreenshotCaption;
                this.ScreenshotClass = ScreenshotClass;
                this.ClickCaption = ClickCaption;
                this.ClickClass = ClickClass;
            }
        }

        public struct ChessMove
        {
            public ChessMove(Point from, Point to)
            {
                this.From = from;
                this.To = to;
            }
            public Point From;
            public Point To;
        }

        private Form _floatingScoreForm;
        private System.Windows.Forms.Timer _floatingTopmostTimer;
        private int _floatingClickCount;
        private int _floatingClickZone;
        private System.Windows.Forms.Timer _floatingClickTimer;

        // === 浮动分数窗（半透明白胶囊，真 per-pixel alpha）===
        // 用 Layered Window (UpdateLayeredWindow + 32bpp ARGB) 实现：窗体可透出背后
        // 游戏/桌面画面，半透明白底在任何背景上都可见，圆角抗锯齿干净。
        // 内容变化（分数/深度/hover）时离屏重绘 bitmap 再整体推送，不走 WM_PAINT。
        const int FloatW = 104, FloatH = 52;
        const int FloatRadius = 12;
        // 半透明白胶囊：底色白 alpha≈210（透出约 18% 背景画面，又不至于让文字糊），
        // 描边深灰，辅助文字中灰。文字用深色，在浅白底上清晰。
        const int FloatBgAlpha = 210;
        static readonly Color FloatBg = Color.FromArgb(248, 250, 252);
        static readonly Color FloatBorder = Color.FromArgb(210, 100, 116, 139);
        static readonly Color FloatDim = Color.FromArgb(180, 71, 85, 105);
        static readonly Color FloatHover = Color.FromArgb(60, 59, 130, 246); // hover 蓝色低透
        // 分数配色（浅白底上）：正分深红、负分深蓝、绝杀深绿，都高对比可读
        static readonly Color FloatScorePositive = Color.FromArgb(220, 38, 38);
        static readonly Color FloatScoreNegative = Color.FromArgb(29, 78, 216);
        static readonly Color FloatScoreMate = Color.FromArgb(21, 128, 61);

        // === 主窗木质棋盘风配色（ApplyWoodTheme + Label 动态配色共用）===
        // 与浮动窗配色并列集中在此，便于统一调色。board.png 已是木质纹理，
        // 窗体/按钮/标题栏用木色系与之呼应，形成统一质感。
        static readonly Color WoodBg        = Color.FromArgb(107, 68, 35);   // 深棕窗体底
        static readonly Color WoodPanel     = Color.FromArgb(139, 94, 60);   // 中棕右侧工具栏
        static readonly Color WoodBtnFace   = Color.FromArgb(212, 165, 116); // 浅木金按钮底
        static readonly Color WoodBtnHover  = Color.FromArgb(224, 181, 130); // hover 更亮金
        static readonly Color WoodBtnText   = Color.FromArgb(58, 36, 18);    // 深褐按钮字
        static readonly Color WoodBorder    = Color.FromArgb(90, 56, 32);    // 深褐描边/分隔
        static readonly Color WoodTitle     = Color.FromArgb(245, 230, 200); // 米白标题字
        static readonly Color WoodDimText   = Color.FromArgb(201, 169, 110); // 暗金次要字(着法)
        static readonly Color WoodAccentRed = Color.FromArgb(192, 57, 43);   // 沉稳深红(GO按钮底/关闭hover)
        // 分数专用亮朱红：沉稳红#c0392b 在中棕面板(#8b5e3c)上明度太接近、看不清。
        // 提亮到#ff5252，与棕底明度差拉大，正分清晰可读。负分用深蓝灰对比已足够。
        static readonly Color WoodScoreRed  = Color.FromArgb(255, 82, 82);   // 亮朱红(正分专用)
        static readonly Color WoodAccentBlk = Color.FromArgb(44, 62, 80);    // 深蓝灰(负分)

        int _floatHoverZone = -1;
        string _floatScoreText = "--";
        Color _floatScoreColor = Color.FromArgb(220, 38, 38);
        string _floatDepthText = "";

        static System.Drawing.Drawing2D.GraphicsPath BuildRoundedRectPath(Rectangle r, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            if (d >= r.Width) d = r.Width;
            if (d >= r.Height) d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public void InitFloatingScore()
        {
            if (!Settings.ShowFloatingScore) return;
            if (_floatingScoreForm != null && !_floatingScoreForm.IsDisposed) return;

            _floatingScoreForm = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(FloatW, FloatH),
                TopMost = true,
                ShowInTaskbar = false,
                // 背景色对 layered window 不影响显示（由位图决定），设为透明便于调试
                BackColor = Color.Fuchsia
            };
            _floatingScoreForm.MouseDown += FloatingScore_MouseDown;
            _floatingScoreForm.MouseMove += FloatForm_MouseMove;
            _floatingScoreForm.MouseLeave += FloatForm_MouseLeave;
            // layered window 的位图通过 ptDst 绑定屏幕坐标，拖动时需重绘跟随新位置
            _floatingScoreForm.LocationChanged += (s, e) => UpdateFloatLayered();

            if (Settings.FloatingScoreX >= 0 && Settings.FloatingScoreY >= 0)
                _floatingScoreForm.Location = new Point(Settings.FloatingScoreX, Settings.FloatingScoreY);
            else
                _floatingScoreForm.Shown += (s, e) => { _floatingScoreForm.Location = new Point(this.Right + 4, this.Top); };

            _floatingScoreForm.Show(this);

            // 设置 WS_EX_LAYERED，使窗体支持 per-pixel alpha。
            // 必须在 Show 之后、第一次 UpdateLayeredWindow 之前设置。
            EnableLayeredWindow(_floatingScoreForm.Handle);
            UpdateFloatLayered();
            PinFloatingTopmost();

            if (_floatingTopmostTimer == null)
            {
                _floatingTopmostTimer = new System.Windows.Forms.Timer();
                _floatingTopmostTimer.Interval = 2000;
                _floatingTopmostTimer.Tick += (s, e) => PinFloatingTopmost();
            }
            _floatingTopmostTimer.Start();
        }

        static void EnableLayeredWindow(IntPtr hwnd)
        {
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED);
        }

        static int FloatZoneOfX(int x, int width)
        {
            return x < width / 3 ? 0 : x < width * 2 / 3 ? 1 : 2;
        }

        void FloatForm_MouseMove(object sender, MouseEventArgs e)
        {
            int zone = FloatZoneOfX(e.X, FloatW);
            if (zone != _floatHoverZone)
            {
                _floatHoverZone = zone;
                UpdateFloatLayered();
            }
            if (_floatingScoreForm != null)
                _floatingScoreForm.Cursor = Cursors.SizeAll;
        }

        void FloatForm_MouseLeave(object sender, EventArgs e)
        {
            if (_floatHoverZone != -1)
            {
                _floatHoverZone = -1;
                UpdateFloatLayered();
            }
        }

        // 离屏绘制浮动窗内容到 32bpp ARGB 位图
        void RenderFloatToBitmap(Bitmap bmp)
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                Rectangle bounds = new Rectangle(0, 0, FloatW, FloatH);

                // 圆角路径作为填充与裁剪区，保证圆角外为完全透明（alpha=0）
                using (var path = BuildRoundedRectPath(bounds, FloatRadius))
                {
                    g.SetClip(path);
                    // 半透明白底
                    using (var bgBrush = new SolidBrush(Color.FromArgb(FloatBgAlpha, FloatBg)))
                        g.FillRectangle(bgBrush, bounds);

                    // hover 分区高亮
                    if (_floatHoverZone >= 0)
                    {
                        int zoneW = FloatW / 3;
                        Rectangle hoverRect = new Rectangle(_floatHoverZone * zoneW, 0, zoneW, FloatH);
                        using (var hoverBrush = new SolidBrush(FloatHover))
                            g.FillRectangle(hoverBrush, hoverRect);
                    }
                    g.ResetClip();

                    // 圆角描边
                    using (var borderPen = new Pen(FloatBorder, 1.4f))
                        g.DrawPath(borderPen, path);
                }

                // 分数大字（居中偏上）
                using (var scoreFont = new Font("Consolas", 18F, FontStyle.Bold))
                using (var scoreBrush = new SolidBrush(_floatScoreColor))
                {
                    SizeF sz = g.MeasureString(_floatScoreText, scoreFont);
                    float sx = (FloatW - sz.Width) / 2f;
                    g.DrawString(_floatScoreText, scoreFont, scoreBrush, sx, 5);
                }

                // 深度小字（右上角）
                if (!string.IsNullOrEmpty(_floatDepthText))
                {
                    using (var depthFont = new Font("Consolas", 8.5F))
                    using (var depthBrush = new SolidBrush(FloatDim))
                    {
                        SizeF dsz = g.MeasureString(_floatDepthText, depthFont);
                        g.DrawString(_floatDepthText, depthFont, depthBrush, FloatW - dsz.Width - 6, 4);
                    }
                }

                // 底部优势条
                if (_floatScoreText != "--")
                {
                    int barY = FloatH - 5;
                    int barH = 3;
                    int midX = FloatW / 2;
                    using (var barBrush = new SolidBrush(_floatScoreColor))
                    {
                        if (_floatScoreColor == FloatScorePositive)
                            g.FillRectangle(barBrush, 10, barY, midX - 10, barH);
                        else if (_floatScoreColor == FloatScoreNegative)
                            g.FillRectangle(barBrush, midX, barY, FloatW - midX - 10, barH);
                    }
                }
            }
        }

        // 把 ARGB 位图通过 UpdateLayeredWindow 推送给 layered window，实现真半透明显示
        void UpdateFloatLayered()
        {
            if (_floatingScoreForm == null || _floatingScoreForm.IsDisposed) return;
            if (!_floatingScoreForm.IsHandleCreated) return;

            IntPtr hwnd = _floatingScoreForm.Handle;
            using (Bitmap bmp = new Bitmap(FloatW, FloatH, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                RenderFloatToBitmap(bmp);
                // 锁定像素获取位图位数据，供 SetBitmap 使用
                var rect = new Rectangle(0, 0, FloatW, FloatH);
                var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, bmp.PixelFormat);
                try
                {
                    IntPtr hBitmap = IntPtr.Zero;
                    IntPtr hOld = IntPtr.Zero;
                    IntPtr hdcScreen = GetDC(IntPtr.Zero);
                    IntPtr hdcMem = CreateCompatibleDC(hdcScreen);
                    try
                    {
                        // 用 GetHbitmap 得到 GDI 位图句柄供 SelectObject
                        hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                        hOld = SelectObject(hdcMem, hBitmap);

                        Point ptDst = new Point(_floatingScoreForm.Location.X, _floatingScoreForm.Location.Y);
                        Size sz = new Size(FloatW, FloatH);
                        Point ptSrc = new Point(0, 0);
                        BLENDFUNCTION blend = new BLENDFUNCTION
                        {
                            BlendOp = AC_SRC_OVER,
                            BlendFlags = 0,
                            SourceConstant = 255,         // 整体不透明度由位图自身 alpha 决定
                            AlphaFormat = AC_SRC_ALPHA
                        };
                        UpdateLayeredWindow(hwnd, hdcScreen, ref ptDst, ref sz,
                            hdcMem, ref ptSrc, 0, ref blend, ULW_ALPHA);
                    }
                    finally
                    {
                        if (hOld != IntPtr.Zero) SelectObject(hdcMem, hOld);
                        if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                        DeleteDC(hdcMem);
                        ReleaseDC(IntPtr.Zero, hdcScreen);
                    }
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }
            }
        }

        private void FloatingScore_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            int zone = FloatZoneOfX(e.X, FloatW);

            _floatingClickCount++;
            if (_floatingClickCount == 1)
            {
                _floatingClickZone = zone;
                if (_floatingClickTimer == null)
                {
                    _floatingClickTimer = new System.Windows.Forms.Timer();
                    _floatingClickTimer.Interval = SystemInformation.DoubleClickTime;
                    _floatingClickTimer.Tick += FloatingClickTimer_Tick;
                }
                _floatingClickTimer.Start();
                ReleaseCapture();
                SendMessage(_floatingScoreForm.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                Settings.FloatingScoreX = _floatingScoreForm.Location.X;
                Settings.FloatingScoreY = _floatingScoreForm.Location.Y;
            }
            else if (_floatingClickCount >= 2 && _floatingClickZone == zone)
            {
                _floatingClickCount = 0;
                if (_floatingClickTimer != null) _floatingClickTimer.Stop();
                switch (zone)
                {
                    case 0: StartDetection(false); break;
                    case 1: if (this.Visible) this.Hide(); else this.Show(); break;
                    case 2: button_stop_detection_Click(null, null); break;
                }
            }
        }

        private void FloatingClickTimer_Tick(object sender, EventArgs e)
        {
            _floatingClickTimer.Stop();
            _floatingClickCount = 0;
        }

        void PinFloatingTopmost()
        {
            if (_floatingScoreForm == null || _floatingScoreForm.IsDisposed) return;
            SetWindowPos(_floatingScoreForm.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public void HideFloatingScore()
        {
            if (_floatingTopmostTimer != null)
            {
                _floatingTopmostTimer.Stop();
            }
            if (_floatingScoreForm != null && !_floatingScoreForm.IsDisposed)
            {
                _floatingScoreForm.Close();
                _floatingScoreForm = null;
            }
            // 重置显示状态，下次开启时回到 "--" 初值
            _floatScoreText = "--";
            _floatScoreColor = Color.Red;
            _floatDepthText = "";
            _floatHoverZone = -1;
        }

        // 主界面是无边框紧凑窗体，没有菜单栏。这里构建一个右键菜单挂到棋盘上，
        // 给"引擎管理/开局库管理/设置/诊断/版本"提供入口(识别开关、截图、导出棋谱用快捷键 F1/F3/F4/F5)，也复活了原先
        // 因菜单栏被移除而失效的 ToolStripMenuItem_*_Click 处理器。
        ContextMenuStrip _boardContextMenu;
        void InitContextMenu()
        {
            _boardContextMenu = new ContextMenuStrip();
            _boardContextMenu.Items.Add("引擎管理", null, (s, e) => ToolStripMenuItem_engine_management_Click(null, null));
            _boardContextMenu.Items.Add("开局库管理", null, (s, e) => ToolStripMenuItem_openbook_management_Click(null, null));
            _boardContextMenu.Items.Add("设置", null, (s, e) => button_settings_Click(null, null));
            _boardContextMenu.Items.Add("-");
            _boardContextMenu.Items.Add("打开日志文件夹", null, (s, e) => OpenLogFolder());
            _boardContextMenu.Items.Add("复制诊断信息", null, (s, e) => CopyDiagnostics());
            _boardContextMenu.Items.Add("检查更新", null, (s, e) => UpdateHelper.CheckForUpdates(this, false));
            _boardContextMenu.Items.Add("版本 v" + Version, null, (s, e) => ToolStripMenuItem_about_Click(null, null));

            pictureBox_board.ContextMenuStrip = _boardContextMenu;
            panel_right.ContextMenuStrip = _boardContextMenu;
            panel_drag_top.ContextMenuStrip = _boardContextMenu;
        }

        void UpdateFloatingScore(string text, Color color, string depth = "")
        {
            _floatScoreText = text;
            _floatScoreColor = color;
            _floatDepthText = depth ?? "";
            // layered window 不响应 Invalidate/WM_PAINT，必须主动重绘位图并推送
            UpdateFloatLayered();
        }

        private void Mainform_Load(object sender, EventArgs e)
        {
            try
            {
                this.Text = $"Vin象棋 {Version}";
                InitFolders();
                LoadSettings();
                EnsureDefaultEngine();
                InitOpenBooks();
                InitYoloModels();
                InitSolutions();
                InitThreads();
                InitResultDisplay();
                InitEngine();
                InitFloatingScore();
                InitContextMenu();
                ApplyWoodTheme();
                Settings.BackgroundAnalysis = false;
                checkBox_auto_go.Checked = Settings.AutoGo;
                checkBox_two_steps.Checked = ShowTwoSteps;
                if (Settings.WindowX >= 0 && Settings.WindowY >= 0)
                {
                    // 坐标合法性检查：换显示器/分辨率后，保存的窗口坐标可能跑到屏幕外，
                    // 导致用户看不到窗口。落在任意一个屏幕工作区内才采用，否则用系统默认居中。
                    Point saved = new Point(Settings.WindowX, Settings.WindowY);
                    bool onScreen = false;
                    foreach (var scr in Screen.AllScreens)
                    {
                        if (scr.WorkingArea.Contains(saved)) { onScreen = true; break; }
                    }
                    if (onScreen)
                    {
                        this.StartPosition = FormStartPosition.Manual;
                        this.Location = saved;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        // 窗体首次显示后兜底重排一次右侧面板。
        // Load 阶段 TableLayoutPanel 的 Fill 子控件 ClientSize 偶有未完成布局的情况，
        // Shown 时布局必然就绪，此时按真实高度重排可避免元素挤在顶部。
        private void Mainform_Shown(object sender, EventArgs e)
        {
            LayoutRightPanel();
            // 启动后静默检查更新：无新版本或网络失败时不打扰
            if (Settings.AutoCheckUpdate)
            {
                UpdateHelper.CheckForUpdates(this, true);
            }
        }

        // 木质棋盘风换肤：集中设置运行时外观属性。
        // FlatAppearance.MouseOverBackColor 等在 Designer 里跨 .NET Framework 版本支持不稳，
        // 统一在代码里设，保证外观一致且可维护。布局/坐标/控件类型一律不动。
        void ApplyWoodTheme()
        {
            // 窗体 + 拖拽条统一深棕底
            this.BackColor = WoodBg;
            panel_drag_top.BackColor = WoodBg;
            panel_drag_bottom.BackColor = WoodBg;
            panel_right.BackColor = WoodPanel;
            // TableLayoutPanel 也设木色：pictureBox_board 背景透明后透出此色，
            // Zoom 模式比例不匹配留出的边不再是白圈，而是与棋盘浑然一体的木色。
            tableLayoutPanel_content.BackColor = WoodBg;

            // 四个按钮统一木金风格(Flat 无立体边框)，GO 用沉稳红强调主操作
            StyleWoodButton(button_stop_detection, false);
            StyleWoodButton(button_settings, false);
            StyleWoodButton(button_size, false);
            StyleWoodButton(button_start, true); // GO 主操作：红底白字

            // 标题栏 Label 常态配色(动态修改在各自事件里)
            label_moves.ForeColor = WoodDimText;
            label_score.ForeColor = WoodScoreRed;
            label_topmost.ForeColor = this.TopMost ? WoodAccentRed : WoodTitle;
            label_close.ForeColor = WoodTitle;
            // 右侧面板上的 CheckBox 文字用米白，在木色底上清晰统一
            checkBox_auto_go.ForeColor = WoodTitle;
            checkBox_two_steps.ForeColor = WoodTitle;

            // 右侧工具栏元素等间距重排。
            // panel_right 可用区 = 36宽 × 178高(Padding 15/3/15/3)。
            // 原坐标是各自硬编码的，间距随意且 GO 与 END 重叠 3px、Checkbox 宽度
            // 超出可用宽被裁切。这里按统一行高+等间距公式重排，宽度统一满宽居中。
            LayoutRightPanel();
        }

        // 右侧工具栏均匀排布。
        // 宽高都动态读取 panel_right 可用区(扣除 Padding)，这样调整 Padding 或
        // 切换大小尺寸时元素都能自动满宽铺开，不会留出难看的空白带。
        // 7 个元素自上而下：自动☑ / 双步☑ / 设置 / 大小 / 分数 / END / GO。
        void LayoutRightPanel()
        {
            // 动态读取可用宽高(扣除 Padding)，与高度同理
            int innerW = panel_right.ClientSize.Width - panel_right.Padding.Left - panel_right.Padding.Right;
            int innerH = panel_right.ClientSize.Height - panel_right.Padding.Top - panel_right.Padding.Bottom;
            if (innerW < 30) innerW = 30;   // 容错：构造期 ClientSize 可能尚未就绪
            if (innerH < 120) innerH = 120;

            // 元素固有高度(自上而下)
            const int hCheck = 20;   // Checkbox 单行
            const int hBtn   = 24;   // 普通按钮
            const int hScore = 22;   // 分数 label
            const int hGo    = 26;   // GO 主操作略高

            int totalH = hCheck * 2 + hBtn * 2 + hScore + hBtn + hGo;
            int gap = (innerH - totalH) / 6; // 7 元素 6 间隙
            if (gap < 2) gap = 2;

            int y = 0;
            PlaceRow(checkBox_auto_go,      ref y, innerW, hCheck, gap);
            PlaceRow(checkBox_two_steps,    ref y, innerW, hCheck, gap);
            PlaceRow(button_settings,       ref y, innerW, hBtn,   gap);
            PlaceRow(button_size,           ref y, innerW, hBtn,   gap);
            PlaceRow(label_score,           ref y, innerW, hScore, gap);
            PlaceRow(button_stop_detection, ref y, innerW, hBtn,   gap);
            PlaceRow(button_start,          ref y, innerW, hGo,    gap);
        }

        // 把控件放到当前行：X=0(满宽居中)、Y=当前行起，并把 y 推进到下一行(高+gap)。
        // 用 ref y 实现链式累加，避免散落的魔法数字。
        static void PlaceRow(Control c, ref int y, int w, int h, int gap)
        {
            c.Location = new Point(0, y);
            c.Size = new Size(w, h);
            y += h + gap;
        }

        // 统一按钮木质外观。isPrimary=true 时用沉稳红底白字(GO)，否则浅木金底深褐字。
        void StyleWoodButton(Button btn, bool isPrimary)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.ForeColor = isPrimary ? Color.White : WoodBtnText;
            btn.BackColor = isPrimary ? WoodAccentRed : WoodBtnFace;
            btn.FlatAppearance.MouseOverBackColor = isPrimary
                ? ControlPaint.Dark(WoodAccentRed, 0.1f)
                : WoodBtnHover;
            btn.FlatAppearance.MouseDownBackColor = isPrimary
                ? ControlPaint.Dark(WoodAccentRed, 0.2f)
                : ControlPaint.Dark(WoodBtnFace, 0.1f);
        }

        public static void LoadSettings()
        {
            if (File.Exists(SettingsPath))
            {
                Settings = JsonConvert.DeserializeObject<ProgramSettings>(File.ReadAllText(SettingsPath));
                // 持久化时引擎路径是相对路径（相对程序基目录），加载后还原为绝对路径
                // 供 File.Exists / Process.Start 使用，保证后续逻辑无需感知路径形式。
                if (Settings != null)
                {
                    foreach (var engine in Settings.EngineList)
                    {
                        engine.Value.ExePath = Utils.ToAbsolutePath(engine.Value.ExePath);
                    }
                }
            }
        }

        public static void SaveSettings()
        {
            // 保存前把引擎路径相对化，避免 settings.json 里出现 D:\... 绝对路径，
            // 使整个目录可在不同机器间拷贝。转换后立即写盘再还原内存中的绝对路径，
            // 避免影响正在运行的引擎（运行期 ExePath 始终为绝对路径）。
            Dictionary<string, string> originalPaths = new Dictionary<string, string>();
            foreach (var engine in Settings.EngineList)
            {
                string rel = Utils.ToRelativePath(engine.Value.ExePath);
                if (rel != engine.Value.ExePath)
                {
                    originalPaths[engine.Key] = engine.Value.ExePath;
                    engine.Value.ExePath = rel;
                }
            }
            try
            {
                File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(Settings));
            }
            finally
            {
                // 写盘后还原内存中的绝对路径，保持运行期状态不变
                foreach (var kv in originalPaths)
                {
                    Settings.EngineList[kv.Key].ExePath = kv.Value;
                }
            }
        }

        void InitThreads()
        {
            DetectionThread = new Thread(new ThreadStart(DetectionLoop));
            DetectionThread.Start();
            AutoClickThread = new Thread(new ThreadStart(AutoClickLoop));
            AutoClickThread.Start();
        }

        void InitFolders()
        {
            if (!Directory.Exists(MODEL_FOLDER))
            {
                Directory.CreateDirectory(MODEL_FOLDER);
                Thread.Sleep(200);
            }
            if (!Directory.Exists(SOLUTION_FOLDER))
            {
                Directory.CreateDirectory(SOLUTION_FOLDER);
                Thread.Sleep(200);
            }
            if (!Directory.Exists(OPENBOOK_FOLDER))
            {
                Directory.CreateDirectory(OPENBOOK_FOLDER);
            }
            // Engines 目录：交付包内置 pikafish 等引擎。确保目录存在，供
            // EnsureDefaultEngine 扫描和用户手动放置引擎使用。
            if (!Directory.Exists(ENGINE_FOLDER))
            {
                Directory.CreateDirectory(ENGINE_FOLDER);
            }
        }

        public static void InitOpenBooks()
        {
            foreach (var book in OpenBookList)
            {
                try
                {
                    book.Value.Dispose();
                }
                catch (Exception ex) { LogHelper.Error(ex, "InitOpenBooks 释放开局库"); }
            }
            OpenBookList.Clear();
            foreach (var file in Directory.GetFiles(OPENBOOK_FOLDER))
            {
                if (file.Split('.').Last() == "obk")
                {
                    try
                    {
                        string[] parts = file.Split('\\').Last().Split('.');
                        string name = string.Join(".", parts.Take(parts.Length - 1));
                        var book = new OpenBookHelper.OpenBook(file);
                        OpenBookList.Add(name, book);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("加载开局库 " + file + " 失败：" + file + "\n" + ex.ToString());
                    }
                }
            }
        }

        void InitYoloModels()
        {
            foreach (string file in Directory.GetFiles("./Models"))
            {
                if (file.EndsWith(".onnx"))
                {
                    string modelName = file.Split('\\').Last().Replace(".onnx", "");
                    try
                    {
                        var model = new YoloScorer<YoloXiangQiModel>(file);
                        ModelList.Add(modelName, model);
                        if (Settings.YoloModel == modelName)
                        {
                            Model = model;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error(ex, "加载模型 " + modelName);
                    }
                }
            }
            // Fallback: if configured model not found, use the first available one
            if (Model == null && ModelList.Count > 0)
            {
                var first = ModelList.First();
                Model = first.Value;
                Settings.YoloModel = first.Key;
            }
            if (Model == null)
            {
                MessageBox.Show("未能加载任何YOLO模型！请检查 Models 目录下的 .onnx 文件。");
            }
        }

        public void InitSolutions()
        {
            if (!Directory.Exists(@".\Solutions"))
            {
                Directory.CreateDirectory(@".\Solutions");
            }
            SolutionList.Clear();
            string[] paramsKeys = "截图标题 截图类 点击标题 点击类".Split(' ');
            foreach (var folder in Directory.GetDirectories(@".\Solutions"))
            {
                if (folder.EndsWith(@"\自定义方案")) continue;
                string file = folder + @"\window.txt";
                if (!File.Exists(file)) continue;
                string[] lines = File.ReadAllLines(file);
                if (lines.Length == 4)
                {
                    List<string> paramList = new List<string>();
                    for (int i = 0; i < 4; i++)
                    {
                        string line = lines[i];
                        string[] args = line.Split('=');
                        if (args[0] != paramsKeys[i])
                        {
                            MessageBox.Show("方案: " + file + " 格式错误: 第" + (i + 1) + "行应为" + paramsKeys[i]);
                            break;
                        }
                        if (args[1] != "")
                        {
                            paramList.Add(args[1]);
                        }
                        else
                        {
                            paramList.Add(null);
                        }
                    }
                    SolutionList.Add(folder.Split('\\').Last(), new Solution(paramList[0], paramList[1], paramList[2], paramList[3]));
                }
                else
                {
                    MessageBox.Show("方案: " + file + " 格式错误");
                }
            }
            // Default selection
            if (!SolutionList.ContainsKey(Settings.SelectedSolution))
            {
                if (SolutionList.Count > 0)
                {
                    Settings.SelectedSolution = SolutionList.Keys.First();
                }
                else
                {
                    Settings.SelectedSolution = "";
                }
                SaveSettings();
            }
        }

        void InitResultDisplay()
        {
            pictureBox_board.Image = BoardDisplayBitmap;
            BoardGDI = Graphics.FromImage(BoardDisplayBitmap);
            RenderDisplayBoard();
        }

        // 引擎健康检查：确保 settings 里至少有一个能找到的引擎。
        // 修复"交付即失败"——交付包内置 pikafish，但用户的 settings.json 可能：
        // 1) 为空(首次启动)；2) 指向开发机外部路径(换机器后失效)；3) 用户误删。
        // 本方法扫描 Engines\ 目录，把找到的引擎(pikafish 优先)自动注册进 EngineList，
        // 并在 SelectedEngine 失效时自动切到可用的引擎。用户无感，开箱即用。
        // 调用时机：LoadSettings 之后、InitEngine 之前。
        void EnsureDefaultEngine()
        {
            try
            {
                // 收集 Engines\ 目录下所有 .exe，pikafish 优先排序
                List<string> engineFiles = new List<string>();
                if (Directory.Exists(ENGINE_FOLDER))
                {
                    string[] exes = Directory.GetFiles(ENGINE_FOLDER, "*.exe", SearchOption.TopDirectoryOnly);
                    // pikafish 排前面(默认推荐引擎)，其余按文件名
                    var ordered = exes.OrderBy(f => f.ToLower().Contains("pikafish") ? 0 : 1).ThenBy(f => f);
                    engineFiles.AddRange(ordered);
                }
                bool autoChanged = false;
                // 清理 EngineList 里文件不存在的旧条目(开发机外部路径等失效配置)
                // 但保留用户通过界面手动添加的、虽暂时找不到的条目？为稳妥，只清理"明显失效"的：
                // 路径不在 Engines\ 下且文件不存在的。Engines\ 下的即使暂时找不到也保留。
                var deadKeys = new List<string>();
                foreach (var kv in Settings.EngineList)
                {
                    string abs = Utils.ToAbsolutePath(kv.Value.ExePath);
                    if (!File.Exists(abs))
                    {
                        // 不在 Engines 目录下且文件不存在 → 开发机残留，清理
                        bool underEngines = abs.StartsWith(Utils.ToAbsolutePath(ENGINE_FOLDER), StringComparison.OrdinalIgnoreCase);
                        if (!underEngines) deadKeys.Add(kv.Key);
                    }
                }
                foreach (var k in deadKeys)
                {
                    Settings.EngineList.Remove(k);
                    autoChanged = true;
                    LogHelper.Info("[EnsureDefaultEngine] 清理失效引擎配置: " + k);
                }
                // 把 Engines\ 里有、但还没注册进 EngineList 的引擎补进去
                foreach (string file in engineFiles)
                {
                    string abs = Path.GetFullPath(file);
                    string rel = Utils.ToRelativePath(abs);
                    bool alreadyRegistered = false;
                    foreach (var kv in Settings.EngineList)
                    {
                        if (string.Equals(Utils.ToAbsolutePath(kv.Value.ExePath), abs, StringComparison.OrdinalIgnoreCase))
                        {
                            alreadyRegistered = true;
                            break;
                        }
                    }
                    if (!alreadyRegistered)
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        // 避免重名
                        string uniqueName = name;
                        int suffix = 1;
                        while (Settings.EngineList.ContainsKey(uniqueName))
                        {
                            uniqueName = name + "_" + suffix;
                            suffix++;
                        }
                        var es = new EngineSettings();
                        es.ExePath = rel;
                        es.Name = uniqueName;
                        es.Configs["Threads"] = "4";
                        Settings.EngineList[uniqueName] = es;
                        autoChanged = true;
                        LogHelper.Info("[EnsureDefaultEngine] 自动注册引擎: " + uniqueName + " -> " + rel);
                    }
                }
                // SelectedEngine 失效时，自动切到第一个能找到的引擎
                bool selectedOk = Settings.SelectedEngine.Length > 0
                    && Settings.EngineList.ContainsKey(Settings.SelectedEngine)
                    && File.Exists(Utils.ToAbsolutePath(Settings.EngineList[Settings.SelectedEngine].ExePath));
                if (!selectedOk && Settings.EngineList.Count > 0)
                {
                    // 选第一个文件存在的引擎(EngineList 已按 pikafish 优先注册)
                    foreach (var kv in Settings.EngineList)
                    {
                        if (File.Exists(Utils.ToAbsolutePath(kv.Value.ExePath)))
                        {
                            Settings.SelectedEngine = kv.Key;
                            autoChanged = true;
                            LogHelper.Info("[EnsureDefaultEngine] 自动切换 SelectedEngine 到: " + kv.Key);
                            break;
                        }
                    }
                }
                if (autoChanged) SaveSettings();
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "EnsureDefaultEngine");
            }
        }

        public void InitEngine()
        {
            if (Engine != null)
            {
                Engine.Stop();
            }
            if (Settings.SelectedEngine.Length > 0 && Settings.EngineList.ContainsKey(Settings.SelectedEngine))
            {
                EngineSettings engineSettings = Settings.EngineList[Settings.SelectedEngine];
                if (File.Exists(engineSettings.ExePath))
                {
                    var configs = Settings.EngineList[Settings.SelectedEngine].Configs;
                    // 给识别和游戏留一个核：线程数超过 (CPU 核数 - 1) 时自动降下来，
                    // 否则老电脑（2~4 核）上引擎会把 CPU 占满，游戏和识别都卡。
                    int maxThreads = Math.Max(1, Environment.ProcessorCount - 1);
                    int threads = Math.Max(1, Math.Min(Settings.ThreadCount, maxThreads));
                    if (threads != Settings.ThreadCount)
                        LogHelper.Info("[Engine] 设置线程 " + Settings.ThreadCount + " 超过本机可用核数，实际使用 " + threads + "（CPU " + Environment.ProcessorCount + " 核）");
                    configs["Threads"] = threads.ToString();
                    Engine = new EngineHelper(engineSettings.ExePath, configs);
                    Engine.BestMoveEvent += Engine_BestMove_Event;
                    Engine.InfoEvent += Engine_InfoEvent;
                    Engine.Init();
                    try { Engine.SetOption("MultiPV", "3"); } catch (Exception ex) { LogHelper.Warn("设置 MultiPV 失败(引擎可能不支持): " + ex.Message); }
                }
                else
                {
                    // 引擎文件不存在：EnsureDefaultEngine 未能自动恢复，给用户明确人话提示。
                    // 不再静默跳过(旧版表现为"软件能开但不下棋"，用户无法排查)。
                    ShowEngineMissingPrompt(engineSettings.ExePath);
                }
            }
            else
            {
                // 没有选中任何引擎：EngineList 为空，提示用户放置引擎。
                ShowEngineMissingPrompt("");
            }
        }

        // 引擎缺失的统一人话提示。引导用户把 pikafish 放到 Engines\ 目录。
        void ShowEngineMissingPrompt(string missingPath)
        {
            LogHelper.Warn("[InitEngine] 引擎未找到: " + (string.IsNullOrEmpty(missingPath) ? "(无选中引擎)" : missingPath));
            string msg = "未找到可用的象棋引擎，软件暂时无法进行 AI 分析与自动下棋。\r\n\r\n";
            if (!string.IsNullOrEmpty(missingPath))
            {
                msg += "配置的引擎路径：\r\n" + missingPath + "\r\n\r\n";
            }
            msg += "解决方法：\r\n";
            msg += "1. 从 pikafish 官方下载引擎程序(pikafish-bmi2.exe 或 pikafish.exe)\r\n";
            msg += "2. 把引擎 exe 放到软件目录下的 Engines\\ 文件夹\r\n";
            msg += "3. 重启本软件(重启后会自动识别)\r\n\r\n";
            msg += "软件目录：" + Utils.ToAbsolutePath(ENGINE_FOLDER);
            try { MessageBox.Show(this, msg, "未找到象棋引擎", MessageBoxButtons.OK, MessageBoxIcon.Warning); } catch { }
        }

        void ResetDetection()
        {
            Engine.StopAnalyze();
            Engine.AnalyzeQueue.Clear();
            Engine.SkipList.Clear();
            InvalidCount = 0;
            LastBoardAreaBitmap?.Dispose();
            LastBoardAreaBitmap = null;
            if (!string.IsNullOrEmpty(Settings.SelectedSolution))
            {
                string key = Settings.SelectedSolution;
                if (!SolutionList.ContainsKey(key))
                {
                    // 方案文件不存在（如 Solutions 目录缺失或方案被删），避免每秒抛 KeyNotFound
                    LogHelper.Warn("[LoadCurrentSolution] 方案不存在: " + key + "，可用方案: " + string.Join(",", SolutionList.Keys));
                    GameHandle = IntPtr.Zero;
                    return;
                }
                CurrentSolution = SolutionList[key];
                GameHandle = ScreenshotHelper.FindWindow(CurrentSolution.ScreenshotClass, CurrentSolution.ScreenshotCaption);
                if (CurrentSolution.ClickClass == null && CurrentSolution.ClickCaption == null)
                {
                    ClickHandle = GameHandle;
                }
                else
                {
                    ClickHandle = ScreenshotHelper.FindWindowEx(GameHandle, IntPtr.Zero, CurrentSolution.ClickClass, CurrentSolution.ClickCaption);
                }
            }
            ReloadBoard();
            BoardArea = new Rectangle(-1, -1, -1, -1);
            EngineAnalyzeCount = 0;
            CurrentBoard = null;
            LastBoard = null;
            PendingBoard = null;
            ExpectedSelfGoBoard = null;
            ExpectedMove = "";
            ExpectedBoardMap.Clear();
            // 清空走法记录：上一局的着法在新局无意义，保留会误触发长打检测，
            // 同时保证棋谱导出只含本局着法
            BestMoveList.Clear();
            // 长打局面计数跨局失效，清空避免新局误判
            PositionRepeatCount.Clear();
        }

        void StartDetection(bool fromOpponent)
        {
            LogHelper.Info("[StartDetection] fromOpponent=" + fromOpponent + " engine=" + (Engine != null)
                + " model=" + (Model != null) + " solution=" + Settings.SelectedSolution + " handle=" + GameHandle);
            if (Engine == null)
            {
                DetectEnabled = false;
                MessageBox.Show(this, "引擎未加载，无法开始。\n\n请确认程序目录下的 Engines 文件夹里有引擎（如 pikafish），"
                    + "或右键 → 引擎管理 里添加引擎。\n\n如果你是把更新包解压到了新文件夹，请改为解压覆盖到原来的程序文件夹。",
                    "无法开始", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ModeDisplay();
                return;
            }
            if (Model == null)
            {
                DetectEnabled = false;
                MessageBox.Show(this, "识别模型未加载，无法开始。\n\n请确认程序目录下的 Models 文件夹里有 .onnx 模型文件。"
                    + "\n\n如果你是把更新包解压到了新文件夹，请改为解压覆盖到原来的程序文件夹。",
                    "无法开始", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ModeDisplay();
                return;
            }
            if (GameHandle == IntPtr.Zero && Settings.SelectedSolution != "剪切板")
            {
                try { LoadCurrentSolution(); } catch (Exception ex) { LogHelper.Error(ex, "StartDetection LoadCurrentSolution"); }
                if (GameHandle == IntPtr.Zero)
                {
                    // 不阻止开始：识别线程会持续寻找窗口，找到后自动开始
                    MessageBox.Show(this, "没有找到对局窗口（当前方案：" + (string.IsNullOrEmpty(Settings.SelectedSolution) ? "未选择" : Settings.SelectedSolution) + "）。"
                        + "\n\n请先打开游戏并进入对局；如果游戏已打开，请到 设置 里选择正确的方案，或用“寻找窗口句柄”重新获取。"
                        + "\n\n找到窗口后会自动开始识别。",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            if (checkBox_debug.Checked)
            {
                ScreenDebug = false;
                checkBox_debug.Checked = false;
            }
            if (BackgroundAnalyzing)
            {
                BackgroundAnalyzing = false;
            }
            TurnToOpponent = fromOpponent;
            DetectEnabled = true;
            ResetDetection();
            ModeDisplay();
        }

        void DisplayStatus(string status)
        {
            Debug.WriteLine(status);
            try { this.Invoke(new Action(() => this.Text = $"Vin象棋 {Version} - {status}")); } catch { }
        }

        public void ModeDisplay(string mode="")
        {
            if (string.IsNullOrEmpty(mode))
            {
                mode = DetectEnabled ? "识别中" : "空闲";
            }
            Debug.WriteLine(mode);
            try { this.Invoke(new Action(() => this.Text = $"Vin象棋 {Version} [{mode}]")); } catch { }
        }

        void MouseLeftClient_2Point(int x1, int y1, int x2, int y2)
        {
            if (Settings.UniversalMouse)
            {
                var originalPos = MouseHelper.MouseOperations.GetCursorPosition();
                var windowPos = ScreenshotHelper.GetWindowRectangle(GameHandle);
                MouseHelper.MouseOperations.SetCursorPosition(x1 + windowPos.X, y1 + windowPos.Y);
                MouseHelper.MouseOperations.MouseEvent(MouseHelper.MouseOperations.MouseEventFlags.LeftDown);
                Thread.Sleep(50);
                MouseHelper.MouseOperations.MouseEvent(MouseHelper.MouseOperations.MouseEventFlags.LeftUp);
                Thread.Sleep(50);
                MouseHelper.MouseOperations.SetCursorPosition(x2 + windowPos.X, y2 + windowPos.Y);
                Thread.Sleep(50);
                MouseHelper.MouseOperations.MouseEvent(MouseHelper.MouseOperations.MouseEventFlags.LeftDown);
                Thread.Sleep(50);
                MouseHelper.MouseOperations.MouseEvent(MouseHelper.MouseOperations.MouseEventFlags.LeftUp);
                Thread.Sleep(50);
                MouseHelper.MouseOperations.SetCursorPosition(originalPos.X, originalPos.Y);
            }
            else
            {
                MouseHelper.MouseLeftClick(ClickHandle, x1, y1);
                Thread.Sleep(300);
                MouseHelper.MouseLeftClick(ClickHandle, x2, y2);
            }
        }

        void MouseLeftClick(int x, int y)
        {
            
            if (Settings.UniversalMouse)
            {
                var originalPos = MouseHelper.MouseOperations.GetCursorPosition();
                var windowPos = ScreenshotHelper.GetWindowRectangle(GameHandle);
                MouseHelper.MouseOperations.SetCursorPosition(x + windowPos.X, y + windowPos.Y);
                MouseHelper.MouseOperations.MouseEvent(MouseHelper.MouseOperations.MouseEventFlags.LeftDown);
                MouseHelper.MouseOperations.MouseEvent(MouseHelper.MouseOperations.MouseEventFlags.LeftUp);
                MouseHelper.MouseOperations.SetCursorPosition(originalPos.X, originalPos.Y);
            }
            else
            {
                MouseHelper.MouseLeftClick(ClickHandle, x, y);
            }
        }

        public void GetHandle()
        {
            Task.Run(new Action(() =>
            {
                DisplayStatus("请点击目标窗体并等待 准备获取游戏窗口句柄...2");
                Thread.Sleep(1000);
                DisplayStatus("请点击目标窗体并等待 准备获取游戏窗口句柄...1");
                Thread.Sleep(1000);
                GameHandle = ScreenshotHelper.GetForegroundWindow();
                ClickHandle = (IntPtr)ScreenshotHelper.WindowFromPoint(Cursor.Position.X, Cursor.Position.Y);
                DisplayStatus("游戏窗口句柄: " + GameHandle);
                ScreenshotHelper.WindowHandleInfo handleInfo = new ScreenshotHelper.WindowHandleInfo(GameHandle);
                var handles = handleInfo.GetAllChildHandles();
                Debug.WriteLine("GameHandle " + GameHandle + " " + ScreenshotHelper.GetWindowTitle(GameHandle));
                Debug.WriteLine(ScreenshotHelper.GetWindowRectangleWithShadow(GameHandle));
                Debug.WriteLine("ClickHandle " + ClickHandle + " " + ScreenshotHelper.GetWindowTitle(ClickHandle));
                Debug.WriteLine(ScreenshotHelper.GetWindowRectangleWithShadow(ClickHandle));
                Settings.SelectedSolution = "";
                foreach (var handle in handles)
                {
                    Debug.WriteLine(handle + " " + ScreenshotHelper.GetWindowTitle(handle));
                    Debug.WriteLine(ScreenshotHelper.GetWindowRectangleWithShadow(handle));
                    Debug.WriteLine(ScreenshotHelper.GetWindowClass(handle));
                }
            }));
        }

        Bitmap Screenshot()
        {
            try
            {
                if (Settings.SelectedSolution == "剪切板")
                {
                    if (Clipboard.ContainsImage())
                    {
                        Debug.WriteLine("Before " + Clipboard.ContainsImage().ToString());
                        Bitmap img = (Bitmap)Clipboard.GetImage().Clone();
                        Debug.WriteLine("After " + Clipboard.ContainsImage().ToString());
                        Debug.WriteLine("-=-=-=-=-=-==-=");
                        return img;
                    }
                    else
                    {
                        Debug.WriteLine("NO Clipboard Image");
                        Bitmap no_image = new Bitmap(150, 100);
                        Graphics gdi = Graphics.FromImage(no_image);
                        gdi.DrawString("剪切板中没有图片", this.Font, Brushes.Black, 0, 40);
                        gdi.Dispose();
                        return no_image;
                    }
                }
                if (Settings.UniversalMode)
                {
                    Rectangle rect;
                    if (Settings.UniversalMouse)
                    {
                        rect = ScreenshotHelper.GetWindowRectangle(GameHandle);
                    }
                    else
                    {
                        rect = ScreenshotHelper.GetWindowRectWithoutTitle(GameHandle);
                    }
                    if (rect.Width > 0 && rect.Height > 0)
                    {
                        Bitmap b = new Bitmap(rect.Width, rect.Height);
                        Graphics g = Graphics.FromImage(b);
                        g.CopyFromScreen(rect.Location, new Point(0, 0), rect.Size);
                        return b;
                    }
                    else
                    {
                        return new Bitmap(1, 1);
                    }
                }
                else
                {
                    return ScreenshotHelper.GetWindowCapture(GameHandle);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "Screenshot");
                GameHandle = IntPtr.Zero;
                ClickHandle = IntPtr.Zero;
                return new Bitmap(1, 1);
            }
        }

        public void LoadCurrentSolution()
        {
            string[] imgExtensions = new string[] { "png", "jpg", "bmp", "jpeg" };
            if (Settings.SelectedSolution == "剪切板")
            {
                ClickHandle = (IntPtr)1;
                GameHandle = (IntPtr)1;
                return;
            }
            if (Settings.SelectedSolution != "" && Settings.SelectedSolution != "自定义方案")
            {
                string key = Settings.SelectedSolution;
                CurrentSolution = SolutionList[key];
                GameHandle = ScreenshotHelper.FindWindow(CurrentSolution.ScreenshotClass, CurrentSolution.ScreenshotCaption);
                if (CurrentSolution.ClickClass == null && CurrentSolution.ClickCaption == null)
                {
                    ClickHandle = GameHandle;
                    ClickOffset = new Size(0, 0);
                }
                else
                {
                    ScreenshotHelper.WindowHandleInfo handleInfo = new ScreenshotHelper.WindowHandleInfo(GameHandle);
                    var childHandles = handleInfo.GetAllChildHandles();
                    foreach (var child in childHandles)
                    {
                        string childTitle = ScreenshotHelper.GetWindowTitle(child);
                        string childClass = ScreenshotHelper.GetWindowClass(child);
                        if ((CurrentSolution.ClickCaption == null || childTitle == CurrentSolution.ClickCaption) &&
                            (CurrentSolution.ClickClass == null || childClass == CurrentSolution.ClickClass))
                        {
                            ClickHandle = child;
                            break;
                        }
                    }
                    ClickHandle = ScreenshotHelper.FindWindowEx(GameHandle, IntPtr.Zero, CurrentSolution.ClickClass, CurrentSolution.ClickCaption);
                    Rectangle gameRect = ScreenshotHelper.GetWindowRectangle(GameHandle);
                    Rectangle clickRect = ScreenshotHelper.GetWindowRectangleWithShadow(ClickHandle);
                    ClickOffset = new Size(0, clickRect.Height - gameRect.Height);
                }

                string autoClickPath = @".\Solutions\" + Settings.SelectedSolution + @"\AutoClick";
                LoadAutoClickImages(autoClickPath);
                SaveSettings();
            }
            else if(Settings.SelectedSolution != "自定义方案")
            {
                if (Directory.Exists(@".\Solutions\自定义方案") && Directory.Exists(@".\Solutions\自定义方案\AutoClick"))
                {
                    LoadAutoClickImages(@".\Solutions\自定义方案\AutoClick");
                }
            }
        }

        // 加载自动点击模板，按 Close/Confirm 语义角色分类填充。
        // 目录约定(跨平台通用)：
        //   {autoClickPath}\Close\   关闭类(红叉/不需要/取消) —— 先处理
        //   {autoClickPath}\Confirm\ 确认类(再来一局/开始下一盘) —— 仅在无关闭类时处理
        // 向后兼容：若两个子目录都不存在，但 {autoClickPath} 下直接有图，则全部当作
        // Close 类加载(旧部署行为不变)。每张图加载后自动抠背景为透明(MakeTransparentFromCorners)。
        void LoadAutoClickImages(string autoClickPath)
        {
            string[] imgExtensions = new string[] { "png", "jpg", "bmp", "jpeg" };
            AutoClickImageList.Clear();
            AutoClickCloseList.Clear();
            AutoClickConfirmList.Clear();

            string closePath = Path.Combine(autoClickPath, "Close");
            string confirmPath = Path.Combine(autoClickPath, "Confirm");
            bool hasSubDirs = Directory.Exists(closePath) || Directory.Exists(confirmPath);

            Action<string, List<Bitmap>> loadInto = (dir, list) =>
            {
                if (!Directory.Exists(dir)) return;
                foreach (var image in Directory.GetFiles(dir))
                {
                    if (imgExtensions.Contains(image.Split('.').Last()))
                    {
                        try
                        {
                            Bitmap tmpImg = new Bitmap(image);
                            // 自动抠背景：实心背景截图会变成透明背景模板，
                            // FindImageFromTop 只比对主体像素，叠加弹窗的半透明遮罩/阴影不再干扰匹配。
                            Bitmap processed = ImageHelper.MakeTransparentFromCorners(tmpImg);
                            tmpImg.Dispose();
                            list.Add(processed);
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Error(ex, "LoadAutoClickImages 加载 " + image);
                        }
                    }
                }
            };

            if (hasSubDirs)
            {
                loadInto(closePath, AutoClickCloseList);
                loadInto(confirmPath, AutoClickConfirmList);
            }
            else
            {
                // 向后兼容：旧目录无子目录，全部当 Close 类
                loadInto(autoClickPath, AutoClickCloseList);
            }
            // 旧列表同步填充(Close+Confirm 合并)，兼容可能仍引用 AutoClickImageList 的旧代码
            AutoClickImageList.AddRange(AutoClickCloseList);
            AutoClickImageList.AddRange(AutoClickConfirmList);
            LogHelper.Info("[LoadAutoClickImages] path=" + autoClickPath
                + " hasSubDirs=" + hasSubDirs
                + " Close=" + AutoClickCloseList.Count + " Confirm=" + AutoClickConfirmList.Count);
        }

        private void Mainform_FormClosing(object sender, FormClosingEventArgs e)
        {
            Running = false;
            Settings.WindowX = this.Location.X;
            Settings.WindowY = this.Location.Y;
            if (_floatingScoreForm != null && !_floatingScoreForm.IsDisposed)
            {
                Settings.FloatingScoreX = _floatingScoreForm.Location.X;
                Settings.FloatingScoreY = _floatingScoreForm.Location.Y;
                _floatingScoreForm.Close();
            }
            SaveSettings();
            if (Engine != null)
            {
                Engine.Stop();
            }
            foreach (var book in OpenBookList)
            {
                try
                {
                    book.Value.Dispose();
                }
                catch (Exception ex)
                {
                    LogHelper.Error(ex, "Mainform 后台任务");
                }
            }
        }

        // 全局快捷键上次按下状态，用于边沿检测（按下瞬间触发一次，避免长按重复触发）
        // F2 取句柄沿用原有逻辑（每 Tick 轮询），这里管理 F1/F3/F4/F5
        static bool _f1Down, _f3Down, _f4Down, _f5Down;

        static bool EdgeTrigger(ref bool prev, Keys key)
        {
            // MouseHelper.GetAsyncKeyState 声明为返回 bool（按下即为 true）。
            // 边沿检测：仅当"本次为按下且上次未按下"时触发一次，避免长按重复。
            bool now = MouseHelper.GetAsyncKeyState(key);
            bool triggered = now && !prev;
            prev = now;
            return triggered;
        }

        void HandleHotkeys()
        {
            // F1 开始识别（从己方）、F3 停止、F4 截图、F5 导出棋谱
            // 均为边沿触发：只在按下瞬间响应一次，松开前不会重复
            try
            {
                if (MouseHelper.GetAsyncKeyState(Keys.F2))
                {
                    GetHandle();
                }
                if (EdgeTrigger(ref _f1Down, Keys.F1))
                {
                    StartDetection(false);
                }
                if (EdgeTrigger(ref _f3Down, Keys.F3))
                {
                    button_stop_detection_Click(null, null);
                }
                if (EdgeTrigger(ref _f4Down, Keys.F4))
                {
                    button_screenshot_Click(null, null);
                }
                if (EdgeTrigger(ref _f5Down, Keys.F5))
                {
                    ExportGameRecord();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "HandleHotkeys");
            }
        }

        /// <summary>
        /// 导出本局棋谱到 logs 同级的 records 目录。
        /// 由于历史每步棋盘快照未持久化，这里导出引擎坐标走法序列 + 当前局面 FEN，
        /// 配合任意象棋软件可复盘。文件名含时间戳，避免覆盖。
        /// </summary>
        public void ExportGameRecord()
        {
            try
            {
                var moves = BestMoveList;
                if (moves == null || moves.Count == 0)
                {
                    MessageBox.Show("当前还没有走法记录，无法导出。");
                    return;
                }
                string dir = @".\records";
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string file = Path.Combine(dir, "棋谱_" + stamp + ".txt");

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Vin象棋 棋谱导出");
                sb.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("本方: " + (RedSide ? "红方" : "黑方"));
                sb.AppendLine("总步数: " + moves.Count);
                sb.AppendLine();
                sb.AppendLine("[引擎坐标走法（UCI格式，如 h2e2）]");
                for (int i = 0; i < moves.Count; i++)
                {
                    sb.AppendLine((i + 1).ToString("D2") + ". " + moves[i]);
                }
                sb.AppendLine();
                string fen = "";
                if (CurrentBoard != null)
                {
                    fen = Utils.BoardToFen(CurrentBoard, RedSide);
                }
                sb.AppendLine("[当前局面 FEN]");
                sb.AppendLine(string.IsNullOrEmpty(fen) ? "(无)" : fen);

                File.WriteAllText(file, sb.ToString(), Encoding.UTF8);
                DisplayStatus("已导出棋谱: " + file);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "ExportGameRecord");
                MessageBox.Show("导出棋谱失败: " + ex.Message);
            }
        }

        private void timer_set_window_hwnd_Tick(object sender, EventArgs e)
        {
            HandleHotkeys();
        }

        private void pictureBox_board_MouseDown(object sender, MouseEventArgs e)
        {
            bool MouseOnImage = false;
            Point MousePositionOnImage = new Point(-1, -1);
            double img_ratio = (double)BoardDisplayBitmap.Width / (double)BoardDisplayBitmap.Height;
            double box_ratio = (double)pictureBox_board.Width / (double)pictureBox_board.Height;
            double display_ratio;
            int display_x, display_y, display_width, display_height;
            if (box_ratio >= img_ratio)
            {
                display_y = 0;
                display_height = pictureBox_board.Height;
                display_x = (int)(pictureBox_board.Width - display_height * img_ratio) / 2;
                display_width = (int)(display_height * img_ratio);
                display_ratio = (double)BoardDisplayBitmap.Height / display_height;
            }
            else
            {
                display_x = 0;
                display_width = pictureBox_board.Width;
                display_y = (int)(pictureBox_board.Height - display_width / img_ratio) / 2;
                display_height = (int)(display_width / img_ratio);
                display_ratio = (double)BoardDisplayBitmap.Width / display_width;
            }
            int img_x, img_y;
            bool mouse_on_x, mouse_on_y;
            if (e.X <= display_x)
            {
                img_x = 0;
                mouse_on_x = false;
            }
            else if (e.X >= display_x + display_width)
            {
                img_x = BoardDisplayBitmap.Width - 1;
                mouse_on_x = false;
            }
            else
            {
                img_x = (int)((e.X - display_x) * display_ratio);
                mouse_on_x = true;
            }
            if (e.Y <= display_y)
            {
                img_y = 0;
                mouse_on_y = false;
            }
            else if (e.Y >= display_y + display_height)
            {
                img_y = BoardDisplayBitmap.Height - 1;
                mouse_on_y = false;
            }
            else
            {
                img_y = (int)((e.Y - display_y) * display_ratio);
                mouse_on_y = true;
            }
            if (mouse_on_x && mouse_on_y)
            {
                MouseOnImage = true;
                MousePositionOnImage = new Point(img_x, img_y);
            }
            else
            {
                MouseOnImage = false;
            }
            if (MouseOnImage)
            {
                // 点击命中交叉点：复用 RenderDisplayBoard 的同一套网格度量
                // (GetBoardRenderMetrics)，保证"看到棋子在哪、点哪个就生效"。
                // 旧实现用 width=40/xoffset=20 硬编码，与实际棋盘网格(44/居中)不一致，
                // 点格子边缘会命中错误位置。这里改为找离鼠标最近的交叉点。
                var m = GetBoardRenderMetrics();
                Point pos = new Point(-1, -1);
                int bestDist = m.Grid / 2 + 2; // 命中半径：半格+少量容差
                for (int y = 0; y < 10; y++)
                {
                    for (int x = 0; x < 9; x++)
                    {
                        int cx = m.OriginX + x * m.Grid;
                        int cy = m.OriginY + y * m.Grid;
                        int dist = Math.Max(Math.Abs(MousePositionOnImage.X - cx), Math.Abs(MousePositionOnImage.Y - cy));
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            pos = new Point(x, y);
                        }
                    }
                }
                if (pos.X != -1)
                {
                    Point p = ClickPositionMap[pos.X, pos.Y];
                    MouseLeftClick(p.X, p.Y);
                }
            }
        }

        public void button_redetect_Click(object sender, EventArgs e)
        {
            ResetDetection();
        }

        public void button_screenshot_Click(object sender, EventArgs e)
        {
            Bitmap screen = Screenshot();
            if (!Directory.Exists(@".\Solutions\" + Settings.SelectedSolution))
            {
                Directory.CreateDirectory(@".\Solutions\" + Settings.SelectedSolution);
            }
            ImageEditForm editForm = new ImageEditForm(screen, DateTime.Now.ToString("yyyyMMddHHmmss"), @".\Solutions\" + Settings.SelectedSolution + @"\AutoClick");
            editForm.StartPosition = FormStartPosition.CenterParent;
            editForm.Text = "方案 " + Settings.SelectedSolution + " 自动点击图片管理";
            editForm.ShowDialog();
            InitSolutions();
        }

        private void ToolStripMenuItem_about_Click(object sender, EventArgs e)
        {
            AboutForm abf = new AboutForm();
            abf.StartPosition = FormStartPosition.CenterParent;
            abf.Show();
        }

        private void button_start_from_self_Click(object sender, EventArgs e)
        {
            StartDetection(false);
        }

        private void button_start_from_oppo_Click(object sender, EventArgs e)
        {
            StartDetection(true);
        }

        public void button_stop_detection_Click(object sender, EventArgs e)
        {
            DetectEnabled = false;
            ScreenDebug = true;
            checkBox_debug.Checked = true;
            if (Engine != null)
            {
                Engine.Stop();
            }
            ModeDisplay();
        }

        public void button_advance_settings_Click(object sender, EventArgs e)
        {
            DetectionSettingsForm detectionSettingsForm = new DetectionSettingsForm();
            detectionSettingsForm.StartPosition = FormStartPosition.CenterParent;
            detectionSettingsForm.ShowDialog();
        }

        private void button_settings_Click(object sender, EventArgs e)
        {
            using (VinSettingsForm settingsForm = new VinSettingsForm(this))
            {
                settingsForm.StartPosition = FormStartPosition.CenterParent;
                settingsForm.ShowDialog(this);
            }
        }

        private void checkBox_auto_go_CheckedChanged(object sender, EventArgs e)
        {
            Settings.AutoGo = checkBox_auto_go.Checked;
            SaveSettings();
        }

        private void checkBox_two_steps_CheckedChanged(object sender, EventArgs e)
        {
            ShowTwoSteps = checkBox_two_steps.Checked;
            RenderDisplayBoard();
        }

        private void StartDrag(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private void button_start_Click(object sender, EventArgs e)
        {
            StartDetection(false);
        }

        private void button_size_Click(object sender, EventArgs e)
        {
            _isLargeSize = !_isLargeSize;
            this.ClientSize = _isLargeSize
                ? new Size(LargeW, LargeH)
                : new Size(SmallW, SmallH);
            // 尺寸切换后 panel_right 高度变化，重新等间距排布右侧元素
            LayoutRightPanel();
        }

        private void label_close_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void label_close_MouseEnter(object sender, EventArgs e)
        {
            label_close.ForeColor = Color.White;
            label_close.BackColor = WoodAccentRed;
        }

        private void label_close_MouseLeave(object sender, EventArgs e)
        {
            label_close.ForeColor = WoodTitle;
            label_close.BackColor = Color.Transparent;
        }

        private void label_topmost_Click(object sender, EventArgs e)
        {
            this.TopMost = !this.TopMost;
            label_topmost.ForeColor = this.TopMost ? WoodAccentRed : WoodTitle;
        }

        private void label_topmost_MouseEnter(object sender, EventArgs e)
        {
            label_topmost.BackColor = Color.FromArgb(60, 245, 230, 200);
        }

        private void label_topmost_MouseLeave(object sender, EventArgs e)
        {
            label_topmost.BackColor = Color.Transparent;
        }

        private void textBox_engine_log_TextChanged(object sender, EventArgs e)
        {
            string text = textBox_engine_log.Text;
            if (string.IsNullOrEmpty(text)) return;
            int lastIdx = text.LastIndexOf("得分: ");
            if (lastIdx < 0) return;
            string afterScore = text.Substring(lastIdx + 4);
            int endIdx = afterScore.IndexOf(' ');
            if (endIdx < 0) endIdx = afterScore.Length;
            string scoreStr = afterScore.Substring(0, endIdx).Trim();
            int score;
            if (int.TryParse(scoreStr, out score))
            {
                string display = (score >= 0 ? "+" : "-") + Math.Abs(score).ToString();
                label_score.Text = Math.Abs(score).ToString();
                label_score.ForeColor = score >= 0 ? WoodScoreRed : WoodAccentBlk;
                this.Text = display;
                // 主窗分数标签用亮朱红/深蓝灰(木色底上清晰)；浮动窗是半透明白底，
                // 用深红/深蓝与之匹配且在浅底上清晰。
                Color floatColor = score >= 0 ? FloatScorePositive : FloatScoreNegative;
                UpdateFloatingScore(display, floatColor, _floatDepthText);
            }
            else
            {
                label_score.Text = scoreStr;
                label_score.ForeColor = WoodAccentRed;
                this.Text = scoreStr;
                UpdateFloatingScore(scoreStr, FloatScorePositive, _floatDepthText);
            }
        }

        private void ToolStripMenuItem_openbook_management_Click(object sender, EventArgs e)
        {
            OpenBookSettingsForm opsf = new OpenBookSettingsForm();
            opsf.StartPosition = FormStartPosition.CenterParent;
            opsf.ShowDialog();
        }

        private void ToolStripMenuItem_engine_management_Click(object sender, EventArgs e)
        {
            EngineManageForm engineManageForm = new EngineManageForm();
            engineManageForm.ShowDialog();
            if (!Settings.EngineList.ContainsKey(Settings.SelectedEngine))
            {
                if (Settings.EngineList.Count > 0)
                {
                    Settings.SelectedEngine = Settings.EngineList.Keys.ToList()[0];
                    InitEngine();
                }
                else
                {
                    Settings.SelectedEngine = "";
                }
                SaveSettings();
            }
        }
    }
}
