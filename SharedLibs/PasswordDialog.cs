﻿using MaterialDesignThemes.Wpf;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VotingPC
{
    public class PasswordDialog
    {
        private readonly DialogHost dialogHost;
        private bool falsePassword;
        private readonly string wrongPasswordTitle;
        private readonly StackPanel stackPanel;
        private readonly TextBlock title;
        private readonly PasswordBox passwordBox;
        public string Password => passwordBox.Password;
        /// <summary>
        /// Create new instance of a Material Design password dialog
        /// </summary>
        /// <param name="dialogHost">The DialogHost to host the password dialog</param>
        /// <param name="title">Title of the password box</param>
        /// <param name="wrongPasswordTitle">Title to show when password is wrong</param>
        /// <param name="buttonText">Content of the button</param>
        /// <param name="buttonClickHandler">Event to call on button click</param>
        public PasswordDialog(DialogHost dialogHost, string title, string wrongPasswordTitle, string buttonText, RoutedEventHandler buttonClickHandler)
        {
            // This is utterly stupid
            // TODO: Fix this mess, make it WPF
            this.dialogHost = dialogHost;
            this.wrongPasswordTitle = wrongPasswordTitle;
            stackPanel = new()
            {
                Margin = new Thickness(32),
                LayoutTransform = new ScaleTransform(2, 2),
                Orientation = Orientation.Vertical
            };
            this.title = new()
            {
                Text = title,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            passwordBox = new()
            {
                MaxLength = 32,
                Margin = new Thickness(10)
            };
            passwordBox.KeyDown += (sender, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    buttonClickHandler(sender, null);
                }
            };
            // Add top text on password box
            HintAssist.SetHelperText(passwordBox, "Mật khẩu");
            Button button = new()
            {
                Content = buttonText,
                HorizontalAlignment = HorizontalAlignment.Right,
                Style = (Style)Application.Current.Resources["MaterialDesignFlatButton"]
            };
            button.Click += buttonClickHandler;

            _ = stackPanel.Children.Add(this.title);
            _ = stackPanel.Children.Add(passwordBox);
            _ = stackPanel.Children.Add(button);
        }

        /// <summary>
        /// Returns StackPanel object that have all the content of the Dialog
        /// </summary>
        public StackPanel Dialog
        {
            get
            {
                if (!falsePassword) return stackPanel;
                title.Text = wrongPasswordTitle;
                passwordBox.Password = "";
                title.Foreground = Brushes.Red;
                return stackPanel;
            }
        }
        /// <summary>
        /// Show password dialog, WON'T WAIT FOR CLOSE
        /// </summary>
        /// <param name="falsePassword">Set to true to show wrong password title instead</param>
        public void Show(bool falsePassword = false)
        {
            this.falsePassword = falsePassword;
            _ = dialogHost.ShowDialog(Dialog);
        }
    }
}
