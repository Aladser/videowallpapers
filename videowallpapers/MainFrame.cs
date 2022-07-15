﻿using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace videowallpapers
{
    public partial class MainForm : Form
    {
        UserActivityHook globalHook;
        BackWork backwork;
        readonly string cfgpath = Path.GetDirectoryName(Application.ExecutablePath) + "\\config.cfg"; // путь конфига
        readonly string shortcut= Environment.GetFolderPath(Environment.SpecialFolder.Startup) + "\\videowallpapers.lnk"; // ярлык автозагрузки 
        string mpcFilter = "MPC плейлист (*.mpcpl)|*.mpcpl|KMP плейлист (*.kpl)|*.kpl|Все файлы (*.*)|*.*";
        string kmpFilter = "KMP плейлист (*.kpl)|*.kpl|MPC плейлист (*.mpcpl)|*.mpcpl|Все файлы (*.*)|*.*";
        string pathname = ""; // путь плейлиста
        int workAfterBoot; // работа после запуска
        readonly OpenFileDialog ofd = new OpenFileDialog();
        bool isEdited = false;
        // процесс Windows
        string[] procs = { "mpc-hc64", "KMPlayer64" }; 
        int procIndex = 0;

        public MainForm()
        {

            backwork = new BackWork(0);
            // дизайн
            InitializeComponent();
            ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            // проверка автозапуска
            if (File.Exists(shortcut))
                autoloaderCheckBox.Checked = true;
            // Считывание конфигурационного файла
            StreamReader reader;
            try
            {
                reader = new StreamReader(cfgpath);
                // считывание period
                string line = reader.ReadLine();
                int number = Int32.Parse(line.Substring(line.IndexOf("= ") + 2));
                timeComboBox.SelectedIndex = number;
                backwork.setTimePeriod(number);
                // считывание workafterboot
                line = reader.ReadLine();
                number = Int32.Parse(line.Substring(line.IndexOf("= ") + 2));
                if (number == 0)
                {
                    workAfterBoot = 0;
                    wabCheckBox.Checked = false;
                }
                else
                {
                    workAfterBoot = 1;
                    wabCheckBox.Checked = true;
                }
                // считывание playerpath
                line = reader.ReadLine();
                line = line.Substring(line.IndexOf("= ") + 2);
                string ext = Path.GetExtension(line);
                if ( File.Exists(line) && (ext==".mpcpl" || ext==".kpl") )
                {
                    playlistNameLabel.Text = line;
                    pathname = line;
                    if (ext == ".mpcpl")
                    {
                        playerComboBox.SelectedIndex = 0;
                        procIndex = 0;
                        ofd.Filter = mpcFilter;
                    }                       
                    else
                    {
                        playerComboBox.SelectedIndex = 1;
                        procIndex = 1;
                        ofd.Filter = kmpFilter;
                    }
                    pathname = line;
                }
                else
                {
                    if(File.Exists(line))
                        playlistNameLabel.Text = "Неизвестный формат плейлиста";
                    else
                        playlistNameLabel.Text = "Не найден плейлист";
                    ofd.Filter = mpcFilter;
                    procIndex = 0;
                    playerComboBox.SelectedIndex = 0;
                    switchPanel.Enabled = false;
                    playlistSelectButton.Enabled = true;
                    notifyIcon.Visible = false;
                    offRadioButton.Checked = true;
                }
                reader.Close();
                // начать работу после запуска
                if (workAfterBoot == 1)
                {
                    notifyIcon.Visible = true;
                    onRadioButton.Checked = true;
                }
                else
                    Visible = true;
                // Создание хука
                globalHook = new UserActivityHook();
                globalHook.KeyPress += GlobalKeyPress;
                globalHook.OnMouseActivity += GlobalMouseActivity;
                globalHook.Start(true, true);
            }
            catch (FileNotFoundException ex)
            {
                var result = MessageBox.Show(ex.Message, "Приложение не запущено", MessageBoxButtons.OK);
                if (result == DialogResult.OK)
                    Environment.Exit(1);
            }
        }
        // глобальное нажатие клавиатуры
        public void GlobalKeyPress(object sender, KeyPressEventArgs e)
        {
            backwork.stopShowWallpaper();
        }
        // глобальное движение мыши
        public void GlobalMouseActivity(object sender, MouseEventArgs e)
        {
            backwork.stopShowWallpaper();
        }
        //Включить фоновую задачу
        private void OnRadioButton_CheckedChanged(object sender, EventArgs e)
        {
            if (onRadioButton.Checked)
            {
                notifyIcon.Text = "Видеообои ВКЛ";
                backwork.start(pathname);
                playlistSelectButton.Enabled = false;
            }
            else
            {
                notifyIcon.Text = "Видеообои ВЫКЛ";
                backwork.stop();
                playlistSelectButton.Enabled = true;
            }               
        }
        // Сворачивание в трей
        private void MainForm_SizeChanged(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                notifyIcon.Visible = true;
            }
        }
        // смена периода бездействия
        private void TimeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            offRadioButton.Checked = true;
            backwork.setTimePeriod(timeComboBox.SelectedIndex);
            isEdited = true;
        }
        // Создание-удаление ярлыка
        private void autoLoader_CheckedChanged(object sender, EventArgs e)
        {
            // Создание ярлыка
            if (autoloaderCheckBox.Checked)
            {
                //Windows Script Host Shell Object
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8")));
                try
                {
                    var lnk = shell.CreateShortcut(shortcut);
                    try
                    {
                        lnk.TargetPath = Application.ExecutablePath;
                        lnk.IconLocation = "shell32.dll, 1";
                        lnk.Save();
                    }
                    finally
                    {
                        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(lnk);
                    }
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
                }
            }
            // Удаление ярлыка
            else
                File.Delete(shortcut);
        }
        // переключение видеоплеера
        private void playerComboBox_SelectionChangeCommitted(object sender, EventArgs e)
        {
            backwork.stop();
            offRadioButton.Checked = true;
            switchPanel.Enabled = false;
            playlistNameLabel.Text = "Не выбран плейлист";
            pathname = "";
            if(playerComboBox.SelectedIndex == 0)
            {
                procIndex = 0;
                ofd.Filter = mpcFilter;
            }
            else
            {
                procIndex = 1;
                ofd.Filter = kmpFilter;
            }
        }
        // переключение плеера и плейлиста
        private void playlistSelectButton_Click(object sender, EventArgs e)
        {
            if (ofd.ShowDialog() != DialogResult.OK)
                return;
            ofd.InitialDirectory = ofd.FileName.Substring(0, ofd.FileName.Length-Path.GetFileName(ofd.FileName).Length);
            switch (Path.GetExtension(ofd.FileName))
            {
                case ".mpcpl":
                    playerComboBox.SelectedIndex = 0;
                    procIndex = 0;
                    ofd.Filter = mpcFilter;
                    break;
                case ".kpl":
                    playerComboBox.SelectedIndex = 1;
                    procIndex = 1;
                    ofd.Filter = kmpFilter;
                    break;
                default:
                    return;
            }
            pathname = ofd.FileName;
            procIndex = playerComboBox.SelectedIndex;
            playlistNameLabel.Text = pathname;
            switchPanel.Enabled = true;
            isEdited = true;
        }
        // Флаг Работа после запуска
        private void wabCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            workAfterBoot = wabCheckBox.Checked ? 1 : 0;
            isEdited = true;
        }
        // Информация о программе
        private void aboutImage_MouseHover(object sender, EventArgs e)
        {
            toolTip.SetToolTip(aboutImage, "Видеобом 1.6\nby Aladser\n2022");
        }
        // Открыть приложение после нажатия на иконку в трее
        private void notifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Visible = true;
            this.WindowState = FormWindowState.Normal;
            notifyIcon.Visible = false;
        }
        // закрытие приложения
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // запись измененных данных
            if (isEdited)
            {
                StreamWriter writer = new StreamWriter(cfgpath, false);
                String text = "period = " + timeComboBox.SelectedIndex + "\n";
                text += "workafterboot = " + workAfterBoot + "\n";
                text += "playerpath = " + pathname + "\n";
                writer.WriteLine(text);
                writer.Close();
            }
            // Закрытие приложения
            var result = MessageBox.Show("Вы действительно хотите закрыть приложение?", "Завершение работы", MessageBoxButtons.YesNo);
            if (result == DialogResult.Yes)
                Process.GetCurrentProcess().Kill();
            else
            {
                e.Cancel = true;
                this.Hide();
                notifyIcon.Visible = true;
            }
        }

    }
}
