using PianoStream.UI.Views;
using System.Windows;
using Wpf.Ui.Controls;

namespace PianoStream
{
    public partial class MainWindow : FluentWindow
    {
        private bool _isUserClosedPane;

        private bool _isPaneOpenedOrClosedFromCode;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => NavigationView.Navigate(typeof(PianoPage));
        }

        private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isUserClosedPane)
            {
                return;
            }

            _isPaneOpenedOrClosedFromCode = true;
            NavigationView.SetCurrentValue(NavigationView.IsPaneOpenProperty, e.NewSize.Width > 1200);
            _isPaneOpenedOrClosedFromCode = false;
        }

        private void OnNavigation_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (sender is not Wpf.Ui.Controls.NavigationView navigationView)
            {
                return;
            }

            NavigationView.SetCurrentValue(
                NavigationView.HeaderVisibilityProperty,
                navigationView.SelectedItem?.TargetPageType != typeof(PianoPage)
                    ? Visibility.Visible
                    : Visibility.Collapsed
            );
        }

        private void NavigationView_OnPaneOpened(NavigationView sender, RoutedEventArgs args)
        {
            if (_isPaneOpenedOrClosedFromCode)
            {
                return;
            }

            _isUserClosedPane = false;
        }

        private void NavigationView_OnPaneClosed(NavigationView sender, RoutedEventArgs args)
        {
            if (_isPaneOpenedOrClosedFromCode)
            {
                return;
            }

            _isUserClosedPane = true;
        }
    }
}