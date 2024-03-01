using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Converter.Helpers;
using Converter.Themes;
using iTextSharp.text.pdf;
using Microsoft.Win32;
using System.IO;
using System.IO.Compression;
using System.Windows;

namespace Converter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    List<string> folders = [];
    List<string> files = [];
    string OutputFormat, InputFormat;
    bool FileSelection = false;

    [ObservableProperty] string? _theme;
    [ObservableProperty] string? __fileMessage;
    [ObservableProperty] double _filesProgress;
    [ObservableProperty] bool _openButtonsEnabled = true;
    [ObservableProperty] bool _convertButtonEnabled = false;
    [ObservableProperty] bool _cancelButtonEnabled = false;
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
            ConvertButtonEnabled = true;
            CancelButtonEnabled = true;
            CheckboxesEnabled = false;
            FileSelection = true;
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
                    files.AddRange(Helper.GetFilesFrom(folder, Constants.ImagesExtensions, false));
            }
                
            folders.AddRange(openFolderDialog.FolderNames);
            FileMessage = $"{files.Count} file(s) loaded from {openFolderDialog.FolderNames.Length} folder(s)";
            ConvertButtonEnabled = true;
            CancelButtonEnabled = true;
            CheckboxesEnabled = false;
        }
    }

    [RelayCommand]
    async Task Convert()
    {
        if (files.Count > 0)
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            OpenButtonsEnabled = false;
            ConvertButtonEnabled = false;
            CancelButtonEnabled = false;
            ShowFilesProgress = true;

            int filesCount = files.Count;
            int processedFiles = 0;
            foreach (string file in files) 
            {
                if (InputFormat == "pdf" && OutputFormat.Contains("cb"))
                {
                    await ExtractImagesAsync(file);
                    string outputFolder = Directory.GetParent(GetFileFolder(file))!.FullName;
                    string[] images = Helper.GetFilesFrom(GetFileFolder(file), Constants.ImagesExtensions, false);
                    string outputFile = $"{Path.GetFileNameWithoutExtension(file)}.{OutputFormat}";
                    await CreateArchiveAsync(images, outputFolder, outputFile);
                    Directory.Delete(GetFileFolder(file), true);
                }
                else if (InputFormat == "pdf" && OutputFormat == "images")
                {
                    await ExtractImagesAsync(file);
                }
                else if (InputFormat.Contains("cb") && OutputFormat.Contains("cb"))
                {
                    File.Move(file, Path.ChangeExtension(file, OutputFormat));
                }
                else if (InputFormat.Contains("cb") && OutputFormat == "images") 
                {
                    string outputFolder = GetFileFolder(file);
                    await Task.Run(() => ZipFile.ExtractToDirectory(file, outputFolder, true));                    
                }
                else if (InputFormat == "images" && OutputFormat.Contains("cb"))
                {
                    int foldersCount = folders.Count;
                    int processedFolders = 0;
                    foreach (string folder in folders)
                    {                        
                        string outputFile = $"{new DirectoryInfo(folder).Name}.{OutputFormat}";
                        if (FileSelection)
                            await CreateArchiveAsync([.. files], folder, outputFile);
                        else
                            await CreateArchiveAsync(Helper.GetFilesFrom(folder, Constants.ImagesExtensions, false), Directory.GetParent(GetFileFolder(folder))!.FullName, outputFile);
                        processedFolders++;
                        FilesProgress = (double)processedFolders / foldersCount * 100;
                    }
                    break;
                }
                processedFiles++;
                FilesProgress = (double)processedFiles / filesCount * 100;
            }

            string text = OutputFormat == "images" ? "extracted" : "converted";
            FileMessage = $"{files.Count} file(s) successfully {text}";
            Cursor = System.Windows.Input.Cursors.Arrow;
            OpenButtonsEnabled = true;
            ConvertButtonEnabled = false;
            CancelButtonEnabled = false;
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
            }
        });
    }

    async Task CreateArchiveAsync(string[] files, string outputFolder, string outputFile)
    {
        await Task.Run(() =>
        {
            string fullPath = Path.Combine(outputFolder, outputFile);
            FileMessage = $"Creating archive {outputFile}";

            if (File.Exists(fullPath))
                File.Delete(fullPath);

            using ZipArchive archive = ZipFile.Open(fullPath, ZipArchiveMode.Create);
            foreach (string file in files)
                archive.CreateEntryFromFile(file, Path.GetFileName(file));
        });
    }

    void Reset()
    {
        OpenButtonsEnabled = true;
        ConvertButtonEnabled = false;
        CancelButtonEnabled = false;
        CheckboxesEnabled = true;
        ShowFilesProgress = false;
        FileMessage = string.Empty;
        FileSelection = false;
        FilesProgress = 0;
        folders.Clear();
        files.Clear();
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
