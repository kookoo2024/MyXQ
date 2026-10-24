using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace VinXiangQi
{
    public class ProgramSettings
    {
        public Dictionary<string, EngineSettings> EngineList = new Dictionary<string, EngineSettings>();
        public string SelectedEngine = "";
        public float ScaleFactor = 1.0f;
        public bool AutoGo = true;
        public double EngineStepTime = 2.0;
        public int StopScore = 2000;
        public int EngineDepth = 200;
        public int ThreadCount = 4;
        public int DetectionConfirmCount = 1;
        public int DetectionInterval = 550;
        public bool KeepDetecting = false;
        public bool UniversalMode = false;
        public bool UniversalMouse = false;
        public string SelectedSolution = "天天象棋";
        public bool AnalyzingMode = false;
        public bool AutoClick = false;
        public bool StopWhenMate = false;
        public string YoloModel = "小模型";
        public bool UseOpenBook = false;
        public bool UseChessDB = false;
        public bool BackgroundAnalysis = false;
        public double MinTimeUsingOpenbook = 1.0;
        public OpenBookMode OpenbookMode = OpenBookMode.HighestScore;
        public int WindowX = -1;
        public int WindowY = -1;
        public bool ShowFloatingScore = false;
        public int FloatingScoreX = -1;
        public int FloatingScoreY = -1;
        // 模仿人类：开启后每次确定着法后插入一段与难度自适应的随机延时再落子，
        // 模拟真人思考节奏，避免像机器一样匀速出招。最长不超过 15 秒。
        // 延时期间若检测到棋盘被人为改动，则放弃本次落子（防止重复落子）。
        public bool MimicHuman = false;
        // 在线更新：启动时自动检查；发布源为公开仓库 "owner/repo" 的 GitHub Releases
        public bool AutoCheckUpdate = true;
        public string UpdateRepo = "kookoo2024/MyXQ";
        // 用户在提示框里选择“否”跳过的版本号，启动检查时不再提示该版本
        public string SkippedVersion = "";

        public enum OpenBookMode
        {
            HighestScore,
            Random
        }

        // CurrentEngine 只是 EngineList[SelectedEngine] 的便捷视图，属计算属性，
        // 不应序列化。原实现被序列化进 settings.json，里面带着 ExePath 绝对路径，
        // 既冗余又会在换机器时造成混乱。
        [JsonIgnore]
        public EngineSettings CurrentEngine
        {
            get
            {
                if (EngineList.ContainsKey(SelectedEngine))
                {
                    return EngineList[SelectedEngine];
                }
                return null;
            }
        }
    }

    public class EngineSettings
    {
        public string ExePath = "";
        public string Name = "";
        public string Author = "";
        public Dictionary<string, string> Configs = new Dictionary<string, string>();
    }
}
