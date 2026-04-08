using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using EmailValidatorPRO.Models;
using EmailValidatorPRO.Services;
using Microsoft.Win32;
using Serilog;

namespace EmailValidatorPRO.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly EmailValidatorService _validatorService;
        private readonly ExportService _exportService;
        private readonly ExcelExportService _excelExportService;
        private LicenseService _license = new();

        private CancellationTokenSource? _cts;
        private bool _disposed;
        private readonly Stopwatch _stopwatch = new();

        // ─── LICENCIA ─────────────────────────────────────────
        public event Action? RequestLicenseWindow;
        public bool IsLicenseActive => _license.IsActivated;

        public void ReloadLicense()
        {
            _license = new LicenseService();
            OnPropertyChanged(nameof(IsLicenseActive));
        }

        // ─── INPUTS ───────────────────────────────────────────
        private string _emailInput = string.Empty;
        public string EmailInput
        {
            get => _emailInput;
            set { _emailInput = value; OnPropertyChanged(); }
        }

        // ─── CONFIG ───────────────────────────────────────────
        public ObservableCollection<int> ThreadOptions { get; } = new() { 1, 5, 10, 20, 50 };

        private int _selectedThreads = 10;
        public int SelectedThreads
        {
            get => _selectedThreads;
            set { _selectedThreads = value; OnPropertyChanged(); }
        }

        private bool _enableSmtp = true;
        public bool EnableSmtp
        {
            get => _enableSmtp;
            set { _enableSmtp = value; OnPropertyChanged(); }
        }

        private bool _syntaxOnly;
        public bool SyntaxOnly
        {
            get => _syntaxOnly;
            set { _syntaxOnly = value; OnPropertyChanged(); }
        }

        private bool _enableCatchAll = true;
        public bool EnableCatchAll
        {
            get => _enableCatchAll;
            set { _enableCatchAll = value; OnPropertyChanged(); }
        }

        // ─── ESTADO VALIDACIÓN ────────────────────────────────
        private bool _isValidating;
        public bool IsValidating
        {
            get => _isValidating;
            set { _isValidating = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotValidating)); }
        }
        public bool IsNotValidating => !_isValidating;

        private int _progressPercent;
        public int ProgressPercent
        {
            get => _progressPercent;
            set { _progressPercent = value; OnPropertyChanged(); }
        }

        private string _progressText = "Listo";
        public string ProgressText
        {
            get => _progressText;
            set { _progressText = value; OnPropertyChanged(); }
        }

        // ─── STATS ────────────────────────────────────────────
        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set { _totalCount = value; OnPropertyChanged(); }
        }

        private int _validCount;
        public int ValidCount
        {
            get => _validCount;
            set { _validCount = value; OnPropertyChanged(); }
        }

        private int _invalidCount;
        public int InvalidCount
        {
            get => _invalidCount;
            set { _invalidCount = value; OnPropertyChanged(); }
        }

        private int _riskyCount;
        public int RiskyCount
        {
            get => _riskyCount;
            set { _riskyCount = value; OnPropertyChanged(); }
        }

        private int _disposableCount;
        public int DisposableCount
        {
            get => _disposableCount;
            set { _disposableCount = value; OnPropertyChanged(); }
        }

        private int _blockedCount;
        public int BlockedCount
        {
            get => _blockedCount;
            set { _blockedCount = value; OnPropertyChanged(); }
        }

        // ─── VELOCIDAD ────────────────────────────────────────
        private string _emailsPerSecond = "0.0";
        public string EmailsPerSecond
        {
            get => _emailsPerSecond;
            set { _emailsPerSecond = value; OnPropertyChanged(); }
        }

        private string _totalTime = "00:00";
        public string TotalTime
        {
            get => _totalTime;
            set { _totalTime = value; OnPropertyChanged(); }
        }

        // ─── RESULTADOS ───────────────────────────────────────
        public ObservableCollection<EmailResult> AllResults { get; set; } = new();

        private EmailResult? _selectedResult;
        public EmailResult? SelectedResult
        {
            get => _selectedResult;
            set { _selectedResult = value; OnPropertyChanged(); }
        }

        public ObservableCollection<EmailResult> ValidResults { get; } = new();
        public ObservableCollection<EmailResult> InvalidResults { get; } = new();
        public ObservableCollection<EmailResult> RiskyResults { get; } = new();
        public ObservableCollection<EmailResult> DisposableResults { get; } = new();
        public ObservableCollection<EmailResult> BlockedResults { get; } = new();

        // ─── TAB ──────────────────────────────────────────────
        private int _selectedTab;
        public int SelectedTab
        {
            get => _selectedTab;
            set { _selectedTab = value; OnPropertyChanged(); }
        }

        // ─── COMANDOS ─────────────────────────────────────────
        public ICommand ValidateCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ExportTxtCommand { get; }
        public ICommand ExportValidCsvCommand { get; }
        public ICommand ExportValidTxtCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportValidExcelCommand { get; }

        // ═══════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════════
        public MainViewModel()
        {
            _validatorService = new EmailValidatorService(new ValidationConfig());
            _exportService = new ExportService();
            _excelExportService = new ExcelExportService();

            ValidateCommand = new AsyncRelayCommand(ExecuteValidateAsync, () => !IsValidating);
            CancelCommand = new RelayCommand(CancelValidation, () => IsValidating);
            ClearCommand = new RelayCommand(ClearResults);

            ExportCsvCommand = new RelayCommand(() => ExportCsv(false), () => AllResults.Count > 0);
            ExportTxtCommand = new RelayCommand(() => ExportTxt(false), () => AllResults.Count > 0);
            ExportValidCsvCommand = new RelayCommand(() => ExportCsv(true), () => ValidResults.Count > 0);
            ExportValidTxtCommand = new RelayCommand(() => ExportTxt(true), () => ValidResults.Count > 0);
            ExportExcelCommand = new RelayCommand(() => ExportExcel(false), () => AllResults.Count > 0);
            ExportValidExcelCommand = new RelayCommand(() => ExportExcel(true), () => ValidResults.Count > 0);

            Log.Information("EmailValidator PRO inicializado");
        }

        // ═══════════════════════════════════════════════════════
        //  VALIDACIÓN PRINCIPAL
        // ═══════════════════════════════════════════════════════
        private async Task ExecuteValidateAsync()
        {
            var emails = EmailInput
                .Split(new[] { '\n', '\r', ',', ';', ' ' },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Distinct()
                .ToArray();

            if (emails.Length == 0)
            {
                MessageBox.Show("Ingresá al menos un email.", "Sin datos",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // CHECK LICENCIA
            var (canProceed, licenseMessage) = _license.CanValidate(emails.Length);
            if (!canProceed)
            {
                MessageBox.Show(licenseMessage, "Licencia requerida",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                RequestLicenseWindow?.Invoke();
                return;
            }

            IsValidating = true;
            _cts = new CancellationTokenSource();
            _stopwatch.Restart();
            ProgressPercent = 0;
            ProgressText = $"0 / {emails.Length}";

            var bag = new ConcurrentBag<EmailResult>();

            try
            {
                var progress = new Progress<(int Processed, int Total, EmailResult Result)>(p =>
                {
                    var pct = (int)((double)p.Processed / p.Total * 100);
                    var elapsed = _stopwatch.Elapsed.TotalSeconds;
                    var eps = elapsed > 0 ? (p.Processed / elapsed).ToString("F1") : "0.0";

                    ProgressPercent = pct;
                    ProgressText = $"{p.Processed} / {p.Total}";
                    EmailsPerSecond = eps;
                    TotalTime = _stopwatch.Elapsed.ToString(@"mm\:ss");
                });

                await _validatorService.ValidateBatchAsync(bag, emails, progress, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                ProgressText = $"Cancelado en {bag.Count} / {emails.Length}";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error en validación");
                MessageBox.Show($"Error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsValidating = false;
                _stopwatch.Stop();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var r in bag.OrderBy(x => x.Email))
                        AllResults.Add(r);

                    RefreshStats();
                    RefreshFilteredCollections();
                });

                ProgressText = $"✅ {AllResults.Count} emails en {_stopwatch.Elapsed.ToString(@"mm\:ss")}";
                _license.RecordUsage(emails.Length);
            }
        }

        private void CancelValidation()
        {
            _cts?.Cancel();
            ProgressText = "Cancelando...";
        }

        // ═══════════════════════════════════════════════════════
        //  STATS & FILTROS
        // ═══════════════════════════════════════════════════════
        private void RefreshStats()
        {
            TotalCount = AllResults.Count;
            ValidCount = AllResults.Count(r => r.Status == EmailStatus.Valid);
            InvalidCount = AllResults.Count(r => r.Status == EmailStatus.Invalid);
            RiskyCount = AllResults.Count(r => r.Status == EmailStatus.Risky);
            DisposableCount = AllResults.Count(r => r.Status == EmailStatus.Disposable);
            BlockedCount = AllResults.Count(IsSmtpBlocked);
        }

        private void RefreshFilteredCollections()
        {
            ValidResults.Clear();
            InvalidResults.Clear();
            RiskyResults.Clear();
            DisposableResults.Clear();
            BlockedResults.Clear();

            foreach (var r in AllResults)
            {
                switch (r.Status)
                {
                    case EmailStatus.Valid: ValidResults.Add(r); break;
                    case EmailStatus.Invalid: InvalidResults.Add(r); break;
                    case EmailStatus.Risky: RiskyResults.Add(r); break;
                    case EmailStatus.Disposable: DisposableResults.Add(r); break;
                }
            }

            foreach (var r in AllResults.Where(IsSmtpBlocked))
                BlockedResults.Add(r);
        }

        private static bool IsSmtpBlocked(EmailResult r)
        {
            return r.SmtpChecked && !r.SmtpAccepted
                && r.SmtpError != null
                && (r.SmtpError.Contains("bloqueado", StringComparison.OrdinalIgnoreCase)
                    || r.SmtpError.Contains("blocked", StringComparison.OrdinalIgnoreCase)
                    || r.SmtpError.Contains("refused", StringComparison.OrdinalIgnoreCase)
                    || r.SmtpError.Contains("denied", StringComparison.OrdinalIgnoreCase));
        }

        // ═══════════════════════════════════════════════════════
        //  EXPORTACIÓN
        // ═══════════════════════════════════════════════════════
        private void ExportCsv(bool onlyValid)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"email_validator_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (sfd.ShowDialog() != true) return;

            _exportService.ExportToCsv(sfd.FileName, AllResults, onlyValid);
            MessageBox.Show($"Exportado a:\n{sfd.FileName}", "CSV OK",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportTxt(bool onlyValid)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "TXT files (*.txt)|*.txt",
                FileName = $"email_validator_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };
            if (sfd.ShowDialog() != true) return;

            _exportService.ExportToTxt(sfd.FileName, AllResults, onlyValid);
            MessageBox.Show($"Exportado a:\n{sfd.FileName}", "TXT OK",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportExcel(bool onlyValid)
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Excel files (*.xlsx)|*.xlsx",
                FileName = $"email_validator_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };
            if (sfd.ShowDialog() != true) return;

            try
            {
                var results = onlyValid
                    ? AllResults.Where(r => r.Status == EmailStatus.Valid).ToList()
                    : AllResults.ToList();
                _excelExportService.ExportToExcel(sfd.FileName, results);
                MessageBox.Show($"Exportados {results.Count} registros a:\n{sfd.FileName}",
                    "Excel OK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al exportar: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════════════════════
        //  LIMPIAR
        // ═══════════════════════════════════════════════════════
        private void ClearResults()
        {
            AllResults.Clear();
            ValidResults.Clear();
            InvalidResults.Clear();
            RiskyResults.Clear();
            DisposableResults.Clear();
            BlockedResults.Clear();

            TotalCount = 0;
            ValidCount = 0;
            InvalidCount = 0;
            RiskyCount = 0;
            DisposableCount = 0;
            BlockedCount = 0;
            SelectedResult = null;

            ProgressPercent = 0;
            ProgressText = "Listo";
            EmailsPerSecond = "0.0";
            TotalTime = "00:00";
        }

        // ═══════════════════════════════════════════════════════
        //  INotifyPropertyChanged
        // ═══════════════════════════════════════════════════════
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void Dispose()
        {
            _disposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}