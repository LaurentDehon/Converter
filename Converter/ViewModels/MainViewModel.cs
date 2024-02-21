using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Converter.Themes;
using GroupDocs.Parser;
using GroupDocs.Parser.Data;
using GroupDocs.Parser.Options;
using Microsoft.Win32;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace Converter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    List<string> files = [];
    [ObservableProperty] string? _theme;
    [ObservableProperty] string? __fileMessage;
    [ObservableProperty] string? __imageMessage;
    [ObservableProperty] double _filesProgress;
    [ObservableProperty] double _fileProgress;
    [ObservableProperty] bool _conversion = false;
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
        if (files.Count != 0)
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            Conversion = true;
            int filesCount = files.Count;
            int processedFiles = 0;
            foreach (string file in files)
            {
                processedFiles++;
                FilesProgress = (double)processedFiles / files.Count * 100;
                FileMessage = $"Working on {file}";
                await ExtractImagesAsync(file);
                CreateArchive(file);
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
        FileProgress = 0;
        List<string> extractedImages = [];
        
        Directory.CreateDirectory(GetFileFolder(file));
        
        await Task.Run(() =>
        {
            int counter = 1;
            
            using Parser parser = new(file);
            IDocumentInfo info = parser.GetDocumentInfo();
            ImageMessage = $@"Fetching images from {file}";
            FileProgress = (double)counter / info.Pages.Count * 100;
            IEnumerable<PageImageArea> images = parser.GetImages();

            foreach (PageImageArea image in images)
            {                
                FileProgress = (double)counter / images.Count() * 100;
                extractedImages.Add($@"{fileFolder}\{counter}.jpeg");
                ImageMessage = $@"Extracting image {fileFolder}\{counter}.jpeg";
                Image.FromStream(image.GetImageStream()).Save($@"{fileFolder}\{counter}.jpeg", System.Drawing.Imaging.ImageFormat.Jpeg);
                counter++;
            }
            ImageMessage = string.Empty;
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
        Conversion = false;
        FileMessage = string.Empty;
        ImageMessage = string.Empty;
        FileProgress = 0;
        FilesProgress = 0;
        files = [];
    }

    static string GetFileFolder(string file)
    {
        return Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file));
    }
}
