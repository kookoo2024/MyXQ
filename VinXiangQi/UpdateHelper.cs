using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;

namespace VinXiangQi
{
    /// <summary>
    /// 在线更新：从 GitHub Releases 读取最新版本，下载 zip，校验后由外部
    /// PowerShell 脚本在本程序退出后覆盖文件并重启。
    ///
    /// 约定：
    /// - 发布源由 Settings.UpdateRepo 指定（"owner/repo"，默认 kookoo2024/MyXQ），
    ///   必须是公开仓库；私有仓库的 Release 无法匿名下载。
    /// - Release 的 tag 形如 v1.5.0，附带一个 .zip 资源，内容为程序目录下要覆盖的文件。
    /// - 覆盖时只写入 zip 里有的文件，并强制跳过 settings.json，用户的配置、
    ///   方案、引擎、模型、开局库、日志都不受影响。
    /// </summary>
    public static class UpdateHelper
    {
        public class UpdateInfo
        {
            public Version Version;
            public string Tag;
            public string Notes;
            public string DownloadUrl;
            public string AssetName;
            public long Size;
            public string Sha256;   // GitHub 资源的 digest，可能为空
        }

        static bool _busy = false;

        static UpdateHelper()
        {
            // .NET 4.8 默认可能不启用 TLS1.2，GitHub 要求 TLS1.2+
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch { }
        }

        public static Version CurrentVersion
        {
            get
            {
                var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            }
        }

        static WebClient CreateClient()
        {
            var wc = new WebClient();
            wc.Encoding = Encoding.UTF8;
            wc.Headers[HttpRequestHeader.UserAgent] = "VinXiangQi-Updater/" + Mainform.Version;
            wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return wc;
        }

        static Version ParseTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return null;
            string s = tag.Trim().TrimStart('v', 'V');
            // 去掉 -beta 之类后缀
            int dash = s.IndexOf('-');
            if (dash > 0) s = s.Substring(0, dash);
            Version v;
            if (!Version.TryParse(s, out v)) return null;
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }

        /// <summary>
        /// 查询最新 Release。网络失败返回 null 并记录日志，不抛异常。
        /// </summary>
        public static async Task<UpdateInfo> GetLatestAsync()
        {
            string repo = (Mainform.Settings.UpdateRepo ?? "").Trim().Trim('/');
            if (string.IsNullOrEmpty(repo) || !repo.Contains("/"))
            {
                LogHelper.Warn("[Update] 未配置 UpdateRepo，跳过检查");
                return null;
            }
            string api = "https://api.github.com/repos/" + repo + "/releases/latest";
            try
            {
                string json;
                using (var wc = CreateClient())
                {
                    var task = wc.DownloadStringTaskAsync(api);
                    if (await Task.WhenAny(task, Task.Delay(20000)) != task)
                    {
                        try { wc.CancelAsync(); } catch { }
                        throw new TimeoutException("连接 GitHub 超时（20 秒）");
                    }
                    json = await task;
                }
                var obj = JObject.Parse(json);
                var info = new UpdateInfo
                {
                    Tag = (string)obj["tag_name"],
                    Notes = (string)obj["body"] ?? ""
                };
                info.Version = ParseTag(info.Tag);
                if (info.Version == null)
                {
                    LogHelper.Warn("[Update] 无法解析版本号: " + info.Tag);
                    return null;
                }
                var assets = obj["assets"] as JArray;
                var zip = assets?.FirstOrDefault(a =>
                    ((string)a["name"] ?? "").EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                if (zip == null)
                {
                    LogHelper.Warn("[Update] Release " + info.Tag + " 没有 zip 资源");
                    return null;
                }
                info.AssetName = (string)zip["name"];
                info.DownloadUrl = (string)zip["browser_download_url"];
                info.Size = (long?)zip["size"] ?? 0;
                string digest = (string)zip["digest"];
                if (!string.IsNullOrEmpty(digest) && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                {
                    info.Sha256 = digest.Substring(7).ToLowerInvariant();
                }
                return info;
            }
            catch (Exception ex)
            {
                LogHelper.Warn("[Update] 检查更新失败: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 检查并在有新版本时询问用户。silent=true 用于启动时的后台检查：
        /// 没有新版本或网络失败时不打扰用户，且尊重“跳过此版本”。
        /// </summary>
        public static async void CheckForUpdates(Mainform owner, bool silent)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                var latest = await GetLatestAsync();
                if (latest == null)
                {
                    if (!silent)
                        MessageBox.Show(owner, "检查更新失败，请检查网络后重试。\n详情见 logs 目录下的日志。",
                            "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (latest.Version <= CurrentVersion)
                {
                    if (!silent)
                        MessageBox.Show(owner, "当前已是最新版本 " + Mainform.Version + "。",
                            "检查更新", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (silent && Mainform.Settings.SkippedVersion == latest.Version.ToString())
                {
                    LogHelper.Info("[Update] 用户已跳过版本 " + latest.Version);
                    return;
                }

                string notes = latest.Notes.Length > 600 ? latest.Notes.Substring(0, 600) + "…" : latest.Notes;
                string sizeText = latest.Size > 0 ? $"（{latest.Size / 1024.0 / 1024.0:0.0} MB）" : "";
                var answer = MessageBox.Show(owner,
                    $"发现新版本 {latest.Version}{sizeText}，当前版本 {Mainform.Version}。\n\n{notes}\n\n" +
                    "现在更新吗？程序会自动关闭、更新并重新打开。\n选“否”下次启动不再提示此版本。",
                    "发现新版本", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                if (answer == DialogResult.No)
                {
                    Mainform.Settings.SkippedVersion = latest.Version.ToString();
                    Mainform.SaveSettings();
                    return;
                }
                if (answer != DialogResult.Yes) return;

                await DownloadAndApplyAsync(owner, latest);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "CheckForUpdates");
                if (!silent)
                    MessageBox.Show(owner, "更新出错：" + ex.Message, "检查更新",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _busy = false;
            }
        }

        static async Task DownloadAndApplyAsync(Mainform owner, UpdateInfo info)
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            if (!IsDirectoryWritable(appDir))
            {
                MessageBox.Show(owner,
                    "程序所在目录没有写入权限，无法自动更新：\n" + appDir +
                    "\n\n请把程序放到非系统目录（如 D:\\VinXiangQi），或以管理员身份运行后再试。",
                    "无法更新", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string workDir = Path.Combine(Path.GetTempPath(), "VinXiangQi_update_" + info.Version);
            if (Directory.Exists(workDir))
            {
                try { Directory.Delete(workDir, true); } catch { }
            }
            Directory.CreateDirectory(workDir);
            string zipPath = Path.Combine(workDir, "update.zip");

            var urls = new List<string> { info.DownloadUrl };
            if (!string.IsNullOrEmpty(info.Sha256) && Mainform.Settings.UpdateMirrors != null)
            {
                foreach (var m in Mainform.Settings.UpdateMirrors)
                {
                    string prefix = (m ?? "").Trim();
                    if (prefix.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        urls.Add(prefix.TrimEnd('/') + "/" + info.DownloadUrl);
                }
            }

            string oldTitle = owner.Text;
            Exception lastError = null;
            bool ok = false;
            try
            {
                for (int i = 0; i < urls.Count && !ok; i++)
                {
                    string url = urls[i];
                    string label = i == 0 ? "" : "（备用线路" + i + "）";
                    try
                    {
                        if (File.Exists(zipPath)) File.Delete(zipPath);
                        LogHelper.Info("[Update] 下载 " + url);
                        await DownloadWithStallTimeoutAsync(owner, url, zipPath, label, i == 0);
                        var fi = new FileInfo(zipPath);
                        if (!fi.Exists || (info.Size > 0 && fi.Length != info.Size))
                            throw new Exception("下载的文件不完整");
                        if (!string.IsNullOrEmpty(info.Sha256))
                        {
                            string actual = ComputeSha256(zipPath);
                            if (actual != info.Sha256)
                                throw new Exception("文件校验失败（SHA256 不一致）");
                        }
                        ok = true;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        LogHelper.Warn("[Update] 线路失败 " + url + "：" + ex.Message);
                    }
                }
            }
            finally
            {
                try { owner.Text = oldTitle; } catch { }
            }
            if (!ok)
                throw new Exception("下载更新失败（已尝试 " + urls.Count + " 条线路）：" + (lastError?.Message ?? "未知错误"));

            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            string scriptPath = Path.Combine(workDir, "apply_update.ps1");
            File.WriteAllText(scriptPath, BuildScript(Process.GetCurrentProcess().Id, zipPath, workDir, appDir, exePath),
                new UTF8Encoding(true));   // 带 BOM，Windows PowerShell 5.1 才能正确识别中文路径

            LogHelper.Info("[Update] 下载完成，准备更新到 " + info.Version + "，脚本: " + scriptPath);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + scriptPath + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workDir
            };
            Process.Start(psi);

            // 正常关闭：触发 FormClosing 保存设置、停止引擎
            owner.Close();
        }

        /// <summary>
        /// 下载文件；超过 StallSeconds 秒没有任何进度就取消，好让调用方换线路。
        /// </summary>
        const int StallSeconds = 25;
        static async Task DownloadWithStallTimeoutAsync(Mainform owner, string url, string path, string label, bool githubHeaders)
        {
            using (var wc = CreateClient())
            {
                if (githubHeaders)
                    wc.Headers[HttpRequestHeader.Accept] = "application/octet-stream";
                else
                    wc.Headers.Remove(HttpRequestHeader.Accept);
                DateTime lastProgress = DateTime.Now;
                bool stalled = false;
                wc.DownloadProgressChanged += (s, e) =>
                {
                    lastProgress = DateTime.Now;
                    try { owner.BeginInvoke(new Action(() => owner.Text = $"Vin象棋 正在下载更新{label} {e.ProgressPercentage}%")); } catch { }
                };
                using (var timer = new System.Threading.Timer(_ =>
                {
                    if ((DateTime.Now - lastProgress).TotalSeconds > StallSeconds)
                    {
                        stalled = true;
                        try { wc.CancelAsync(); } catch { }
                    }
                }, null, 1000, 1000))
                {
                    try
                    {
                        await wc.DownloadFileTaskAsync(new Uri(url), path);
                    }
                    catch (Exception) when (stalled)
                    {
                        throw new TimeoutException(StallSeconds + " 秒没有下载进度");
                    }
                }
            }
        }

        static string PsQuote(string s)
        {
            return "'" + s.Replace("'", "''") + "'";
        }

        static string BuildScript(int pid, string zipPath, string workDir, string appDir, string exePath)
        {
            string logPath = Path.Combine(appDir, "logs", "update.log");
            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'Stop'");
            sb.AppendLine("$log = " + PsQuote(logPath));
            sb.AppendLine("function Log($m) { try { New-Item -ItemType Directory -Force -Path (Split-Path $log) | Out-Null; Add-Content -Path $log -Value (\"[{0}] {1}\" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $m) -Encoding UTF8 } catch {} }");
            sb.AppendLine("$appDir = " + PsQuote(appDir));
            sb.AppendLine("$exe = " + PsQuote(exePath));
            sb.AppendLine("$zip = " + PsQuote(zipPath));
            sb.AppendLine("$stage = Join-Path " + PsQuote(workDir) + " 'stage'");
            sb.AppendLine("try {");
            sb.AppendLine("  Log '开始更新，等待主程序退出'");
            sb.AppendLine("  try { Wait-Process -Id " + pid + " -Timeout 60 -ErrorAction SilentlyContinue } catch {}");
            sb.AppendLine("  if (Get-Process -Id " + pid + " -ErrorAction SilentlyContinue) { Stop-Process -Id " + pid + " -Force }");
            sb.AppendLine("  Start-Sleep -Milliseconds 800");
            sb.AppendLine("  if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }");
            sb.AppendLine("  Expand-Archive -Path $zip -DestinationPath $stage -Force");
            // zip 里如果只有一个顶层文件夹，就以它为根
            sb.AppendLine("  $items = @(Get-ChildItem $stage)");
            sb.AppendLine("  if ($items.Count -eq 1 -and $items[0].PSIsContainer) { $stage = $items[0].FullName }");
            // 永远不覆盖用户配置
            sb.AppendLine("  foreach ($keep in @('settings.json')) { $p = Join-Path $stage $keep; if (Test-Path $p) { Remove-Item $p -Force } }");
            // 被占用的文件重试几次
            sb.AppendLine("  for ($i = 0; $i -lt 5; $i++) {");
            sb.AppendLine("    try { Copy-Item -Path (Join-Path $stage '*') -Destination $appDir -Recurse -Force; break }");
            sb.AppendLine("    catch { if ($i -eq 4) { throw }; Start-Sleep -Seconds 2 }");
            sb.AppendLine("  }");
            sb.AppendLine("  Log '文件覆盖完成'");
            sb.AppendLine("} catch {");
            sb.AppendLine("  Log ('更新失败: ' + $_.Exception.Message)");
            sb.AppendLine("  Add-Type -AssemblyName System.Windows.Forms");
            sb.AppendLine("  [System.Windows.Forms.MessageBox]::Show('自动更新失败：' + $_.Exception.Message + \"`n详情见 logs\\update.log\", 'Vin象棋') | Out-Null");
            sb.AppendLine("}");
            sb.AppendLine("Start-Process -FilePath $exe -WorkingDirectory $appDir");
            return sb.ToString();
        }

        static string ComputeSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
            }
        }

        static bool IsDirectoryWritable(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".write_test_" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
