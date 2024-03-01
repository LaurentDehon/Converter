using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Converter.Helpers;
using Converter.Themes;
using iTextSharp.text.pdf;
using Microsoft.Win32;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace Converter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    List<string> folders = [];
    List<string> files = [];
    string OutputFormat, InputFormat;

    [ObservableProperty] string? _theme;
    [ObservableProperty] string? __fileMessage;
    [ObservableProperty] string? __imageMessage;
    [ObservableProperty] double _filesProgress;
    [ObservableProperty] double _fileProgress;
    [ObservableProperty] bool _notReadyToConvert = true;
    [ObservableProperty] bool _readyToConvert = false;
    [ObservableProperty] bool _showFileProgress = false;
    [ObservableProperty] bool _showFilesProgress = false;
    [ObservableProperty] bool _checkboxesEnabled = true;
    [ObservableProperty] System.Windows.Input.Cursor _cursor = System.Windows.Input.Cursors.Arrow;

    public MainViewModel()
    {
        Theme = $"Theme {Settings.Default.Theme}";
        InputFormat = Settings.Default.InputFormat;
        OutputFormat = Settings.Default.OutputFormat;
    }

    [RelayCommand]
    void SetInputFormat(string inputFormat)
    {
        InputFormat = inputFormat;
        Settings.Default.InputFormat = inputFormat;
    }

    [RelayCommand]
    void SetOutputFormat(string outputFormat)
    {
        OutputFormat = outputFormat;
        Settings.Default.OutputFormat = outputFormat;
    }

    [RelayCommand]
    void OpenFile()
    {        
        Reset();

        if (InputFormat == OutputFormat)
        {
            FileMessage = "Input and ouput format must be different";
            return;
        }

        string filter;
        if (InputFormat != "images")
            filter = $"{InputFormat.Capitalize()} files (*.{InputFormat})|*.{InputFormat}";
        else
        {
            filter = "Image Files|";
            foreach (string extension in Constants.ImagesExtensions)
                filter +=$"*.{extension};";
        }
        
        OpenFileDialog openFileDialog = new() { Filter = filter, Multiselect = true };
        if (openFileDialog.ShowDialog() == true)
        {
            files = [.. openFileDialog.FileNames];
            folders.Add(Path.GetDirectoryName(files[0])!);
            FileMessage = $"{files.Count} file(s) loaded";
            ReadyToConvert = true;
            CheckboxesEnabled = false;
        }
    }

    [RelayCommand]
    void OpenFolder()
    {
        Reset();

        if (InputFormat == OutputFormat)
        {
            FileMessage = "Input and ouput format must be different";
            return;
        }

        OpenFolderDialog openFolderDialog = new() { Multiselect = true };
        if (openFolderDialog.ShowDialog() == true)
        {
            foreach (string folder in openFolderDialog.FolderNames)
            {
                if (InputFormat != "images")
                    files.AddRange(Directory.GetFiles(folder, $"*.{InputFormat.ToLower()}"));
                else
                    files.AddRange(Directory.GetFiles(folder, "*.*").Where(f => Constants.ImagesExtensions.Contains(Path.GetExtension(f)[1..])));
            }
                
            folders.AddRange(openFolderDialog.FolderNames);
            FileMessage = $"{files.Count} file(s) loaded from {openFolderDialog.FolderNames.Length} folder(s)";
            ReadyToConvert = true;
            CheckboxesEnabled = false;
        }
    }

    [RelayCommand]
    async Task Convert()
    {
        if (files.Count > 0)
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            ReadyToConvert = false;
            NotReadyToConvert = false;
            ShowFilesProgress = true;

            int filesCount;
            if (InputFormat == "images")
            {
                if (folders.Count > 1)
                {
                    filesCount = folders.Count;
                    int processedFiles = 0;
                    foreach (string folder in folders)
                    {
                        await CreateArchive(folder: folder);
                        processedFiles++;
                        FilesProgress = (double)processedFiles / filesCount * 100;
                    }
                }
                else
                {
                    filesCount = files.Count;
                    await CreateArchive(files: files);
                }
            }
            else
            {
                ShowFileProgress = true;
                filesCount = files.Count;
                int processedFiles = 0;
                foreach (string file in files)
                {
                    if (InputFormat == "pdf")
                        await ExtractImagesAsync(file);
                    if (InputFormat.Contains("cb") && OutputFormat.Contains("cb"))
                        File.Move(file, Path.ChangeExtension(file, OutputFormat));
                    else if (OutputFormat != "images")
                        await CreateArchive(file: file);                
                    processedFiles++;
                    FilesProgress = (double)processedFiles / filesCount * 100;
                }
            }

            FileMessage = $"{filesCount} file(s) successfully converted";
            Cursor = System.Windows.Input.Cursors.Arrow;

            NotReadyToConvert = true;
            ReadyToConvert = false;
            CheckboxesEnabled = true;
            files.Clear();
            folders.Clear();
        }
    }

    [RelayCommand]
    void Cancel()
    {
        Reset();
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

    async Task CreateArchive(string? file = null, string? folder = null, List<string>? files = null)
    {
        await Task.Run(() =>
        {
            if (file != null) 
            {
                string fileFolder = GetFileFolder(file);
                string outputFile = $"{fileFolder}.{OutputFormat}";
                FileMessage = $"Creating archive {outputFile}";

                if (File.Exists(outputFile))
                    File.Delete(outputFile);

                ZipFile.CreateFromDirectory(fileFolder, outputFile);
                Directory.Delete(fileFolder, true);
            }
            else if (folder != null)
            {
                string outputFolder = Directory.GetParent(folder)!.FullName;
                string outputFile = $"{new DirectoryInfo(folder).Name}.{OutputFormat}";
                FileMessage = $"Creating archive {outputFile}";

                if (File.Exists(Path.Combine(outputFolder, outputFile)))
                    File.Delete(Path.Combine(outputFolder, outputFile));

                ZipFile.CreateFromDirectory(folder, Path.Combine(outputFolder, outputFile));       
            }
            else if (files != null)
            {
                string outputFolder = Directory.GetParent(files[0])!.FullName;
                string outputFile = $"{new DirectoryInfo(outputFolder).Name}.{OutputFormat}";
                FileMessage = $"Creating archive {outputFile}";

                if (File.Exists(Path.Combine(outputFolder, outputFile)))
                    File.Delete(Path.Combine(outputFolder, outputFile));

                using ZipArchive archive = ZipFile.Open(Path.Combine(outputFolder, outputFile), ZipArchiveMode.Create);
                int filesCount = files.Count;
                int processedFiles = 0;
                foreach (string f in files)
                {
                    archive.CreateEntryFromFile(f, Path.GetRelativePath(outputFolder, f));
                    processedFiles++;
                    FilesProgress = (double)processedFiles / filesCount * 100;
                }
            }
        });
    }

    void Reset()
    {
        NotReadyToConvert = true;
        ReadyToConvert = false;
        CheckboxesEnabled = true;
        ShowFileProgress = false;
        ShowFilesProgress = false;
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
