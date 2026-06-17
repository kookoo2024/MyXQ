using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VinXiangQi
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 全局异常兜底：作为最后一道防线，把未捕获的异常写进日志，
            // 避免用户看到闪退黑屏却无任何线索
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
            {
                LogHelper.Error(e.Exception, "UI线程未捕获异常");
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogHelper.Error(e.ExceptionObject as Exception, "AppDomain未捕获异常");
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            LogHelper.Info("程序启动 版本 " + Mainform.Version);
            Application.Run(new Mainform());
            LogHelper.Info("程序退出");
        }
    }
}
