﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using WinForms = System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Win32;


namespace AutoUnzip
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private string _progressString;
        private int _progressValue;
        private bool processing = false;


        public event PropertyChangedEventHandler? PropertyChanged;

        public string programPath
        {
            get
            {
                return userSettings.Default.ProgramFilePath;
            }
            set
            {
                userSettings.Default.ProgramFilePath = value;
                userSettings.Default.Save();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("programPath"));
            }
        }

        public string sourceFolder
        {
            get
            {
                return userSettings.Default.SourceFolder == "" ?
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads" :
                    userSettings.Default.SourceFolder;
            }
            set
            {
                userSettings.Default.SourceFolder = value;
                userSettings.Default.Save();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("sourceFolder"));
            }
        }

        public string destinationFolder
        {
            get
            {
                return userSettings.Default.DestinationFolder == "" ?
                  Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Documents\" :
                   userSettings.Default.DestinationFolder;
            }
            set
            {
                userSettings.Default.DestinationFolder = value;
                userSettings.Default.Save();
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

            progressValue = 0;
            progressString = "";
        }


        /*
         * Handles the Browse button's Click event. Depending on the sender of the event,
         * update the source destination folder path, or program file path.
         * */
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

        /*
         * handle the click to find the 7zip executable. 
         * */
        private void filePathBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Executable files (*.exe)|*.exe|All files(*.*)|*.*";

            dialog.InitialDirectory = System.IO.Path.GetDirectoryName(userSettings.Default.ProgramFilePath);

            if (dialog.ShowDialog() == true)
            {
                programPath = dialog.FileName;
            }
        }

        /*
         * Handles the submit button click. Confirm with user that files will be deleted,
         * then start processing.
         * */
        private void submitBtn_Click(object sender, RoutedEventArgs e)
        {
            if (processing)
                return;

            //Check if 7zip exe is correct
            string programFile = System.IO.Path.GetFileName(userSettings.Default.ProgramFilePath);
            if(programFile != "7z.exe")
            {
                MessageBox.Show($"{programFile} is not a valid 7z.exe. Please select a valid executable.");
                return;
            }


            if (deleteCheckBox.IsChecked == false)
                StartProcessing();
            else
            {
                WarnUser();
            }
        }

        /*
         * Start a Task on a new background thread in other to display
         * visual elements to the UI, namely the progress, which increments with each file processed.
         */
        private async void StartProcessing()
        {

            processing = true;
            List<string> files = System.IO.Directory.GetFiles(sourceFolder).Where(file => file.ToLower().EndsWith(".zip") || file.ToLower().EndsWith(".rar")).ToList();
            await Task.Run(() =>
            {
                for (int i = 0; i < files.Count; i++)
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


        /*
         * First, check if the destination contains files within the zip files. Prompt the user to replace or skip in that case.
         * Then, start a new Process with the overwrite argument (if user skipped, then assume always overwrite)
         */
        private void ProcessFile(string file)
        {
            //Check if 7zip is installed, and in the main directory.

            //Check if destination already contains file
            List<string> matches = CheckForMatches(file);

            if (matches.Count > 0)
            {
                bool replace = ReplacePrompt(matches, file);
                if (!replace)
                {
                    return;
                }
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.FileName = userSettings.Default.ProgramFilePath;
            startInfo.Arguments = $"x \"{file}\" -o\"{destinationFolder}\" -aoa";
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Encountered error in ProcessFile: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        /*
         * Create a process that outputs the files in the .zip, and pass it out the parsing function.
         */
        private List<string> CheckForMatches(string file)
        {
            List<string> fileMatches = new List<string>();

            var startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.FileName = userSettings.Default.ProgramFilePath;
            startInfo.Arguments = $"l \"{file}\" -o\"{destinationFolder}\"";
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;

            var process = new System.Diagnostics.Process();
            process.StartInfo = startInfo;
            try
            {
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                List<string> fileNames = ParseOutput(output);

                foreach (string potentialMatch in fileNames)
                {
                    string checkString = destinationFolder + $"\\{potentialMatch}";
                    if (File.Exists(checkString))
                    {
                        fileMatches.Add(potentialMatch);
                    }
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Encountered error in CheckForMatches: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }

            return fileMatches;
        }

        /*
         * Parses the output from 7zip l command to get file names. 
         */
        private List<string> ParseOutput(string output)
        {
            List<string> files = new List<string>();

            string delimiter = "-------------------";
            string[] lines = output.Split('\n');
            int indexOfDelimiter = Array.FindIndex(lines, (l => l.Contains(delimiter)));
            int indexOfNameColumn = lines[indexOfDelimiter - 1].IndexOf("Name");

            for (int i = indexOfDelimiter + 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(delimiter))  // we've reached the end of the file output.
                    break;

                if (lines[i].Substring(0, indexOfNameColumn).Contains("D...."))  // this refers to a folder, and not a file.
                    continue;

                string fileName = lines[i].Substring(indexOfNameColumn).Trim();
                files.Add(fileName);
            }

            return files;
        }

        /*
  * Deletes zip files after processing all files if user checked  the box.
  */
        private void DeleteAllFiles(List<string> files)
        {
            foreach (var file in files)
            {

                try
                {
                    if (!File.Exists(file))
                        continue;

                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Encountered error in ProcessFile: {ex.Message}");
                }
            }
        }

        private void WarnUser()
        {
            MessageBoxResult result = MessageBox.Show("Warning: All zip files will be deleted after unzipping. Proceed?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                StartProcessing();
            }

        }

        private bool ReplacePrompt(List<string> files, string archive)
        {
            string listString = "";

            for (int i = 0; i < files.Count; i++)
            {
                listString += files[i];
                if (i != files.Count - 1)
                    listString += "\n";
            }
            MessageBoxResult result = MessageBox.Show($"The following files from archive {archive} exist in the destination folder. Do you want to replace them? \n \n {listString}",
                "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
                return true;

            return false;
        }

    }

}
