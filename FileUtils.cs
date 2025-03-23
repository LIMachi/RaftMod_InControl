using System.IO;
using HMLLibrary;

namespace InControl
{
    public class FileUtils
    {
        public static string Root = Path.Combine(HLib.path_modsFolder, "ModData", "InControl");

        static FileUtils()
        {
            Directory.CreateDirectory(Root);
        }
        
        public static string ModDataPath(string fileName)
        {
            return Path.Combine(Root, fileName);
        }

        public static bool FileAvailable(string fileName)
        {
            return File.Exists(ModDataPath(fileName));
        }

        public static void WriteEntireFile(string fileName, string content)
        {
            var f = new StreamWriter(new FileStream(ModDataPath(fileName), FileMode.Create, FileAccess.Write, FileShare.None));
            f.Write(content);
            f.Flush();
            f.Close();
        }

        public static string ReadEntireFile(string fileName)
        {
            return File.ReadAllText(ModDataPath(fileName));
        }
        
        public static string ReadOrCreateDefault(string fileName, string def)
        {
            CreateIfMissing(fileName, def);
            return ReadEntireFile(fileName);
        }

        public static void CreateIfMissing(string fileName, string content)
        {
            if (!FileAvailable(fileName))
                WriteEntireFile(fileName, content);
        }
    }
}