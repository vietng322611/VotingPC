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

namespace AsyncDialog
{
    /// <summary>
    /// Don't use this class, use AsyncDialog instead.
    /// </summary>
    public partial class TextDialog : UserControl
    {
        // Fake Bindings
        public string Text { get => textBox.Text; set => textBox.Text = value; }
        public string ButtonLabel { get => (string)button.Content; set => button.Content = value; }
        public string LeftButtonLabel { get => (string)leftButton.Content; set => leftButton.Content = value; }
        public string Title
        {
            get => titleBox.Text;
            set
            {
                if (value is null) titleBox.Visibility = Visibility.Collapsed;
                else
                {
                    titleBox.Visibility = Visibility.Visible;
                    titleBox.Text = value;
                }
            }
        }
        public bool EnableLeftButton
        {
            get => leftButton.Visibility == Visibility.Visible;
            // Enable button if true, else disable it
            set => leftButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public TextDialog()
        {
            InitializeComponent();
        }

        public double ScaleFactor
        {
            set
            {
                scale.ScaleX = value;
                scale.ScaleY = value;
            }
        }
    }
}