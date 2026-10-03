using System.Windows;

namespace Stokbox.App.Views
{
    public partial class AskWindow : Window
    {
        public AskWindow(string headline, string question)
        {
            InitializeComponent();
            HeadlineText.Text = headline;
            QuestionText.Text = question;
            Loaded += (sender, e) => YesButton.Focus();
        }

        private void OnYesClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
