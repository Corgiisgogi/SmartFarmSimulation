using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using SmartFarmUI.Infrastructure;
using SmartFarmUI.Services;

namespace SmartFarmUI.ViewModels
{
    public class LogViewModel : ViewModelBase
    {
        private readonly ILogService _logService;

        public ObservableCollection<string> AllEntries { get; } = new ObservableCollection<string>();

        public ICollectionView AllLogsView { get; }
        public ICollectionView ErrorLogsView { get; }
        public ICollectionView WarningLogsView { get; }
        public ICollectionView InfoLogsView { get; }

        private int _errorCount;
        public int ErrorCount { get => _errorCount; private set => SetField(ref _errorCount, value); }

        private int _warningCount;
        public int WarningCount { get => _warningCount; private set => SetField(ref _warningCount, value); }

        private int _infoCount;
        public int InfoCount { get => _infoCount; private set => SetField(ref _infoCount, value); }

        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

        public ICommand SaveLogsCommand { get; }
        public ICommand LoadLogsCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand RefreshCommand { get; }

        public LogViewModel(ILogService logService)
        {
            _logService = logService;

            AllLogsView = CollectionViewSource.GetDefaultView(AllEntries);

            var errorSrc = new CollectionViewSource { Source = AllEntries };
            errorSrc.Filter += (s, e) => e.Item is string entry && IsError(entry);
            ErrorLogsView = errorSrc.View;

            var warnSrc = new CollectionViewSource { Source = AllEntries };
            warnSrc.Filter += (s, e) => e.Item is string entry && IsWarning(entry);
            WarningLogsView = warnSrc.View;

            var infoSrc = new CollectionViewSource { Source = AllEntries };
            infoSrc.Filter += (s, e) => e.Item is string entry && !IsError(entry) && !IsWarning(entry);
            InfoLogsView = infoSrc.View;

            SaveLogsCommand = new RelayCommand(ExecuteSave);
            LoadLogsCommand = new RelayCommand(ExecuteLoad);
            ClearLogsCommand = new RelayCommand(ExecuteClear);
            RefreshCommand = new RelayCommand(LoadFromService);

            _logService.LogEntryAdded += entry =>
                DispatcherHelper.RunOnUI(() => { if (entry != null) { AllEntries.Add(entry); RefreshCounts(); } });

            LoadFromService();
        }

        private void LoadFromService()
        {
            AllEntries.Clear();
            var entries = _logService.GetLogEntries();
            if (entries != null)
                foreach (var e in entries) AllEntries.Add(e);
            RefreshCounts();
        }

        private void RefreshCounts()
        {
            ErrorCount = AllEntries.Count(IsError);
            WarningCount = AllEntries.Count(IsWarning);
            InfoCount = AllEntries.Count(e => !IsError(e) && !IsWarning(e));
        }

        private static bool IsError(string e) =>
            e != null && (e.Contains("[오류]") || e.Contains("오류:") || e.Contains("Error"));

        private static bool IsWarning(string e) =>
            e != null && (e.Contains("[경고]") || e.Contains("경고:") || e.Contains("⚠️") || e.Contains("Warning"));

        private void ExecuteSave()
        {
            var dlg = new SaveFileDialog
            {
                Filter = "텍스트 파일 (*.txt)|*.txt",
                DefaultExt = ".txt",
                FileName = $"farm_logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };
            if (dlg.ShowDialog() == true)
            {
                try { File.WriteAllLines(dlg.FileName, AllEntries, System.Text.Encoding.UTF8); StatusMessage = $"저장됨: {dlg.FileName}"; }
                catch (Exception ex) { StatusMessage = $"저장 오류: {ex.Message}"; }
            }
        }

        private void ExecuteLoad()
        {
            var dlg = new OpenFileDialog { Filter = "텍스트 파일 (*.txt)|*.txt" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var lines = File.ReadAllLines(dlg.FileName, System.Text.Encoding.UTF8);
                    AllEntries.Clear();
                    foreach (var l in lines) AllEntries.Add(l);
                    RefreshCounts();
                    StatusMessage = $"로드됨: {lines.Length}개";
                }
                catch (Exception ex) { StatusMessage = $"로드 오류: {ex.Message}"; }
            }
        }

        private void ExecuteClear()
        {
            _logService.ClearLogs();
            AllEntries.Clear();
            RefreshCounts();
            StatusMessage = "로그 초기화됨";
        }
    }
}
