using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Converter.Themes;
using Microsoft.Win32;
using Pdf2Image;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Windows;

namespace Converter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    List<string> files = [];
    [ObservableProperty] string? _theme;
    [ObservableProperty] string? __fileMessage;
    [ObservableProperty] double _filesProgress;
    [ObservableProperty] bool _isFree = true;
    [ObservableProperty] System.Windows.Input.Cursor _cursor = System.Windows.Input.Cursors.Arrow;

    public MainViewModel()
    { 
        Theme = $"Theme {Settings.Default.Theme}";
    }

    [RelayCommand]
    void OpenFile()
    {
        Reset();
        OpenFileDialog openFileDialog = new() { Filter = "Pdf files (*.pdf)|*.pdf", Multiselect = true };
        if (openFileDialog.ShowDialog() == true)
        {
            files = [.. openFileDialog.FileNames];
            FileMessage = $"{files.Count} file(s) loaded";
        }
    }

    [RelayCommand]
    void OpenFolder()
    {
        Reset();
        OpenFolderDialog openFolderDialog = new() { Multiselect = true };
        if (openFolderDialog.ShowDialog() == true)
        {
            foreach (string folder in openFolderDialog.FolderNames)
                files.AddRange(Directory.GetFiles(folder, "*.pdf"));
            FileMessage = $"{files.Count} file(s) loaded from {openFolderDialog.FolderNames.Length} folder(s)";
        }
    }

    [RelayCommand]
    async Task Convert()
    {
        if (files.Count > 0)
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            IsFree = false;
            int filesCount = files.Count;
            int processedFiles = 0;
            foreach (string file in files)
            {
                await ExtractImagesAsync(file);
                CreateArchive(file);
                processedFiles++;
                FilesProgress = (double)processedFiles / files.Count * 100;
            }
            FileMessage = $"Done converting {filesCount} file(s)";
            Cursor = System.Windows.Input.Cursors.Arrow;
        }
    }

    [RelayCommand]
    void ThemeSelect(string header)
    {
        ThemeManager.LoadTheme(header);
        Theme = $"Theme {header}";
        Settings.Default.Theme = header;
    }

    [RelayCommand]
    static void Close()
    {
        Settings.Default.Save();
        Application.Current.Shutdown();
    }

    async Task ExtractImagesAsync(string file)
    {
        string fileFolder = GetFileFolder(file);
        List<string> extractedImages = [];
        
        Directory.CreateDirectory(GetFileFolder(file));
        
        await Task.Run(() =>
        {
            FileMessage = $@"Extracting images from {Path.GetFileName(file)}";
            List<Image> images = PdfSplitter.GetImages(file, PdfSplitter.Scale.High);
            FileMessage = $@"Writing images to disk";
            PdfSplitter.WriteImages(file, fileFolder, PdfSplitter.Scale.High, PdfSplitter.CompressionLevel.None);
        });
    }

    void CreateArchive(string file)
    {
        string fileFolder = GetFileFolder(file);
        string cbzFile = $"{fileFolder}.cbz";
        FileMessage = $"Creating archive {cbzFile}";
        if (File.Exists(cbzFile))
            File.Delete(cbzFile);
        ZipFile.CreateFromDirectory(fileFolder, cbzFile);
        Directory.Delete(fileFolder, true);
    }

    void Reset()
    {
        IsFree = true;
        FileMessage = string.Empty;
        FilesProgress = 0;
        files = [];
    }

    static string GetFileFolder(string file)
    {
        return Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file));
    }
}
