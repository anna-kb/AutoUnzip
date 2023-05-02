using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using WinForms = System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;


namespace AutoUnzip
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _sourceFolder;
        private string _destinationFolder;
        private string _progressString;
        private int _progressValue;
        private bool processing = false;
        private bool replaceAll = false;
        private bool skipAll = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string sourceFolder
        {
            get { return _sourceFolder; }
            set
            {
                _sourceFolder = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("sourceFolder"));
            }
        }

        public string destinationFolder
        {
            get { return _destinationFolder; }
            set
            {
                _destinationFolder = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("destinationFolder"));
            }
        }

        public string progressString
        {
            get { return _progressString; }
            set
            {
                _progressString = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("progressString"));
            }
        }

        public int progressValue
        {
            get { return _progressValue; }
            set
            {
                _progressValue = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("progressValue"));
            }
        }


        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();

            sourceFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads";
            destinationFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Documents\Electronic Arts\The Sims 3\Mods\Packages";
            progressValue = 0;
        }

        private void browseBtn_Click(object sender, RoutedEventArgs e)
        {
            WinForms.FolderBrowserDialog dialog = new WinForms.FolderBrowserDialog();
            
            Button button = (Button)sender;
            bool isDestination = button.Name == "destinationBtn";

            dialog.InitialDirectory = isDestination ? destinationFolder : sourceFolder;

            WinForms.DialogResult result = dialog.ShowDialog();

            if (result == WinForms.DialogResult.OK)
            {
                if (isDestination)
                {
                    destinationFolder = dialog.SelectedPath;
                }
                else
                    sourceFolder = dialog.SelectedPath;
            }
        }

        private void submitBtn_Click(object sender, RoutedEventArgs e)
        {
            if (processing)
                return;

            if(deleteCheckBox.IsChecked == false)
            StartProcessing();
            else
            {
                WarnUser();
            }
        }

        private async void StartProcessing()
        {
            processing = true;
            List<string> files = System.IO.Directory.GetFiles(sourceFolder).Where(file => file.ToLower().EndsWith(".zip") || file.ToLower().EndsWith(".rar")).ToList();
            await Task.Run(() =>
            {
                for(int i=0; i < files.Count; i++)
                {
                    Dispatcher.Invoke(() => progressString = "Working on " + files[i] + "...");
                    Dispatcher.Invoke(() => progressValue = (int)Math.Floor(((float)i / (float)files.Count) * 100f));

                    ProcessFile(files[i]);
                }
            });

            if (deleteCheckBox.IsChecked == true)
            {
                Dispatcher.Invoke(() => progressString = "Deleting files...");
                DeleteAllFiles(files);
            }

            processing = false;
            progressValue = 100;
            progressString = "All Done!";
        }

        private void DeleteAllFiles(List<string> files)
        {
            foreach(var file in files)
            {

                try{
                    if (!File.Exists(file))
                        continue;

                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    using (StreamWriter writer = new StreamWriter(sourceFolder, true))
                    {
                        writer.WriteLine($"{DateTime.Now}: {ex.Message}");
                    }

                    MessageBox.Show($"An error occured and an error log was generated in {sourceFolder}.");
                    break;
                }
            }
        }

        private void ProcessFile(string file)
        {
            //Check if destination already contains file
            string possibleFile = destinationFolder + file.Substring(file.LastIndexOf('\\') );
            System.Diagnostics.Debug.WriteLine(possibleFile);
            string overWriteMode = "";
            
            if(File.Exists(possibleFile))
            {
                bool b= PromptReplace(file);

                overWriteMode = b ? "-aoa" : "-aos";
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.FileName = @"C:\Program Files\7-Zip\7z.exe";
            startInfo.Arguments = $"x \"{file}\" -o\"{destinationFolder}\" {overWriteMode}";
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;

            var process = new System.Diagnostics.Process();
            process.StartInfo = startInfo;
            try
            {
                process.Start();
                process.WaitForExit();
            }
            finally
            {
                process.Dispose();
            }
        }

        private void WarnUser()
        {
            MessageBoxResult result = MessageBox.Show("Warning: All zip files will be deleted after unzipping. Proceed?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if(result == MessageBoxResult.Yes)
            {
                StartProcessing();
            }

        }

        private bool PromptReplace(string file)
        {
            MessageBoxResult result = MessageBox.Show($"The file, {file}, already exists in the destination folder. Would you like to replace it?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
                return true;

            return false;
        }

    }

}
