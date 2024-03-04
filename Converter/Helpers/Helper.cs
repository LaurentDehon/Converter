using System.IO;

namespace Converter.Helpers
{
    public static class Helper
    {
        public static string Capitalize(this string word)
        {
            return word[..1].ToUpper() + word[1..].ToLower();
        }

        public static string[] GetFilesFromFolder(string searchFolder, string[] filters, bool isRecursive)
        {
            List<string> filesFound = [];
            SearchOption searchOption = isRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            foreach (var filter in filters)
                filesFound.AddRange(Directory.GetFiles(searchFolder, string.Format("*.{0}", filter), searchOption));
            return [.. filesFound];
        }

        public static string GetFileFolder(string file)
        {
            return Path.Combine(Path.GetDirectoryName(file)!, Path.GetFileNameWithoutExtension(file));
        }

        public static string GetParentFolder(string folder)
        {
            return Directory.GetParent(GetFileFolder(folder))!.FullName;
        }

        public static void DeleteFolder(string folder)
        {
            string[] files = Directory.GetFiles(folder);
            string[] subfolders = Directory.GetDirectories(folder);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string subfolder in subfolders)
                DeleteFolder(subfolder);

            Directory.Delete(folder, false);
        }
    }    
}
