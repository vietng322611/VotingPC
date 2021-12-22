﻿using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using AsyncDialog;
using MaterialDesignThemes.Wpf.Transitions;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using SQLite;
using SQLitePCL;
using VotingPCNew.Scanner;

// using System.Collections.Generic;
// using System.Windows.Controls;
// using System.Windows.Data;
// using System.Windows.Documents;
// using System.Windows.Input;
// using System.Windows.Media;
// using System.Windows.Media.Imaging;
// using System.Windows.Navigation;
// using System.Windows.Shapes;

namespace VotingPCNew;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private const int BaudRate = 115200;
    private const string ConfigPath = "config.json";
    private readonly string _configParseError;

    private readonly AsyncDialogManager _dialogs;
    private readonly ScannerManager _scanner;
    private AsyncDatabaseManager _db;

    public MainWindow()
    {
        Batteries_V2.Init();
        InitializeComponent();
        _dialogs = new AsyncDialogManager(dialogHost)
        {
            ScaleFactor = 1.5
        };

        // Init scanner manager
        ScannerSignalTable signalTable = new()
        {
            Acknowledgement = 'V',
            Receive = new ReceiveSignalTable
            {
                FingerFound = 'F',
                InvalidFinger = 'I'
            },
            Send = new SendSignalTable
            {
                StartScan = 'S',
                AcknowledgedFinger = 'K',
                Close = 'C'
            }
        };
        _scanner = new ScannerManager(signalTable);

        // Read config.json config file
        // TODO: fix error handling for json
        try
        {
            string configFile = File.ReadAllText(ConfigPath);
            JsonSerializerOptions options = new()
            {
                PropertyNameCaseInsensitive = true
            };
            var config = JsonSerializer.Deserialize<Config>(configFile, options);

            slide1.TitleConfig = config.Title;
            slide1.TopHeaderConfig = config.Header;
            slide1.TopSubheaderConfig = config.Subheader;
            // Replace logo with custom image in app folder if exists and is valid
            if (File.Exists(config.IconPath))
            {
                slide1.IconPath = config.IconPath;
            }
            else _configParseError = "Không tìm thấy file logo. Kiểm tra lại đường dẫn.";
        }
        catch (Exception e)
        {
            _configParseError = e.Message;
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_configParseError != null)
        {
            await _dialogs.ShowTextDialog(_configParseError, "Lỗi file config.json");
            Close(); return;
        }

        string databasePath = ShowOpenDatabaseDialog();
        string outputPath;
        if (databasePath == null) { Close(); return; }

        bool saveToMultipleFile = await _dialogs.ShowConfirmTextDialog
        (
            title: "Chọn kiểu lưu",
            text: "Lưu kết quả vào file ban đầu hay tách các cấp thành nhiều file?",
            leftButtonLabel: "FILE BAN ĐẦU",
            rightButtonLabel: "NHIỀU FILE"
        );

        _db = new AsyncDatabaseManager(saveToMultipleFile);
        
        _dialogs.ShowLoadingDialog("Kiểm tra file .db và đường dẫn lưu kết quả");
        bool isReadOnly;
        if (!saveToMultipleFile)
        {
            isReadOnly = await Extensions.FileIsReadOnly(databasePath);
            outputPath = databasePath;
        }
        else while (true)
        {
            // Show open folder dialog
            VistaFolderBrowserDialog dialog = new()
            {
                Description = "Chọn thư mục chứa file cơ sở dữ liệu xuất ra",
                UseDescriptionForTitle = true,
                Multiselect = false
            };
            // If cancelled, exit the app
            if (dialog.ShowDialog() == false) { Close(); return; }

            if (!Directory.Exists(dialog.SelectedPath))
            {
                bool result = await _dialogs.ShowConfirmTextDialog(
                    title: "Lỗi tìm thư mục",
                    text: "Thư mục không tồn tại. Chọn lại?",
                    leftButtonLabel: "THOÁT", 
                    rightButtonLabel: "OK");
                if (result) continue;
                Close(); return;
            }

            if (Directory.GetFiles(dialog.SelectedPath).Length == 0)
            {
                bool result = await _dialogs.ShowConfirmTextDialog(
                    title: "Lỗi lưu kết quả",
                    text: "Thư mục lưu kết quả đã chọn không phải thư mục trống. Chọn lại?",
                    leftButtonLabel: "THOÁT", 
                    rightButtonLabel: "OK");
                if (result) continue;
                Close(); return;
            }
            isReadOnly = Extensions.FolderIsReadOnly(dialog.SelectedPath);
            outputPath = dialog.SelectedPath;
            break;
        }

        if (isReadOnly)
        {
            await _dialogs.CloseDialog();
            await _dialogs.ShowTextDialog(
                title: "File/thư mục chỉ đọc",
                text: "Thiếu quyền Admin hoặc phân quyền sai.\n"
                      + "Vui lòng chạy lại chương trình với quyền Admin, sửa quyền truy cập file\n"
                      + "hoặc chuyển file/thư mục vào nơi có thể ghi được như Desktop.");
            Close(); return;
        }

        await _dialogs.CloseDialog();

        string password;
        while (true)
        {
            // Get password
            password = await _dialogs.ShowPasswordDialog
            (
                "Nhập mật khẩu cơ sở dữ liệu",
                "Mật khẩu",
                "Để trống nếu không có mật khẩu",
                cancelButtonLabel: "THOÁT"
            );
            // Quit app if user cancelled
            if (password is null) { Close(); return; }

            // Try to open connection to db
            _dialogs.ShowLoadingDialog("Giải mã và mở kết nối đến file .db");
            bool wrongPassword = await _db.Open(databasePath, password);
            if (wrongPassword)
            {
                await Task.Delay(3000); // Avoid brute-force
                await _dialogs.CloseDialog();
                await _dialogs.ShowTextDialog("Mật khẩu sai, vui lòng nhập lại.");
            }
            else break;
        }
        
        _dialogs.ShowLoadingDialog("Tìm và kết nối với máy quét vân tay");
        try
        {
            await _scanner.Init(BaudRate, 10);
        }
        // Not found
        catch (DriveNotFoundException)
        {
            await _dialogs.CloseDialog();
            await _dialogs.ShowTextDialog("Không tìm thấy thiết bị Arduino");
            // TODO: add retry here
            Close();
            return;
        }
        // Other exceptions that we don't know, just in case
        catch (Exception err)
        {
            await _dialogs.CloseDialog();
            await _dialogs.ShowTextDialog(
                title: "Lỗi tìm thiết bị Arduino",
                text: "Mã lỗi: " + err.Message
            );
            // TODO: add retry here
            Close(); return;
        }
        
        _dialogs.ShowLoadingDialog("Load và xác thực file cơ sở dữ liệu");
        try
        {
            await _db.Load();
            if (_db.Validate() == false)
                throw new InvalidDataException();
        }
        catch (Exception)
        {
            await _dialogs.CloseDialog();
            await _dialogs.ShowTextDialog(
                title: "Cơ sở dữ liệu không hợp lệ", 
                text: "Vui lòng kiểm tra lại, hoặc sử dụng DbMaker để tạo file mới", 
                buttonLabel: "Đóng"
            );
            Close(); return;
        }

        string resultPassword = await _dialogs.ShowPasswordDialog
        (
            "Nhập mật khẩu cơ sở dữ liệu",
            "Mật khẩu",
            "Để trống nếu không có mật khẩu",
            cancelButtonLabel: "Dùng mật khẩu ban đầu"
        );
        // Use original password
        if (resultPassword is null) resultPassword = password;

        if (saveToMultipleFile) while (true)
        {
            
            try
            {
                await _db.SplitFiles(outputPath, resultPassword);
                break;
            }
            catch (IOException)
            {
                await _dialogs.ShowTextDialog(title: "Lỗi lưu file", text: "Thư mục chỉ đọc, vui lòng kiểm tra lại");
                Close();
                return;
            }
            catch (SQLiteException)
            {
                // TODO: implement this
            }
        }
        await _dialogs.CloseDialog();

        _scanner.StartScan();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        _scanner.Dispose();
        _db.Dispose();
    }

    /// <summary>
    ///     Show a dialog to select database file
    /// </summary>
    /// <returns>A path to selected file, null if not selected or canceled</returns>
    private static string ShowOpenDatabaseDialog(string title = @"Chọn tệp cơ sở dữ liệu")
    {
        OpenFileDialog openFileDialog = new()
        {
            Filter = "Database file (*.db)|*.db",
            Multiselect = false,
            Title = title,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (openFileDialog.ShowDialog() == true)
        {
            return openFileDialog.FileName;
        }

        return null;
    }

    // Transition methods
    /// <summary>
    ///     Move to next Slide
    /// </summary>
    private void NextPage()
    {
        Transitioner.MoveNextCommand.Execute(null, transitionerObj);
    }

    /// <summary>
    ///     Move to previous Slide
    /// </summary>
    private void PreviousPage()
    {
        Transitioner.MovePreviousCommand.Execute(null, transitionerObj);
    }
}