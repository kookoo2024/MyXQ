using System;
using System.Drawing;
using System.Windows.Forms;

namespace VinXiangQi
{
    /// <summary>
    /// 引擎设置页底部的“如何选择引擎”说明，并按本机 CPU 给出推荐版本。
    /// </summary>
    public partial class VinSettingsForm
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool IsProcessorFeaturePresent(int feature);
        const int PF_AVX2 = 40;      // Win10+
        const int PF_AVX512F = 41;   // Win10+

        static string GetCpuName()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    var v = k?.GetValue("ProcessorNameString") as string;
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
            }
            catch { }
            return "未知 CPU";
        }

        static bool SafeFeature(int f)
        {
            try { return IsProcessorFeaturePresent(f); } catch { return false; }
        }

        // 锐龙 1000/2000/3000 系列(Zen1/Zen+/Zen2)的 PEXT 指令极慢，bmi2 版反而比 avx2 版慢
        static bool IsOldRyzen(string cpu)
        {
            var m = System.Text.RegularExpressions.Regex.Match(cpu, @"Ryzen.*?\b([1-3])\d{3}");
            return cpu.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 && m.Success;
        }

        static string RecommendEngine(out string cpu)
        {
            cpu = GetCpuName();
            bool avx2 = SafeFeature(PF_AVX2);
            bool avx512 = SafeFeature(PF_AVX512F);
            if (!avx2) return "modern（若打不开就用 x86-64）";
            if (IsOldRyzen(cpu)) return "avx2（这款锐龙不要用 bmi2）";
            if (avx512) return "avx512（打不开就退回 bmi2）";
            return "bmi2";
        }

        void AddEngineHelp()
        {
            string cpu;
            string rec = RecommendEngine(out cpu);
            int cores = Environment.ProcessorCount;
            int threads = Math.Max(1, cores - 2);

            var box = new GroupBox
            {
                Text = "如何选择引擎",
                Location = new Point(12, 224),
                Size = new Size(452, 176),
            };
            var tip = new Label
            {
                Location = new Point(12, 22),
                Size = new Size(430, 148),
                Text =
                    "本机：" + cpu + "，" + cores + " 核\r\n" +
                    "推荐：文件名带 " + rec + " 的引擎，线程数 " + threads + "\r\n\r\n" +
                    "• 引擎文件名后缀表示用到的 CPU 指令，从快到慢：\r\n" +
                    "   vnni512 > avx512 > bmi2 > avx2 > modern > x86-64\r\n" +
                    "• 选 CPU 支持的最快一版；选太新的会闪退或没有着法。\r\n" +
                    "• 不确定时按 avx2 → modern → x86-64 依次试。\r\n" +
                    "• 线程数取核数减 1～2；步时越长棋越强，但越慢。",
            };
            box.Controls.Add(tip);
            tabPage_engine.Controls.Add(box);
        }
    }
}
