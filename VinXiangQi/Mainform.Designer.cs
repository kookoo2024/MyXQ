namespace VinXiangQi
{
    partial class Mainform
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

        #region Windows 窗体设计器生成的代码

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Mainform));
            this.timer_set_window_hwnd = new System.Windows.Forms.Timer(this.components);
            this.pictureBox_board = new System.Windows.Forms.PictureBox();
            this.contextMenuStrip_board = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItem_copy_fen = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem_exit = new System.Windows.Forms.ToolStripMenuItem();
            this.textBox_engine_log = new System.Windows.Forms.RichTextBox();
            this.label_score = new System.Windows.Forms.Label();
            this.label_close = new System.Windows.Forms.Label();
            this.label_moves = new System.Windows.Forms.Label();
            this.button_start = new System.Windows.Forms.Button();
            this.button_stop_detection = new System.Windows.Forms.Button();
            this.button_settings = new System.Windows.Forms.Button();
            this.button_size = new System.Windows.Forms.Button();
            this.checkBox_auto_go = new System.Windows.Forms.CheckBox();
            this.checkBox_two_steps = new System.Windows.Forms.CheckBox();
            this.panel_right = new System.Windows.Forms.Panel();
            this.pictureBox_show_result = new System.Windows.Forms.PictureBox();
            this.checkBox_debug = new System.Windows.Forms.CheckBox();
            this.checkBox_auto_scroll = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel_content = new System.Windows.Forms.TableLayoutPanel();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox_board)).BeginInit();
            this.contextMenuStrip_board.SuspendLayout();
            this.panel_right.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox_show_result)).BeginInit();
            this.tableLayoutPanel_content.SuspendLayout();
            this.SuspendLayout();
            // 
            // timer_set_window_hwnd
            // 
            this.timer_set_window_hwnd.Enabled = true;
            this.timer_set_window_hwnd.Interval = 200;
            this.timer_set_window_hwnd.Tick += new System.EventHandler(this.timer_set_window_hwnd_Tick);
            // 
            // pictureBox_board
            // 
            this.pictureBox_board.ContextMenuStrip = this.contextMenuStrip_board;
            this.pictureBox_board.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox_board.Location = new System.Drawing.Point(2, 3);
            this.pictureBox_board.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.pictureBox_board.Name = "pictureBox_board";
            this.pictureBox_board.Size = new System.Drawing.Size(149, 200);
            this.pictureBox_board.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox_board.TabIndex = 0;
            this.pictureBox_board.TabStop = false;
            this.pictureBox_board.MouseDown += new System.Windows.Forms.MouseEventHandler(this.pictureBox_board_MouseDown);
            // 
            // contextMenuStrip_board
            // 
            this.contextMenuStrip_board.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.contextMenuStrip_board.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItem_copy_fen,
            this.toolStripMenuItem_exit});
            this.contextMenuStrip_board.Name = "contextMenuStrip_board";
            this.contextMenuStrip_board.Size = new System.Drawing.Size(125, 48);
            // 
            // toolStripMenuItem_copy_fen
            // 
            this.toolStripMenuItem_copy_fen.Name = "toolStripMenuItem_copy_fen";
            this.toolStripMenuItem_copy_fen.Size = new System.Drawing.Size(124, 22);
            this.toolStripMenuItem_copy_fen.Text = "复制局面";
            this.toolStripMenuItem_copy_fen.Click += new System.EventHandler(this.ToolStripMenuItem_copy_fen_Click);
            // 
            // toolStripMenuItem_exit
            // 
            this.toolStripMenuItem_exit.Name = "toolStripMenuItem_exit";
            this.toolStripMenuItem_exit.Size = new System.Drawing.Size(124, 22);
            this.toolStripMenuItem_exit.Text = "退出";
            this.toolStripMenuItem_exit.Click += new System.EventHandler(this.ToolStripMenuItem_exit_Click);
            // 
            // textBox_engine_log
            // 
            this.textBox_engine_log.Location = new System.Drawing.Point(-9999, -9999);
            this.textBox_engine_log.Name = "textBox_engine_log";
            this.textBox_engine_log.Size = new System.Drawing.Size(200, 100);
            this.textBox_engine_log.TabIndex = 102;
            this.textBox_engine_log.TabStop = false;
            this.textBox_engine_log.Text = "";
            this.textBox_engine_log.Visible = false;
            this.textBox_engine_log.TextChanged += new System.EventHandler(this.textBox_engine_log_TextChanged);
            // 
            // label_score
            // 
            this.label_score.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.label_score.Font = new System.Drawing.Font("Consolas", 11F, System.Drawing.FontStyle.Bold);
            this.label_score.ForeColor = System.Drawing.Color.Red;
            this.label_score.Location = new System.Drawing.Point(6, 121);
            this.label_score.Margin = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this.label_score.Name = "label_score";
            this.label_score.Size = new System.Drawing.Size(51, 27);
            this.label_score.TabIndex = 15;
            this.label_score.Text = "--";
            this.label_score.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label_score.MouseDown += new System.Windows.Forms.MouseEventHandler(this.StartDrag);
            // 
            // label_close
            // 
            this.label_close.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label_close.Cursor = System.Windows.Forms.Cursors.Hand;
            this.label_close.Font = new System.Drawing.Font("Marlett", 9F);
            this.label_close.ForeColor = System.Drawing.Color.IndianRed;
            this.label_close.Location = new System.Drawing.Point(208, 8);
            this.label_close.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label_close.Name = "label_close";
            this.label_close.Size = new System.Drawing.Size(12, 14);
            this.label_close.TabIndex = 200;
            this.label_close.Text = "r";
            this.label_close.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label_close.Click += new System.EventHandler(this.label_close_Click);
            this.label_close.MouseEnter += new System.EventHandler(this.label_close_MouseEnter);
            this.label_close.MouseLeave += new System.EventHandler(this.label_close_MouseLeave);
            // 
            // label_moves — top bar, 3 candidate moves
            // 
            this.label_moves.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.label_moves.Font = new System.Drawing.Font("微软雅黑", 8F);
            this.label_moves.ForeColor = System.Drawing.Color.Gray;
            this.label_moves.Location = new System.Drawing.Point(3, 2);
            this.label_moves.Name = "label_moves";
            this.label_moves.Size = new System.Drawing.Size(200, 14);
            this.label_moves.TabIndex = 201;
            this.label_moves.Text = "";
            this.label_moves.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // button_start
            // 
            this.button_start.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.button_start.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.button_start.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.button_start.ForeColor = System.Drawing.SystemColors.MenuHighlight;
            this.button_start.Location = new System.Drawing.Point(0, 173);
            this.button_start.Margin = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this.button_start.Name = "button_start";
            this.button_start.Size = new System.Drawing.Size(64, 23);
            this.button_start.TabIndex = 10;
            this.button_start.Text = "GO！";
            this.button_start.UseVisualStyleBackColor = false;
            this.button_start.Click += new System.EventHandler(this.button_start_Click);
            // 
            // button_stop_detection
            // 
            this.button_stop_detection.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.button_stop_detection.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.button_stop_detection.ForeColor = System.Drawing.SystemColors.HotTrack;
            this.button_stop_detection.Location = new System.Drawing.Point(8, 150);
            this.button_stop_detection.Margin = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this.button_stop_detection.Name = "button_stop_detection";
            this.button_stop_detection.Size = new System.Drawing.Size(46, 24);
            this.button_stop_detection.TabIndex = 11;
            this.button_stop_detection.Text = "END";
            this.button_stop_detection.UseVisualStyleBackColor = true;
            this.button_stop_detection.Click += new System.EventHandler(this.button_stop_detection_Click);
            // 
            // button_settings
            // 
            this.button_settings.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.button_settings.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.button_settings.Location = new System.Drawing.Point(5, 66);
            this.button_settings.Margin = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this.button_settings.Name = "button_settings";
            this.button_settings.Size = new System.Drawing.Size(46, 24);
            this.button_settings.TabIndex = 13;
            this.button_settings.Text = "设置";
            this.button_settings.UseVisualStyleBackColor = true;
            this.button_settings.Click += new System.EventHandler(this.button_settings_Click);
            // 
            // button_size
            // 
            this.button_size.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.button_size.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.button_size.Location = new System.Drawing.Point(6, 92);
            this.button_size.Margin = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this.button_size.Name = "button_size";
            this.button_size.Size = new System.Drawing.Size(42, 24);
            this.button_size.TabIndex = 14;
            this.button_size.Text = "大小";
            this.button_size.UseVisualStyleBackColor = true;
            this.button_size.Click += new System.EventHandler(this.button_size_Click);
            // 
            // checkBox_auto_go
            // 
            this.checkBox_auto_go.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBox_auto_go.Checked = true;
            this.checkBox_auto_go.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_auto_go.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.checkBox_auto_go.Location = new System.Drawing.Point(6, 22);
            this.checkBox_auto_go.Margin = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this.checkBox_auto_go.Name = "checkBox_auto_go";
            this.checkBox_auto_go.Size = new System.Drawing.Size(51, 21);
            this.checkBox_auto_go.TabIndex = 12;
            this.checkBox_auto_go.Text = "自动";
            this.checkBox_auto_go.UseVisualStyleBackColor = true;
            this.checkBox_auto_go.CheckedChanged += new System.EventHandler(this.checkBox_auto_go_CheckedChanged);
            // 
            // checkBox_two_steps
            // 
            this.checkBox_two_steps.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.checkBox_two_steps.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.checkBox_two_steps.Location = new System.Drawing.Point(6, 45);
            this.checkBox_two_steps.Margin = new System.Windows.Forms.Padding(0, 2, 0, 0);
            this.checkBox_two_steps.Name = "checkBox_two_steps";
            this.checkBox_two_steps.Size = new System.Drawing.Size(55, 19);
            this.checkBox_two_steps.TabIndex = 16;
            this.checkBox_two_steps.Text = "双步";
            this.checkBox_two_steps.UseVisualStyleBackColor = true;
            this.checkBox_two_steps.CheckedChanged += new System.EventHandler(this.checkBox_two_steps_CheckedChanged);
            // 
            // panel_right
            // 
            this.panel_right.Controls.Add(this.label_score);
            this.panel_right.Controls.Add(this.button_size);
            this.panel_right.Controls.Add(this.checkBox_two_steps);
            this.panel_right.Controls.Add(this.button_settings);
            this.panel_right.Controls.Add(this.checkBox_auto_go);
            this.panel_right.Controls.Add(this.button_stop_detection);
            this.panel_right.Controls.Add(this.button_start);
            this.panel_right.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel_right.Location = new System.Drawing.Point(155, 3);
            this.panel_right.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.panel_right.Name = "panel_right";
            this.panel_right.Padding = new System.Windows.Forms.Padding(15, 3, 15, 3);
            this.panel_right.Size = new System.Drawing.Size(66, 200);
            this.panel_right.TabIndex = 0;
            this.panel_right.MouseDown += new System.Windows.Forms.MouseEventHandler(this.StartDrag);
            // 
            // pictureBox_show_result
            // 
            this.pictureBox_show_result.Location = new System.Drawing.Point(-9999, -9999);
            this.pictureBox_show_result.Name = "pictureBox_show_result";
            this.pictureBox_show_result.Size = new System.Drawing.Size(400, 200);
            this.pictureBox_show_result.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox_show_result.TabIndex = 99;
            this.pictureBox_show_result.TabStop = false;
            this.pictureBox_show_result.Visible = false;
            // 
            // checkBox_debug
            // 
            this.checkBox_debug.Checked = true;
            this.checkBox_debug.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_debug.Location = new System.Drawing.Point(-9999, -9999);
            this.checkBox_debug.Name = "checkBox_debug";
            this.checkBox_debug.Size = new System.Drawing.Size(89, 19);
            this.checkBox_debug.TabIndex = 100;
            this.checkBox_debug.Text = "调试状态";
            this.checkBox_debug.Visible = false;
            // 
            // checkBox_auto_scroll
            // 
            this.checkBox_auto_scroll.Checked = true;
            this.checkBox_auto_scroll.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox_auto_scroll.Location = new System.Drawing.Point(-9999, -9999);
            this.checkBox_auto_scroll.Name = "checkBox_auto_scroll";
            this.checkBox_auto_scroll.Size = new System.Drawing.Size(89, 19);
            this.checkBox_auto_scroll.TabIndex = 101;
            this.checkBox_auto_scroll.Text = "自动滚动";
            this.checkBox_auto_scroll.Visible = false;
            // 
            // tableLayoutPanel_content
            // 
            this.tableLayoutPanel_content.ColumnCount = 2;
            this.tableLayoutPanel_content.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel_content.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 70F));
            this.tableLayoutPanel_content.Controls.Add(this.pictureBox_board, 0, 0);
            this.tableLayoutPanel_content.Controls.Add(this.panel_right, 1, 0);
            this.tableLayoutPanel_content.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel_content.Location = new System.Drawing.Point(1, 1);
            this.tableLayoutPanel_content.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.tableLayoutPanel_content.Name = "tableLayoutPanel_content";
            this.tableLayoutPanel_content.RowCount = 1;
            this.tableLayoutPanel_content.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel_content.Size = new System.Drawing.Size(223, 206);
            this.tableLayoutPanel_content.TabIndex = 0;
            // 
            // Mainform
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(225, 208);
            this.Controls.Add(this.label_close);
            this.Controls.Add(this.label_moves);
            this.Controls.Add(this.tableLayoutPanel_content);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.MaximizeBox = false;
            this.Name = "Mainform";
            this.Padding = new System.Windows.Forms.Padding(1);
            this.Text = "VIN 象棋";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Mainform_FormClosing);
            this.Load += new System.EventHandler(this.Mainform_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox_board)).EndInit();
            this.contextMenuStrip_board.ResumeLayout(false);
            this.panel_right.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox_show_result)).EndInit();
            this.tableLayoutPanel_content.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Timer timer_set_window_hwnd;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel_content;
        private System.Windows.Forms.PictureBox pictureBox_board;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip_board;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem_copy_fen;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem_exit;
        private System.Windows.Forms.Panel panel_right;
        private System.Windows.Forms.Button button_start;
        private System.Windows.Forms.Button button_stop_detection;
        private System.Windows.Forms.CheckBox checkBox_auto_go;
        private System.Windows.Forms.CheckBox checkBox_two_steps;
        private System.Windows.Forms.Button button_settings;
        private System.Windows.Forms.Button button_size;
        private System.Windows.Forms.Label label_score;
        private System.Windows.Forms.Label label_close;
        private System.Windows.Forms.Label label_moves;
        private System.Windows.Forms.RichTextBox textBox_engine_log;
        private System.Windows.Forms.PictureBox pictureBox_show_result;
        internal System.Windows.Forms.CheckBox checkBox_debug;
        private System.Windows.Forms.CheckBox checkBox_auto_scroll;
    }
}
