using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Converter.Helpers;
using Converter.Themes;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Win32;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using System.Diagnostics;
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
    [ObservableProperty] bool _folderOpen;
    [ObservableProperty] System.Windows.Input.Cursor _cursor = System.Windows.Input.Cursors.Arrow;

    public MainViewModel()
    {
        Theme = $"Theme {Settings.Default.Theme}";
        InputFormat = Settings.Default.InputFormat;
        OutputFormat = Settings.Default.OutputFormat;
        FolderOpen = Settings.Default.FolderOpen;
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
                    files.AddRange(Helper.GetFilesFromFolder(folder, Constants.ImagesExtensions, false));
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
                string outputFolder = Helper.GetFileFolder(file);
                string[] images = [];
                string outputFile = $"{Path.GetFileNameWithoutExtension(file)}.{OutputFormat}";

                if (InputFormat == "images")
                {
                    int foldersCount = folders.Count;
                    int processedFolders = 0;

                    foreach (string folder in folders)
                    {
                        outputFile = $"{new DirectoryInfo(folder).Name}.{OutputFormat}";
                        if (FileSelection)
                            await CreateArchiveAsync([.. files], folder, outputFile);
                        else
                            await CreateArchiveAsync(Helper.GetFilesFromFolder(folder, Constants.ImagesExtensions, false), Helper.GetParentFolder(folder), outputFile);
                        processedFolders++;
                        FilesProgress = (double)processedFolders / foldersCount * 100;
                    }
                    break;
                }
                else if (InputFormat == "pdf")
                    await ExtractFromPdfAsync(file);
                else if (InputFormat == "cbz")
                    await ExtractFromCbzAsync(file);
                else if (InputFormat == "cbr")
                    await ExtractFromCbrAsync(file);
                if (OutputFormat != "images")
                {
                    images = Helper.GetFilesFromFolder(outputFolder, Constants.ImagesExtensions, false);
                    await CreateArchiveAsync(images, Directory.GetParent(outputFolder)!.FullName, outputFile);
                    Directory.Delete(outputFolder, true);
                }                
                processedFiles++;
                FilesProgress = (double)processedFiles / filesCount * 100;
            }

            if (FolderOpen)
                if (FileSelection)
                    foreach (string folder in folders.Distinct())
                        Process.Start("explorer.exe", folder); 
                else
                    Process.Start("explorer.exe", Directory.GetParent(folders[0])!.FullName);

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

    async Task ExtractFromCbzAsync(string file)
    {
        string fileFolder = Helper.GetFileFolder(file);
        await Task.Run(() =>
        {
            FileMessage = $"Extracting from {Path.GetFileName(file)}";
            ZipFile.ExtractToDirectory(file, fileFolder, true);
        });
    }

    async Task ExtractFromCbrAsync(string file)
    {
        string fileFolder = Helper.GetFileFolder(file);
        Directory.CreateDirectory(fileFolder);

        await Task.Run(() =>
        {
            FileMessage = $"Extracting from {Path.GetFileName(file)}";
            RarArchive archive = RarArchive.Open(file);
            foreach (RarArchiveEntry entry in archive.Entries.Where(entry => !entry.IsDirectory))
            {
                entry.WriteToDirectory(fileFolder, new ExtractionOptions()
                {
                    ExtractFullPath = true,
                    Overwrite = true
                });
            }
        });
    }

    async Task ExtractFromPdfAsync(string file)
    {
        string fileFolder = Helper.GetFileFolder(file);
        Directory.CreateDirectory(fileFolder);

        await Task.Run(() =>
        {
            FileMessage = $"Extracting from {Path.GetFileName(file)}";
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

            if (OutputFormat == "cbz")
            {
                using ZipArchive archive = ZipFile.Open(fullPath, ZipArchiveMode.Create);
                foreach (string file in files)
                    archive.CreateEntryFromFile(file, Path.GetFileName(file)); 
            }
            else if (OutputFormat == "cbr")
            {
                List<string> collectionFiles = files.Select(file => "\"" + file).ToList();
                string fileList = string.Join("\" ", collectionFiles);
                fileList += "\"";
                var arguments = $"A \"{fullPath}\" {fileList} -ep1 -r";

                var processStartInfo = new ProcessStartInfo
                {
                    ErrorDialog = false,
                    UseShellExecute = true,
                    Arguments = arguments,
                    FileName = @"C:\Program Files\WinRAR\WinRAR.exe",
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                var process = Process.Start(processStartInfo);

                process?.WaitForExit();
            }
            else if (OutputFormat == "pdf")
            {
                float h = Image.GetInstance(files[0]).ScaledHeight;
                float w = Image.GetInstance(files[0]).ScaledWidth;
                Document document = new(new Rectangle(w, h), 0f, 0f, 0f, 0f);
                PdfWriter.GetInstance(document, new FileStream(fullPath, FileMode.Create));
                document.Open();
                foreach (string file in files)
                {
                    Image image = Image.GetInstance(file);
                    document.Add(image);
                    document.NewPage();
                }
                document.Close();
            }
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
                    File.WriteAllBytes($@"{outputFolder}\{pageNumber:000}.jpeg", bytes);
                else if (PdfName.FORM.Equals(subType))
                    ExtractImagesFromResources(dict.GetAsDict(PdfName.RESOURCES), outputFolder, pageNumber);
            }
        }
    }
}
