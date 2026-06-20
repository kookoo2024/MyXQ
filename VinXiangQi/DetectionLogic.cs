using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Imaging;
using Yolov5Net.Scorer;
using System.Threading;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace VinXiangQi
{
    partial class Mainform
    {
        // 是否为调试截图状态
        public static bool ScreenDebug = true;
        // 是否为红方
        public static bool RedSide = true;
        // 最近一次识别的棋盘，可能不合法 / 处于动画状态
        public static string[,] PendingBoard = new string[9, 10];
        // 上一个稳定棋盘
        public static string[,] LastBoard = new string[9, 10];
        // 储存当前引擎正在计算的棋盘
        public static string[,] EngineAnalyzingBoard = new string[9, 10];
        // 当前已经被确认的棋盘
        public static string[,] CurrentBoard = new string[9, 10];
        // 自己走棋后的预期状态，遇到该状态不触发引擎计算
        public static string[,] ExpectedSelfGoBoard = new string[9, 10];
        public static Dictionary<string, string[,]> ExpectedBoardMap = new Dictionary<string, string[,]>();
        // 用于后台计算的预期棋盘
        public static string[,] PonderBoard = new string[9, 10];
        public static List<string> BestMoveList = new List<string>();
        // 长打(长将/长捉)检测：记录"本方走完后局面"的出现次数。
        // key = Utils.BoardLayoutKey(局面布局，不含走子方)；value = 该局面累计出现次数。
        // 同一局面出现 >=3 次即判长打作负，禁掉导致重复的着法迫使变招。
        // 关键：按"局面"而非"走法"判定——车在 b1b2↔b2b1 换着将军时走法虽变，
        // 但每次形成的局面会重复，第3次即触发，能彻底解决"换招继续长将"。
        // 新局/被吃子时清空(见 ReloadBoard 新局分支和吃子判定)。
        public static Dictionary<string, int> PositionRepeatCount = new Dictionary<string, int>();
        // 局面重复的阈值：同局面出现 >=3 次(即 PositionRepeatCount[key] 达到 3)判长打。
        const int PositionRepeatLimit = 3;
        // 图片上方形的棋盘区域 x, y, width, height 包括棋盘的框框 一个扩展的范围
        Rectangle BoardArea = new Rectangle(-1, -1, -1, -1);
        // Yolo模型识别出的棋盘区域 x, y, width, height，因为可能送进模型的已经是裁切过的棋盘区域，所以该数值需要被还原为上方的BoardArea
        Rectangle BoardAreaRaw = new Rectangle(-1, -1, -1, -1);
        // 上一次截图时，棋盘显示区域的图片，用于对比图片来判断是否处在动画中
        Bitmap LastBoardAreaBitmap = null;
        // 当失败次数过多时，自动重载
        int InvalidCount = 0;
        // 是否第一次收到绝杀
        bool FirstMate = true;
        Color NormalColor = Color.Black;
        Color PositiveColor = Color.DarkBlue;
        Color NegativeColor = Color.DarkRed;
        Color MateColor = Color.Green;

        static readonly Pen ArrowRedPen = new Pen(Color.FromArgb(180, Color.OrangeRed), 10) { EndCap = LineCap.ArrowAnchor };
        static readonly Pen ArrowBluePen = new Pen(Color.FromArgb(180, Color.Blue), 10) { EndCap = LineCap.ArrowAnchor };
        static readonly Pen ArrowRedDimPen = new Pen(Color.FromArgb(180, Color.DarkOrange), 8) { EndCap = LineCap.ArrowAnchor };
        static readonly Pen ArrowBlueDimPen = new Pen(Color.FromArgb(180, Color.DarkGreen), 8) { EndCap = LineCap.ArrowAnchor };

        public string ExpectedMove = "";
        public bool ChangeDetectedAfterClick = false;
        public bool TurnToOpponent = false;
        public bool BackgroundAnalyzing = false;

        public XiangQiGame Game = new XiangQiGame();

        //Dictionary<string, Bitmap> TemplateList = new Dictionary<string, Bitmap>();
        //public bool TemplateInitiated = false;

        public enum ResultSource
        {
            Openbook,
            ChessDB,
            Engine
        }

        // 自动续盘循环：截图 → 模板匹配 → 命中则点击。
        // 与 DetectionLoop / 引擎进程是独立线程，互不影响运算速度。
        // 两阶段处理(先关后确认)：解决了 JJ象棋"看视频领奖励"(红叉小弹窗)叠在
        // "再来一局"(大按钮)上时卡死的问题。模板按角色分两类：
        //   Close(关闭类:红叉/取消/不需要) —— 每轮优先处理，先关掉上层干扰弹窗
        //   Confirm(确认类:再来一局/开始下一盘) —— 仅当没有任何 Close 弹窗时才点
        // 这样叠加弹窗时一定先关上层再点下层，不会互相干扰。
        //   1) 命中即 break，下一轮用全新截图再找下一个按钮（旧代码命中后仍用旧截图
        //      继续匹配列表里的图，导致界面已变化时第二个图标识别不到）；
        //   2) 命中后进入 cooldown，等界面动画/弹窗切换稳定再继续；
        //   3) 放宽匹配阈值到 AutoClickThreshold，缓解渲染差异（缩放/抗锯齿/阴影）；
        //   4) 每轮截图用完即释放，避免全屏 Bitmap 内存泄漏。
        //   5) 点击后验证：点完下一帧在原位置小范围重匹配，确认目标是否消失。
        //      未消失说明点击无效(误匹配/没点中)，对该位置启动快速重试(2秒去重)
        //      而非默认15秒去重，打破"点错一次锁死15秒"的死循环。
        const int AutoClickThreshold = 80;
        const int AutoClickCooldownMs = 1500;
        const int AutoClickIntervalMs = 2000;
        // 默认去重窗口：模板图在下棋界面可能误匹配到某固定区域，限制同一坐标在
        // 窗口内只点一次，避免反复误点打断对局。局间按钮点掉后界面变化，下一轮是
        // 不同坐标或按钮消失，不受影响。
        const int AutoClickDedupMs = 15000;
        const int AutoClickRetryDedupMs = 2000; // 点击验证失败时，缩短到2秒快速重试
        const int AutoClickDedupTolerance = 10; // 坐标偏差小于此值视为同一位置
        const int AutoClickMaxRetry = 4;        // 同一目标快速重试上限，超过则放弃该轮
        // 点击后验证的重匹配范围：在点击位置周围 AutoClickVerifyMargin 像素内找模板
        const int AutoClickVerifyMargin = 25;
        // 点完等界面响应，再做验证截图
        const int AutoClickVerifyDelayMs = 400;
        Point _lastAutoClickPos = new Point(-1, -1);
        DateTime _lastAutoClickTime = DateTime.MinValue;
        // 自适应去重状态：上次点击的目标(用于验证)+快速重试计数
        // _retryMode=true 时，该位置的去重窗口用 AutoClickRetryDedupMs 而非默认值
        Point _pendingVerifyPos = new Point(-1, -1);
        Size _pendingVerifySize = Size.Empty;
        Bitmap _pendingVerifyTemplate = null;
        int _pendingVerifyRetry = 0;

        // 在 screen(相对游戏窗口的截图)里匹配某个模板列表，返回第一个命中结果。
        // 列表内按面积升序(小图优先)。返回 hitImage/pos(左上角)；未命中 pos.X==-1。
        void AutoClickFindMatch(Bitmap screen, List<Bitmap> images, out Bitmap hitImage, out Point pos)
        {
            hitImage = null;
            pos = new Point(-1, -1);
            if (images == null || images.Count == 0) return;
            var ordered = images.OrderBy(img => img.Width * img.Height).ToArray();
            foreach (Bitmap image in ordered)
            {
                Point p = ImageHelper.FindImageFromTop(screen, image, AutoClickThreshold);
                if (p.X != -1)
                {
                    hitImage = image;
                    pos = p;
                    return;
                }
            }
        }

        // 点击后验证：在 clickPos 附近小范围重新找 template，找到了=未消失(点击无效)。
        // 用限定区域(FindImageFromTop 的 startFindX/Y/deltaX/Y)避免全屏扫描，极快。
        bool AutoClickTargetStillThere(Bitmap screen, Bitmap template, Point clickPos)
        {
            if (template == null) return false;
            int sx = Math.Max(0, clickPos.X - AutoClickVerifyMargin);
            int sy = Math.Max(0, clickPos.Y - AutoClickVerifyMargin);
            int dx = template.Width + AutoClickVerifyMargin * 2;
            int dy = template.Height + AutoClickVerifyMargin * 2;
            Point p = ImageHelper.FindImageFromTop(screen, template, AutoClickThreshold, sx, sy, dx, dy);
            return p.X != -1;
        }

        // 判断 clickPos 是否落在自身窗口(主窗体/浮动分数窗)内，避免误点关掉本程序。
        // 注意坐标空间：AutoClickLoop 的 clickPos 是"相对游戏窗口截图"的坐标(0,0 是
        // 游戏窗口左上角)，而 this.Bounds/_floatingScoreForm.Bounds 是屏幕坐标。
        // 必须先把 clickPos 加上游戏窗口的屏幕位置转成屏幕坐标，再和自身窗口比较。
        // 早期版本漏了这步转换，把"截图里的相对坐标"直接当屏幕坐标比较，导致游戏窗口
        // 上方的"再来一局"按钮(相对坐标 y≈716)被误判成落在主窗体内，自动下一盘被永久跳过。
        bool AutoClickHitSelf(Point clickPos)
        {
            try
            {
                Point screenPos = clickPos;
                if (GameHandle != IntPtr.Zero)
                {
                    var winRect = ScreenshotHelper.GetWindowRectangle(GameHandle);
                    screenPos = new Point(clickPos.X + winRect.X, clickPos.Y + winRect.Y);
                }
                if (this != null && !IsDisposed && IsHandleCreated && this.Bounds.Contains(screenPos))
                    return true;
                if (_floatingScoreForm != null && !_floatingScoreForm.IsDisposed
                    && _floatingScoreForm.IsHandleCreated && _floatingScoreForm.Bounds.Contains(screenPos))
                    return true;
            }
            catch
            {
                // 窗体关闭过程中访问 Bounds 可能抛异常，按"未命中自身"放行
            }
            return false;
        }

        // 判断 clickPos 是否被去重窗口拦截。_retryMode 期间用短去重(快速重试)。
        bool AutoClickIsDedupBlocked(Point clickPos)
        {
            if (_lastAutoClickPos.X == -1) return false;
            bool samePos = Math.Abs(clickPos.X - _lastAutoClickPos.X) <= AutoClickDedupTolerance
                        && Math.Abs(clickPos.Y - _lastAutoClickPos.Y) <= AutoClickDedupTolerance;
            if (!samePos) return false;
            double elapsed = (DateTime.Now - _lastAutoClickTime).TotalMilliseconds;
            // 快速重试模式(上次点击验证失败)用短窗口；否则用默认长窗口
            int window = _pendingVerifyRetry > 0 ? AutoClickRetryDedupMs : AutoClickDedupMs;
            return elapsed < window;
        }

        void AutoClickLoop()
        {
            while (Running)
            {
                try
                {
                    if (!Settings.AutoClick)
                    {
                        Thread.Sleep(500);
                        continue;
                    }
                    bool clicked = false;
                    Bitmap screen = Screenshot();
                    try
                    {
                        // —— 阶段A：优先处理关闭类(Close)弹窗 ——
                        // 关掉"看视频领奖励"这类上层小弹窗，露出下层的"再来一局"。
                        Bitmap closeHit = null;
                        Point closePos;
                        AutoClickFindMatch(screen, AutoClickCloseList, out closeHit, out closePos);

                        if (closeHit != null)
                        {
                            Point clickPos = new Point(closePos.X + closeHit.Width / 2, closePos.Y + closeHit.Height / 2);
                            if (AutoClickIsDedupBlocked(clickPos))
                            {
                                LogHelper.Info("[AutoClickLoop][Close] 命中但去重跳过 "
                                    + closeHit.Width + "x" + closeHit.Height + " @px" + clickPos.X + "," + clickPos.Y
                                    + " retry=" + _pendingVerifyRetry);
                            }
                            else if (AutoClickHitSelf(clickPos))
                            {
                                LogHelper.Info("[AutoClickLoop][Close] 命中但跳过(落在自身窗口内) "
                                    + closeHit.Width + "x" + closeHit.Height + " @px" + clickPos.X + "," + clickPos.Y);
                            }
                            else
                            {
                                MouseLeftClick(clickPos.X, clickPos.Y);
                                _lastAutoClickPos = clickPos;
                                _lastAutoClickTime = DateTime.Now;
                                // 记录待验证目标，下一轮验证它是否消失
                                _pendingVerifyPos = closePos;
                                _pendingVerifySize = new Size(closeHit.Width, closeHit.Height);
                                _pendingVerifyTemplate = closeHit;
                                _pendingVerifyRetry = 0;
                                LogHelper.Info("[AutoClickLoop][Close] 命中并点击 "
                                    + closeHit.Width + "x" + closeHit.Height + " @px" + clickPos.X + "," + clickPos.Y);
                                clicked = true;
                            }
                        }
                        else
                        {
                            // —— 阶段B：无关闭类弹窗时，处理确认类(Confirm) ——
                            Bitmap confirmHit = null;
                            Point confirmPos;
                            AutoClickFindMatch(screen, AutoClickConfirmList, out confirmHit, out confirmPos);
                            if (confirmHit != null)
                            {
                                Point clickPos = new Point(confirmPos.X + confirmHit.Width / 2, confirmPos.Y + confirmHit.Height / 2);
                                if (AutoClickIsDedupBlocked(clickPos))
                                {
                                    LogHelper.Info("[AutoClickLoop][Confirm] 命中但去重跳过 "
                                        + confirmHit.Width + "x" + confirmHit.Height + " @px" + clickPos.X + "," + clickPos.Y
                                        + " retry=" + _pendingVerifyRetry);
                                }
                                else if (AutoClickHitSelf(clickPos))
                                {
                                    LogHelper.Info("[AutoClickLoop][Confirm] 命中但跳过(落在自身窗口内) "
                                        + confirmHit.Width + "x" + confirmHit.Height + " @px" + clickPos.X + "," + clickPos.Y);
                                }
                                else
                                {
                                    MouseLeftClick(clickPos.X, clickPos.Y);
                                    _lastAutoClickPos = clickPos;
                                    _lastAutoClickTime = DateTime.Now;
                                    _pendingVerifyPos = confirmPos;
                                    _pendingVerifySize = new Size(confirmHit.Width, confirmHit.Height);
                                    _pendingVerifyTemplate = confirmHit;
                                    _pendingVerifyRetry = 0;
                                    LogHelper.Info("[AutoClickLoop][Confirm] 命中并点击 "
                                        + confirmHit.Width + "x" + confirmHit.Height + " @px" + clickPos.X + "," + clickPos.Y);
                                    clicked = true;
                                }
                            }
                            else if (AutoClickImageList.Count > 0 && AutoClickCloseList.Count == 0 && AutoClickConfirmList.Count == 0)
                            {
                                // 向后兼容：旧部署只有平级 AutoClickImageList(无角色分类)，
                                // 退回原始行为——按面积升序匹配，命中即点。
                                Bitmap legacyHit = null;
                                Point legacyPos;
                                AutoClickFindMatch(screen, AutoClickImageList, out legacyHit, out legacyPos);
                                if (legacyHit != null)
                                {
                                    Point clickPos = new Point(legacyPos.X + legacyHit.Width / 2, legacyPos.Y + legacyHit.Height / 2);
                                    if (!AutoClickIsDedupBlocked(clickPos) && !AutoClickHitSelf(clickPos))
                                    {
                                        MouseLeftClick(clickPos.X, clickPos.Y);
                                        _lastAutoClickPos = clickPos;
                                        _lastAutoClickTime = DateTime.Now;
                                        _pendingVerifyPos = legacyPos;
                                        _pendingVerifySize = new Size(legacyHit.Width, legacyHit.Height);
                                        _pendingVerifyTemplate = legacyHit;
                                        _pendingVerifyRetry = 0;
                                        LogHelper.Info("[AutoClickLoop][Legacy] 命中并点击 "
                                            + legacyHit.Width + "x" + legacyHit.Height + " @px" + clickPos.X + "," + clickPos.Y);
                                        clicked = true;
                                    }
                                }
                            }
                        }

                        // —— 点击后验证：上一轮若有待验证目标，检查它是否消失 ——
                        if (_pendingVerifyTemplate != null && !clicked)
                        {
                            bool stillThere = AutoClickTargetStillThere(screen, _pendingVerifyTemplate, _pendingVerifyPos);
                            if (stillThere)
                            {
                                _pendingVerifyRetry++;
                                if (_pendingVerifyRetry <= AutoClickMaxRetry)
                                {
                                    // 目标还在=上次点击无效。_pendingVerifyRetry>0 会让下轮
                                    // AutoClickIsDedupBlocked 改用 2 秒短窗口(AutoClickRetryDedupMs)，
                                    // 该位置得以快速重试。这是打破"点错一次锁死15秒"死循环的关键。
                                    LogHelper.Info("[AutoClickLoop][Verify] 目标未消失,缩短去重快速重试 retry="
                                        + _pendingVerifyRetry + " @px" + _pendingVerifyPos.X + "," + _pendingVerifyPos.Y);
                                }
                                else
                                {
                                    // 超过重试上限，放弃该目标，清待验证状态，恢复正常去重
                                    LogHelper.Info("[AutoClickLoop][Verify] 目标多次未消失,放弃重试 @px"
                                        + _pendingVerifyPos.X + "," + _pendingVerifyPos.Y);
                                    _pendingVerifyTemplate = null;
                                    _pendingVerifyRetry = 0;
                                }
                            }
                            else
                            {
                                // 目标消失=点击成功，清待验证状态
                                if (_pendingVerifyRetry > 0)
                                {
                                    LogHelper.Info("[AutoClickLoop][Verify] 重试后目标消失(成功) retry=" + _pendingVerifyRetry);
                                }
                                _pendingVerifyTemplate = null;
                                _pendingVerifyRetry = 0;
                            }
                        }
                    }
                    finally
                    {
                        screen.Dispose();
                    }
                    // 命中后等界面稳定再进入下一轮；未命中则按常规节拍继续
                    Thread.Sleep(clicked ? AutoClickCooldownMs : AutoClickIntervalMs);
                }
                catch(Exception ex)
                {
                    LogHelper.Error(ex, "AutoClickLoop");
                }
            }
        }

        void DetectionLoop()
        {
            int getHandleCount = 0;
            bool randomTransform = false;
            int invalidCount = 0;
            bool modelChanged = false;
            while (Running)
            {
                DateTime startTime = DateTime.Now;
                try
                {
                    if (GameHandle == IntPtr.Zero && Settings.SelectedSolution != "剪切板")
                    {
                        ModeDisplay("未找到窗体");
                        if (Settings.SelectedSolution != "")
                        {
                            getHandleCount++;
                            if (getHandleCount % 10 == 0)
                            {
                                getHandleCount = 0;
                                LoadCurrentSolution();
                            }
                        }
                        Thread.Sleep(100);
                        continue;
                    }
                    else
                    {
                        if (DetectEnabled)
                        {
                            ModeDisplay("识别中");
                        }
                        else
                        {
                            ModeDisplay("空闲");
                        }
                    }
                    if (checkBox_debug.Checked)
                    {
                        this.Invoke(new Action(() =>
                        {
                            var oldImg = pictureBox_show_result.Image;
                            pictureBox_show_result.Image = Screenshot();
                            oldImg?.Dispose();
                            pictureBox_show_result.Refresh();
                        }));
                        Thread.Sleep(200);
                        continue;
                    }
                    if (!Settings.BackgroundAnalysis && !BackgroundAnalyzing && Engine.AnalyzeQueue.Count > 0)
                    {
                        DisplayStatus("引擎思考中……识别暂停");
                        Thread.Sleep(50);
                        continue;
                    }
                    Bitmap screenshot = Screenshot();
                     try
                     {
                         Size maxSize = screenshot.Size;
                         if (LastBoardAreaBitmap != null && BoardArea.X != -1)
                         {
                             using (Bitmap currentBoard = (Bitmap)ImageHelper.CropImage(screenshot, BoardArea))
                             {
                                 DisplayStatus("检测图像");
                                 bool result = ModelDetectBoard(currentBoard, false);
                                 if (result)
                                 {
                                     var oldBitmap = LastBoardAreaBitmap;
                                     LastBoardAreaBitmap = (Bitmap)currentBoard.Clone();
                                     oldBitmap?.Dispose();
                                     bool checkValid = Utils.CheckBoardValid(PendingBoard, RedSide);
                                     if (!checkValid)
                                     {
                                         DisplayStatus("当前棋盘不合法，可能是出现了识别错误！");
                                         invalidCount++;
                                         if (invalidCount > 5)
                                         {
                                             if (ModelList.Count > 1 && !modelChanged)
                                             {
                                                 foreach (var model in ModelList)
                                                 {
                                                     if (model.Key != Settings.YoloModel)
                                                     {
                                                         Model = model.Value;
                                                         modelChanged = true;
                                                         DisplayStatus("尝试切换模型识别");
                                                         break;
                                                     }
                                                 }
                                             }
                                             else
                                             {
                                                 randomTransform = true;
                                             }
                                             invalidCount = 0;
                                             ResetDetection();
                                         }
                                         Thread.Sleep(300);
                                     }
                                     else
                                     {
                                         invalidCount = 0;
                                         bool checkResult = CheckNewBoard();
                                         if (checkResult)
                                         {
                                             if (modelChanged)
                                             {
                                                 Model = ModelList[Settings.YoloModel];
                                                 modelChanged = false;
                                             }
                                             ReloadBoard();
                                         }
                                     }
                                 }
                                 else
                                 {
                                     BoardArea = new Rectangle(-1, -1, -1, -1);
                                     Thread.Sleep(200);
                                 }
                             }
                         }
                         else
                         {
                             DisplayStatus("识别不到棋盘，重置图像范围");
                             bool result = ModelDetectBoard(screenshot, true);
                             if (result)
                             {
                                 BoardArea = Utils.ExpendArea(Utils.RestoreArea(BoardArea, BoardAreaRaw), maxSize);
                                 var oldBitmap = LastBoardAreaBitmap;
                                 LastBoardAreaBitmap = (Bitmap)ImageHelper.CropImage(screenshot, BoardArea);
                                 oldBitmap?.Dispose();
                                 DisplayStatus("更新范围成功");
                             }
                         }
                     }
                     finally
                     {
                         screenshot.Dispose();
                     }

                }
                catch (Exception ex)
                {
                    LogHelper.Error(ex, "DetectionLoop");
                }
                DateTime endTime = DateTime.Now;
                double miliSec = (endTime - startTime).TotalMilliseconds;
                Debug.WriteLine($"总耗时: {miliSec}ms");
                if (miliSec < Settings.DetectionInterval)
                {
                    // 以 50ms 为粒度分片等待，程序退出时最多 50ms 内响应（而非整个 DetectionInterval）
                    int waitMs = (int)(Settings.DetectionInterval - miliSec);
                    while (waitMs > 0 && Running)
                    {
                        int chunk = waitMs > 50 ? 50 : waitMs;
                        Thread.Sleep(chunk);
                        waitMs -= chunk;
                    }
                }
            }
        }

        // 棋子图片缓存：避免每帧每个棋子都通过 ResourceManager.GetObject 反射查找 + 强转
        // 启动后首次渲染时惰性加载，之后直接查表
        static Dictionary<string, Bitmap> _pieceImageCache;
        static Bitmap GetPieceImage(string name)
        {
            if (_pieceImageCache == null)
            {
                _pieceImageCache = new Dictionary<string, Bitmap>();
                string[] names = { "r_bing", "r_che", "r_jiang", "r_ma", "r_pao", "r_shi", "r_xiang",
                                   "b_bing", "b_che", "b_jiang", "b_ma", "b_pao", "b_shi", "b_xiang" };
                foreach (var n in names)
                {
                    var img = Properties.Resources.ResourceManager.GetObject(n) as Bitmap;
                    if (img != null) _pieceImageCache[n] = img;
                }
            }
            Bitmap result;
            _pieceImageCache.TryGetValue(name, out result);
            return result;
        }

        // 棋盘背景图裁剪缓存：board.png 四周带一圈不透明白边(四角为纯白 R=G=B=255)，
        // 直接画会把 Clear 的木色底盖掉，形成难看的白圈。这里首次使用时扫描出非白内容的
        // 边界矩形，克隆出裁掉白边的版本缓存，之后绘制直接用裁剪版，白圈消失。
        // 用动态扫描而非硬编码边距，换图时自适应。
        static Bitmap _boardImageTrimmed;
        static Bitmap GetTrimmedBoardImage()
        {
            if (_boardImageTrimmed != null) return _boardImageTrimmed;
            Bitmap src = Properties.Resources.board;
            if (src == null) return null;
            int w = src.Width, h = src.Height;
            int minX = w, minY = h, maxX = 0, maxY = 0;
            // 步长4快速扫描边界(棋盘内容区域大，粗扫足够定位白边)
            for (int x = 0; x < w; x += 4)
            {
                for (int y = 0; y < h; y += 4)
                {
                    Color c = src.GetPixel(x, y);
                    // 非纯白即视为内容(阈值248容忍极浅木色边缘)
                    if (c.R < 248 || c.G < 248 || c.B < 248)
                    {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            // 扫描边界向内收2像素保险(粗扫可能落在边界像素上)，并夹紧到合法范围
            minX = Math.Max(0, minX - 0);
            minY = Math.Max(0, minY - 0);
            maxX = Math.Min(w - 1, maxX);
            maxY = Math.Min(h - 1, maxY);
            if (maxX <= minX || maxY <= minY)
            {
                _boardImageTrimmed = src; // 退化：扫描失败则用原图
                return _boardImageTrimmed;
            }
            var rect = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
            _boardImageTrimmed = src.Clone(rect, src.PixelFormat);
            return _boardImageTrimmed;
        }

        // 主窗示意棋盘的渲染度量：把裁剪后的 board.png 画进 400×440 位图后，
        // 棋盘交叉点的真实像素坐标。旧代码假设格距=40、首交叉点=20，棋子整体偏左上、
        // 越往右下累积偏移越大且纵向出框。
        // 这里用实测精确值(probe2 工具，最小二乘拟合棋盘线)：
        //   裁剪后 board.png(629×689)画进 400×440，棋盘首交叉点在 (27,27)、格距 43。
        //   水平/垂直首线都是 27(棋盘左右上下留白对称，互证)，垂直拟合误差仅1px。
        //   棋盘范围 X:27~371(8格×43=344)、Y:27~414(9格×43=387)，均在 400×440 内。
        // 结果缓存。
        struct BoardRenderMetrics
        {
            public int Grid;           // 交叉点间距(像素)
            public int OriginX;        // 第一个(左上)交叉点 x
            public int OriginY;        // 第一个(左上)交叉点 y
            public int PieceSize;      // 棋子绘制尺寸(像素)
            public int BoardImageWidth;  // 棋盘背景图画成的宽
            public int BoardImageHeight; // 棋盘背景图画成的高
        }
        static BoardRenderMetrics? _boardMetricsCache;
        static BoardRenderMetrics GetBoardRenderMetrics()
        {
            if (_boardMetricsCache.HasValue) return _boardMetricsCache.Value;
            var m = new BoardRenderMetrics
            {
                Grid = 43,
                OriginX = 27,
                OriginY = 27,
                PieceSize = 40,            // 棋子略小于格距(43)，留 3px 缝隙视觉清晰
                BoardImageWidth = 400,     // 与 BoardDisplayBitmap 宽一致，棋盘图铺满
                BoardImageHeight = 440     // 与 BoardDisplayBitmap 高一致
            };
            _boardMetricsCache = m;
            return m;
        }

        public void RenderDisplayBoard(string[,] renderBoard=null)
        {
            if (renderBoard == null) renderBoard = CurrentBoard;
            if (renderBoard == null) return;
            // 程序关闭过程中可能被引擎后台线程触发，避免对已释放窗体 Invoke 导致崩溃
            if (IsDisposed || !IsHandleCreated) return;
            this.Invoke(new Action(() =>
            {
                // 主窗示意棋盘的渲染坐标系，经实测校准（详见 GetBoardRenderMetrics）。
                // 旧实现用 width=height=40 当格距又当棋子尺寸、xoffset=yoffset=20 当首格偏移，
                // 但裁剪后的 board.png 被画进 400×440 后实际格距约 44px、首交叉点在 ~(22,21)，
                // 旧参数导致棋子整体偏左上且越往右下累积偏移越多，棋子没落在格子正中。
                // 这里改用真实测得的格距/原点，棋子按"中心对齐交叉点"绘制。
                var m = GetBoardRenderMetrics();
                int grid = m.Grid;          // 交叉点间距(像素)
                int originX = m.OriginX;    // 第一个(左上)交叉点的 x
                int originY = m.OriginY;    // 第一个(左上)交叉点的 y
                int pieceW = m.PieceSize;   // 棋子绘制尺寸
                int pieceHalf = pieceW / 2;
                int boardImgW = m.BoardImageWidth;   // 棋盘背景图画成的宽
                int boardImgH = m.BoardImageHeight;  // 棋盘背景图画成的高
                // 用木色底代替白底：board.png 未铺满时露边，木色与棋盘浑然一体。
                // 关键：用裁掉白边的棋盘图(GetTrimmedBoardImage)，board.png 原图四周带
                // 不透明白边，直接画会把木色底盖掉形成白圈。裁剪后内容区画满，白圈消失。
                BoardGDI.Clear(WoodBg);
                Bitmap boardImg = GetTrimmedBoardImage();
                if (boardImg != null)
                {
                    BoardGDI.DrawImage(boardImg, 0, 0, boardImgW, boardImgH);
                }
                for (int y = 0; y < 10; y++)
                {
                    for (int x = 0; x < 9; x++)
                    {
                        string cell = renderBoard[x, y];
                        if (cell != null && cell != "")
                        {
                            Bitmap pieceImg = GetPieceImage(cell);
                            if (pieceImg != null)
                            {
                                // 棋子中心对齐交叉点：左上角 = 交叉点 - 半径
                                int px = originX + x * grid - pieceHalf;
                                int py = originY + y * grid - pieceHalf;
                                BoardGDI.DrawImage(pieceImg, px, py, pieceW, pieceW);
                            }
                        }
                    }
                }
                if (BestMove.From.X != -1)
                {
                    Point bestFrom = new Point(originX + BestMove.From.X * grid, originY + BestMove.From.Y * grid);
                    Point bestTo = new Point(originX + BestMove.To.X * grid, originY + BestMove.To.Y * grid);
                    BoardGDI.DrawLine(RedSide ? ArrowRedPen : ArrowBluePen, bestFrom, bestTo);
                }
                if (PonderMove.From.X != -1)
                {
                    Point ponderFrom = new Point(originX + PonderMove.From.X * grid, originY + PonderMove.From.Y * grid);
                    Point ponderTo = new Point(originX + PonderMove.To.X * grid, originY + PonderMove.To.Y * grid);
                    BoardGDI.DrawLine(RedSide ? ArrowBluePen : ArrowRedPen, ponderFrom, ponderTo);
                }
                if (ShowTwoSteps && Move3.From.X != -1)
                {
                    Point m3From = new Point(originX + Move3.From.X * grid, originY + Move3.From.Y * grid);
                    Point m3To = new Point(originX + Move3.To.X * grid, originY + Move3.To.Y * grid);
                    BoardGDI.DrawLine(RedSide ? ArrowRedDimPen : ArrowBlueDimPen, m3From, m3To);
                }
                if (ShowTwoSteps && Move4.From.X != -1)
                {
                    Point m4From = new Point(originX + Move4.From.X * grid, originY + Move4.From.Y * grid);
                    Point m4To = new Point(originX + Move4.To.X * grid, originY + Move4.To.Y * grid);
                    BoardGDI.DrawLine(RedSide ? ArrowBlueDimPen : ArrowRedDimPen, m4From, m4To);
                }
                if (BackgroundAnalysisMove.From.X != -1)
                {
                    Point bgFrom = new Point(originX + BackgroundAnalysisMove.From.X * grid, originY + BackgroundAnalysisMove.From.Y * grid);
                    Point bgTo = new Point(originX + BackgroundAnalysisMove.To.X * grid, originY + BackgroundAnalysisMove.To.Y * grid);
                    BoardGDI.DrawLine(RedSide ? ArrowRedDimPen : ArrowBlueDimPen, bgFrom, bgTo);
                }

                // 分数条：贴位图底端绘制。棋盘最下排棋子(y=9)底边在 originY+9*grid+pieceHalf
                // = 27+387+20 = 434，旧值 barY=428 会压住下排棋子。改为贴底并减薄，
                // barY=433/barH=7(占433~440)仅与最下排棋子底部约1px重叠，视觉不再挡棋子。
                int barY = boardImgH - 7;
                int barH = 7;
                int barW = boardImgW;
                int segW = barW / 10;
                int midX = barW / 2;
                BoardGDI.FillRectangle(Brushes.Red, 0, barY, midX, barH);
                BoardGDI.FillRectangle(Brushes.Black, midX, barY, barW - midX, barH);
                for (int i = 1; i < 10; i++)
                {
                    BoardGDI.DrawLine(Pens.White, i * segW, barY, i * segW, barY + barH);
                }
                if (label_score.Text != "--" && int.TryParse(label_score.Text, out int absScore))
                {
                    float ratio = Math.Min(absScore / 3000f, 1.0f);
                    int offset = (int)(midX * ratio);
                    // 用分数正负判断优势方，不依赖 label_score.ForeColor
                    // (换肤后 ForeColor 是 WoodScoreRed 而非纯 Color.Red，原判断会恒为 false)
                    bool redAdvantage = label_score.ForeColor == WoodScoreRed;
                    if (redAdvantage)
                        BoardGDI.FillRectangle(Brushes.Red, midX, barY, offset, barH);
                    else
                        BoardGDI.FillRectangle(Brushes.Black, midX - offset, barY, offset, barH);
                    for (int i = 1; i < 10; i++)
                    {
                        BoardGDI.DrawLine(Pens.White, i * segW, barY, i * segW, barY + barH);
                    }
                }
                pictureBox_board.Refresh();
            }));
        }

        int StableDetectionCount = 0;
        int ClickRetryCount = 0;
        int AutoGoFailingCheckCount = 0;
        // 用于检测是否稳定的棋盘，当有棋盘变化时，在第一次变化时会置该棋盘
        string[,] CheckingBoard = new string[9, 10];
        // 每次检测棋盘后这个函数会被调用
        bool CheckNewBoard()
        {
            var cmpResult = Utils.CompareBoard(LastBoard, PendingBoard);
            // [诊断] 只在 DiffCount > 0 时记录，避免刷屏
            if (cmpResult.DiffCount > 0)
            {
                LogHelper.Info("[CheckNewBoard] Diff=" + cmpResult.DiffCount + " Stable=" + StableDetectionCount
                    + " ConfirmN=" + Settings.DetectionConfirmCount + " ChangeDetected=" + ChangeDetectedAfterClick);
            }
            if (cmpResult.DiffCount > 0)
            {
                if (ChangeDetectedAfterClick == false)
                {
                    BestMove = new ChessMove(new Point(-1, -1), new Point(-1, -1));
                    ChangeDetectedAfterClick = true;
                    AutoGoFailingCheckCount = 0;
                    ClickRetryCount = 0;
                }

                foreach (var expectedBoard in ExpectedBoardMap)
                {
                    var cmpExpected = Utils.CompareBoard(expectedBoard.Value, PendingBoard);
                    if (cmpExpected.DiffCount == 0)
                    {
                        DisplayStatus("棋盘发生变化 为预期棋盘 跳过确认");
                        CurrentBoard = (string[,])PendingBoard.Clone();
                        return true;
                    }
                }
               
                var cmpChecking = Utils.CompareBoard(CheckingBoard, PendingBoard);
                if (cmpChecking.DiffCount == 0)
                {
                    StableDetectionCount++;
                    DisplayStatus("棋盘发生变化 第" + StableDetectionCount + "次确认");
                    if (StableDetectionCount >= Settings.DetectionConfirmCount) // 三次检测相同
                    {
                        CurrentBoard = (string[,])PendingBoard.Clone();
                        return true;
                    }
                    return false;
                }
                else
                {
                    StableDetectionCount = 0;
                    CheckingBoard = (string[,])PendingBoard.Clone();
                    DisplayStatus("棋盘发生变化 等待确认");
                    return false;
                }
            }
            else
            {
                DisplayStatus("棋盘未改变");
                StableDetectionCount = 0;
                // 处理可能未完成的点击，允许多点2次。
                // 关键守卫：只有当 ExpectedMove 起点的本方棋子【仍在 PendingBoard 原位】时，
                // 才说明点击真的没生效，可以重试。若起点棋子已不在原位，说明点击其实已生效
                // (棋子已移走)，只是当前帧棋盘变化未被确认——此时绝不能重试，否则会在对方
                // 思考时反复点击本方棋子。
                // 关键：模仿人类延时期间棋子还没动是正常的，绝不能误判为"落子失败"而抢先点击，
                // 否则会绕过延时直接落子(导致节奏错乱)，并被人为干预检测误判放弃本次落子。
                if (Settings.AutoGo && !ChangeDetectedAfterClick && IsExpectedMoveStillPending() && !_mimicDelaying)
                {
                    AutoGoFailingCheckCount++;
                    if (AutoGoFailingCheckCount >= 5)
                    {
                        if (ClickRetryCount < 2)
                        {
                            AutoGoFailingCheckCount = 0;
                            ClickRetryCount++;
                            DisplayStatus("检测到可能的落子失败，重试");
                            LogHelper.Info("[CheckNewBoard] 落子失败重试 retryCount=" + ClickRetryCount + " move=" + ExpectedMove);
                            PlayChess(ExpectedMove);
                        }
                        else
                        {
                            ClickRetryCount = 0;
                            ExpectedMove = ""; // 放弃重试，清空预期着法，避免后续误触发
                            ResetDetection();
                        }
                    }
                }
                else if (Settings.AutoGo && !ChangeDetectedAfterClick && !IsExpectedMoveStillPending())
                {
                    // 起点棋子已不在原位 → 点击已生效，只是变化未被确认。
                    // 停止重试计数，等待识别跟上；不清 ExpectedMove(ReloadBoard 确认后会清)。
                    AutoGoFailingCheckCount = 0;
                }
                return false;
            }
        }

        // 判断 ExpectedMove(上一手本方着法)的起点棋子是否仍停留在 PendingBoard 原位。
        // 用于区分"点击真没生效"(棋子还在，应重试) 与 "点击生效但未确认"(棋子已走，勿重试)。
        // ExpectedMove 为空或格式不合法时返回 false，保守地不触发重试。
        bool IsExpectedMoveStillPending()
        {
            if (string.IsNullOrEmpty(ExpectedMove) || ExpectedMove.Length != 4) return false;
            try
            {
                Point from = Utils.Move2Point(ExpectedMove.Substring(0, 2), RedSide);
                if (from.X < 0 || from.X > 8 || from.Y < 0 || from.Y > 9) return false;
                string piece = PendingBoard[from.X, from.Y];
                if (string.IsNullOrEmpty(piece)) return false; // 起点已空 → 已走
                string myPrefix = RedSide ? "r_" : "b_";
                return piece.StartsWith(myPrefix); // 起点仍是本方棋子 → 还在原位
            }
            catch
            {
                return false;
            }
        }

        bool ModelDetectBoard(Bitmap image, bool refreshBoard)
        {
            var predictions = ModelPredict(image);
            return GetBoardFromPrediction(predictions, refreshBoard);
        }

        bool GetBoardFromPrediction(List<YoloPrediction> predictions, bool refreshBoard = false)
        {
            RectangleF board = new Rectangle(-1, -1, -1, -1);
            int totalChessmanCount = 0;
            double totalChessmanWidth = 0, totalChessmanHeight = 0;
            foreach (var prediction in predictions)
            {
                if (prediction.Label.Name == "board")
                {
                    board = prediction.Rectangle;
                }
            }
            foreach (var prediction in predictions)
            {
                if (prediction.Label.Name != "board")
                {
                    double chessRatio = prediction.Rectangle.Width / prediction.Rectangle.Height;
                    if (chessRatio > 1.3 || chessRatio < 0.7) continue;
                    totalChessmanCount++;
                    totalChessmanWidth += prediction.Rectangle.Width;
                    totalChessmanHeight += prediction.Rectangle.Height;
                }
            }
            List<YoloPrediction> validPredictions = new List<YoloPrediction>();
            foreach (var prediction in predictions)
            {
                if (prediction.Label.Name == "board" || prediction.Label.Name == "obstacle") continue;
                double chessRatio = prediction.Rectangle.Width / prediction.Rectangle.Height;
                if (chessRatio > 1.3 || chessRatio < 0.7) continue;
                var rect = prediction.Rectangle;
                if (board.Contains(rect.X, rect.Y) || board.Contains(rect.Right, rect.Top) || board.Contains(rect.Left, rect.Bottom) || board.Contains(rect.Right, rect.Bottom))
                {
                    validPredictions.Add(prediction);
                }
            }
            double minDistense = int.MaxValue;
            for (int i = 0; i < validPredictions.Count; i++)
            {
                var prediction = validPredictions[i];
                for (int j = i + 1; j < validPredictions.Count; j++)
                {
                    double d = Math.Sqrt(Math.Pow((prediction.Rectangle.X - validPredictions[j].Rectangle.X), 2) + Math.Pow((prediction.Rectangle.Y - validPredictions[j].Rectangle.Y), 2));
                    if (d < minDistense) minDistense = d;
                }
            }
            double minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0;
            foreach (var prediction in validPredictions)
            {
                float centerX = prediction.Rectangle.X + prediction.Rectangle.Width / 2;
                float centerY = prediction.Rectangle.Y + prediction.Rectangle.Height / 2;
                if (centerX < minX) minX = centerX;
                if (centerY < minY) minY = centerY;
                if (centerX > maxX) maxX = centerX;
                if (centerY > maxY) maxY = centerY;
            }
            double boardWidth = maxX - minX;
            double boardHeight = maxY - minY;
            float gridWidth = board.Width / 8f;
            float gridHeight = board.Height / 9f;

            if (boardHeight >= minDistense * 8.6 && boardWidth >= minDistense * 7.6)
            {
                board.Width = (int)boardWidth;
                board.Height = (int)boardHeight;
                board.X = (int)minX;
                board.Y = (int)minY;
                gridWidth = (int)boardWidth / 8;
                gridHeight = (int)boardHeight / 9;
                // 仅在调试状态下绘制检测框可视化（YoloGDI 在非调试时为 null，避免 NRE）
                if (ScreenDebug && YoloGDI != null)
                {
                    YoloGDI.DrawRectangle(Pens.Blue, board.X, board.Y, board.Width, board.Height);
                    this.Invoke(new Action(() => {
                        pictureBox_show_result.Refresh();
                    }));
                }
            }

            double avgChessmanWidth = totalChessmanWidth / totalChessmanCount;
            double avgChessmanHeight = totalChessmanHeight / totalChessmanCount;
            string[,] tmpBoard = new string[9, 10];
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 9; x++)
                {
                    tmpBoard[x, y] = null;
                }
            }
            if (board.X == -1) return false;
            if (board.Width / board.Height > 1.3 || board.Width / board.Height < 0.7) return false;
            if (board.Width < avgChessmanWidth * 7 || board.Height < avgChessmanHeight * 8) return false;
            if (refreshBoard)
            {
                BoardAreaRaw = new Rectangle((int)board.X, (int)board.Y, (int)board.Width, (int)board.Height);
            }

            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 9; x++)
                {
                    double pos_x = board.X + x * gridWidth;
                    double pos_y = board.Y + y * gridHeight;
                    ClickPositionMap[x, y] = new Point((int)pos_x + BoardArea.X, (int)pos_y + BoardArea.Y);
                }
            }

            foreach (var prediction in predictions)
            {
                if (prediction.Label.Name == "board" || prediction.Label.Name == "obstacle") continue;
                double chessRatio = prediction.Rectangle.Width / prediction.Rectangle.Height;
                if (chessRatio > 1.3 || chessRatio < 0.7) continue;
                float centerX = prediction.Rectangle.X + prediction.Rectangle.Width / 2;
                float centerY = prediction.Rectangle.Y + prediction.Rectangle.Height / 2;
                float offsetX = centerX - board.X; // offset from the board
                float offsetY = centerY - board.Y;
                int xPos = (int)Math.Round(offsetX / gridWidth);
                int yPos = (int)Math.Round(offsetY / gridHeight);
                if (xPos >= 0 && xPos <= 8 && yPos >= 0 && yPos <= 9)
                {
                    tmpBoard[xPos, yPos] = prediction.Label.Name;
                }

                if (prediction.Label.Name == "r_jiang")
                {
                    if (yPos < 5)
                    {
                       RedSide = false;
                    }
                    else
                    {
                       RedSide = true;
                    }
                }
            }
            PendingBoard = tmpBoard;
            return true;
        }

        static readonly Font YoloLabelFont = new Font("Arial", 16, GraphicsUnit.Pixel);
        static readonly Brush YoloLabelBrush = new SolidBrush(Color.Red);
        static readonly Brush YoloCenterBrush = Brushes.Lime;
        static readonly Pen YoloRectPen = new Pen(Color.Red, 1);

        List<YoloPrediction> ModelPredict(Bitmap image)
        {
            // 模型推理（核心耗时部分）
            DateTime st = DateTime.Now;
            List<YoloPrediction> predictions = Model.Predict(image);
            Debug.WriteLine("模型耗时: " + Math.Round((DateTime.Now - st).TotalSeconds, 2).ToString() + "s");

            // 可视化绘制仅在调试状态下进行（pictureBox_show_result 默认隐藏）
            // 非调试状态下跳过整张图复制 + GDI 重建 + 逐框绘制，每帧省 10-30%
            if (!ScreenDebug)
            {
                return predictions;
            }

            // dispose previous GDI resources to prevent handle leak
            YoloGDI?.Dispose();
            YoloDisplayBitmap?.Dispose();

            YoloDisplayBitmap = new Bitmap(image.Width, image.Height);
            YoloGDI = Graphics.FromImage(YoloDisplayBitmap);
            YoloGDI.DrawImage(image, 0, 0);
            foreach (var prediction in predictions)
            {
                YoloGDI.DrawRectangles(YoloRectPen, new[] { prediction.Rectangle });

                var (x, y) = (prediction.Rectangle.X - 3, prediction.Rectangle.Y - 23);
                float centerX = prediction.Rectangle.X + prediction.Rectangle.Width / 2;
                float centerY = prediction.Rectangle.Y + prediction.Rectangle.Height / 2;
                YoloGDI.FillEllipse(YoloCenterBrush, new RectangleF(centerX - 2, centerY - 2, 4, 4));
                YoloGDI.DrawString($"{prediction.Label.Name} {Math.Round(prediction.Score, 2)}",
                    YoloLabelFont, YoloLabelBrush, new PointF(x, y));
            }
            pictureBox_show_result.Image = YoloDisplayBitmap;
            return predictions;
        }

        async void StartAnalyze(string fen)
        {
            LogHelper.Info("[StartAnalyze] 入口 fen=" + fen + " AutoGo=" + Settings.AutoGo + " EngineInit=" + (Engine != null && Engine.Initialized));
            CandidateMoves = new string[3] { "", "", "" };
            CandidateScores = new int[3] { 0, 0, 0 };
            this.Invoke(new Action(() => { label_moves.Text = ""; }));
            await Task.Run(new Action(() =>
            {
              try
              {
                // 长打(长将/长捉)检测 —— 基于"局面重复"而非"走法重复"。
                // 中国象棋规则：同一局面(双方棋子布局完全一致)重复 3 次即判长打作负。
                // 关键优势：按局面判定能解决"换着将军"——车在 b1b2↔b2b1 反复将军时走法虽变，
                // 但每次形成的局面会重复，第 3 次即触发禁招，引擎无法再用任何招回到重复局面。
                // 局面计数在 ReceiveBestMove 里(本方确定走某招后)记录，此处是下一次分析时检测。
                // 检测对象：PositionRepeatCount 里计数已达阈值的局面，把"导致该重复局面的着法"禁掉。
                bool repetitionDetected = false;
                if (BestMoveList.Count >= 1)
                {
                    string lastMove = BestMoveList[BestMoveList.Count - 1];
                    // 兼容保留：纯走法连续重复(单招长将)也立即判定，更激进地止损
                    bool moveRepeat = false;
                    if (BestMoveList.Count >= 2)
                    {
                        moveRepeat = lastMove == BestMoveList[BestMoveList.Count - 2];
                        if (!moveRepeat && BestMoveList.Count >= 3)
                        {
                            moveRepeat = lastMove == BestMoveList[BestMoveList.Count - 3];
                        }
                    }
                    // 局面重复判定：当前本方走完后的预期局面(ExpectedSelfGoBoard)是否已重复
                    bool positionRepeat = false;
                    string layoutKey = Utils.BoardLayoutKey(ExpectedSelfGoBoard);
                    if (!string.IsNullOrEmpty(layoutKey)
                        && PositionRepeatCount.ContainsKey(layoutKey)
                        && PositionRepeatCount[layoutKey] >= PositionRepeatLimit)
                    {
                        positionRepeat = true;
                    }
                    if (moveRepeat || positionRepeat)
                    {
                        // 禁招升级：局面重复时，把本方最近用过的、可能回到重复局面的着法都禁掉。
                        // 取最近若干步本方着法去重后作为 banmoves，迫使引擎真正变招。
                        // SendCommand("banmoves " + BanMoves) 是字符串拼接，可发多招(空格分隔)；
                        // 若引擎只认单招会退化为忽略，不比现状差。
                        var recentMoves = new HashSet<string>();
                        int lookBack = Math.Min(BestMoveList.Count, 4);
                        for (int i = BestMoveList.Count - lookBack; i < BestMoveList.Count; i++)
                        {
                            if (!string.IsNullOrEmpty(BestMoveList[i]) && BestMoveList[i].Length == 4)
                            {
                                recentMoves.Add(BestMoveList[i]);
                            }
                        }
                        string banStr = string.Join(" ", recentMoves);
                        Engine.BanMoves = banStr;
                        repetitionDetected = true;
                        DisplayStatus("检测到长打(局面重复)，禁招: " + banStr + " 迫使变招");
                        LogHelper.Info("[StartAnalyze] 检测到长打 moveRepeat=" + moveRepeat
                            + " positionRepeat=" + positionRepeat + " count="
                            + (PositionRepeatCount.ContainsKey(layoutKey) ? PositionRepeatCount[layoutKey].ToString() : "0")
                            + " 禁招: " + banStr);
                    }
                }
                // 长打时必须走引擎：开局库/云库命中后会直接 return，根本不会发 banmoves，
                // 禁招永远不生效。故 repetitionDetected=true 时跳过这两条路径，强制走引擎。
                if (!repetitionDetected && Settings.UseOpenBook)
                {
                    try
                    {
                        var resultList = OpenBookHelper.QueryAll(OpenBookList, fen);
                        int index = 0;
                        if (resultList.Count > 0)
                        {
                            if (Settings.OpenbookMode == ProgramSettings.OpenBookMode.Random)
                            {
                                index = Rand.Next(resultList.Count);
                            }
                            else
                            {
                                // sort 
                                resultList.Sort((q1, q2) =>
                                {
                                    return q2.Score - q1.Score;
                                });
                                index = 0;
                            }
                            var result = resultList[index];
                            DisplayStatus("命中 " + result.Book + " 开局库");
                            this.Invoke(new Action(() =>
                            {
                                if (result.Score > 20000)
                                {
                                    textBox_engine_log.SelectionColor = MateColor;
                                }
                                else if (result.Score >= 0)
                                {
                                    textBox_engine_log.SelectionColor = PositiveColor;
                                }
                                else
                                {
                                    textBox_engine_log.SelectionColor = NegativeColor;
                                }
                                textBox_engine_log.AppendText(
                                    $"开局库 {result.Book} {result.Memo}\r\n" +
                                    $"得分: {result.Score}\r\n" + Utils.FenToChina(EngineAnalyzingBoard, new string[] { result.Move },RedSide) +
                                    "\r\n"
                                    );
                                if (checkBox_auto_scroll.Checked)
                                {
                                    textBox_engine_log.ScrollToCaret();
                                }
                                EngineAnalyzeCount++;
                                ReceiveBestMove(result.Move, "", ResultSource.Openbook, fen);
                            }));
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.Invoke(new Action(() =>
                        {
                            textBox_engine_log.AppendText("查询开局库时出错: " + ex.Message);
                        }));
                    }
                }
                if (!repetitionDetected && Settings.UseChessDB)
                {
                    try
                    {
                        DisplayStatus("检索云库中");
                        var result = ChessDBHelper.GetPV(fen);
                        if (result != null)
                        {
                            DisplayStatus("命中云库");
                            this.Invoke(new Action(() =>
                            {
                                if (result.Score > 20000)
                                {
                                    textBox_engine_log.SelectionColor = MateColor;
                                }
                                else if (result.Score >= 0)
                                {
                                    textBox_engine_log.SelectionColor = PositiveColor;
                                }
                                else
                                {
                                    textBox_engine_log.SelectionColor = NegativeColor;
                                }
                                textBox_engine_log.AppendText($"云库 深度: {result.Depth} 得分: {result.Score}\r\n");
                                textBox_engine_log.SelectionColor = Color.Black;
                                textBox_engine_log.AppendText(Utils.FenToChina(EngineAnalyzingBoard, result.PV.ToArray(),RedSide));
                                textBox_engine_log.AppendText("\r\n\r\n");
                                if (checkBox_auto_scroll.Checked)
                                {
                                    textBox_engine_log.ScrollToCaret();
                                }
                                string bestMove = result.PV[0];
                                string ponderMove = "";
                                if (result.PV.Count >= 2) ponderMove = result.PV[1];
                                EngineAnalyzeCount++;
                                ReceiveBestMove(bestMove, ponderMove, ResultSource.ChessDB, fen);
                            }));
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.Invoke(new Action(() =>
                        {
                            textBox_engine_log.AppendText("查询云库时出错: " + ex.Message);
                        }));
                    }
                }

                DisplayStatus("开始引擎计算");
                Engine.StartAnalyze(fen, Settings.EngineStepTime, Settings.EngineDepth);
              }
              catch (Exception ex)
              {
                  LogHelper.Error(ex, "StartAnalyze");
              }
            }));
        }
       
        void ReloadBoard()
        {
            if (CurrentBoard == null) return;
            var compareWithLast = Utils.CompareBoard(LastBoard, CurrentBoard);
            var compareWithExpected = Utils.CompareBoard(ExpectedSelfGoBoard, CurrentBoard);
            // [诊断] 再来一盘后观察 ReloadBoard 走向：LastBoard 是否为 null、变化数、TurnToOpponent、AnalyzingMode
            LogHelper.Info("[ReloadBoard] LastNull=" + (LastBoard == null) + " ExpNull=" + (ExpectedSelfGoBoard == null)
                + " Diff=" + compareWithLast.DiffCount + " ExpDiff=" + compareWithExpected.DiffCount
                + " RedDiff=" + compareWithLast.RedDiff + " BlackDiff=" + compareWithLast.BlackDiff
                + " TurnToOppo=" + TurnToOpponent + " Analyzing=" + Settings.AnalyzingMode + " RedSide=" + RedSide);
            if (compareWithLast.DiffCount == 0) return;
            // [修复] 识别"新一局开局"：棋子只会越下越少（被吃），任一方棋子数变多
            // (RedDiff<0 或 BlackDiff<0，即上一局棋子数 < 当前) 只可能发生在"再来一盘"
            // 后从残局跳回开局。原代码动画检测只判断 >1，棋子变多的负数差值绕过了保护。
            // 这里只清掉上一局残留的预期态（避免被"和预期一样"误判跳过），
            // 然后放行到下方正常的分析路径——开局是稳定合法局面，本方该走，应当直接计算并落子。
            // 注意：不能把 LastBoard 设成新棋盘后 return，否则下一轮 PendingBoard==LastBoard
            // 会判定 DiffCount==0"棋盘未改变"，永久卡死不再分析。
            if (LastBoard != null && (compareWithLast.RedDiff < 0 || compareWithLast.BlackDiff < 0))
            {
                LogHelper.Info("[ReloadBoard] 检测到新局(棋子变多 R=" + compareWithLast.RedDiff + " B=" + compareWithLast.BlackDiff + ")，清理上局残留态后正常分析");
                DisplayStatus("检测到新一局，开始计算");
                InvalidCount = 0;
                ExpectedSelfGoBoard = null;
                ExpectedMove = "";
                ExpectedBoardMap.Clear();
                // 新局清空：上一局的走法序列在新局无意义，不清会导致新局第一手就被
                // 误判为"重复"触发禁招，同时修复棋谱导出跨局污染的问题。
                BestMoveList.Clear();
                // 长打局面计数同样跨局失效，必须清空，否则新局会沿用上局的重复计数误判。
                PositionRepeatCount.Clear();
            }
            string opponentSymbol =RedSide ? "b_" : "r_";
            string mySymbol = RedSide ? "r_" : "b_";
            Debug.WriteLine($"Diff Result: {compareWithLast.DiffCount} R: {compareWithLast.RedDiff} B: {compareWithLast.BlackDiff}");
            // 长打局面计数：吃子(任一方棋子减少)时清空。
            // 吃子后棋盘布局必然改变，之前的局面不可能再重复；清空避免计数字典无限膨胀，
            // 也防止极端边界误判。非新局(RedDiff<0/BlackDiff<0 是棋子变多的新局，上面已处理)。
            // 仅当确实有棋子被吃(RedDiff>0 或 BlackDiff>0)时清空。
            if (compareWithLast.RedDiff > 0 || compareWithLast.BlackDiff > 0)
            {
                if (PositionRepeatCount.Count > 0) PositionRepeatCount.Clear();
            }
            string startingFen = "rnbakabnr/9/1c5c1/p1p1p1p1p/9/9/P1P1P1P1P/1C5C1/9/RNBAKABNR";
            string currentFen = Utils.BoardToFen(CurrentBoard, RedSide ? "w" : "b", RedSide ? "w" : "b");
            if (currentFen.StartsWith(startingFen) && !RedSide)
            {
                TurnToOpponent = true;
            }

            if (TurnToOpponent && !Settings.AnalyzingMode)
            {
                TurnToOpponent = false;
                LastBoard = (string[,])CurrentBoard.Clone();
                ExpectedSelfGoBoard = null;
                ExpectedMove = "";
                RenderDisplayBoard();
                DisplayStatus("从对手开始，跳过当前局面");
                LogHelper.Info("[ReloadBoard] 分支: 从对手开始，跳过当前局面");
                return;
            }
            if (compareWithLast.BlackDiff > 1 || compareWithLast.RedDiff > 1)
            {
                InvalidCount++;
                LogHelper.Info("[ReloadBoard] 分支: 动画中(黑红差>1) InvalidCount=" + InvalidCount);
                DisplayStatus($"棋盘变化数异常: 黑棋消失: {compareWithLast.BlackDiff} 红棋消失:{compareWithLast.RedDiff} 判断为动画中，跳过 | 错误次数: {InvalidCount}");
                if (InvalidCount > 2)
                {
                    DisplayStatus("错误次数过多，自动重置");
                    ResetDetection();
                }
                return;
            }
            if (compareWithLast.DiffCount != 2 && compareWithExpected.DiffCount != 2 && compareWithLast.DiffCount < 10)
            {
                InvalidCount++;
                LogHelper.Info("[ReloadBoard] 分支: 变化数异常(动画) Diff=" + compareWithLast.DiffCount + " InvalidCount=" + InvalidCount);
                DisplayStatus($"棋盘变化数异常: {compareWithLast.DiffCount} 判断为动画中，跳过 | 错误次数: {InvalidCount}");
                if (InvalidCount > 2)
                {
                    DisplayStatus("错误次数过多，自动重置");
                    ResetDetection();
                }
                return;
            }
            if (compareWithLast.DiffCount == 2 && compareWithLast.Chess != null)
            {
                if (compareWithLast.Chess.Contains(mySymbol))
                {
                    RenderDisplayBoard();
                    if (Settings.AnalyzingMode)
                    {
                        InvalidCount = 0;
                        LastBoard = (string[,])CurrentBoard.Clone();
                        EngineAnalyzeCount++;
                        DisplayStatus("开始引擎计算");
                        string oppofen = Utils.BoardToFen(CurrentBoard, RedSide ? "w" : "b", RedSide ? "b" : "w");
                        EngineAnalyzingBoard = (string[,])CurrentBoard.Clone();
                        Engine.StopAnalyze();
                        StartAnalyze(oppofen);
                        LogHelper.Info("[ReloadBoard] 分支: 己方棋子变化(分析模式) fen=" + oppofen);
                    }
                    else
                    {
                        InvalidCount = 0;
                        LastBoard = (string[,])CurrentBoard.Clone();
                        DisplayStatus("己方棋子变化，跳过分析");
                        LogHelper.Info("[ReloadBoard] 分支: 己方棋子变化，跳过分析(非分析模式)");
                        ExpectedMove = "";
                    }
                    return;
                }
                else
                {
                    TurnToOpponent = false;
                }
            }
            if (compareWithExpected.DiffCount == 0 && !Settings.AnalyzingMode)
            {
                LastBoard = (string[,])CurrentBoard.Clone();
                ExpectedMove = "";
                RenderDisplayBoard();
                DisplayStatus("和预期棋盘一样，跳过");
                LogHelper.Info("[ReloadBoard] 分支: 和预期棋盘一样，跳过");
                return;
            }
            if ((compareWithLast.BlackDiff > 1 || compareWithLast.RedDiff > 1) && compareWithLast.DiffCount < 10)
            {
                InvalidCount++;
                DisplayStatus($"差别过大，可能有动画，跳过 | 错误次数: {InvalidCount}");
                LogHelper.Info("[ReloadBoard] 分支: 差别过大可能有动画 InvalidCount=" + InvalidCount);
                if (InvalidCount > 2)
                {
                    DisplayStatus("错误次数过多，自动重置");
                    LogHelper.Info("[ReloadBoard] 错误次数过多 → ResetDetection");
                    ResetDetection();
                }
                return;
            }
            RenderDisplayBoard();
            InvalidCount = 0;
            EngineAnalyzeCount++;
            LastBoard = (string[,])CurrentBoard.Clone();
            LogHelper.Info("[ReloadBoard] 分支: 进入正常分析路径(走到这里才会触发引擎)");

            if (Settings.AnalyzingMode)
            {
                InvalidCount = 0;
                string analyzeFen;
                if (TurnToOpponent)
                {
                    analyzeFen = Utils.BoardToFen(CurrentBoard, RedSide ? "w" : "b", RedSide ? "b" : "w");
                }
                else
                {
                    analyzeFen = Utils.BoardToFen(CurrentBoard, RedSide ? "w" : "b", RedSide ? "w" : "b");
                }
                Engine.StopAnalyze();
                EngineAnalyzingBoard = (string[,])CurrentBoard.Clone();
                StartAnalyze(analyzeFen);
                return;
            }

            string fen = Utils.BoardToFen(CurrentBoard,RedSide ? "w" : "b",RedSide ? "w" : "b");
            if (BackgroundAnalyzing)
            {
                BackgroundAnalyzing = false;
                var compareWithPonder = Utils.CompareBoard(PonderBoard, CurrentBoard);
                if (compareWithPonder.DiffCount == 0)
                {
                    DisplayStatus("后台思考命中");
                    Engine.PonderHit();
                }
                else
                {
                    DisplayStatus("开始引擎计算");
                    Engine.PonderMiss(Utils.BoardToFen(PonderBoard, RedSide));
                    EngineAnalyzingBoard = (string[,])CurrentBoard.Clone();
                    StartAnalyze(fen);
                }
            }
            else
            {
                EngineAnalyzingBoard = (string[,])CurrentBoard.Clone();
                StartAnalyze(fen);
            }
        }

        private void Engine_InfoEvent(string cmd, Dictionary<string, string> infos)
        {
            // 引擎在后台线程持续输出 info，程序关闭时引擎可能仍在输出，
            // 此时 this.Invoke 会抛 ObjectDisposedException(无法访问已释放的 Mainform)。
            // 窗体已释放或句柄未创建时直接放弃这次更新，绝不让回调把主程序搞崩。
            if (IsDisposed || !IsHandleCreated) return;

            string info_str = "";
            if (infos.ContainsKey("score"))
            {
                //this.Invoke(new Action(() =>
                //{
                //    progressBar_score.Value = int.Parse(infos["score"]);
                //}));
                if (Settings.StopWhenMate && infos.ContainsKey("pv"))
                {
                    int currScore = 0;
                    if (infos["score"].Contains("绝杀") && int.Parse(infos["score"].Split('(').Last().Split(')').First()) > 0)
                    {
                        if (FirstMate)
                        {
                            FirstMate = false;
                            string fen = Utils.BoardToFen(CurrentBoard, RedSide);
                            Engine.StopAnalyzeAndSkip(fen);
                            string[] moves = infos["pv"].Split(' ');
                            string ponderMove = "";
                            if (moves.Length >= 2) ponderMove = moves[1];
                            ReceiveBestMove(moves[0], ponderMove, ResultSource.Openbook, fen);
                        }                            
                    }
                    else if (int.TryParse(infos["score"], out currScore) && currScore > Settings.StopScore)
                    {
                        if (FirstMate)
                        {
                            FirstMate = false;
                            string fen = Utils.BoardToFen(CurrentBoard, RedSide);
                            Engine.StopAnalyzeAndSkip(fen);
                            string[] moves = infos["pv"].Split(' ');
                            string ponderMove = "";
                            if (moves.Length >= 2) ponderMove = moves[1];
                            ReceiveBestMove(moves[0], ponderMove, ResultSource.Openbook, fen);
                        }
                    }
                    else
                    {
                        FirstMate = true;
                    }
                }
                if (infos["score"].Contains("绝杀") && (double.Parse(infos["depth"]) % 10 != 0))
                {
                    return;
                }
                int score;
                if (int.TryParse(infos["score"], out score))
                {
                    if (score >= 0)
                    {
                        this.Invoke(new Action(() => textBox_engine_log.SelectionColor = PositiveColor));
                    }
                    else
                    {
                        this.Invoke(new Action(() => textBox_engine_log.SelectionColor = NegativeColor));
                    }
                }
                else
                {
                    if (infos["score"].Contains("绝杀"))
                    {
                        this.Invoke(new Action(() => textBox_engine_log.SelectionColor = MateColor));
                    }
                }
                info_str = "";
                if (BackgroundAnalyzing)
                {
                    info_str += "[后台思考] ";
                }
                info_str += (
                    $"深度: {infos["depth"]} " +
                    $"得分: {infos["score"]} " +
                    $"时间: {(infos.ContainsKey("time") ? Math.Round(double.Parse(infos["time"]) / 1000, 1) : 0)}秒 " +
                    $"nps: {(infos.ContainsKey("nps") ? Math.Round(double.Parse(infos["nps"]) / 1000) : 0)}K" +
                    "\r\n"
                    );
                // 同步更新浮动分数窗：分数 + 深度，使其与日志同源且实时
                // 在后台线程触发，必须封送到 UI 线程更新绘制
                string _depthStr = "";
                if (infos.ContainsKey("depth"))
                {
                    double dd;
                    if (double.TryParse(infos["depth"], out dd))
                        _depthStr = "D" + ((int)dd).ToString();
                    else
                        _depthStr = infos["depth"];
                }
                int _floatScore;
                Color _floatColor;
                string _floatText;
                if (infos["score"].Contains("绝杀"))
                {
                    _floatText = "杀";
                    _floatColor = FloatScoreMate;
                }
                else if (int.TryParse(infos["score"], out _floatScore))
                {
                    _floatText = (_floatScore >= 0 ? "+" : "-") + Math.Abs(_floatScore).ToString();
                    // 浅白底浮动窗配色：正分深红、负分深蓝（深色在浅底上清晰可读）。
                    _floatColor = _floatScore >= 0 ? FloatScorePositive : FloatScoreNegative;
                }
                else
                {
                    _floatText = infos["score"];
                    _floatColor = FloatScorePositive;
                }
                this.Invoke(new Action(() => UpdateFloatingScore(_floatText, _floatColor, _depthStr)));
                this.Invoke(new Action(() =>
                {
                    textBox_engine_log.AppendText(info_str);
                    TrimEngineLog();
                }));
            }
            if (infos.ContainsKey("pv"))
            {
                string fen = Utils.BoardToFen(EngineAnalyzingBoard, RedSide ? "w" : "b",RedSide ? "w" : "b");
                string[] moves = infos["pv"].Split(' ');
                string bestMove = moves[0];
                Point fromPoint = Utils.Move2Point(bestMove.Substring(0, 2),RedSide);
                Point toPoint = Utils.Move2Point(bestMove.Substring(2, 2),RedSide);
                long nowTick = DateTime.Now.Ticks;
                bool shouldInvokeUI = (nowTick - _lastInfoInvokeTick) >= 80 * TimeSpan.TicksPerMillisecond;
                try
                {
                    info_str = Utils.FenToChina(EngineAnalyzingBoard, moves,RedSide) + "\r\n";
                    string[] moveNames = info_str.Trim().Split(' ');
                    if (moveNames.Length > 0 && !BackgroundAnalyzing)
                    {
                        int pvIdx = 0;
                        if (infos.ContainsKey("multipv"))
                        {
                            int.TryParse(infos["multipv"], out pvIdx);
                            pvIdx--;
                        }
                        if (pvIdx >= 0 && pvIdx < 3)
                        {
                            CandidateMoves[pvIdx] = moveNames[0];
                            int s = 0;
                            if (infos.ContainsKey("score") && int.TryParse(infos["score"], out s))
                            {
                                CandidateScores[pvIdx] = s;
                            }
                        }
                    }
                    if (shouldInvokeUI)
                    {
                        _lastInfoInvokeTick = nowTick;
                        this.Invoke(new Action(() =>
                        {
                            textBox_engine_log.SelectionColor = NormalColor;
                            textBox_engine_log.AppendText(info_str + "\r\n");
                            string display = "";
                            string[] circleNums = { "①", "②", "③" };
                            for (int i = 0; i < 3; i++)
                            {
                                if (CandidateMoves[i] != "")
                                {
                                    display += (display == "" ? "" : " ") + circleNums[i] + CandidateMoves[i];
                                }
                            }
                            label_moves.Text = display;
                            TrimEngineLog();
                        }));
                    }
                }
                catch (Exception ex)
                {
                    // 此处在引擎输出的后台线程触发（每秒多次），绝不能用 MessageBox，
                    // 否则挂机自动下棋时会堆叠弹框卡死引擎输出管道
                    LogHelper.Error(ex, "Engine_InfoEvent");
                }
                Point ponderFrom = new Point(-1, -1);
                Point ponderTo = new Point(-1, -1);
                if (moves.Length > 1)
                {
                    string ponderMove = moves[1];
                    ponderFrom = Utils.Move2Point(ponderMove.Substring(0, 2), RedSide);
                    ponderTo = Utils.Move2Point(ponderMove.Substring(2, 2), RedSide);
                }
                // 把思考中的局面加入预期局面，如果对手走了就能快速确认，无需等待
                string[,] expectedSelfBoard = (string[,])EngineAnalyzingBoard.Clone();
                expectedSelfBoard[toPoint.X, toPoint.Y] = expectedSelfBoard[fromPoint.X, fromPoint.Y];
                expectedSelfBoard[fromPoint.X, fromPoint.Y] = null;
                string boardKey = fen + " moves " + bestMove;
                if (!ExpectedBoardMap.ContainsKey(boardKey))
                {
                    ExpectedBoardMap.Add(boardKey, expectedSelfBoard);
                }
                if (moves.Length > 1)
                {
                    string[,] expectedPonderBoard = (string[,])expectedSelfBoard.Clone();
                    expectedPonderBoard[ponderTo.X, ponderTo.Y] = expectedPonderBoard[ponderFrom.X, ponderFrom.Y];
                    expectedPonderBoard[ponderFrom.X, ponderFrom.Y] = null;
                    boardKey += " " + moves[1];
                    if (!ExpectedBoardMap.ContainsKey(boardKey))
                    {
                        ExpectedBoardMap.Add(boardKey, expectedPonderBoard);
                    }
                }
                if (!BackgroundAnalyzing)
                {
                    BackgroundAnalysisMove = new ChessMove(new Point(-1, -1), new Point(-1, -1));
                    BestMove = new ChessMove(fromPoint, toPoint);
                    if (moves.Length > 1)
                    {
                        PonderMove = new ChessMove(ponderFrom, ponderTo);
                    }
                    if (moves.Length > 2)
                    {
                        Point m3From = Utils.Move2Point(moves[2].Substring(0, 2), RedSide);
                        Point m3To = Utils.Move2Point(moves[2].Substring(2, 2), RedSide);
                        Move3 = new ChessMove(m3From, m3To);
                    }
                    else
                    {
                        Move3 = new ChessMove(new Point(-1, -1), new Point(-1, -1));
                    }
                    if (moves.Length > 3)
                    {
                        Point m4From = Utils.Move2Point(moves[3].Substring(0, 2), RedSide);
                        Point m4To = Utils.Move2Point(moves[3].Substring(2, 2), RedSide);
                        Move4 = new ChessMove(m4From, m4To);
                    }
                    else
                    {
                        Move4 = new ChessMove(new Point(-1, -1), new Point(-1, -1));
                    }
                }
                else
                {
                    BackgroundAnalysisMove = new ChessMove(fromPoint, toPoint);
                }

                if (shouldInvokeUI)
                {
                    RenderDisplayBoard();
                }
            }
            if (info_str != "")
            {
                this.Invoke(new Action(() =>
                {
                    if (checkBox_auto_scroll.Checked)
                    {
                        textBox_engine_log.ScrollToCaret();
                    }
                }));
            }
        }

        void ReceiveBestMove(string bestMove, string ponderMove, ResultSource source, string sourceFen="")
        {
            LogHelper.Info("[ReceiveBestMove] 收到着法 bestMove=" + bestMove + " ponder=" + ponderMove + " source=" + source + " AutoGo=" + Settings.AutoGo);
            if (source == ResultSource.Engine)
            {
                FirstMate = true;
            }
            BestMoveList.Add(bestMove);
            bestMove = bestMove.Trim('\0', ' ');
            ponderMove = ponderMove.Trim('\0', ' ');
            string mySideStr = RedSide ? "r" : "b";
            if (bestMove.Length != 4) return;
            Point fromPoint = Utils.Move2Point(bestMove.Substring(0, 2),RedSide);
            Point toPoint = Utils.Move2Point(bestMove.Substring(2, 2),RedSide);
            Point ponderFrom = new Point(-1, -1);
            Point ponderTo = new Point(-1, -1);
            BestMove = new ChessMove(fromPoint, toPoint);
            if (ponderMove != "")
            {
                ponderFrom = Utils.Move2Point(ponderMove.Substring(0, 2),RedSide);
                ponderTo = Utils.Move2Point(ponderMove.Substring(2, 2),RedSide);
                PonderMove = new ChessMove(ponderFrom, ponderTo);
            }
            else
            {
                PonderMove = new ChessMove(new Point(-1, -1), new Point(-1, -1));
            }
            Move3 = new ChessMove(new Point(-1, -1), new Point(-1, -1));
            Move4 = new ChessMove(new Point(-1, -1), new Point(-1, -1));
            if (!BackgroundAnalyzing)
            {
                BackgroundAnalysisMove = new ChessMove(new Point(-1, -1), new Point(-1, -1));
            }
            RenderDisplayBoard();
            string fen = "";
            if (!string.IsNullOrEmpty(sourceFen))
            {
                fen = sourceFen;
            }
            else
            {
                fen = Utils.BoardToFen(CurrentBoard, RedSide);
            }
            if (!string.IsNullOrEmpty(fen))
            {
                string boardKey = fen + " moves " + bestMove;
                ExpectedSelfGoBoard = (string[,])EngineAnalyzingBoard.Clone();
                ExpectedSelfGoBoard[toPoint.X, toPoint.Y] = ExpectedSelfGoBoard[fromPoint.X, fromPoint.Y];
                ExpectedSelfGoBoard[fromPoint.X, fromPoint.Y] = null;
                if (!ExpectedBoardMap.ContainsKey(boardKey))
                {
                    ExpectedBoardMap.Add(boardKey, ExpectedSelfGoBoard);
                }
                ExpectedMove = bestMove;
                if (ponderMove != "")
                {
                    boardKey += " " + ponderMove;
                    string[,] expectedPonderBoard = (string[,])ExpectedSelfGoBoard.Clone();
                    expectedPonderBoard[ponderTo.X, ponderTo.Y] = expectedPonderBoard[ponderFrom.X, ponderFrom.Y];
                    expectedPonderBoard[ponderFrom.X, ponderFrom.Y] = null;
                    if (!ExpectedBoardMap.ContainsKey(boardKey))
                    {
                        ExpectedBoardMap.Add(boardKey, expectedPonderBoard);
                    }
                }
            }

            if (Settings.AutoGo && EngineAnalyzingBoard[fromPoint.X, fromPoint.Y] != null && EngineAnalyzingBoard[fromPoint.X, fromPoint.Y].StartsWith(mySideStr + "_"))
            {
                // 防重保护：避免同一局面(fen)下同一走法在短期内被反复点击。
                // 触发场景：开局库/云库/引擎对"本方走完后对方没动"的同一局面反复返回同一招，
                // 而 AutoGo 守卫只检查"起点是本方棋子"，不检查"这招刚走过"，于是每秒点一次
                // 同一招停不下来(对方读秒时尤为明显)。这里用 fen+move 作 key，窗口内只点一次。
                // 正常的"同招不同回合"(对方已走、局面已变 → fen 变)不受影响，不会被误杀。
                // 窗口设 8 秒：覆盖开局库 MinTimeUsingOpenbook 等延迟，又不至于卡住正常重走。
                string dedupKey = fen + "|" + bestMove;
                bool isRepeat = _lastAutoGoKey == dedupKey
                    && (DateTime.Now - _lastAutoGoTime).TotalSeconds < 8;
                if (isRepeat)
                {
                    LogHelper.Info("[ReceiveBestMove] 本局面同一着法短期内已点击，跳过 bestMove=" + bestMove + " fen=" + fen);
                }
                else
                {
                    LogHelper.Info("[ReceiveBestMove] 通过AutoGo守卫，执行点击 bestMove=" + bestMove + " source=" + source);
                    _lastAutoGoKey = dedupKey;
                    _lastAutoGoTime = DateTime.Now;
                    // 长打检测的记录点：本方确定要走这一招(已通过 AutoGo 守卫)，记录"本方走完后"
                    // 的局面(ExpectedSelfGoBoard 已在上方 1649-1651 推算好)的出现次数。
                    // 下次 StartAnalyze 入口会据此判定是否长打。注意此处只计数、不判定——
                    // 判定在 StartAnalyze 入口，因为那时才轮到本方再次决策、需要决定是否禁招。
                    if (ExpectedSelfGoBoard != null)
                    {
                        string lk = Utils.BoardLayoutKey(ExpectedSelfGoBoard);
                        if (!string.IsNullOrEmpty(lk))
                        {
                            if (!PositionRepeatCount.ContainsKey(lk)) PositionRepeatCount[lk] = 0;
                            PositionRepeatCount[lk]++;
                        }
                    }
                    if (source == ResultSource.ChessDB || source == ResultSource.Openbook)
                    {
                        if (Settings.MinTimeUsingOpenbook > 0)
                        {
                            Thread.Sleep((int)(Settings.MinTimeUsingOpenbook * 1000));
                        }
                    }
                    ChangeDetectedAfterClick = false;
                    PlayChessWithMimicHumanDelay(bestMove);
                    if (Settings.BackgroundAnalysis && ponderMove != "" && !string.IsNullOrEmpty(fen) && source == ResultSource.Engine)
                    {
                        PonderBoard = (string[,])ExpectedSelfGoBoard.Clone();
                        PonderBoard[ponderTo.X, ponderTo.Y] = PonderBoard[ponderFrom.X, ponderFrom.Y];
                        PonderBoard[ponderFrom.X, ponderFrom.Y] = null;
                        string ponderFen = Utils.BoardToFen(PonderBoard, RedSide);
                        EngineAnalyzingBoard = (string[,])PonderBoard.Clone();
                        Engine.StartAnalyzePonder(ponderFen, Settings.EngineStepTime, Settings.EngineDepth);
                        BackgroundAnalyzing = true;
                    }
                }
            }
        }

        // ReceiveBestMove 的 AutoGo 防重状态：记录上一次实际点击的"局面+走法"，防止
        // 对方未走时对同一局面反复点击同一招导致狂点。
        string _lastAutoGoKey = "";
        DateTime _lastAutoGoTime = DateTime.MinValue;

        private void Engine_BestMove_Event(string sourceFen, string bestMove, string ponderMove)
        {
            EngineAnalyzeCount--;
            ReceiveBestMove(bestMove, ponderMove, ResultSource.Engine, sourceFen);
        }


        public async void PlayChess(string move)
        {
            if (move == null || move.Length != 4)
            {
                LogHelper.Info("[PlayChess] 着法无效被忽略 move=" + (move == null ? "null" : move));
                return;
            }
            await Task.Run(new Action(()=>{
              try
              {
                Point fromPoint = Utils.Move2Point(move.Substring(0, 2),RedSide);
                Point toPoint = Utils.Move2Point(move.Substring(2, 2),RedSide);
                Point fromClickPoint = ClickPositionMap[fromPoint.X, fromPoint.Y];
                Point toClickPoint = ClickPositionMap[toPoint.X, toPoint.Y];
                LogHelper.Info("[PlayChess] 执行点击 move=" + move + " RedSide=" + RedSide
                    + " from(" + fromPoint.X + "," + fromPoint.Y + ")->px" + fromClickPoint.X + "," + fromClickPoint.Y
                    + " to(" + toPoint.X + "," + toPoint.Y + ")->px" + toClickPoint.X + "," + toClickPoint.Y
                    + " ClickHandle=" + ClickHandle + " GameHandle=" + GameHandle);
                ChangeDetectedAfterClick = false;
                MouseLeftClient_2Point(fromClickPoint.X, fromClickPoint.Y, toClickPoint.X, toClickPoint.Y);
              }
              catch (Exception ex)
              {
                  LogHelper.Error(ex, "PlayChess");
              }
            }));
        }

        const int MaxLogLines = 500;
        long _lastInfoInvokeTick;

        // === 模仿人类：出招前插入难度自适应延时 ===
        // 开启 Settings.MimicHuman 后，ReceiveBestMove 不再直接落子，而是先延时一段
        // 随机时长（必然局面偏快、复杂局面偏慢，最长 15 秒）再落子，模拟真人思考节奏。
        // 延时期间若棋盘被人为改动（棋手自己走了），则放弃本次落子，避免重复落子。
        // 「立即出招」按钮可置位 _mimicAbortFlag 立即结束延时去落子。
        // 这三个字段被 UI 线程(立即出招按钮)、DetectionLoop 线程(刷新棋盘)、
        // 延时 Task 线程访问，用 volatile 保证可见性。
        volatile bool _mimicDelaying = false;    // 当前是否处于模仿人类延时等待中
        volatile bool _mimicAbortFlag = false;   // 外部请求立即结束延时(立即出招按钮)
        string[,] _mimicBaselineBoard = null;    // 延时开始时的棋盘快照，用于检测人为干预

        // 时快时慢：每步掷骰决定是否延时，制造"有时秒走、有时思考"的真人节奏。
        // 硬约束：最近窗口内"走延时"的步数占比不得超过 35%（即至少 65% 的步直接快速走）。
        // 用稍大的窗口(20 步)让 35% 比例统计稳定：20×35%=7，窗口内最多 7 步延时。
        // 既不会延时过多(像在故意拖时间)，也保留足够的延时步让节奏有变化、不全是秒走。
        // 仅在本次确实落子(跳过落子/延时完成落子)时才 push 记录，保证窗口反映"真实落子节奏"，
        // 不被放弃落子(人为干预)污染统计。
        const int MimicWindow = 20;              // 滑动窗口大小
        const double MimicDelayRatioMax = 0.35;  // 延时步数占比硬上限 35%
        // 窗口内允许的最大延时步数 = floor(20 × 0.35) = 7
        const int MimicDelayCountCap = 7;
        readonly Queue<bool> _mimicRecentDelayed = new Queue<bool>(); // 最近若干步是否走了延时(true=延时)
        int _mimicRecentDelayCount = 0;         // _mimicRecentDelayed 中 true(延时)的个数，O(1) 计数

        // 模仿人类延时常量（写死不暴露给设置）：
        // baseShort=必然局面/强制招的下限延时；baseLong=复杂局面的常规上限延时；
        // gapRange=用来衡量"必然程度"的参考分差(厘兵)，gap 达到/超过此值即视为完全必然。
        // 硬上限(hardCap)设 8 秒而非 15 秒：用户最初说"最长不超过 15 秒"是天花板，
        // 但实测带读秒的棋局(天天象棋单步限时 ~15-30s)，15 秒延时叠加算棋+等待对方，
        // 单步总耗时极易超时判负。8 秒仍是"人类思考一步"的合理上限，又给算棋留足余量。
        const int MimicDelayBaseShortMs = 800;
        const int MimicDelayBaseLongMs = 6000;
        const int MimicDelayHardCapMs = 8000;    // 硬上限：任何情况都不超过 8 秒
        const int MimicDelayGapRange = 300;
        const int MimicDelaySliceMs = 100;       // 分片 Sleep 粒度，便于及时响应中断
        const int MimicInterfereCheckMs = 300;   // 人为干预检测周期(与 DetectionInterval 同量级)

        // 决定本步是否走延时。返回 true=延时，false=跳过延时直接走。
        // 硬约束：若窗口内延时步数已达上限(7/20=35%)，本步强制跳过(直接走)。
        // 否则按概率掷骰：长期均值约 30%，留出余量使实际占比稳定贴在 35% 以下。
        // 注意：此函数只"决定"，不"记录"。真正落子后由 RecordMimicStep 写入窗口。
        bool ShouldDelayMimicThisStep()
        {
            // 窗口内延时步已达上限 → 本步必须直接走，不许再延时
            if (_mimicRecentDelayCount >= MimicDelayCountCap)
                return false;
            // 窗口还没满时也按比例卡：已延时数 / 当前窗口大小 >= 35% 就停
            int curWin = _mimicRecentDelayed.Count;
            if (curWin > 0 && (double)_mimicRecentDelayCount / curWin >= MimicDelayRatioMax)
                return false;
            // 正常掷骰：均值约 30%，给 35% 上限留余量，避免频繁触发硬约束导致节奏僵硬
            return Rand.NextDouble() < 0.30;
        }

        // 记录本步节奏(是否走了延时)，维护固定容量为 MimicWindow 的滑动窗口。
        // 超出容量时弹出最旧的一项并相应调整计数。
        void RecordMimicStep(bool delayed)
        {
            _mimicRecentDelayed.Enqueue(delayed);
            if (delayed) _mimicRecentDelayCount++;
            if (_mimicRecentDelayed.Count > MimicWindow)
            {
                bool old = _mimicRecentDelayed.Dequeue();
                if (old) _mimicRecentDelayCount--;
            }
        }

        // 依据当前候选分(CandidateScores)估算本手延时(毫秒)。
        // 前 1~2 候选分差越大→局面越必然→延时越短；分差越小→选择越多→延时越长。
        // 叠加 ±20% 随机抖动，使节奏更自然。最后硬钳到 15 秒。
        int ComputeMimicHumanDelayMs()
        {
            // 取前两个有效候选分。开局库/云库路径下 CandidateScores 可能未填充(全 0)，
            // 此时按"只有一招/信息不足"处理，走偏短延时，体验自然。
            int s0 = CandidateScores.Length > 0 ? CandidateScores[0] : 0;
            int s1 = CandidateScores.Length > 1 ? CandidateScores[1] : s0;
            // 候选招未填(空串)时其分不可信，退化为单候选
            if (CandidateMoves.Length < 2 || string.IsNullOrEmpty(CandidateMoves[1])) s1 = s0;
            int gap = Math.Abs(s0 - s1);
            // 归一化到 [0,1]：gap=0(选择最多)→1，gap>=gapRange(完全必然)→0
            double norm = 1.0 - Math.Min(1.0, (double)gap / MimicDelayGapRange);
            double delay = MimicDelayBaseShortMs + norm * (MimicDelayBaseLongMs - MimicDelayBaseShortMs);
            // ±20% 随机抖动
            double jitter = 1.0 + (Rand.NextDouble() * 0.4 - 0.2);
            delay *= jitter;
            int ms = (int)Math.Round(delay);
            if (ms < MimicDelayBaseShortMs / 2) ms = MimicDelayBaseShortMs / 2; // 抖动后下限保护
            if (ms > MimicDelayHardCapMs) ms = MimicDelayHardCapMs;             // 硬上限 15 秒
            return ms;
        }

        // 模仿人类延时落子入口。不开 MimicHuman 时直接落子，行为与原先完全一致。
        // 开启时整段放到 Task.Run，绝不阻塞 UI 线程（开局库/云库路径经 this.Invoke 跑在 UI 线程）。
        async void PlayChessWithMimicHumanDelay(string move)
        {
            if (move == null || move.Length != 4) return;
            if (!Settings.MimicHuman)
            {
                PlayChess(move);
                return;
            }
            // 时快时慢：每步掷骰，硬约束"延时步数占比 ≤ 35%"，保证至少 65% 的步直接快速走，
            // 其余走延时。制造真人"有时秒走、有时思考"的节奏，又不会延时过多像在故意拖时间。
            // 跳过的步等于"按设置时间要求走"——引擎 EngineStepTime 算完即落，不再额外等。
            if (!ShouldDelayMimicThisStep())
            {
                LogHelper.Info("[MimicHuman] 本步跳过延时，直接落子 move=" + move
                    + " (窗口内已延时 " + _mimicRecentDelayCount + "/" + _mimicRecentDelayed.Count + ")");
                RecordMimicStep(false);
                PlayChess(move);
                return;
            }
            int delayMs = ComputeMimicHumanDelayMs();
            // 人为干预检测的基准棋盘：用延时开始那一刻的实时 PendingBoard 快照。
            // 注意不能用 EngineAnalyzingBoard——它在某些 ReloadBoard 分支(和预期一样/己方变化跳过)
            // 里不会更新，可能是上一手的过时分析快照，拿它比 PendingBoard 必然 DiffCount>0，
            // 导致几乎每次延时都被误判为"人为干预"而放弃落子(实测日志全部放弃，无一落子)。
            // 用实时 PendingBoard 快照作基准，延时期间只要盘面被改动(对方走/棋手手动走)即不一致。
            _mimicBaselineBoard = PendingBoard != null
                ? (string[,])PendingBoard.Clone()
                : null;
            _mimicAbortFlag = false;
            _mimicDelaying = true;
            _mimicInterfereStreak = 0; // 新一轮延时，重置连续命中计数
            LogHelper.Info("[MimicHuman] 进入出招延时 " + delayMs + "ms move=" + move
                + " (窗口内已延时 " + _mimicRecentDelayCount + "/" + _mimicRecentDelayed.Count + ")");
            await Task.Run(new Action(() =>
            {
                try
                {
                    int waited = 0;
                    int sinceCheck = 0;
                    while (waited < delayMs && Running)
                    {
                        // 立即出招按钮置位 → 立即结束延时去落子
                        if (_mimicAbortFlag)
                        {
                            LogHelper.Info("[MimicHuman] 被立即出招打断，剩余 " + (delayMs - waited) + "ms，立即落子");
                            break;
                        }
                        Thread.Sleep(MimicDelaySliceMs);
                        waited += MimicDelaySliceMs;
                        sinceCheck += MimicDelaySliceMs;
                        // 周期性检测人为干预：棋手在延时期间自己走了，棋盘已变 → 放弃本次落子
                        if (sinceCheck >= MimicInterfereCheckMs)
                        {
                            sinceCheck = 0;
                            if (HumanInterferedDuringDelay())
                            {
                                LogHelper.Info("[MimicHuman] 延时期间检测到棋盘被人为改动，放弃本次落子 move=" + move);
                                _mimicDelaying = false;
                                _mimicAbortFlag = false;
                                return; // 注意：此处 return 只退出 Task，不落子，也不记入节奏窗口
                            }
                        }
                    }
                    // 延时结束：最后再核对一次未被人为改动，然后落子
                    if (!Running)
                    {
                        _mimicDelaying = false;
                        return;
                    }
                    if (HumanInterferedDuringDelay())
                    {
                        LogHelper.Info("[MimicHuman] 延时结束时检测到棋盘已变，放弃落子 move=" + move);
                        _mimicDelaying = false;
                        return;
                    }
                    _mimicDelaying = false;
                    RecordMimicStep(true); // 这一步走了延时，记入窗口(delayed=true)
                    PlayChess(move);
                }
                catch (Exception ex)
                {
                    _mimicDelaying = false;
                    LogHelper.Error(ex, "PlayChessWithMimicHumanDelay");
                }
            }));
        }

        // 人为干预连续命中计数。PendingBoard 在动画/识别抖动时可能短暂出现 DiffCount>0，
        // 单次判定会误报。要求连续两次检查都检测到变化才认定真被人为改动。
        int _mimicInterfereStreak = 0;

        // 判断延时期间棋盘是否被人为改动：基准快照(延时开始时的 PendingBoard)与实时 PendingBoard 比对。
        // 一次正常走棋会让 DiffCount=2(起点清空+终点落子)，故以 DiffCount>=2 作为"盘面确实被动过"门槛，
        // 过滤掉识别抖动(单格闪现)。再叠加连续 2 次命中确认，进一步防动画误报。
        // 基准为空时保守返回 false（不阻断）。
        bool HumanInterferedDuringDelay()
        {
            if (_mimicBaselineBoard == null || PendingBoard == null) return false;
            try
            {
                var cmp = Utils.CompareBoard(_mimicBaselineBoard, PendingBoard);
                if (cmp.DiffCount >= 2)
                {
                    _mimicInterfereStreak++;
                    // 连续 2 次命中才认定，避免单帧识别抖动误判
                    if (_mimicInterfereStreak >= 2)
                    {
                        _mimicInterfereStreak = 0;
                        return true;
                    }
                    return false;
                }
                else
                {
                    _mimicInterfereStreak = 0;
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        // 立即出招按钮调用：打断当前正在进行的模仿人类延时，立即落子。
        public void AbortMimicHumanDelay()
        {
            if (_mimicDelaying) _mimicAbortFlag = true;
        }

        void TrimEngineLog()
        {
            if (textBox_engine_log.Lines.Length > MaxLogLines)
            {
                int removeCount = textBox_engine_log.Lines.Length - MaxLogLines + 50;
                textBox_engine_log.Select(0, textBox_engine_log.GetFirstCharIndexFromLine(removeCount));
                textBox_engine_log.SelectedText = "";
            }
        }
    }
}
