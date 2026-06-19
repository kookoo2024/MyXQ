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
        // 修复多图标场景失效：
        //   1) 命中即 break，下一轮用全新截图再找下一个按钮（旧代码命中后仍用旧截图
        //      继续匹配列表里的图，导致界面已变化时第二个图标识别不到）；
        //   2) 命中后进入 cooldown，等界面动画/弹窗切换稳定再继续；
        //   3) 放宽匹配阈值到 AutoClickThreshold，缓解渲染差异（缩放/抗锯齿/阴影）；
        //   4) 每轮截图用完即释放，避免全屏 Bitmap 内存泄漏。
        const int AutoClickThreshold = 80;
        const int AutoClickCooldownMs = 1500;
        const int AutoClickIntervalMs = 2000;
        // 同一位置的去重窗口：模板图在下棋界面可能误匹配到某固定区域，
        // 限制同一坐标在窗口内只点一次，避免反复误点打断对局；
        // 局间按钮被点掉后界面变化，下一轮是不同坐标或按钮消失，不受影响。
        const int AutoClickDedupMs = 15000;
        const int AutoClickDedupTolerance = 10; // 坐标偏差小于此值视为同一位置
        Point _lastAutoClickPos = new Point(-1, -1);
        DateTime _lastAutoClickTime = DateTime.MinValue;

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
                        // 按"面积升序"匹配：上层小弹窗（如"×"关闭按钮，面积小）优先于
                        // 底层大弹窗（如"再来一局"，面积大）。否则会因命中即 break，
                        // 永远先点到大弹窗，而该优先处理的小弹窗被跳过，导致死循环卡住。
                        var orderedImages = AutoClickImageList
                            .OrderBy(img => img.Width * img.Height)
                            .ToArray();
                        foreach (Bitmap image in orderedImages)
                        {
                            Point pos = ImageHelper.FindImageFromTop(screen, image, AutoClickThreshold);
                            if (pos.X != -1)
                            {
                                Point clickPos = new Point(pos.X + image.Width / 2, pos.Y + image.Height / 2);
                                // 去重：同一位置在去重窗口内不重复点击。
                                // 抑制下棋时模板误匹配导致的反复误点；局间按钮点掉后坐标变化，不受影响。
                                bool isDuplicate = _lastAutoClickPos.X != -1
                                    && Math.Abs(clickPos.X - _lastAutoClickPos.X) <= AutoClickDedupTolerance
                                    && Math.Abs(clickPos.Y - _lastAutoClickPos.Y) <= AutoClickDedupTolerance
                                    && (DateTime.Now - _lastAutoClickTime).TotalMilliseconds < AutoClickDedupMs;
                                if (isDuplicate)
                                {
                                    LogHelper.Info("[AutoClickLoop] 命中但去重跳过(同位置短期内已点) " + image.Width + "x" + image.Height
                                        + " @px" + clickPos.X + "," + clickPos.Y);
                                    break;
                                }
                                MouseLeftClick(clickPos.X, clickPos.Y);
                                _lastAutoClickPos = clickPos;
                                _lastAutoClickTime = DateTime.Now;
                                LogHelper.Info("[AutoClickLoop] 命中并点击 " + image.Width + "x" + image.Height
                                    + " @px" + clickPos.X + "," + clickPos.Y);
                                clicked = true;
                                break; // 用全新截图找下一个，避免在旧截图上误判
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

        public void RenderDisplayBoard(string[,] renderBoard=null)
        {
            if (renderBoard == null) renderBoard = CurrentBoard;
            if (renderBoard == null) return;
            // 程序关闭过程中可能被引擎后台线程触发，避免对已释放窗体 Invoke 导致崩溃
            if (IsDisposed || !IsHandleCreated) return;
            this.Invoke(new Action(() =>
            {
                int width = 40;
                int height = 40;
                int xoffset = width / 2;
                int yoffset = height / 2;
                // 用木色底代替白底：board.png 未铺满时露边，木色与棋盘浑然一体。
                // 关键：用裁掉白边的棋盘图(GetTrimmedBoardImage)，board.png 原图四周带
                // 不透明白边，直接画会把木色底盖掉形成白圈。裁剪后内容区画满，白圈消失。
                BoardGDI.Clear(WoodBg);
                Bitmap boardImg = GetTrimmedBoardImage();
                if (boardImg != null)
                {
                    BoardGDI.DrawImage(boardImg, 0, 0, width * 10, height * 11);
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
                                BoardGDI.DrawImage(pieceImg, x * width + xoffset, y * height + yoffset, width, height);
                            }
                        }
                    }
                }
                if (BestMove.From.X != -1)
                {
                    Point bestFrom = new Point(BestMove.From.X * width + xoffset + width / 2, BestMove.From.Y * height + yoffset + height / 2);
                    Point bestTo = new Point(BestMove.To.X * width + xoffset + width / 2, BestMove.To.Y * height + yoffset + height / 2);
                    BoardGDI.DrawLine(RedSide ? ArrowRedPen : ArrowBluePen, bestFrom, bestTo);
                }
                if (PonderMove.From.X != -1)
                {
                    Point ponderFrom = new Point(PonderMove.From.X * width + xoffset + width / 2, PonderMove.From.Y * height + yoffset + height / 2);
                    Point ponderTo = new Point(PonderMove.To.X * width + xoffset + width / 2, PonderMove.To.Y * height + yoffset + height / 2);
                    BoardGDI.DrawLine(RedSide ? ArrowBluePen : ArrowRedPen, ponderFrom, ponderTo);
                }
                if (ShowTwoSteps && Move3.From.X != -1)
                {
                    Point m3From = new Point(Move3.From.X * width + xoffset + width / 2, Move3.From.Y * height + yoffset + height / 2);
                    Point m3To = new Point(Move3.To.X * width + xoffset + width / 2, Move3.To.Y * height + yoffset + height / 2);
                    BoardGDI.DrawLine(RedSide ? ArrowRedDimPen : ArrowBlueDimPen, m3From, m3To);
                }
                if (ShowTwoSteps && Move4.From.X != -1)
                {
                    Point m4From = new Point(Move4.From.X * width + xoffset + width / 2, Move4.From.Y * height + yoffset + height / 2);
                    Point m4To = new Point(Move4.To.X * width + xoffset + width / 2, Move4.To.Y * height + yoffset + height / 2);
                    BoardGDI.DrawLine(RedSide ? ArrowBlueDimPen : ArrowRedDimPen, m4From, m4To);
                }
                if (BackgroundAnalysisMove.From.X != -1)
                {
                    Point bgFrom = new Point(BackgroundAnalysisMove.From.X * width + xoffset + width / 2, BackgroundAnalysisMove.From.Y * height + yoffset + height / 2);
                    Point bgTo = new Point(BackgroundAnalysisMove.To.X * width + xoffset + width / 2, BackgroundAnalysisMove.To.Y * height + yoffset + height / 2);
                    BoardGDI.DrawLine(RedSide ? ArrowRedDimPen : ArrowBlueDimPen, bgFrom, bgTo);
                }

                int barY = height * 11 - 12;
                int barH = 10;
                int barW = width * 10;
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
                if (Settings.AutoGo && !ChangeDetectedAfterClick && IsExpectedMoveStillPending())
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
                // 长打(长将/长捉)检测。
                // BestMoveList 只记录本方着法(每次决策在 ReceiveBestMove 里 Add 一次)，故入口处
                // List[-1] 即"上一步本方刚走的着法"。中国象棋 3 次重复局面即判负，因此第 3 次
                // 准备走同一招时就该干预——原阈值 Count>=6 要等走到第 7 次才管，是"超过四次还
                // 在重复"的直接原因。
                // - List[-1]==List[-2]：连续两步走同一招(如车一直将军，单招长将)
                // - List[-1]==List[-3]：交替重复(如 h2e2→h2h8→h2e2，换一种将军法继续长将)
                // 两种都判定为重复，本次禁掉 List[-1] 这一招，迫使引擎变招。
                // 注意：只禁单招(Engine.BanMoves = lastMove)。多招 banmoves(a b c)很多引擎
                // 不认，且禁招过多时引擎会返回 bestmove (none)→ ReceiveBestMove 因长度!=4
                // 直接 return 不点击，程序卡死停住。单招禁招所有引擎都兼容。
                bool repetitionDetected = false;
                if (BestMoveList.Count >= 2)
                {
                    string lastMove = BestMoveList[BestMoveList.Count - 1];
                    bool isRepeat = lastMove == BestMoveList[BestMoveList.Count - 2];
                    if (!isRepeat && BestMoveList.Count >= 3)
                    {
                        isRepeat = lastMove == BestMoveList[BestMoveList.Count - 3];
                    }
                    if (isRepeat)
                    {
                        Engine.BanMoves = lastMove;
                        repetitionDetected = true;
                        DisplayStatus("检测到长打，禁招: " + lastMove + " 迫使变招");
                        LogHelper.Info("[StartAnalyze] 检测到长打，禁招: " + lastMove);
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
            }
            string opponentSymbol =RedSide ? "b_" : "r_";
            string mySymbol = RedSide ? "r_" : "b_";
            Debug.WriteLine($"Diff Result: {compareWithLast.DiffCount} R: {compareWithLast.RedDiff} B: {compareWithLast.BlackDiff}");
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
                LogHelper.Info("[ReceiveBestMove] 通过AutoGo守卫，执行点击 bestMove=" + bestMove + " source=" + source);
                if (source == ResultSource.ChessDB || source == ResultSource.Openbook)
                {
                    if (Settings.MinTimeUsingOpenbook > 0)
                    {
                        Thread.Sleep((int)(Settings.MinTimeUsingOpenbook * 1000));
                    }
                }
                ChangeDetectedAfterClick = false;
                PlayChess(bestMove);
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
