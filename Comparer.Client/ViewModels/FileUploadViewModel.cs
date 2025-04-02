// FileUploadViewModel.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Comparer.Client.ViewModels
{
    public class FileUploadViewModel : ObservableObject
    {
        private readonly HttpClient _httpClient;
        private bool _isAnalysisModeSelected;

        public ObservableCollection<FileItem> SelectedFiles { get; } = new();
        public ObservableCollection<WorkerNode> AvailableWorkers { get; } = new();

        public bool IsAnalysisModeSelected
        {
            get => _isAnalysisModeSelected;
            set => SetProperty(ref _isAnalysisModeSelected, value);
        }

        public ICommand SelectFilesCommand { get; }
        public ICommand RemoveFileCommand { get; }
        public ICommand StartProcessingCommand { get; }

        public FileUploadViewModel()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

            SelectFilesCommand = new AsyncRelayCommand(SelectFilesAsync);
            RemoveFileCommand = new RelayCommand<FileItem>(RemoveFile);
            StartProcessingCommand = new AsyncRelayCommand(StartProcessingAsync);

            LoadAvailableWorkersAsync();
        }

        private async Task SelectFilesAsync()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Pliki tekstowe (*.txt)|*.txt|Wszystkie pliki (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var fileName in dialog.FileNames)
                {
                    var fileInfo = new FileInfo(fileName);
                    SelectedFiles.Add(new FileItem
                    {
                        Name = fileInfo.Name,
                        Size = fileInfo.Length / 1024.0 / 1024.0,
                        FilePath = fileName
                    });
                }
            }
        }

        private void RemoveFile(FileItem file)
        {
            if (file != null)
            {
                SelectedFiles.Remove(file);
            }
        }

        private async Task LoadAvailableWorkersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/workers");
                if (response.IsSuccessStatusCode)
                {
                    var workers = await response.Content.ReadFromJsonAsync<List<WorkerNode>>();
                    foreach (var worker in workers)
                    {
                        AvailableWorkers.Add(worker);
                    }
                }
            }
            catch (Exception ex)
            {
                AvailableWorkers.Add(new WorkerNode { Id = 1, Name = "Węzeł roboczy 1", IsAvailable = true });
                AvailableWorkers.Add(new WorkerNode { Id = 2, Name = "Węzeł roboczy 2", IsAvailable = true });
                AvailableWorkers.Add(new WorkerNode { Id = 3, Name = "Węzeł roboczy 3", IsAvailable = true });
            }
        }

        private async Task StartProcessingAsync()
        {
            if (SelectedFiles.Count < 2)
            {
                MessageBox.Show("Musisz wybrać co najmniej dwa pliki.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tempZipFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.zip");

            try
            {
                // Tworzenie archiwum ZIP
                using (var archive = ZipFile.Open(tempZipFile, ZipArchiveMode.Create))
                {
                    foreach (var file in SelectedFiles)
                    {
                        archive.CreateEntryFromFile(file.FilePath, file.Name);
                    }
                }

                var selectedWorkers = AvailableWorkers.Where(w => w.IsSelected).Select(w => w.Id).ToList();

                using (var content = new MultipartFormDataContent())
                using (var fileStream = File.OpenRead(tempZipFile))
                using (var streamContent = new StreamContent(fileStream))
                {
                    content.Add(streamContent, "files", "files.zip");
                    content.Add(new StringContent(JsonSerializer.Serialize(selectedWorkers)), "workers");
                    content.Add(new StringContent(IsAnalysisModeSelected.ToString()), "analyzeMode");

                    // Wysyłanie plików na serwer
                    var response = await _httpClient.PostAsync("/api/files/upload", content);
                    var result = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        MessageBox.Show($"Serwer odpowiedział: {result}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Błąd serwera: {response.StatusCode} - {result}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                try { if (File.Exists(tempZipFile)) File.Delete(tempZipFile); } catch { }
            }
        }


    }

    public class FileItem
    {
        public string Name { get; set; }
        public double Size { get; set; }
        public string FilePath { get; set; }
        public string SizeDisplay => $"{Size:F1} MB";
    }

    public class WorkerNode
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsSelected { get; set; }
    }

    public class UploadResult
    {
        public string SessionId { get; set; }
        public List<string> UploadedFiles { get; set; }
        public List<int> AssignedWorkers { get; set; }
    }
}