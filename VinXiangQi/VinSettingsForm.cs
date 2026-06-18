using System;
using System.Linq;
using System.Windows.Forms;

namespace VinXiangQi
{
    public partial class VinSettingsForm : Form
    {
        private Mainform _mainform;

        public VinSettingsForm(Mainform mainform)
        {
            InitializeComponent();
            _mainform = mainform;
            LoadSettings();
        }

        void LoadSettings()
        {
            // 引擎设置
            comboBox_engine.Items.Clear();
            foreach (var engine in Mainform.Settings.EngineList)
            {
                comboBox_engine.Items.Add(engine.Key);
                if (engine.Key == Mainform.Settings.SelectedEngine)
                    comboBox_engine.SelectedIndex = comboBox_engine.Items.Count - 1;
            }
            if (comboBox_engine.Items.Count > 0 && comboBox_engine.SelectedItem == null)
                comboBox_engine.SelectedIndex = 0;
            numericUpDown_thread_count.Value = Mainform.Settings.ThreadCount;
            numericUpDown_step_time.Value = (decimal)Mainform.Settings.EngineStepTime;
            numericUpDown_engine_depth.Value = (decimal)Mainform.Settings.EngineDepth;
            checkBox_auto_go.Checked = Mainform.Settings.AutoGo;
            checkBox_analyze_mode.Checked = Mainform.Settings.AnalyzingMode;

            // 连线设置
            comboBox_solution.Items.Clear();
            foreach (var solution in Mainform.SolutionList)
            {
                comboBox_solution.Items.Add(solution.Key);
                if (solution.Key == Mainform.Settings.SelectedSolution)
                    comboBox_solution.SelectedIndex = comboBox_solution.Items.Count - 1;
            }
            numericUpDown_scale_factor.Value = (decimal)(1.0f / Mainform.Settings.ScaleFactor);
            checkBox_universal_mode.Checked = Mainform.Settings.UniversalMode;
            checkBox_universal_mouse.Checked = Mainform.Settings.UniversalMouse;

            // 识别设置
            comboBox_yolo_models.Items.Clear();
            foreach (var yolo in Mainform.ModelList)
            {
                comboBox_yolo_models.Items.Add(yolo.Key);
                if (yolo.Key == Mainform.Settings.YoloModel)
                    comboBox_yolo_models.SelectedIndex = comboBox_yolo_models.Items.Count - 1;
            }
            if (comboBox_yolo_models.Items.Count > 0 && comboBox_yolo_models.SelectedItem == null)
                comboBox_yolo_models.SelectedIndex = 0;
            checkBox_debug.Checked = Mainform.ScreenDebug;
            _mainform.checkBox_debug.Checked = Mainform.ScreenDebug;

            // 高级
            checkBox_stop_when_mate.Checked = Mainform.Settings.StopWhenMate;
            numericUpDown_stop_score.Value = (decimal)Mainform.Settings.StopScore;
            checkBox_auto_click.Checked = Mainform.Settings.AutoClick;
            checkBox_show_floating_score.Checked = Mainform.Settings.ShowFloatingScore;
        }

        // === 引擎设置事件 ===
        private void comboBox_engine_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox_engine.SelectedItem == null) return;
            if (comboBox_engine.SelectedItem.ToString() != Mainform.Settings.SelectedEngine)
            {
                Mainform.Settings.SelectedEngine = comboBox_engine.SelectedItem.ToString();
                if (!Mainform.Settings.EngineList.ContainsKey(Mainform.Settings.SelectedEngine) || !System.IO.File.Exists(Mainform.Settings.EngineList[Mainform.Settings.SelectedEngine].ExePath))
                {
                    MessageBox.Show("引擎文件不存在！");
                    return;
                }
                _mainform.InitEngine();
                Mainform.SaveSettings();
            }
        }

        private void button_engine_add_Click(object sender, EventArgs e)
        {
            if (openFileDialog_engine.ShowDialog() == DialogResult.OK)
            {
                // 存绝对路径，由 Mainform.SaveSettings 统一相对化（避免 CurrentDirectory 不稳定）
                string path = openFileDialog_engine.FileName;
                if (Mainform.Settings.EngineList.Values.Any(eng => string.Equals(eng.ExePath, path, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("该引擎已存在");
                    return;
                }
                EngineSettings esettings = new EngineSettings();
                esettings.ExePath = path;
                // 用引擎文件名作为 key，和 EngineManageForm 保持一致，便于后续按名查找
                string key = System.IO.Path.GetFileNameWithoutExtension(path);
                string baseKey = key;
                for (int i = 1; Mainform.Settings.EngineList.ContainsKey(key); i++)
                    key = baseKey + "_" + i;
                esettings.Name = key;
                Mainform.Settings.EngineList.Add(key, esettings);
                Mainform.Settings.SelectedEngine = key;
                Mainform.SaveSettings();
                LoadSettings();
            }
        }

        private void button_engine_delete_Click(object sender, EventArgs e)
        {
            if (Mainform.Settings.SelectedEngine.Length > 0)
            {
                if (Mainform.Engine != null) Mainform.Engine.Stop();
                Mainform.Settings.EngineList.Remove(Mainform.Settings.SelectedEngine);
                if (Mainform.Settings.EngineList.Count > 0)
                    Mainform.Settings.SelectedEngine = Mainform.Settings.EngineList.Keys.First().ToString();
                else
                    Mainform.Settings.SelectedEngine = "";
                Mainform.SaveSettings();
                LoadSettings();
            }
        }

        private void numericUpDown_thread_count_ValueChanged(object sender, EventArgs e)
        {
            string threadStr = numericUpDown_thread_count.Value.ToString();
            if (Mainform.Engine != null)
            {
                Mainform.Engine.SetOption("Threads", threadStr);
            }
            Mainform.Settings.ThreadCount = (int)numericUpDown_thread_count.Value;
            if (Mainform.Settings.CurrentEngine == null) return;
            if (Mainform.Settings.CurrentEngine.Configs.ContainsKey("Threads"))
                Mainform.Settings.CurrentEngine.Configs["Threads"] = threadStr;
            else
                Mainform.Settings.CurrentEngine.Configs.Add("Threads", threadStr);
            Mainform.SaveSettings();
        }

        private void numericUpDown_step_time_ValueChanged(object sender, EventArgs e)
        {
            Mainform.Settings.EngineStepTime = (double)numericUpDown_step_time.Value;
            Mainform.SaveSettings();
        }

        private void numericUpDown_engine_depth_ValueChanged(object sender, EventArgs e)
        {
            Mainform.Settings.EngineDepth = (int)numericUpDown_engine_depth.Value;
            Mainform.SaveSettings();
        }

        private void checkBox_auto_go_CheckedChanged(object sender, EventArgs e)
        {
            Mainform.Settings.AutoGo = checkBox_auto_go.Checked;
            if (Mainform.Settings.AutoGo)
            {
                checkBox_analyze_mode.Checked = false;
            }
            Mainform.SaveSettings();
        }

        private void checkBox_analyze_mode_CheckedChanged(object sender, EventArgs e)
        {
            if (Mainform.Engine != null)
            {
                Mainform.Engine.StopAnalyze();
                Mainform.Engine.AnalyzeQueue.Clear();
                Mainform.Engine.SkipList.Clear();
            }
            Mainform.Settings.AnalyzingMode = checkBox_analyze_mode.Checked;
            if (Mainform.Settings.AnalyzingMode)
            {
                if (Mainform.Settings.AutoGo)
                {
                    checkBox_auto_go.Checked = false;
                }
            }
            Mainform.SaveSettings();
        }

        // === 连线设置事件 ===
        private void comboBox_solution_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox_solution.SelectedItem != null)
            {
                Mainform.Settings.SelectedSolution = comboBox_solution.SelectedItem.ToString();
                Mainform.SaveSettings();
                _mainform.LoadCurrentSolution();
            }
            else
            {
                if (comboBox_solution.Text == "自定义方案")
                {
                    Mainform.Settings.SelectedSolution = "自定义方案";
                    Mainform.SaveSettings();
                    _mainform.LoadCurrentSolution();
                }
            }
        }

        private void button_get_hwnd_Click(object sender, EventArgs e)
        {
            _mainform.button_stop_detection_Click(sender, e);
            _mainform.GetHandle();
        }

        private void numericUpDown_scale_factor_ValueChanged(object sender, EventArgs e)
        {
            Mainform.Settings.ScaleFactor = 1.0f / (float)numericUpDown_scale_factor.Value;
            Mainform.SaveSettings();
        }

        private void button_save_as_solution_Click(object sender, EventArgs e)
        {
            SolutionSavingForm ssf = new SolutionSavingForm();
            ssf.StartPosition = FormStartPosition.CenterParent;
            ssf.ShowDialog();
            _mainform.InitSolutions();
            LoadSettings();
        }

        private void button_clipboard_solution_Click(object sender, EventArgs e)
        {
            if (Mainform.SolutionList.ContainsKey("剪切板"))
            {
                Mainform.Settings.SelectedSolution = "剪切板";
            }
            else
            {
                Mainform.SolutionList.Add("剪切板", new Mainform.Solution("", "", "", ""));
                Mainform.Settings.SelectedSolution = "剪切板";
            }
            LoadSettings();
        }

        private void checkBox_universal_mode_CheckedChanged(object sender, EventArgs e)
        {
            Mainform.Settings.UniversalMode = checkBox_universal_mode.Checked;
            Mainform.SaveSettings();
        }

        private void checkBox_universal_mouse_CheckedChanged(object sender, EventArgs e)
        {
            Mainform.Settings.UniversalMouse = checkBox_universal_mouse.Checked;
            Mainform.SaveSettings();
        }

        private void button_screenshot_Click(object sender, EventArgs e)
        {
            _mainform.button_screenshot_Click(sender, e);
        }

        // === 识别设置事件 ===
        private void comboBox_yolo_models_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox_yolo_models.SelectedItem != null)
            {
                Mainform.Settings.YoloModel = comboBox_yolo_models.SelectedItem.ToString();
            }
            Mainform.Model = Mainform.ModelList[Mainform.Settings.YoloModel];
            Mainform.SaveSettings();
        }

        private void checkBox_debug_CheckedChanged(object sender, EventArgs e)
        {
            Mainform.ScreenDebug = checkBox_debug.Checked;
            _mainform.checkBox_debug.Checked = checkBox_debug.Checked;
            if (checkBox_debug.Checked)
            {
                if (Mainform.DetectEnabled)
                {
                    Mainform.DetectEnabled = false;
                    _mainform.ModeDisplay();
                }
            }
        }

        private void button_redetect_Click(object sender, EventArgs e)
        {
            _mainform.button_redetect_Click(sender, e);
        }

        private void button_advance_settings_Click(object sender, EventArgs e)
        {
            _mainform.button_advance_settings_Click(sender, e);
        }

        // === 高级设置事件 ===
        private void checkBox_stop_when_mate_CheckedChanged(object sender, EventArgs e)
        {
            Mainform.Settings.StopWhenMate = checkBox_stop_when_mate.Checked;
            Mainform.SaveSettings();
        }

        private void numericUpDown_stop_score_ValueChanged(object sender, EventArgs e)
        {
            Mainform.Settings.StopScore = (int)numericUpDown_stop_score.Value;
            Mainform.SaveSettings();
        }

        private void checkBox_auto_click_CheckedChanged(object sender, EventArgs e)
        {
            Mainform.Settings.AutoClick = checkBox_auto_click.Checked;
            Mainform.SaveSettings();
        }

        private void checkBox_show_floating_score_CheckedChanged(object sender, EventArgs e)
        {
            Mainform.Settings.ShowFloatingScore = checkBox_show_floating_score.Checked;
            if (Mainform.Settings.ShowFloatingScore)
                _mainform.InitFloatingScore();
            else
                _mainform.HideFloatingScore();
            Mainform.SaveSettings();
        }

        private void button_go_immediately_Click(object sender, EventArgs e)
        {
            if (Mainform.Engine != null) Mainform.Engine.StopAnalyze();
        }

        private void button_openbook_manage_Click(object sender, EventArgs e)
        {
            OpenBookSettingsForm opsf = new OpenBookSettingsForm();
            opsf.StartPosition = FormStartPosition.CenterParent;
            opsf.ShowDialog();
        }
    }
}
