using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VinXiangQi
{
    /// <summary>
    /// 一键诊断：打开日志文件夹、把程序状态和最近日志复制到剪贴板，方便反馈问题。
    /// </summary>
    public partial class Mainform
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool IsProcessorFeaturePresent(int feature);   // 40 = PF_AVX2_INSTRUCTIONS_AVAILABLE（Win10+）

        static string AppDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'); }
        }

        void OpenLogFolder()
        {
            try
            {
                string dir = Path.Combine(AppDir, "logs");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Process.Start("explorer.exe", "\"" + dir + "\"");
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "OpenLogFolder");
                MessageBox.Show(this, "打开日志文件夹失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        static string CountFiles(string sub, string pattern)
        {
            try
            {
                string dir = Path.Combine(AppDir, sub);
                if (!Directory.Exists(dir)) return "目录不存在";
                return Directory.GetFiles(dir, pattern, SearchOption.AllDirectories).Length + " 个";
            }
            catch (Exception ex) { return "读取失败 " + ex.Message; }
        }

        public string BuildDiagnosticReport()
        {
            var sb = new StringBuilder();
            Action<string, Func<object>> add = (k, f) =>
            {
                string v;
                try { v = Convert.ToString(f()); } catch (Exception ex) { v = "取值失败 " + ex.Message; }
                sb.AppendLine(k + "：" + v);
            };
            sb.AppendLine("==== Vin象棋 诊断信息 ====");
            add("时间", () => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            add("版本", () => Version);
            add("系统", () => Environment.OSVersion + (Environment.Is64BitOperatingSystem ? " 64位" : " 32位"));
            add("进程", () => Environment.Is64BitProcess ? "64位" : "32位");
            add("CPU", () => Environment.ProcessorCount + " 核，AVX2 " + (IsProcessorFeaturePresent(40) ? "支持" : "不支持"));
            add("识别性能", () => InferenceStats);
            add("程序目录", () => AppDir);
            add("当前目录", () => Environment.CurrentDirectory);
            add("settings.json", () => File.Exists(Path.Combine(AppDir, "settings.json")) ? "存在" : "不存在");
            add("Models(.onnx)", () => CountFiles("Models", "*.onnx"));
            add("Engines(.exe)", () => CountFiles("Engines", "*.exe"));
            add("Solutions", () => CountFiles("Solutions", "window.txt"));
            add("OpenBooks", () => CountFiles("OpenBooks", "*.*"));
            add("引擎已加载", () => Engine != null ? "是 " + Engine.EngineName : "否");
            add("选中引擎", () => Settings.SelectedEngine);
            add("引擎路径", () =>
            {
                EngineSettings es;
                if (Settings.EngineList != null && Settings.SelectedEngine != null && Settings.EngineList.TryGetValue(Settings.SelectedEngine, out es))
                    return es.ExePath + (File.Exists(es.ExePath) || File.Exists(Path.Combine(AppDir, es.ExePath ?? "")) ? "（存在）" : "（文件不存在）");
                return "无";
            });
            add("引擎列表", () => Settings.EngineList == null ? "" : string.Join(", ", Settings.EngineList.Keys));
            add("模型已加载", () => Model != null ? "是 " + Settings.YoloModel : "否");
            add("可用模型", () => string.Join(", ", ModelList.Keys));
            add("当前方案", () => Settings.SelectedSolution);
            add("可用方案", () => string.Join(", ", SolutionList.Keys));
            add("窗口句柄", () => GameHandle == IntPtr.Zero ? "未找到" : GameHandle.ToString());
            add("识别开启", () => DetectEnabled ? "是" : "否");
            add("开局库", () => string.Join(", ", OpenBookList.Keys));
            add("自动更新", () => Settings.AutoCheckUpdate + " 源 " + Settings.UpdateRepo);
            sb.AppendLine();
            sb.AppendLine("==== 最近日志 ====");
            try
            {
                string dir = Path.Combine(AppDir, "logs");
                var file = Directory.Exists(dir)
                    ? new DirectoryInfo(dir).GetFiles("*.log").OrderByDescending(f => f.LastWriteTime).FirstOrDefault()
                    : null;
                if (file == null)
                {
                    sb.AppendLine("（没有日志文件）");
                }
                else
                {
                    string[] lines;
                    using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        lines = sr.ReadToEnd().Split('\n');
                    }
                    sb.AppendLine(file.Name);
                    foreach (var l in lines.Skip(Math.Max(0, lines.Length - 60))) sb.AppendLine(l.TrimEnd('\r'));
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("读取日志失败：" + ex.Message);
            }
            return sb.ToString();
        }

        void CopyDiagnostics()
        {
            try
            {
                string report = BuildDiagnosticReport();
                LogHelper.Info("[Diagnostics] 用户复制了诊断信息");
                Clipboard.SetText(report);
                MessageBox.Show(this, "诊断信息已复制。\n\n直接在聊天框里按 Ctrl+V 粘贴发出即可。",
                    "复制诊断信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "CopyDiagnostics");
                MessageBox.Show(this, "复制失败：" + ex.Message, "复制诊断信息", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
