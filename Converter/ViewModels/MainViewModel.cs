using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Converter.Helpers;
using Converter.Themes;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.xmp.impl.xpath;
using log4net;
using Microsoft.Win32;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Windows;
using Image = iTextSharp.text.Image;

namespace Converter.ViewModels;

public partial class MainViewModel : ObservableObject
{
    static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);
    List<string> folders = [];
    List<string> files = [];
    string OutputFormat, InputFormat;
    bool FileSelection = false;

    [ObservableProperty] string? _theme;
    [ObservableProperty] string? __fileMessage;
    [ObservableProperty] double _filesProgress;
    [ObservableProperty] bool _openButtonsEnabled = true;
    [ObservableProperty] bool _convertButtonEnabled = false;
    [ObservableProperty] bool _renumberButtonEnabled = false;
    [ObservableProperty] bool _cancelButtonEnabled = false;
    [ObservableProperty] bool _showFilesProgress = false;
    [ObservableProperty] bool _checkboxesEnabled = true;
    [ObservableProperty] bool _folderOpen;
    [ObservableProperty] string _pathRAR;
    [ObservableProperty] bool _pathRARFound;
    [ObservableProperty] System.Windows.Input.Cursor _cursor = System.Windows.Input.Cursors.Arrow;

    public MainViewModel()
    {
        Theme = $"Theme {Settings.Default.Theme}";
        InputFormat = Settings.Default.InputFormat;
        OutputFormat = Settings.Default.OutputFormat;
        FolderOpen = Settings.Default.FolderOpen;
        PathRAR = Settings.Default.PathRAR;
        PathRARFound = File.Exists(PathRAR);
        log.Info("New session initialized");
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
    void BrowseForRAR()
    {
        OpenFileDialog openFileDialog = new() { Filter = "Executable files (*.exe)|*.exe", Multiselect = false };
        if (openFileDialog.ShowDialog() == true)
        {
            PathRAR = openFileDialog.FileName;
            PathRARFound = true;
        }
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

        if (OutputFormat == "cbr" && !PathRARFound)
        {
            FileMessage = "Unable to find RAR executable";
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
            if (InputFormat == "images")
                RenumberButtonEnabled = true;
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
            Reset();
            return;
        }

        if (OutputFormat == "cbr" && !PathRARFound)
        {
            FileMessage = "Unable to find RAR executable";
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
                {
                    files.AddRange(Helper.GetFilesFromFolder(folder, Constants.ImagesExtensions, false));
                    RenumberButtonEnabled = true;
                }
            }
                
            folders.AddRange(openFolderDialog.FolderNames);
            FileMessage = $"{files.Count} file(s) loaded from {openFolderDialog.FolderNames.Length} folder(s)";
            ConvertButtonEnabled = true;
            CancelButtonEnabled = true;
            CheckboxesEnabled = false;
        }
    }

    [RelayCommand]
    async Task Renumber()
    {
        await Task.Run(() =>
        {
            foreach (string folder in folders)
            {
                int counter = 1;
                foreach (string file in files.Where(f => Path.GetDirectoryName(f) == folder))
                {
                    File.Move(file, Path.Combine(Path.GetDirectoryName(file)!, $"{counter:000}.{Path.GetExtension(file)}"));
                    counter++;
                }
            }
        });
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

            int failed = 0;
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
                else
                {
                    try 
                    { 
                        await ExtractAsync(file);
                    }
                    catch (Exception)
                    {
                        log.Error($"Extraction of {Path.GetFileName(file)} failed");
                        failed++;
                        Helper.DeleteFolder(outputFolder);
                    }

                    if (OutputFormat != "images")
                    {
                        images = Helper.GetFilesFromFolder(outputFolder, Constants.ImagesExtensions, false);
                        await CreateArchiveAsync(images, Directory.GetParent(outputFolder)!.FullName, outputFile);
                        Helper.DeleteFolder(outputFolder);
                    }
                }
                processedFiles++;
                FilesProgress = (double)processedFiles / filesCount * 100;
            }
                        
            if (FolderOpen)
                if (FileSelection)
                {
                    if (failed < filesCount)
                        foreach (string folder in folders.Distinct())
                            Process.Start("explorer.exe", folder); 
                }
                else
                    Process.Start("explorer.exe", Directory.GetParent(folders[0])!.FullName);

            string text = OutputFormat == "images" ? "extracted" : "converted";
            FileMessage = $"{files.Count - failed} file(s) successfully {text}";
            if (failed > 0)
                FileMessage += $"\n{failed} file(s) failed";

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
    static void OpenLog()
    {
        Process.Start("notepad.exe", @$"Logs\{DateTime.Now:yyyy-MM-dd}.log");
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

    async Task ExtractAsync(string file)
    {
        await Task.Run(() =>
        {
            string fileFolder = Helper. GetFileFolder(file);
            FileMessage = $"Extracting from {Path.GetFileName(file)}";
            log.Info($"Extraction of {Path.GetFileName(file)} in progress");
            Directory.CreateDirectory(fileFolder);

            if (InputFormat == "cbz")
            {
                int index = 1;
                using ZipArchive archive = ZipFile.OpenRead(file);
                foreach (ZipArchiveEntry entry in archive.Entries.Where(e => Helper.IsImage(e.Name)))
                {
                    string fileName = $"{index:000}{Path.GetExtension(entry.FullName)}";
                    entry.ExtractToFile(Path.Combine(fileFolder, fileName));
                    System.Drawing.Size imageSize = Helper.GetImageDimensions(Path.Combine(fileFolder, fileName));
                    if (imageSize.Width > imageSize.Height)
                        log.Info($"Image {Path.GetFileName(fileName)} is in landscape orientation");
                    index++;
                }
            }
            else if (InputFormat == "cbr")
            {
                int index = 1;
                RarArchive archive = RarArchive.Open(file);
                foreach (RarArchiveEntry entry in archive.Entries.Where(e => !e.IsDirectory && Helper.IsImage(e.Key)))
                {
                    string fileName = $"{index:000}{Path.GetExtension(entry.Key)}";
                    entry.WriteToFile(Path.Combine(fileFolder, fileName), new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                    System.Drawing.Size imageSize = Helper.GetImageDimensions(Path.Combine(fileFolder, fileName));
                    if (imageSize.Width > imageSize.Height)
                        log.Info($"Image {Path.GetFileName(fileName)} is in landscape orientation");
                    index++;
                }
            }
            else
            {
                using PdfReader reader = new(file);
                for (int pageNumber = 1; pageNumber <= reader.NumberOfPages; pageNumber++)
                {
                    PdfDictionary page = reader.GetPageN(pageNumber);
                    PdfDictionary resources = page.GetAsDict(PdfName.RESOURCES);
                    ExtractImagesFromResources(resources, fileFolder, pageNumber);
                }
            }
            log.Info($"Extraction of {Path.GetFileName(file)} successfull");
        });
    }

    async Task CreateArchiveAsync(string[] files, string outputFolder, string outputFile)
    {
        await Task.Run(() =>
        {
            string fullPath = Path.Combine(outputFolder, outputFile);
            FileMessage = $"Creating archive {outputFile}";
            log.Info($"Creation of {outputFile} in progress");

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
                string arguments = $"A \"{fullPath}\" {fileList} -ep1 -r";

                ProcessStartInfo processStartInfo = new()
                {
                    ErrorDialog = false,
                    UseShellExecute = true,
                    Arguments = arguments,
                    FileName = PathRAR,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process process = Process.Start(processStartInfo)!;
                process.WaitForExit();                
            }
            else if (OutputFormat == "pdf")
            {
                float h = Image.GetInstance(files[0]).ScaledHeight;
                float w = Image.GetInstance(files[0]).ScaledWidth;
                Document document = new(new iTextSharp.text.Rectangle(w, h), 0f, 0f, 0f, 0f);
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
            log.Info($"Creation of {outputFile} successfull");
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
                {
                    string imagePath = $@"{outputFolder}\{pageNumber:000}.jpg";
                    File.WriteAllBytes(imagePath, bytes);

                    using (var image = System.Drawing.Image.FromFile(imagePath))
                    if (image.Width > image.Height)
                        log.Info($"Image {Path.GetFileName(imagePath)} is in landscape orientation");

                }
                else if (PdfName.FORM.Equals(subType))
                    ExtractImagesFromResources(dict.GetAsDict(PdfName.RESOURCES), outputFolder, pageNumber);
            }
        }
    }
}
