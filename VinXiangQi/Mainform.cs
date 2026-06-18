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
        // 是否开始连线
        public static bool DetectEnabled = false;
        // 自动点击的图片
        public static List<Bitmap> AutoClickImageList = new List<Bitmap>();
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

        private Label _floatingScoreLabel;
        private Form _floatingScoreForm;
        private System.Windows.Forms.Timer _floatingTopmostTimer;
        private int _floatingClickCount;
        private int _floatingClickZone;
        private System.Windows.Forms.Timer _floatingClickTimer;

        public void InitFloatingScore()
        {
            if (!Settings.ShowFloatingScore) return;
            if (_floatingScoreForm != null && !_floatingScoreForm.IsDisposed) return;

            _floatingScoreForm = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(90, 44),
                TopMost = true,
                ShowInTaskbar = false,
                BackColor = SystemColors.Control
            };
            _floatingScoreForm.Paint += (s, e) =>
            {
                var g = e.Graphics;
                int w = _floatingScoreForm.ClientSize.Width;
                int h = _floatingScoreForm.ClientSize.Height;
                using (Pen p = new Pen(Color.Green, 2))
                    g.DrawRectangle(p, 1, 1, w - 2, h - 2);
                using (Pen d = new Pen(Color.Green, 1))
                {
                    d.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                    g.DrawLine(d, w / 3, 4, w / 3, h - 4);
                    g.DrawLine(d, w * 2 / 3, 4, w * 2 / 3, h - 4);
                }
            };

            _floatingScoreLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 20F, FontStyle.Bold),
                ForeColor = Color.Red,
                Text = "--",
                TextAlign = ContentAlignment.MiddleCenter
            };
            _floatingScoreLabel.MouseDown += FloatingScore_MouseDown;
            _floatingScoreForm.MouseDown += FloatingScore_MouseDown;

            _floatingScoreForm.Controls.Add(_floatingScoreLabel);

            if (Settings.FloatingScoreX >= 0 && Settings.FloatingScoreY >= 0)
                _floatingScoreForm.Location = new Point(Settings.FloatingScoreX, Settings.FloatingScoreY);
            else
                _floatingScoreForm.Shown += (s, e) => { _floatingScoreForm.Location = new Point(this.Right + 4, this.Top); };

            _floatingScoreForm.Show(this);
            PinFloatingTopmost();

            if (_floatingTopmostTimer == null)
            {
                _floatingTopmostTimer = new System.Windows.Forms.Timer();
                _floatingTopmostTimer.Interval = 2000;
                _floatingTopmostTimer.Tick += (s, e) => PinFloatingTopmost();
            }
            _floatingTopmostTimer.Start();
        }

        private void FloatingScore_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            int w = _floatingScoreForm.ClientSize.Width;
            int zone = e.X < w / 3 ? 0 : e.X < w * 2 / 3 ? 1 : 2;

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
                _floatingScoreLabel = null;
            }
        }

        // 主界面是无边框紧凑窗体，没有菜单栏。这里构建一个右键菜单挂到棋盘上，
        // 既给"导出棋谱/关于/引擎管理/开局库管理"提供入口，也复活了原先
        // 因菜单栏被移除而失效的 ToolStripMenuItem_*_Click 处理器。
        ContextMenuStrip _boardContextMenu;
        void InitContextMenu()
        {
            _boardContextMenu = new ContextMenuStrip();
            _boardContextMenu.Items.Add("导出棋谱 (F5)", null, (s, e) => ExportGameRecord());
            _boardContextMenu.Items.Add("-");
            _boardContextMenu.Items.Add("开始识别 (F1)", null, (s, e) => StartDetection(false));
            _boardContextMenu.Items.Add("停止识别 (F3)", null, (s, e) => button_stop_detection_Click(null, null));
            _boardContextMenu.Items.Add("截图 (F4)", null, (s, e) => button_screenshot_Click(null, null));
            _boardContextMenu.Items.Add("-");
            _boardContextMenu.Items.Add("引擎管理", null, (s, e) => ToolStripMenuItem_engine_management_Click(null, null));
            _boardContextMenu.Items.Add("开局库管理", null, (s, e) => ToolStripMenuItem_openbook_management_Click(null, null));
            _boardContextMenu.Items.Add("设置", null, (s, e) => button_settings_Click(null, null));
            _boardContextMenu.Items.Add("-");
            _boardContextMenu.Items.Add("关于", null, (s, e) => ToolStripMenuItem_about_Click(null, null));

            pictureBox_board.ContextMenuStrip = _boardContextMenu;
            panel_right.ContextMenuStrip = _boardContextMenu;
            panel_drag_top.ContextMenuStrip = _boardContextMenu;
        }

        void UpdateFloatingScore(string text, Color color)
        {
            if (_floatingScoreLabel == null) return;
            _floatingScoreLabel.Text = text;
            _floatingScoreLabel.ForeColor = color;
        }

        private void Mainform_Load(object sender, EventArgs e)
        {
            try
            {
                this.Text = $"Vin象棋 {Version}";
                InitFolders();
                LoadSettings();
                InitOpenBooks();
                InitYoloModels();
                InitSolutions();
                InitThreads();
                InitResultDisplay();
                InitEngine();
                InitFloatingScore();
                InitContextMenu();
                Settings.BackgroundAnalysis = false;
                checkBox_auto_go.Checked = Settings.AutoGo;
                checkBox_two_steps.Checked = ShowTwoSteps;
                if (Settings.WindowX >= 0 && Settings.WindowY >= 0)
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = new Point(Settings.WindowX, Settings.WindowY);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
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
                    if (configs.ContainsKey("Threads"))
                    {
                        configs["Threads"] = Settings.ThreadCount.ToString();
                    }
                    else
                    {
                        configs.Add("Threads", Settings.ThreadCount.ToString());
                    }
                    Engine = new EngineHelper(engineSettings.ExePath, configs);
                    Engine.BestMoveEvent += Engine_BestMove_Event;
                    Engine.InfoEvent += Engine_InfoEvent;
                    Engine.Init();
                    try { Engine.SetOption("MultiPV", "3"); } catch (Exception ex) { LogHelper.Warn("设置 MultiPV 失败(引擎可能不支持): " + ex.Message); }
                }

            }
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
        }

        void StartDetection(bool fromOpponent)
        {
            if (Engine == null)
            {
                DetectEnabled = false;
                MessageBox.Show("引擎未加载！");
                ModeDisplay();
                return;
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
                if (Directory.Exists(autoClickPath))
                {
                    AutoClickImageList.Clear();
                    foreach (var image in Directory.GetFiles(autoClickPath))
                    {
                        if (imgExtensions.Contains(image.Split('.').Last()))
                        {
                            Bitmap tmpImg = new Bitmap(image);
                            Bitmap img = new Bitmap(tmpImg);
                            tmpImg.Dispose();
                            AutoClickImageList.Add(img);
                        }
                    }
                }
                SaveSettings();
            }
            else if(Settings.SelectedSolution != "自定义方案")
            {
                if (Directory.Exists(@".\Solutions\自定义方案") && Directory.Exists(@".\Solutions\自定义方案\AutoClick"))
                {
                    AutoClickImageList.Clear();
                    foreach (var image in Directory.GetFiles(@".\Solutions\自定义方案\AutoClick"))
                    {
                        if (imgExtensions.Contains(image.Split('.').Last()))
                        {
                            Bitmap tmpImg = new Bitmap(image);
                            Bitmap img = new Bitmap(tmpImg);
                            tmpImg.Dispose();
                            AutoClickImageList.Add(img);
                        }
                    }
                }
            }
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
                int width = 40;
                int height = 40;
                int xoffset = width / 2;
                int yoffset = height / 2;
                Point pos = new Point(-1, -1);
                for (int y = 0; y < 10; y++)
                {
                    for (int x = 0; x < 9; x++)
                    {
                        if (MousePositionOnImage.X >= x * width + xoffset && MousePositionOnImage.X <= x * width + xoffset + width &&
                            MousePositionOnImage.Y >= y * height + yoffset && MousePositionOnImage.Y <= y * height + yoffset + height)
                        {
                            pos = new Point(x, y);
                            break;
                        }
                    }
                    if (pos.X != -1) break;
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
        }

        private void label_close_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void label_close_MouseEnter(object sender, EventArgs e)
        {
            label_close.ForeColor = Color.White;
            label_close.BackColor = Color.IndianRed;
        }

        private void label_close_MouseLeave(object sender, EventArgs e)
        {
            label_close.ForeColor = Color.Gray;
            label_close.BackColor = Color.Transparent;
        }

        private void label_topmost_Click(object sender, EventArgs e)
        {
            this.TopMost = !this.TopMost;
            label_topmost.ForeColor = this.TopMost ? Color.DeepSkyBlue : Color.Gray;
        }

        private void label_topmost_MouseEnter(object sender, EventArgs e)
        {
            label_topmost.BackColor = Color.LightGray;
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
                label_score.ForeColor = score >= 0 ? Color.Red : Color.Black;
                this.Text = display;
                UpdateFloatingScore(display, score >= 0 ? Color.Red : Color.Black);
            }
            else
            {
                label_score.Text = scoreStr;
                label_score.ForeColor = Color.Red;
                this.Text = scoreStr;
                UpdateFloatingScore(scoreStr, Color.Red);
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
