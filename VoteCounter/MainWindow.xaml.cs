﻿using VotingPC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using SQLite;
using System.IO;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using MaterialDesignThemes.Wpf.Transitions;

namespace VoteCounter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Global variables
        // TODO: Change password dialog to MVVM user control, not a class
        private readonly PasswordDialog passwordDialog;
        private static string[] databasePath;
        private readonly Dialogs dialogs;
        private static readonly Dictionary<string, List<Candidate>> sections = new();
        // List of information about sections (each sector is a list of candidates)
        private static Dictionary<string, Info> infos;
        #endregion

        public MainWindow()
        {
            SQLitePCL.Batteries_V2.Init();
            InitializeComponent();

            // Init Dialogs classes for MaterialDesign dialogs
            dialogs = new(dialogHost);
            passwordDialog = new(dialogHost,
                "Nhập mật khẩu cơ sở dữ liệu:",
                "Mật khẩu không chính xác hoặc cơ sở dữ liệu không hợp lệ!",
                "Hoàn tất", PasswordDialogButton_Click);

            // Set click event handlers for buttons in slides
            slideLanding.SingleFileButton.Click += SingleFileButton_Click;
            slideLanding.MultipleFileButton.Click += MultipleFileButton_Click;
            slideLanding.FolderButton.Click += FolderButton_Click;
            slideDetail.backButton.Click += (sender, e) => PreviousPage();
        }

        // Button click events
        private void SingleFileButton_Click(object sender, RoutedEventArgs e)
        {
            // Open file dialog
            if (!ShowOpenDatabaseFileDialog()) return;
            // TODO: show "Cancel" button on the password dialog as well
            passwordDialog.Show();
        }
        private void MultipleFileButton_Click(object sender, RoutedEventArgs e)
        {
            // Allow multiple file, then open file dialog
            if (!ShowOpenDatabaseFileDialog(multiFile: true)) return;

            passwordDialog.Show();
        }
        private void FolderButton_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog dialog = new()
            {
                Description = "Chọn thư mục chứa file .db",
                UseDescriptionForTitle = true
            };

            if (!(bool)dialog.ShowDialog()) return;

            string[] filePaths = Directory.GetFiles(dialog.SelectedPath, "*.db", SearchOption.AllDirectories);

            if (filePaths.Length == 0)
            {
                dialogs.ShowTextDialog("Không tìm thấy file .db trong thư mục đã chọn.\nVui lòng kiểm tra lại.", "OK", Close);
                return;
            }
            databasePath = filePaths;

            passwordDialog.Show();
        }

        /// <summary>
        /// Show Windows's default open file dialog to select database file(s), then save paths to databasePath
        /// </summary>
        /// <param name="multiFile">Allow multiple file or not</param>
        /// <returns>True if file(s) if selected, else false</returns>
        private static bool ShowOpenDatabaseFileDialog(bool multiFile = false)
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Database file (*.db)|*.db",
                Title = "Chọn tệp cơ sở dữ liệu",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            openFileDialog.Multiselect = multiFile;

            if (openFileDialog.ShowDialog() == true)
            {
                databasePath = openFileDialog.FileNames;
                return true;
            }
            else return false;
        }

        /// <summary>
        /// Click handler for the Password Dialog button.
        /// </summary>
        private async void PasswordDialogButton_Click(object sender, RoutedEventArgs e)
        {
            dialogs.CloseDialog();
            dialogs.ShowLoadingDialog();

            foreach (string database in databasePath)
            {
                // Check if file can be read or not. Exit if can't be read or not exist
                try
                {
                    using FileStream file = new(database, FileMode.Open, FileAccess.Read);
                }
                catch
                {
                    dialogs.CloseDialog();
                    dialogs.ShowTextDialog("Không đủ quyền đọc file đã chọn.\n" +
                        "Vui lòng chạy lại chương trình với quyền admin hoặc\n" +
                        "chuyển file vào nơi có thể đọc được như Desktop.", "OK", Close);
                    return;
                }

                // Create new SQLite database connection for each file
                SQLiteConnectionString options = new(database, storeDateTimeAsTicks: true, passwordDialog.Password);
                SQLiteAsyncConnection connection = new(options);

                // Check password
                try
                {
                    //This will try to query the SQLite Schema Database, if the key is correct then no error is raised
                    _ = await connection.QueryAsync<int>("SELECT count(*) FROM sqlite_master");
                }
                catch (SQLiteException) // Wrong password
                {
                    dialogs.CloseDialog();
                    await connection.CloseAsync();
                    // Request password from user again, don't run init code
                    passwordDialog.Show(true);
                    return;
                }

                // Parse data from database
                try
                {
                    // Parse Info table about sections in current file
                    string query = $"SELECT * FROM Info";
                    List<Info> currentFileInfos = await connection.QueryAsync<Info>(query);
                    // Validate the parsed file
                    if (ValidateData(currentFileInfos) == false) throw new InvalidDataException();
                    // If infos is not read before, just save the object
                    if (infos == null)
                        infos = currentFileInfos.ToDictionary(x => x.Sector, x => x);
                    // Merge two infos if infos is read already
                    else
                    {
                        foreach (Info info in currentFileInfos)
                        {
                            if (infos.ContainsKey(info.Sector)) continue;
                            infos.Add(info.Sector, info);
                        }
                    }

                    query = $"SELECT * FROM \"";
                    foreach (Info sector in currentFileInfos)
                    {
                        List<Candidate> candidateList = await connection.QueryAsync<Candidate>(query + sector.Sector + "\"");
                        if (ValidateData(candidateList) == false) throw new InvalidDataException();

                        FindMaxVote(candidateList);

                        // If sector not yet saved, save new sector into sections collection
                        if (!sections.ContainsKey(sector.Sector))
                        {
                            sections.Add(sector.Sector, candidateList);
                            continue;
                        }

                        // Else merge with current file's sector
                        Dictionary<string, Candidate> candidateDict = candidateList.ToDictionary(x => x.Name, x => x);
                        foreach (Candidate candidate in sections[sector.Sector])
                        {
                            candidate.Votes += candidateDict[candidate.Name].Votes;
                            candidate.TotalWinningPlaces += candidateDict[candidate.Name].TotalWinningPlaces;
                        }
                    }
                }
                catch
                {
                    // TODO: Add detailed error report if possible
                    dialogs.CloseDialog();
                    dialogs.ShowTextDialog("Cơ sở dữ liệu không hợp lệ, vui lòng kiểm tra lại.", "Đóng", Close);
                    return;
                }
            }

            // The line below equals to await Task.Run(PopulateUI); but WPF is stupid so...
            Application.Current.Dispatcher.Invoke(PopulateUI);
            dialogs.CloseDialog();
            NextPage();
        }


        ///***********************************************************///
        /// Loading section
        ///***********************************************************///
        #region Database validating
        private static bool ValidateData(List<Info> infoList)
        {
            foreach (Info info in infoList)
            {
                if (!info.IsValid) return false;
            }
            return true;
        }
        private static bool ValidateData(List<Candidate> candidateList)
        {

            foreach (Candidate candidate in candidateList)
            {
                if (!candidate.IsValid) return false;
            }
            return true;
        }
        #endregion

        /// <summary>
        /// Use loaded database lists to populate vote UI
        /// </summary>
        private void PopulateUI()
        {
            bool left = true;
            foreach (var info in infos)
            {
                // Sort the candidate list
                SortByHighestVotes(sections[info.Value.Sector]);

                DisplayCard displayCard = new(info.Value, sections[info.Value.Sector]);
                if (left)
                {
                    displayCard.Margin = new(0, 16, 56, 16);
                    left = false;
                }
                else
                {
                    displayCard.Margin = new(56, 16, 0, 16);
                    left = true;
                }

                displayCard.Click += (sender, e) => {
                    slideDetail.ChangeData(((DisplayCard)sender).SectionInfo, ((DisplayCard)sender).Candidates);
                    NextPage();
                };
                _ = slideOverview.OverviewStackPanel.Children.Add(displayCard);
            }
        }

        /// <summary>
        /// Sort the given list of Candidate by highest votes to lowest votes
        /// </summary>
        /// <param name="candidates">List of candidates to sort</param>
        private static void SortByHighestVotes(in List<Candidate> candidates)
        {
            // Compare function, hover over new() for more info on how this works
            Comparison<Candidate> comparison = new((x, y) => (int)y.Votes - (int)x.Votes);
            candidates.Sort(comparison);
        }
        private static void FindMaxVote(in List<Candidate> candidates)
        {
            if (candidates.Count == 0) throw new ArgumentException("Empty candidate list as parameter");
            long max = candidates[0].Votes;
            List<int> winningCandidates = new();
            winningCandidates.Add(0);
            for (int i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].Votes >= max)
                {
                    if (candidates[i].Votes > max)
                    {
                        winningCandidates.Clear();
                        max = candidates[i].Votes;
                    }
                    winningCandidates.Add(i);
                }
            }

            foreach (int index in winningCandidates)
            {
                candidates[index].TotalWinningPlaces++;
            }
        }

        #region Transitioner methods
        /// <summary>
        /// Move to next Slide
        /// </summary>
        private void NextPage()
        {
            // 0 is argument, meaning no arg
            // Pass object as second argument
            Transitioner.MoveNextCommand.Execute(0, TransitionerObj);
        }
        /// <summary>
        /// Move to previous Slide
        /// </summary>
        private void PreviousPage()
        {
            Transitioner.MovePreviousCommand.Execute(0, TransitionerObj);
        }
        #endregion
    }
}
