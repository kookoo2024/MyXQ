using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VinXiangQi
{
    /// <summary>
    /// 全项目统一的日志出口。线程安全、按日期轮转、自身永不抛异常。
    /// 替代原先散落各处的 File.AppendAllText("err.log") 与 Debug.WriteLine。
    /// </summary>
    public static class LogHelper
    {
        static readonly object _lock = new object();
        static readonly string LogDir = @".\logs";
        const int RetainDays = 7;
        const string FilePrefix = "vinxiangqi_";

        /// <summary>
        /// 记录异常（含完整堆栈）。context 用于标注来源（如 "DetectionLoop"）。
        /// </summary>
        public static void Error(Exception ex, string context = "")
        {
            if (ex == null) return;
            string msg = string.IsNullOrEmpty(context)
                ? ex.ToString()
                : "[" + context + "] " + ex.ToString();
            Write("ERROR", msg);
        }

        /// <summary>
        /// 记录错误文本（无异常对象）。
        /// </summary>
        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        static void Write(string level, string message)
        {
            // 日志本身绝不能抛异常把调用方（尤其是 catch 块里的检测循环）搞崩
            string line;
            try
            {
                string thread = Thread.CurrentThread.IsThreadPoolThread
                    ? "ThreadPool"
                    : (Thread.CurrentThread.Name ?? "Thread#" + Thread.CurrentThread.ManagedThreadId);
                line = string.Format("[{0}] [{1}] [{2}] {3}",
                    DateTime.Now.ToString("HH:mm:ss"), level, thread, message);
            }
            catch
            {
                line = "[" + level + "] " + message;
            }

            try
            {
                lock (_lock)
                {
                    if (!Directory.Exists(LogDir))
                    {
                        Directory.CreateDirectory(LogDir);
                    }
                    string fileName = FilePrefix + DateTime.Now.ToString("yyyy-MM-dd") + ".log";
                    string fullPath = Path.Combine(LogDir, fileName);
                    File.AppendAllText(fullPath, line + Environment.NewLine, Encoding.UTF8);
                }
                CleanOldLogs();
            }
            catch (Exception ex)
            {
                // 写文件失败则降级到调试输出，绝不上抛
                Debug.WriteLine("LogHelper 写入失败: " + ex.Message);
                Debug.WriteLine(line);
            }
        }

        /// <summary>
        /// 清理超过保留天数的旧日志文件。每次写入后顺带执行（轻量，且在 lock 内）。
        /// </summary>
        static void CleanOldLogs()
        {
            try
            {
                DateTime cutoff = DateTime.Now.AddDays(-RetainDays);
                string[] files = Directory.GetFiles(LogDir, FilePrefix + "*.log");
                foreach (string file in files)
                {
                    try
                    {
                        if (File.GetLastWriteTime(file) < cutoff)
                        {
                            File.Delete(file);
                        }
                    }
                    catch
                    {
                        // 单个文件清理失败忽略，不影响主流程
                    }
                }
            }
            catch
            {
                // 清理整体失败忽略
            }
        }
    }
}
