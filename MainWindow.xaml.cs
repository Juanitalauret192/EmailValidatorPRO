using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using EmailValidatorPRO.Models;
using EmailValidatorPRO.ViewModels;
using EmailValidatorPRO.Services;

namespace EmailValidatorPRO
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void Header_ActivateLicense(object sender, RoutedEventArgs e)
        {
            var licenseService = new LicenseService();
            var window = new LicenseWindow(licenseService);
            window.Owner = this;
            window.ShowDialog();

            if (window.LicenseWasActivated)
            {
                ((MainViewModel)DataContext).ReloadLicense();
            }
        }
    }

    public class EnumToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EmailStatus status)
            {
                return status switch
                {
                    EmailStatus.Valid => 0,
                    EmailStatus.Invalid => 1,
                    EmailStatus.Risky => 2,
                    EmailStatus.Disposable => 3,
                    _ => 0
                };
            }
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int index)
            {
                return index switch
                {
                    0 => EmailStatus.Valid,
                    1 => EmailStatus.Invalid,
                    2 => EmailStatus.Risky,
                    3 => EmailStatus.Disposable,
                    _ => EmailStatus.Valid
                };
            }
            return EmailStatus.Valid;
        }
    }
}