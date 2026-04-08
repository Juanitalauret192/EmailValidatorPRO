using System.Windows;
using System.Windows.Media;
using EmailValidatorPRO.Services;

namespace EmailValidatorPRO
{
    public partial class LicenseWindow : Window
    {
        private readonly LicenseService _license;

        public bool LicenseWasActivated { get; private set; }

        public LicenseWindow(LicenseService license)
        {
            InitializeComponent();
            _license = license;
            UpdateStatus();
            KeyInput.Focus();
        }

        private void UpdateStatus()
        {
            if (_license.IsActivated && !_license.IsExpired)
            {
                StatusText.Text = $"✅ {_license.LicenseType} - ACTIVADA";
                StatusText.Foreground = new SolidColorBrush(Colors.LimeGreen);
                UsageText.Text = $"Activada el {_license.ActivatedDate:dd/MM/yyyy HH:mm}";

                if (_license.ExpirationDate.HasValue)
                {
                    var diasRestantes = (_license.ExpirationDate.Value - DateTime.Now).Days;
                    ExpirationText.Text = $"Expira: {_license.ExpirationDate:dd/MM/yyyy} ({diasRestantes} días restantes)";
                    ExpirationText.Foreground = diasRestantes <= 7
                        ? new SolidColorBrush(Colors.OrangeRed)
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D29922"));
                }
                else
                {
                    ExpirationText.Text = "Sin vencimiento";
                    ExpirationText.Foreground = new SolidColorBrush(Colors.LimeGreen);
                }
            }
            else if (_license.IsExpired)
            {
                StatusText.Text = "⏰ LICENCIA EXPIRADA";
                StatusText.Foreground = new SolidColorBrush(Colors.OrangeRed);
                UsageText.Text = $"Expiró el {_license.ExpirationDate:dd/MM/yyyy}";
                ExpirationText.Text = "Contactá al vendedor para renovar";
            }
            else
            {
                StatusText.Text = "🧪 MODO TRIAL";
                StatusText.Foreground = new SolidColorBrush(Colors.Orange);
                var remaining = 50 - _license.TotalValidated;
                UsageText.Text = remaining > 0
                    ? $"Podés validar {remaining} emails más antes de necesitar licencia."
                    : "⚠️ Trial agotado. Necesitás una licencia.";
                ExpirationText.Text = "";
            }
        }

        private void Activate_Click(object sender, RoutedEventArgs e)
        {
            var key = KeyInput.Text.Trim();
            var (success, message) = _license.Activate(key);

            if (success)
            {
                ResultMessage.Text = "✅ " + message;
                ResultMessage.Foreground = new SolidColorBrush(Colors.LimeGreen);
                LicenseWasActivated = true;
                UpdateStatus();

                System.Threading.Tasks.Task.Delay(2500).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => Close());
                });
            }
            else
            {
                ResultMessage.Text = "❌ " + message;
                ResultMessage.Foreground = new SolidColorBrush(Colors.IndianRed);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}