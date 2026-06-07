namespace VinXiangQi
{
    partial class VinSettingsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabPage_connection = new System.Windows.Forms.TabPage();
            this.tabPage_engine = new System.Windows.Forms.TabPage();
            this.tabPage_detection = new System.Windows.Forms.TabPage();
            this.tabPage_openbook = new System.Windows.Forms.TabPage();
            this.tabPage_advanced = new System.Windows.Forms.TabPage();
            this.openFileDialog_engine = new System.Windows.Forms.OpenFileDialog();

            // --- Tab 1: 连线设置 ---
            this.label_solution = new System.Windows.Forms.Label();
            this.comboBox_solution = new System.Windows.Forms.ComboBox();
            this.button_get_hwnd = new System.Windows.Forms.Button();
            this.button_save_as_solution = new System.Windows.Forms.Button();
            this.button_clipboard_solution = new System.Windows.Forms.Button();
            this.button_screenshot = new System.Windows.Forms.Button();
            this.label_scale = new System.Windows.Forms.Label();
            this.numericUpDown_scale_factor = new System.Windows.Forms.NumericUpDown();
            this.checkBox_universal_mode = new System.Windows.Forms.CheckBox();
            this.checkBox_universal_mouse = new System.Windows.Forms.CheckBox();

            // --- Tab 2: 引擎设置 ---
            this.label_engine = new System.Windows.Forms.Label();
            this.comboBox_engine = new System.Windows.Forms.ComboBox();
            this.button_engine_add = new System.Windows.Forms.Button();
            this.button_engine_delete = new System.Windows.Forms.Button();
            this.label_thread = new System.Windows.Forms.Label();
            this.numericUpDown_thread_count = new System.Windows.Forms.NumericUpDown();
            this.label_step_time = new System.Windows.Forms.Label();
            this.numericUpDown_step_time = new System.Windows.Forms.NumericUpDown();
            this.label_depth = new System.Windows.Forms.Label();
            this.numericUpDown_engine_depth = new System.Windows.Forms.NumericUpDown();
            this.checkBox_auto_go = new System.Windows.Forms.CheckBox();
            this.checkBox_analyze_mode = new System.Windows.Forms.CheckBox();
            this.button_go_immediately = new System.Windows.Forms.Button();

            // --- Tab 3: 识别设置 ---
            this.label_model = new System.Windows.Forms.Label();
            this.comboBox_yolo_models = new System.Windows.Forms.ComboBox();
            this.checkBox_debug = new System.Windows.Forms.CheckBox();
            this.button_redetect = new System.Windows.Forms.Button();
            this.button_advance_settings = new System.Windows.Forms.Button();

            // --- Tab 4: 开局库 ---
            this.label_openbook_info = new System.Windows.Forms.Label();
            this.button_openbook_manage = new System.Windows.Forms.Button();

            // --- Tab 5: 高级 ---
            this.checkBox_stop_when_mate = new System.Windows.Forms.CheckBox();
            this.label_stop_score = new System.Windows.Forms.Label();
            this.numericUpDown_stop_score = new System.Windows.Forms.NumericUpDown();
            this.label_score_unit = new System.Windows.Forms.Label();
            this.checkBox_auto_click = new System.Windows.Forms.CheckBox();

            this.tabControl.SuspendLayout();
            this.tabPage_connection.SuspendLayout();
            this.tabPage_engine.SuspendLayout();
            this.tabPage_detection.SuspendLayout();
            this.tabPage_openbook.SuspendLayout();
            this.tabPage_advanced.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_scale_factor)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_thread_count)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_step_time)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_engine_depth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_stop_score)).BeginInit();
            this.SuspendLayout();

            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabPage_connection);
            this.tabControl.Controls.Add(this.tabPage_engine);
            this.tabControl.Controls.Add(this.tabPage_detection);
            this.tabControl.Controls.Add(this.tabPage_openbook);
            this.tabControl.Controls.Add(this.tabPage_advanced);
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.Location = new System.Drawing.Point(0, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(484, 438);
            this.tabControl.TabIndex = 0;
            // 
            // tabPage_connection
            // 
            this.tabPage_connection.Controls.Add(this.checkBox_universal_mouse);
            this.tabPage_connection.Controls.Add(this.checkBox_universal_mode);
            this.tabPage_connection.Controls.Add(this.numericUpDown_scale_factor);
            this.tabPage_connection.Controls.Add(this.label_scale);
            this.tabPage_connection.Controls.Add(this.button_screenshot);
            this.tabPage_connection.Controls.Add(this.button_clipboard_solution);
            this.tabPage_connection.Controls.Add(this.button_save_as_solution);
            this.tabPage_connection.Controls.Add(this.button_get_hwnd);
            this.tabPage_connection.Controls.Add(this.comboBox_solution);
            this.tabPage_connection.Controls.Add(this.label_solution);
            this.tabPage_connection.Location = new System.Drawing.Point(4, 26);
            this.tabPage_connection.Name = "tabPage_connection";
            this.tabPage_connection.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage_connection.Size = new System.Drawing.Size(476, 408);
            this.tabPage_connection.TabIndex = 0;
            this.tabPage_connection.Text = "连线设置";
            this.tabPage_connection.UseVisualStyleBackColor = true;
            // 
            // label_solution
            // 
            this.label_solution.AutoSize = true;
            this.label_solution.Location = new System.Drawing.Point(14, 15);
            this.label_solution.Name = "label_solution";
            this.label_solution.Size = new System.Drawing.Size(45, 15);
            this.label_solution.TabIndex = 0;
            this.label_solution.Text = "方案:";
            // 
            // comboBox_solution
            // 
            this.comboBox_solution.FormattingEnabled = true;
            this.comboBox_solution.Location = new System.Drawing.Point(65, 12);
            this.comboBox_solution.Name = "comboBox_solution";
            this.comboBox_solution.Size = new System.Drawing.Size(200, 23);
            this.comboBox_solution.TabIndex = 1;
            this.comboBox_solution.SelectedIndexChanged += new System.EventHandler(this.comboBox_solution_SelectedIndexChanged);
            // 
            // button_get_hwnd
            // 
            this.button_get_hwnd.Location = new System.Drawing.Point(271, 10);
            this.button_get_hwnd.Name = "button_get_hwnd";
            this.button_get_hwnd.Size = new System.Drawing.Size(120, 28);
            this.button_get_hwnd.TabIndex = 2;
            this.button_get_hwnd.Text = "寻找新连线窗口";
            this.button_get_hwnd.UseVisualStyleBackColor = true;
            this.button_get_hwnd.Click += new System.EventHandler(this.button_get_hwnd_Click);
            // 
            // button_save_as_solution
            // 
            this.button_save_as_solution.Location = new System.Drawing.Point(14, 48);
            this.button_save_as_solution.Name = "button_save_as_solution";
            this.button_save_as_solution.Size = new System.Drawing.Size(140, 28);
            this.button_save_as_solution.TabIndex = 3;
            this.button_save_as_solution.Text = "保存当前方案";
            this.button_save_as_solution.UseVisualStyleBackColor = true;
            this.button_save_as_solution.Click += new System.EventHandler(this.button_save_as_solution_Click);
            // 
            // button_clipboard_solution
            // 
            this.button_clipboard_solution.Location = new System.Drawing.Point(160, 48);
            this.button_clipboard_solution.Name = "button_clipboard_solution";
            this.button_clipboard_solution.Size = new System.Drawing.Size(140, 28);
            this.button_clipboard_solution.TabIndex = 4;
            this.button_clipboard_solution.Text = "识别剪切板图片";
            this.button_clipboard_solution.UseVisualStyleBackColor = true;
            this.button_clipboard_solution.Click += new System.EventHandler(this.button_clipboard_solution_Click);
            // 
            // button_screenshot
            // 
            this.button_screenshot.Location = new System.Drawing.Point(14, 82);
            this.button_screenshot.Name = "button_screenshot";
            this.button_screenshot.Size = new System.Drawing.Size(140, 28);
            this.button_screenshot.TabIndex = 5;
            this.button_screenshot.Text = "自动点击续盘管理";
            this.button_screenshot.UseVisualStyleBackColor = true;
            this.button_screenshot.Click += new System.EventHandler(this.button_screenshot_Click);
            // 
            // label_scale
            // 
            this.label_scale.AutoSize = true;
            this.label_scale.Location = new System.Drawing.Point(14, 122);
            this.label_scale.Name = "label_scale";
            this.label_scale.Size = new System.Drawing.Size(45, 15);
            this.label_scale.TabIndex = 6;
            this.label_scale.Text = "缩放比";
            // 
            // numericUpDown_scale_factor
            // 
            this.numericUpDown_scale_factor.DecimalPlaces = 2;
            this.numericUpDown_scale_factor.Increment = new decimal(new int[] {
            5,
            0,
            0,
            131072});
            this.numericUpDown_scale_factor.Location = new System.Drawing.Point(65, 118);
            this.numericUpDown_scale_factor.Maximum = new decimal(new int[] {
            4,
            0,
            0,
            0});
            this.numericUpDown_scale_factor.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this.numericUpDown_scale_factor.Name = "numericUpDown_scale_factor";
            this.numericUpDown_scale_factor.Size = new System.Drawing.Size(100, 25);
            this.numericUpDown_scale_factor.TabIndex = 7;
            this.numericUpDown_scale_factor.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDown_scale_factor.ValueChanged += new System.EventHandler(this.numericUpDown_scale_factor_ValueChanged);
            // 
            // checkBox_universal_mode
            // 
            this.checkBox_universal_mode.AutoSize = true;
            this.checkBox_universal_mode.Location = new System.Drawing.Point(14, 158);
            this.checkBox_universal_mode.Name = "checkBox_universal_mode";
            this.checkBox_universal_mode.Size = new System.Drawing.Size(89, 19);
            this.checkBox_universal_mode.TabIndex = 8;
            this.checkBox_universal_mode.Text = "前台截图";
            this.checkBox_universal_mode.UseVisualStyleBackColor = true;
            this.checkBox_universal_mode.CheckedChanged += new System.EventHandler(this.checkBox_universal_mode_CheckedChanged);
            // 
            // checkBox_universal_mouse
            // 
            this.checkBox_universal_mouse.AutoSize = true;
            this.checkBox_universal_mouse.Location = new System.Drawing.Point(120, 158);
            this.checkBox_universal_mouse.Name = "checkBox_universal_mouse";
            this.checkBox_universal_mouse.Size = new System.Drawing.Size(89, 19);
            this.checkBox_universal_mouse.TabIndex = 9;
            this.checkBox_universal_mouse.Text = "前台鼠标";
            this.checkBox_universal_mouse.UseVisualStyleBackColor = true;
            this.checkBox_universal_mouse.CheckedChanged += new System.EventHandler(this.checkBox_universal_mouse_CheckedChanged);
            // 
            // tabPage_engine
            // 
            this.tabPage_engine.Controls.Add(this.button_go_immediately);
            this.tabPage_engine.Controls.Add(this.checkBox_analyze_mode);
            this.tabPage_engine.Controls.Add(this.checkBox_auto_go);
            this.tabPage_engine.Controls.Add(this.numericUpDown_engine_depth);
            this.tabPage_engine.Controls.Add(this.label_depth);
            this.tabPage_engine.Controls.Add(this.numericUpDown_step_time);
            this.tabPage_engine.Controls.Add(this.label_step_time);
            this.tabPage_engine.Controls.Add(this.numericUpDown_thread_count);
            this.tabPage_engine.Controls.Add(this.label_thread);
            this.tabPage_engine.Controls.Add(this.button_engine_delete);
            this.tabPage_engine.Controls.Add(this.button_engine_add);
            this.tabPage_engine.Controls.Add(this.comboBox_engine);
            this.tabPage_engine.Controls.Add(this.label_engine);
            this.tabPage_engine.Location = new System.Drawing.Point(4, 26);
            this.tabPage_engine.Name = "tabPage_engine";
            this.tabPage_engine.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage_engine.Size = new System.Drawing.Size(476, 408);
            this.tabPage_engine.TabIndex = 1;
            this.tabPage_engine.Text = "引擎设置";
            this.tabPage_engine.UseVisualStyleBackColor = true;
            // 
            // label_engine
            // 
            this.label_engine.AutoSize = true;
            this.label_engine.Location = new System.Drawing.Point(14, 15);
            this.label_engine.Name = "label_engine";
            this.label_engine.Size = new System.Drawing.Size(45, 15);
            this.label_engine.TabIndex = 0;
            this.label_engine.Text = "引擎:";
            // 
            // comboBox_engine
            // 
            this.comboBox_engine.FormattingEnabled = true;
            this.comboBox_engine.Location = new System.Drawing.Point(65, 12);
            this.comboBox_engine.Name = "comboBox_engine";
            this.comboBox_engine.Size = new System.Drawing.Size(200, 23);
            this.comboBox_engine.TabIndex = 1;
            this.comboBox_engine.SelectedIndexChanged += new System.EventHandler(this.comboBox_engine_SelectedIndexChanged);
            // 
            // button_engine_add
            // 
            this.button_engine_add.Location = new System.Drawing.Point(271, 10);
            this.button_engine_add.Name = "button_engine_add";
            this.button_engine_add.Size = new System.Drawing.Size(60, 28);
            this.button_engine_add.TabIndex = 2;
            this.button_engine_add.Text = "添加";
            this.button_engine_add.UseVisualStyleBackColor = true;
            this.button_engine_add.Click += new System.EventHandler(this.button_engine_add_Click);
            // 
            // button_engine_delete
            // 
            this.button_engine_delete.Location = new System.Drawing.Point(337, 10);
            this.button_engine_delete.Name = "button_engine_delete";
            this.button_engine_delete.Size = new System.Drawing.Size(60, 28);
            this.button_engine_delete.TabIndex = 3;
            this.button_engine_delete.Text = "删除";
            this.button_engine_delete.UseVisualStyleBackColor = true;
            this.button_engine_delete.Click += new System.EventHandler(this.button_engine_delete_Click);
            // 
            // label_thread
            // 
            this.label_thread.AutoSize = true;
            this.label_thread.Location = new System.Drawing.Point(14, 52);
            this.label_thread.Name = "label_thread";
            this.label_thread.Size = new System.Drawing.Size(52, 15);
            this.label_thread.TabIndex = 4;
            this.label_thread.Text = "线程数:";
            // 
            // numericUpDown_thread_count
            // 
            this.numericUpDown_thread_count.Location = new System.Drawing.Point(72, 48);
            this.numericUpDown_thread_count.Maximum = new decimal(new int[] {
            128,
            0,
            0,
            0});
            this.numericUpDown_thread_count.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDown_thread_count.Name = "numericUpDown_thread_count";
            this.numericUpDown_thread_count.Size = new System.Drawing.Size(70, 25);
            this.numericUpDown_thread_count.TabIndex = 5;
            this.numericUpDown_thread_count.Value = new decimal(new int[] {
            8,
            0,
            0,
            0});
            this.numericUpDown_thread_count.ValueChanged += new System.EventHandler(this.numericUpDown_thread_count_ValueChanged);
            // 
            // label_step_time
            // 
            this.label_step_time.AutoSize = true;
            this.label_step_time.Location = new System.Drawing.Point(160, 52);
            this.label_step_time.Name = "label_step_time";
            this.label_step_time.Size = new System.Drawing.Size(52, 15);
            this.label_step_time.TabIndex = 6;
            this.label_step_time.Text = "步时(s):";
            // 
            // numericUpDown_step_time
            // 
            this.numericUpDown_step_time.DecimalPlaces = 1;
            this.numericUpDown_step_time.Increment = new decimal(new int[] {
            5,
            0,
            0,
            65536});
            this.numericUpDown_step_time.Location = new System.Drawing.Point(218, 48);
            this.numericUpDown_step_time.Maximum = new decimal(new int[] {
            9999,
            0,
            0,
            0});
            this.numericUpDown_step_time.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            this.numericUpDown_step_time.Name = "numericUpDown_step_time";
            this.numericUpDown_step_time.Size = new System.Drawing.Size(70, 25);
            this.numericUpDown_step_time.TabIndex = 7;
            this.numericUpDown_step_time.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDown_step_time.ValueChanged += new System.EventHandler(this.numericUpDown_step_time_ValueChanged);
            // 
            // label_depth
            // 
            this.label_depth.AutoSize = true;
            this.label_depth.Location = new System.Drawing.Point(14, 88);
            this.label_depth.Name = "label_depth";
            this.label_depth.Size = new System.Drawing.Size(67, 15);
            this.label_depth.TabIndex = 8;
            this.label_depth.Text = "思考深度:";
            // 
            // numericUpDown_engine_depth
            // 
            this.numericUpDown_engine_depth.Location = new System.Drawing.Point(87, 84);
            this.numericUpDown_engine_depth.Maximum = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this.numericUpDown_engine_depth.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDown_engine_depth.Name = "numericUpDown_engine_depth";
            this.numericUpDown_engine_depth.Size = new System.Drawing.Size(70, 25);
            this.numericUpDown_engine_depth.TabIndex = 9;
            this.numericUpDown_engine_depth.Value = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this.numericUpDown_engine_depth.ValueChanged += new System.EventHandler(this.numericUpDown_engine_depth_ValueChanged);
            // 
            // checkBox_auto_go
            // 
            this.checkBox_auto_go.AutoSize = true;
            this.checkBox_auto_go.Checked = true;
            this.checkBox_auto_go.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_auto_go.Location = new System.Drawing.Point(14, 128);
            this.checkBox_auto_go.Name = "checkBox_auto_go";
            this.checkBox_auto_go.Size = new System.Drawing.Size(89, 19);
            this.checkBox_auto_go.TabIndex = 10;
            this.checkBox_auto_go.Text = "自动走棋";
            this.checkBox_auto_go.UseVisualStyleBackColor = true;
            this.checkBox_auto_go.CheckedChanged += new System.EventHandler(this.checkBox_auto_go_CheckedChanged);
            // 
            // checkBox_analyze_mode
            // 
            this.checkBox_analyze_mode.AutoSize = true;
            this.checkBox_analyze_mode.Location = new System.Drawing.Point(120, 128);
            this.checkBox_analyze_mode.Name = "checkBox_analyze_mode";
            this.checkBox_analyze_mode.Size = new System.Drawing.Size(89, 19);
            this.checkBox_analyze_mode.TabIndex = 11;
            this.checkBox_analyze_mode.Text = "分析模式";
            this.checkBox_analyze_mode.UseVisualStyleBackColor = true;
            this.checkBox_analyze_mode.CheckedChanged += new System.EventHandler(this.checkBox_analyze_mode_CheckedChanged);
            // 
            // button_go_immediately
            // 
            this.button_go_immediately.Location = new System.Drawing.Point(14, 166);
            this.button_go_immediately.Name = "button_go_immediately";
            this.button_go_immediately.Size = new System.Drawing.Size(120, 28);
            this.button_go_immediately.TabIndex = 12;
            this.button_go_immediately.Text = "立即出招";
            this.button_go_immediately.UseVisualStyleBackColor = true;
            this.button_go_immediately.Click += new System.EventHandler(this.button_go_immediately_Click);
            // 
            // tabPage_detection
            // 
            this.tabPage_detection.Controls.Add(this.button_advance_settings);
            this.tabPage_detection.Controls.Add(this.button_redetect);
            this.tabPage_detection.Controls.Add(this.checkBox_debug);
            this.tabPage_detection.Controls.Add(this.comboBox_yolo_models);
            this.tabPage_detection.Controls.Add(this.label_model);
            this.tabPage_detection.Location = new System.Drawing.Point(4, 26);
            this.tabPage_detection.Name = "tabPage_detection";
            this.tabPage_detection.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage_detection.Size = new System.Drawing.Size(476, 408);
            this.tabPage_detection.TabIndex = 2;
            this.tabPage_detection.Text = "识别设置";
            this.tabPage_detection.UseVisualStyleBackColor = true;
            // 
            // label_model
            // 
            this.label_model.AutoSize = true;
            this.label_model.Location = new System.Drawing.Point(14, 15);
            this.label_model.Name = "label_model";
            this.label_model.Size = new System.Drawing.Size(67, 15);
            this.label_model.TabIndex = 0;
            this.label_model.Text = "识别模型:";
            // 
            // comboBox_yolo_models
            // 
            this.comboBox_yolo_models.FormattingEnabled = true;
            this.comboBox_yolo_models.Location = new System.Drawing.Point(87, 12);
            this.comboBox_yolo_models.Name = "comboBox_yolo_models";
            this.comboBox_yolo_models.Size = new System.Drawing.Size(200, 23);
            this.comboBox_yolo_models.TabIndex = 1;
            this.comboBox_yolo_models.SelectedIndexChanged += new System.EventHandler(this.comboBox_yolo_models_SelectedIndexChanged);
            // 
            // checkBox_debug
            // 
            this.checkBox_debug.AutoSize = true;
            this.checkBox_debug.Checked = true;
            this.checkBox_debug.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_debug.Location = new System.Drawing.Point(14, 50);
            this.checkBox_debug.Name = "checkBox_debug";
            this.checkBox_debug.Size = new System.Drawing.Size(89, 19);
            this.checkBox_debug.TabIndex = 2;
            this.checkBox_debug.Text = "调试状态";
            this.checkBox_debug.UseVisualStyleBackColor = true;
            this.checkBox_debug.CheckedChanged += new System.EventHandler(this.checkBox_debug_CheckedChanged);
            // 
            // button_redetect
            // 
            this.button_redetect.Location = new System.Drawing.Point(14, 82);
            this.button_redetect.Name = "button_redetect";
            this.button_redetect.Size = new System.Drawing.Size(120, 28);
            this.button_redetect.TabIndex = 3;
            this.button_redetect.Text = "重新检测";
            this.button_redetect.UseVisualStyleBackColor = true;
            this.button_redetect.Click += new System.EventHandler(this.button_redetect_Click);
            // 
            // button_advance_settings
            // 
            this.button_advance_settings.Location = new System.Drawing.Point(14, 116);
            this.button_advance_settings.Name = "button_advance_settings";
            this.button_advance_settings.Size = new System.Drawing.Size(140, 28);
            this.button_advance_settings.TabIndex = 4;
            this.button_advance_settings.Text = "识别高级设置";
            this.button_advance_settings.UseVisualStyleBackColor = true;
            this.button_advance_settings.Click += new System.EventHandler(this.button_advance_settings_Click);
            // 
            // tabPage_openbook
            // 
            this.tabPage_openbook.Controls.Add(this.button_openbook_manage);
            this.tabPage_openbook.Controls.Add(this.label_openbook_info);
            this.tabPage_openbook.Location = new System.Drawing.Point(4, 26);
            this.tabPage_openbook.Name = "tabPage_openbook";
            this.tabPage_openbook.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage_openbook.Size = new System.Drawing.Size(476, 408);
            this.tabPage_openbook.TabIndex = 3;
            this.tabPage_openbook.Text = "开局库";
            this.tabPage_openbook.UseVisualStyleBackColor = true;
            // 
            // label_openbook_info
            // 
            this.label_openbook_info.AutoSize = true;
            this.label_openbook_info.Location = new System.Drawing.Point(14, 15);
            this.label_openbook_info.Name = "label_openbook_info";
            this.label_openbook_info.Size = new System.Drawing.Size(200, 15);
            this.label_openbook_info.TabIndex = 0;
            this.label_openbook_info.Text = "开局库文件放在 ./OpenBooks/ 目录下";
            // 
            // button_openbook_manage
            // 
            this.button_openbook_manage.Location = new System.Drawing.Point(14, 48);
            this.button_openbook_manage.Name = "button_openbook_manage";
            this.button_openbook_manage.Size = new System.Drawing.Size(140, 28);
            this.button_openbook_manage.TabIndex = 1;
            this.button_openbook_manage.Text = "开局库管理";
            this.button_openbook_manage.UseVisualStyleBackColor = true;
            this.button_openbook_manage.Click += new System.EventHandler(this.button_openbook_manage_Click);
            // 
            // tabPage_advanced
            // 
            this.tabPage_advanced.Controls.Add(this.checkBox_auto_click);
            this.tabPage_advanced.Controls.Add(this.label_score_unit);
            this.tabPage_advanced.Controls.Add(this.numericUpDown_stop_score);
            this.tabPage_advanced.Controls.Add(this.label_stop_score);
            this.tabPage_advanced.Controls.Add(this.checkBox_stop_when_mate);
            this.tabPage_advanced.Location = new System.Drawing.Point(4, 26);
            this.tabPage_advanced.Name = "tabPage_advanced";
            this.tabPage_advanced.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage_advanced.Size = new System.Drawing.Size(476, 408);
            this.tabPage_advanced.TabIndex = 4;
            this.tabPage_advanced.Text = "高级";
            this.tabPage_advanced.UseVisualStyleBackColor = true;
            // 
            // checkBox_stop_when_mate
            // 
            this.checkBox_stop_when_mate.AutoSize = true;
            this.checkBox_stop_when_mate.Location = new System.Drawing.Point(14, 15);
            this.checkBox_stop_when_mate.Name = "checkBox_stop_when_mate";
            this.checkBox_stop_when_mate.Size = new System.Drawing.Size(120, 19);
            this.checkBox_stop_when_mate.TabIndex = 0;
            this.checkBox_stop_when_mate.Text = "绝杀自动立即走棋";
            this.checkBox_stop_when_mate.UseVisualStyleBackColor = true;
            this.checkBox_stop_when_mate.CheckedChanged += new System.EventHandler(this.checkBox_stop_when_mate_CheckedChanged);
            // 
            // label_stop_score
            // 
            this.label_stop_score.AutoSize = true;
            this.label_stop_score.Location = new System.Drawing.Point(14, 50);
            this.label_stop_score.Name = "label_stop_score";
            this.label_stop_score.Size = new System.Drawing.Size(67, 15);
            this.label_stop_score.TabIndex = 1;
            this.label_stop_score.Text = "提前出招:";
            // 
            // numericUpDown_stop_score
            // 
            this.numericUpDown_stop_score.Increment = new decimal(new int[] {
            200,
            0,
            0,
            0});
            this.numericUpDown_stop_score.Location = new System.Drawing.Point(87, 46);
            this.numericUpDown_stop_score.Maximum = new decimal(new int[] {
            40000,
            0,
            0,
            0});
            this.numericUpDown_stop_score.Minimum = new decimal(new int[] {
            40000,
            0,
            0,
            -2147483648});
            this.numericUpDown_stop_score.Name = "numericUpDown_stop_score";
            this.numericUpDown_stop_score.Size = new System.Drawing.Size(80, 25);
            this.numericUpDown_stop_score.TabIndex = 2;
            this.numericUpDown_stop_score.Value = new decimal(new int[] {
            2000,
            0,
            0,
            0});
            this.numericUpDown_stop_score.ValueChanged += new System.EventHandler(this.numericUpDown_stop_score_ValueChanged);
            // 
            // label_score_unit
            // 
            this.label_score_unit.AutoSize = true;
            this.label_score_unit.Location = new System.Drawing.Point(173, 50);
            this.label_score_unit.Name = "label_score_unit";
            this.label_score_unit.Size = new System.Drawing.Size(22, 15);
            this.label_score_unit.TabIndex = 3;
            this.label_score_unit.Text = "分";
            // 
            // checkBox_auto_click
            // 
            this.checkBox_auto_click.AutoSize = true;
            this.checkBox_auto_click.Location = new System.Drawing.Point(14, 86);
            this.checkBox_auto_click.Name = "checkBox_auto_click";
            this.checkBox_auto_click.Size = new System.Drawing.Size(89, 19);
            this.checkBox_auto_click.TabIndex = 4;
            this.checkBox_auto_click.Text = "自动点击";
            this.checkBox_auto_click.UseVisualStyleBackColor = true;
            this.checkBox_auto_click.CheckedChanged += new System.EventHandler(this.checkBox_auto_click_CheckedChanged);
            // 
            // openFileDialog_engine
            // 
            this.openFileDialog_engine.CheckFileExists = false;
            this.openFileDialog_engine.CheckPathExists = false;
            this.openFileDialog_engine.FileName = "Engine";
            this.openFileDialog_engine.Filter = "可执行文件|*.exe";
            // 
            // VinSettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 438);
            this.Controls.Add(this.tabControl);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "VinSettingsForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "设置";
            this.tabControl.ResumeLayout(false);
            this.tabPage_connection.ResumeLayout(false);
            this.tabPage_connection.PerformLayout();
            this.tabPage_engine.ResumeLayout(false);
            this.tabPage_engine.PerformLayout();
            this.tabPage_detection.ResumeLayout(false);
            this.tabPage_detection.PerformLayout();
            this.tabPage_openbook.ResumeLayout(false);
            this.tabPage_openbook.PerformLayout();
            this.tabPage_advanced.ResumeLayout(false);
            this.tabPage_advanced.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_scale_factor)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_thread_count)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_step_time)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_engine_depth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_stop_score)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabPage_connection;
        private System.Windows.Forms.TabPage tabPage_engine;
        private System.Windows.Forms.TabPage tabPage_detection;
        private System.Windows.Forms.TabPage tabPage_openbook;
        private System.Windows.Forms.TabPage tabPage_advanced;
        private System.Windows.Forms.OpenFileDialog openFileDialog_engine;

        // Tab 1: 连线设置
        private System.Windows.Forms.Label label_solution;
        private System.Windows.Forms.ComboBox comboBox_solution;
        private System.Windows.Forms.Button button_get_hwnd;
        private System.Windows.Forms.Button button_save_as_solution;
        private System.Windows.Forms.Button button_clipboard_solution;
        private System.Windows.Forms.Button button_screenshot;
        private System.Windows.Forms.Label label_scale;
        private System.Windows.Forms.NumericUpDown numericUpDown_scale_factor;
        private System.Windows.Forms.CheckBox checkBox_universal_mode;
        private System.Windows.Forms.CheckBox checkBox_universal_mouse;

        // Tab 2: 引擎设置
        private System.Windows.Forms.Label label_engine;
        private System.Windows.Forms.ComboBox comboBox_engine;
        private System.Windows.Forms.Button button_engine_add;
        private System.Windows.Forms.Button button_engine_delete;
        private System.Windows.Forms.Label label_thread;
        private System.Windows.Forms.NumericUpDown numericUpDown_thread_count;
        private System.Windows.Forms.Label label_step_time;
        private System.Windows.Forms.NumericUpDown numericUpDown_step_time;
        private System.Windows.Forms.Label label_depth;
        private System.Windows.Forms.NumericUpDown numericUpDown_engine_depth;
        private System.Windows.Forms.CheckBox checkBox_auto_go;
        private System.Windows.Forms.CheckBox checkBox_analyze_mode;
        private System.Windows.Forms.Button button_go_immediately;

        // Tab 3: 识别设置
        private System.Windows.Forms.Label label_model;
        private System.Windows.Forms.ComboBox comboBox_yolo_models;
        private System.Windows.Forms.CheckBox checkBox_debug;
        private System.Windows.Forms.Button button_redetect;
        private System.Windows.Forms.Button button_advance_settings;

        // Tab 4: 开局库
        private System.Windows.Forms.Label label_openbook_info;
        private System.Windows.Forms.Button button_openbook_manage;

        // Tab 5: 高级
        private System.Windows.Forms.CheckBox checkBox_stop_when_mate;
        private System.Windows.Forms.Label label_stop_score;
        private System.Windows.Forms.NumericUpDown numericUpDown_stop_score;
        private System.Windows.Forms.Label label_score_unit;
        private System.Windows.Forms.CheckBox checkBox_auto_click;
    }
}
