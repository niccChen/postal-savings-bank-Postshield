using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace PostShield
{
    public partial class MainWindow : Window
    {
        private string _folder = "";
        private string[] _allFiles = Array.Empty<string>();
        private string _searchText = "";
        private Dictionary<string, string> _backupFiles = new Dictionary<string, string>(); // Track backup files

        public MainWindow()
        {
            InitializeComponent();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            
            // Wire up events
            BrowseBtn.Click += async (_, __) => await OnBrowseAsync();
            ScanBtn.Click += (_, __) => OnScan();
            MaskBtn.Click += (_, __) => OnMask();
            UndoMaskBtn.Click += (_, __) => OnUndoMask();
            
            // Wire up operation buttons
            OpenBtn.Click += OnOpenFile;
            RenameBtn.Click += OnRenameFile;
            DeleteBtn.Click += OnDeleteFile;
            
            // Wire up search functionality
            SearchBox.TextChanged += (_, __) => OnSearch();
            ClearSearchBtn.Click += (_, __) => ClearSearch();
            
            // Enable/disable operation buttons based on selection
            FileList.SelectionChanged += (_, __) =>
            {
                var hasSelection = FileList.SelectedItem != null;
                OpenBtn.IsEnabled = hasSelection;
                RenameBtn.IsEnabled = hasSelection;
                DeleteBtn.IsEnabled = hasSelection;
            };
            
            // Set up double-click
            FileList.DoubleTapped += (_, __) =>
            {
                if (FileList.SelectedItem is string path && File.Exists(path))
                    TryOpen(path);
            };
            
            // Add keyboard shortcuts
            this.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
                {
                    SearchBox.Focus();
                }
                else if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
                {
                    OnUndoMask();
                }
            };
        }

        private async Task OnBrowseAsync()
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose Folder",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                _folder = folders[0].Path.LocalPath;
                StatusBar.Text = $"Chosen: {_folder}";
                ClearSearch();
                // Clear backups when changing folder
                CleanupBackups();
                _backupFiles.Clear();
                UndoMaskBtn.IsEnabled = false;
            }
        }

        private void OnScan()
        {
            if (!Directory.Exists(_folder))
            {
                StatusBar.Text = "Please choose a valid folder first.";
                return;
            }

            var all = Directory.GetFiles(_folder, "*.xlsx", SearchOption.AllDirectories);
            _allFiles = all.Where(p => !Path.GetFileName(p).StartsWith("~$")).ToArray();

            UpdateFileList(_allFiles);
            StatusBar.Text = $"Found {_allFiles.Length} files in {_folder}";
        }

        private void OnSearch()
        {
            _searchText = SearchBox.Text ?? "";
            
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                UpdateFileList(_allFiles);
                return;
            }

            var filtered = _allFiles.Where(file => 
                Path.GetFileName(file).Contains(_searchText, StringComparison.OrdinalIgnoreCase)
            ).ToArray();

            UpdateFileList(filtered);
            StatusBar.Text = $"Search Result: {filtered.Length} / {_allFiles.Length} files found";
        }

        private void ClearSearch()
        {
            SearchBox.Text = "";
            _searchText = "";
            UpdateFileList(_allFiles);
            StatusBar.Text = $"{_allFiles.Length} files found";
        }

        private void UpdateFileList(string[] files)
        {
            FileList.ItemsSource = files;
        }

        private void OnMask()
        {
            try
            {
                var rules = LoadRules("Config/DataMaskingRules.xml");
                int processedCount = 0;
                int successCount = 0;
                
                var filesToProcess = FileList.ItemsSource as IEnumerable<string> ?? Array.Empty<string>();
                
                // Clean up old backups before creating new ones
                CleanupBackups();
                _backupFiles.Clear();
                
                foreach (var path in filesToProcess)
                {
                    // Create backup before masking
                    var backupPath = CreateBackup(path);
                    if (backupPath != null)
                    {
                        _backupFiles[path] = backupPath;
                        
                        if (ApplyMask(path, rules))
                        {
                            successCount++;
                        }
                    }
                    processedCount++;
                }

                StatusBar.Text = $"Data Masking Complete - Successfully processed {successCount}/{processedCount} files";
                UndoMaskBtn.IsEnabled = _backupFiles.Count > 0;
            }
            catch (Exception ex)
            {
                StatusBar.Text = $"Data Masking errored: {ex.Message}";
            }
        }

        private void OnUndoMask()
        {
            if (_backupFiles.Count == 0)
            {
                StatusBar.Text = "No masking operations to undo.";
                return;
            }

            try
            {
                int restoredCount = 0;
                var errors = new List<string>();

                foreach (var kvp in _backupFiles)
                {
                    var originalPath = kvp.Key;
                    var backupPath = kvp.Value;

                    try
                    {
                        if (File.Exists(backupPath))
                        {
                            // Restore from backup
                            File.Copy(backupPath, originalPath, true);
                            restoredCount++;
                        }
                        else
                        {
                            errors.Add($"{Path.GetFileName(originalPath)}: Backup not found");
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{Path.GetFileName(originalPath)}: {ex.Message}");
                    }
                }

                // Clean up backups after restore
                CleanupBackups();
                _backupFiles.Clear();
                UndoMaskBtn.IsEnabled = false;

                if (errors.Count > 0)
                {
                    StatusBar.Text = $"Undo complete with {errors.Count} errors. Restored {restoredCount} files.";
                }
                else
                {
                    StatusBar.Text = $"Undo complete - Restored {restoredCount} files to original state.";
                }
            }
            catch (Exception ex)
            {
                StatusBar.Text = $"Undo failed: {ex.Message}";
            }
        }

        private string? CreateBackup(string originalPath)
        {
            try
            {
                var backupDir = Path.Combine(Path.GetTempPath(), "PostShield_Backups");
                Directory.CreateDirectory(backupDir);
                
                var fileName = Path.GetFileName(originalPath);
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var backupFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_backup_{timestamp}{Path.GetExtension(fileName)}";
                var backupPath = Path.Combine(backupDir, backupFileName);
                
                File.Copy(originalPath, backupPath, true);
                return backupPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create backup for {originalPath}: {ex.Message}");
                return null;
            }
        }

        private void CleanupBackups()
        {
            try
            {
                foreach (var backupPath in _backupFiles.Values)
                {
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }
                }
                
                // Clean up old backup files (older than 1 day)
                var backupDir = Path.Combine(Path.GetTempPath(), "PostShield_Backups");
                if (Directory.Exists(backupDir))
                {
                    var oldFiles = Directory.GetFiles(backupDir)
                        .Where(f => File.GetCreationTime(f) < DateTime.Now.AddDays(-1));
                    
                    foreach (var file in oldFiles)
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Cleanup error: {ex.Message}");
            }
        }

        private List<(Regex Pattern, string Mask)> LoadRules(string xml)
        {
            if (!File.Exists(xml))
            {
                return new List<(Regex, string)>
                {
                    (new Regex(@"\d{11}"), "###****####"),
                    (new Regex(@"\d{18}"), "######********####"),
                    (new Regex(@"\d{16,19}"), "####****####")
                };
            }
            
            var doc = XDocument.Load(xml);
            return doc.Descendants("Rule")
                      .Select(r => 
                          (
                            new Regex(r.Element("Pattern")?.Value ?? ""),
                            r.Element("Mask")?.Value ?? ""
                          ))
                      .ToList();
        }

        private bool ApplyMask(string file, List<(Regex Pattern, string Mask)> rules)
        {
            try
            {
                using var pkg = new ExcelPackage(new FileInfo(file));
                bool hasChanges = false;
                
                foreach (var ws in pkg.Workbook.Worksheets)
                {
                    if (ws.Dimension == null) continue;
                    
                    for (int r = ws.Dimension.Start.Row; r <= ws.Dimension.End.Row; r++)
                    for (int c = ws.Dimension.Start.Column; c <= ws.Dimension.End.Column; c++)
                    {
                        if (ws.Cells[r, c].Value is string s)
                        {
                            foreach (var (pat, mask) in rules)
                            {
                                if (pat.IsMatch(s))
                                {
                                    ws.Cells[r, c].Value = ApplyMaskTemplate(s, mask);
                                    hasChanges = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                
                if (hasChanges)
                {
                    pkg.Save();
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Skipped {Path.GetFileName(file)}: {ex.Message}");
                return false;
            }
        }

        private static string ApplyMaskTemplate(string input, string mask)
        {
            var sb = new StringBuilder();
            int idx = 0;
            foreach (var m in mask)
            {
                if (idx >= input.Length) break;
                if (m == '#') { sb.Append(input[idx++]); }
                else if (m == '*') { sb.Append('*'); idx++; }
                else sb.Append(m);
            }
            return sb.ToString();
        }

        // Rest of your methods remain the same...
        private void OnOpenFile(object? sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is string path && File.Exists(path))
                TryOpen(path);
        }

        private async void OnDeleteFile(object? sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is not string path || !File.Exists(path)) return;

            var result = await ConfirmAction("Proceed to Delete", $"Are you sure you want to delete {Path.GetFileName(path)}?");
            
            if (result)
            {
                try
                {
                    // Remove backup if exists
                    if (_backupFiles.ContainsKey(path))
                    {
                        var backupPath = _backupFiles[path];
                        if (File.Exists(backupPath))
                            File.Delete(backupPath);
                        _backupFiles.Remove(path);
                    }
                    
                    File.Delete(path);
                    _allFiles = _allFiles.Where(f => f != path).ToArray();
                    OnSearch();
                    
                    UndoMaskBtn.IsEnabled = _backupFiles.Count > 0;
                }
                catch (Exception ex)
                {
                    StatusBar.Text = $"Delete failed: {ex.Message}";
                }
            }
        }

        private async void OnRenameFile(object? sender, RoutedEventArgs e)
        {
            if (FileList.SelectedItem is not string oldPath || !File.Exists(oldPath)) return;

            var newName = await GetTextInput("Rename file", "Please type new file name (with extension):", Path.GetFileName(oldPath));
            
            if (!string.IsNullOrWhiteSpace(newName))
            {
                var dir = Path.GetDirectoryName(oldPath)!;
                var newPath = Path.Combine(dir, newName.Trim());
                
                try
                {
                    File.Move(oldPath, newPath);
                    
                    // Update backup tracking
                    if (_backupFiles.ContainsKey(oldPath))
                    {
                        var backupPath = _backupFiles[oldPath];
                        _backupFiles.Remove(oldPath);
                        _backupFiles[newPath] = backupPath;
                    }
                    
                    for (int i = 0; i < _allFiles.Length; i++)
                    {
                        if (_allFiles[i] == oldPath)
                        {
                            _allFiles[i] = newPath;
                            break;
                        }
                    }
                    OnSearch();
                }
                catch (Exception ex)
                {
                    StatusBar.Text = $"Rename failed: {ex.Message}";
                }
            }
        }

        private void TryOpen(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StatusBar.Text = $"Open failed: {ex.Message}";
            }
        }

        private async Task<bool> ConfirmAction(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var result = false;

            var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };
            panel.Children.Add(new TextBlock 
            { 
                Text = message, 
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(0, 0, 0, 20)
            });

            var buttonPanel = new StackPanel 
            { 
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };

            var yesButton = new Button { Content = "Yes", Margin = new Avalonia.Thickness(0, 0, 10, 0) };
            var noButton = new Button { Content = "No" };

            yesButton.Click += (_, __) => { result = true; dialog.Close(); };
            noButton.Click += (_, __) => { result = false; dialog.Close(); };

            buttonPanel.Children.Add(yesButton);
            buttonPanel.Children.Add(noButton);
            panel.Children.Add(buttonPanel);

            dialog.Content = panel;
            await dialog.ShowDialog(this);

            return result;
        }

        private async Task<string?> GetTextInput(string title, string message, string defaultValue = "")
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            string? result = null;

            var panel = new StackPanel { Margin = new Avalonia.Thickness(20) };
            panel.Children.Add(new TextBlock 
            { 
                Text = message,
                Margin = new Avalonia.Thickness(0, 0, 0, 10)
            });

            var textBox = new TextBox 
            { 
                Text = defaultValue,
                Margin = new Avalonia.Thickness(0, 0, 0, 20)
            };
            panel.Children.Add(textBox);

            var buttonPanel = new StackPanel 
            { 
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };

            var okButton = new Button { Content = "Proceed", Margin = new Avalonia.Thickness(0, 0, 10, 0) };
            var cancelButton = new Button { Content = "Cancel" };

            okButton.Click += (_, __) => { result = textBox.Text; dialog.Close(); };
            cancelButton.Click += (_, __) => { result = null; dialog.Close(); };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            panel.Children.Add(buttonPanel);

            dialog.Content = panel;
            await dialog.ShowDialog(this);

            return result;
        }
    }
}