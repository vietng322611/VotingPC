﻿using System;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace VotingPC
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly PasswordDialog passwordDialog;
        private string databasePath;
        public MainWindow()
        {
            SQLitePCL.Batteries_V2.Init();
            InitializeComponent();
            if (!ShowOpenDatabaseDialog()) return;

            // Check if file can be written to or not. Exit if read-only
            try
            {
                using FileStream file = new(databasePath, FileMode.Open, FileAccess.ReadWrite);
            }
            catch
            {
                CloseDialog();
                ShowTextDialog("File cơ sở dữ liệu chỉ đọc. Thiếu quyền admin.\n" +
                    "Vui lòng chạy lại chương trình với quyền admin hoặc\n" +
                    "chuyển file vào nơi có thể ghi được như Desktop.", "OK", () =>
                    {
                        Close();
                    });
                return;
            }

            passwordDialog = new("Nhập mật khẩu cơ sở dữ liệu:", "Mật khẩu không chính xác hoặc cơ sở dữ liệu không hợp lệ!", "Hoàn tất", PasswordDialogButton_Click);
            ShowPasswordDialog();
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (connection != null) _ = connection.CloseAsync();
            isListening = false;
            if (serial != null)
            {
                serial.Write("C"); // App closed signal
                serial.Close();
                serial.Dispose();
            }
        }
        private bool ShowOpenDatabaseDialog()
        {
            OpenFileDialog openFileDialog = new()
            {
                Filter = "Database file (*.db)|*.db",
                Multiselect = false,
                Title = "Chọn tệp cơ sở dữ liệu",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                databasePath = openFileDialog.FileName;
                return true;
            }
            else
            {
                Close();
                return false;
            }
        }
    }
}
