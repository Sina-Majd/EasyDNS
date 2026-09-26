using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EasyDNS.Views
{
    public partial class DarkMessageBox : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public DarkMessageBox(string message, string title, MessageBoxButton button, MessageBoxImage icon)
        {
            InitializeComponent();

            TxtTitle.Text = string.IsNullOrEmpty(title) ? "EasyDNS" : title;
            TxtMessage.Text = message;

            ConfigureButtons(button, TxtTitle.Text, message);
            ConfigureIcon(icon);
        }

        private void ConfigureButtons(MessageBoxButton button, string title, string message)
        {
            if (button == MessageBoxButton.YesNo)
            {
                BtnOk.Visibility = Visibility.Collapsed;
                BtnYes.Visibility = Visibility.Visible;
                BtnNo.Visibility = Visibility.Visible;

                bool isDestructive = title.Contains("Delete", System.StringComparison.OrdinalIgnoreCase) ||
                                     message.Contains("Delete", System.StringComparison.OrdinalIgnoreCase);
                if (isDestructive && Application.Current.TryFindResource("ModernDangerButton") is Style dangerStyle)
                {
                    BtnYes.Style = dangerStyle;
                }
            }
            else
            {
                BtnOk.Visibility = Visibility.Visible;
                BtnYes.Visibility = Visibility.Collapsed;
                BtnNo.Visibility = Visibility.Collapsed;
            }
        }

        private void ConfigureIcon(MessageBoxImage icon)
        {
            IconWarning.Visibility = Visibility.Collapsed;
            IconInfo.Visibility = Visibility.Collapsed;
            IconError.Visibility = Visibility.Collapsed;
            IconQuestion.Visibility = Visibility.Collapsed;

            switch (icon)
            {
                case MessageBoxImage.Warning:
                    IconWarning.Visibility = Visibility.Visible;
                    IconBadge.Background = new SolidColorBrush(Color.FromArgb(28, 245, 158, 11)); // translucent amber 11%
                    IconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(85, 245, 158, 11)); // subtle amber border 33%
                    break;
                case MessageBoxImage.Error:
                    IconError.Visibility = Visibility.Visible;
                    IconBadge.Background = new SolidColorBrush(Color.FromArgb(28, 244, 63, 94)); // translucent rose 11%
                    IconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(85, 244, 63, 94)); // subtle rose border 33%
                    break;
                case MessageBoxImage.Question:
                    IconQuestion.Visibility = Visibility.Visible;
                    IconBadge.Background = new SolidColorBrush(Color.FromArgb(28, 6, 182, 212)); // translucent cyan 11%
                    IconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(85, 6, 182, 212)); // subtle cyan border 33%
                    break;
                case MessageBoxImage.Information:
                default:
                    IconInfo.Visibility = Visibility.Visible;
                    IconBadge.Background = new SolidColorBrush(Color.FromArgb(28, 16, 185, 129)); // translucent emerald 11%
                    IconBadge.BorderBrush = new SolidColorBrush(Color.FromArgb(85, 16, 185, 129)); // subtle emerald border 33%
                    break;
            }
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
            Result = MessageBoxResult.Cancel;
            Close();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            Close();
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            Close();
        }

        public static MessageBoxResult Show(Window? owner, string message, string title, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.Information)
        {
            var msgBox = new DarkMessageBox(message, title, button, icon);
            if (owner != null && owner.IsVisible)
            {
                msgBox.Owner = owner;
            }
            msgBox.ShowDialog();
            return msgBox.Result;
        }
    }
}
