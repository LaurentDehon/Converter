using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Converter.Themes;
using iTextSharp.text.pdf;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace Converter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    List<string> folders = [];
    List<string> files = [];
    string Format;
    [ObservableProperty] string? _theme;
    [ObservableProperty] string? __fileMessage;
    [ObservableProperty] string? __imageMessage;
    [ObservableProperty] double _filesProgress;
    [ObservableProperty] double _fileProgress;
    [ObservableProperty] bool _isFree = true;
    [ObservableProperty] bool _isReady = false;
    [ObservableProperty] bool _conversion = false;
    [ObservableProperty] bool _folderOpen;
    [ObservableProperty] System.Windows.Input.Cursor _cursor = System.Windows.Input.Cursors.Arrow;

    public MainViewModel()
    {
        Theme = $"Theme {Settings.Default.Theme}";
        Format = Settings.Default.Format;
        FolderOpen = Settings.Default.FolderOpen;
    }

    [RelayCommand]
    void SetFormat(string format)
    {
        Format = format;
        Settings.Default.Format = format;
    }

    [RelayCommand]
    void OpenFile()
    {
        Reset();
        OpenFileDialog openFileDialog = new() { Filter = "Pdf files (*.pdf)|*.pdf", Multiselect = true };
        if (openFileDialog.ShowDialog() == true)
        {
            files = [.. openFileDialog.FileNames];
            folders.Add(Path.GetDirectoryName(files[0])!);
            FileMessage = $"{files.Count} file(s) loaded";
            IsReady = true;
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
            folders.AddRange(openFolderDialog.FolderNames);
            FileMessage = $"{files.Count} file(s) loaded from {openFolderDialog.FolderNames.Length} folder(s)";
            IsReady = true;
        }
    }

    [RelayCommand]
    async Task Convert()
    {
        if (files.Count > 0)
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            IsFree = false;
            Conversion = true;
            int filesCount = files.Count;
            int processedFiles = 0;
            foreach (string file in files)
            {
                await ExtractImagesAsync(file);
                if (Format != "images")
                    CreateArchive(file);                
                processedFiles++;
                FilesProgress = (double)processedFiles / files.Count * 100;
            }
            FileMessage = $"{filesCount} file(s) successfully converted";
            Cursor = System.Windows.Input.Cursors.Arrow;
            if (FolderOpen)
                foreach(string folder in folders)
                    Process.Start("explorer.exe", folder);
            IsFree = true;
            IsReady = false;
            files.Clear();
            folders.Clear();
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
            FileMessage = $"Converting {Path.GetFileName(file)}";
            using PdfReader reader = new(file);
            for (int pageNumber = 1; pageNumber <= reader.NumberOfPages; pageNumber++)
            {
                PdfDictionary page = reader.GetPageN(pageNumber);
                PdfDictionary resources = page.GetAsDict(PdfName.RESOURCES);
                ExtractImagesFromResources(resources, fileFolder, pageNumber);
                FileProgress = (double)pageNumber / reader.NumberOfPages * 100;
                ImageMessage = $"Extracting image {pageNumber}";
            }
            ImageMessage = $"{Path.GetFileName(file)} done";
        });
    }

    void CreateArchive(string file)
    {
        string fileFolder = GetFileFolder(file);
        string cbzFile = $"{fileFolder}.{Format}";
        FileMessage = $"Creating archive {cbzFile}";
        if (File.Exists(cbzFile))
            File.Delete(cbzFile);
        ZipFile.CreateFromDirectory(fileFolder, cbzFile);
        Directory.Delete(fileFolder, true);
    }

    void Reset()
    {
        IsFree = true;
        Conversion = false;
        FileMessage = string.Empty;
        ImageMessage = string.Empty;
        FilesProgress = 0;
        FileProgress = 0;
        folders = [];
        files = [];
    }

    static string GetFileFolder(string file)
    {
        return Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file));
    }

    static void ExtractImagesFromResources(PdfDictionary resources, string outputFolder, int pageNumber)
    {
        if (resources == null)
            return;

        PdfDictionary xObjects = resources.GetAsDict(PdfName.XOBJECT);
        if (xObjects == null)
            return;

        foreach (var key in xObjects.Keys)
        {
            PdfObject obj = xObjects.Get(key);
            if (obj == null || !obj.IsIndirect())
                continue;

            PdfDictionary dict = (PdfDictionary)PdfReader.GetPdfObject(obj);
            PdfName subType = dict.GetAsName(PdfName.SUBTYPE);

            if (PdfName.IMAGE.Equals(subType))
            {
                byte[] bytes = [];
                if (obj.IsStream())
                    bytes = PdfReader.GetStreamBytesRaw((PRStream)obj);
                else if (obj.IsIndirect())
                {
                    PdfObject directObject = PdfReader.GetPdfObjectRelease(obj);
                    if (directObject != null && directObject.IsStream())
                        bytes = PdfReader.GetStreamBytesRaw((PRStream)directObject);
                }

                if (bytes != null)
                    File.WriteAllBytes($@"{outputFolder}\{pageNumber}.jpeg", bytes);
                else if (PdfName.FORM.Equals(subType))
                    ExtractImagesFromResources(dict.GetAsDict(PdfName.RESOURCES), outputFolder, pageNumber);
            }
        }
    }
}
