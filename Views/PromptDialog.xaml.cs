using System.Windows;
using System.Windows.Input;

namespace EasyDNS.Views
{
    public partial class PromptDialog : Window
    {
        public string? InputText { get; private set; }

        public PromptDialog(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();

            TxtTitle.Text = string.IsNullOrEmpty(title) ? "EasyDNS" : title;
            TxtPrompt.Text = prompt;
            TxtInput.Text = defaultValue;

            Loaded += delegate
            {
                TxtInput.Focus();
                TxtInput.SelectAll();
            };
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            InputText = null;
            DialogResult = false;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            InputText = null;
            DialogResult = false;
            Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            InputText = TxtInput.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnSave_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                BtnCancel_Click(sender, e);
            }
        }

        public static string? Show(Window? owner, string title, string prompt, string defaultValue = "")
        {
            var dialog = new PromptDialog(title, prompt, defaultValue);
            if (owner != null && owner.IsVisible)
            {
                dialog.Owner = owner;
            }
            return dialog.ShowDialog() == true ? dialog.InputText : null;
        }
    }
}
