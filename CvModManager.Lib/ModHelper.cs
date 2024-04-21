using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CvModManager.Lib
{
    public static partial class ModHelper
    {
        private static readonly JsonSerializerOptions jsonOpt = new(JsonSerializerDefaults.Web);

        private static readonly string ModPath;

        public static bool VerboseMessage { get; set; } = Tools.IsDebug;

        static ModHelper()
        {
            ModPath = Path.Combine(NativeMethods.GetLocalLowPath(), "ArchivalEugeneNaelstrof",
                "ChurnVector", "mods");
        }

        public static bool HasModPath()
        {
            return Directory.Exists(ModPath);
        }

        public static string GetModPath() => ModPath;

        public static ModInfo[] GetInstalledMods()
        {
            var ret = new List<ModInfo>();
            if (!Directory.Exists(ModPath))
            {
                return [];
            }
            foreach (var dir in Directory.GetDirectories(ModPath))
            {
                var infoFile = Path.Combine(dir, "info.json");
                bool enabled = true;
                if (!File.Exists(infoFile))
                {
                    enabled = false;
                    infoFile = Path.Combine(dir, "info.json.DISABLED");
                }
                if (!File.Exists(infoFile))
                {
                    Print("Directory in mod folder not a mod: {0}", Path.GetFileName(dir));
                    continue;
                }
                var info = JsonSerializer.Deserialize<InternalModInfo>(File.ReadAllText(infoFile), jsonOpt);
                if (info == null)
                {
                    Print("{0} in mod folder not valid. Deserialization failed", Path.GetFileName(infoFile));
                    continue;
                }
                ret.Add(new(info, dir, enabled));
            }
            return [.. ret];
        }

        public static bool Enable(ModInfo info)
        {
            ArgumentNullException.ThrowIfNull(info);

            var infoFile = Path.Combine(info.ModDir, "info.json.DISABLED");
            if (File.Exists(infoFile))
            {
                try
                {
                    File.Move(infoFile, Path.Combine(info.ModDir, "info.json"));
                    info.Enabled = true;
                    Print("{0} was enabled", info.Title);
                    return true;
                }
                catch (Exception ex)
                {
                    Print("Could not enable {0}. [{1}] {2}", info.Title, ex.GetType().Name, ex.Message);
                    return false;
                }
            }
            return false;
        }

        public static bool Disable(ModInfo info)
        {
            ArgumentNullException.ThrowIfNull(info);

            var infoFile = Path.Combine(info.ModDir, "info.json");
            if (File.Exists(infoFile))
            {
                try
                {
                    File.Move(infoFile, Path.Combine(info.ModDir, "info.json.DISABLED"));
                    info.Enabled = false;
                    Print("{0} was disabled", info.Title);
                    return true;
                }
                catch (Exception ex)
                {
                    Print("Could not enable {0}. [{1}] {2}", info.Title, ex.GetType().Name, ex.Message);
                    return false;
                }
            }
            return false;
        }

        public static void Pack(ModInfo info, Stream target, TextWriter? logOutput = null)
        {
            ArgumentNullException.ThrowIfNull(info);
            ArgumentNullException.ThrowIfNull(target);

            logOutput ??= TextWriter.Null;

            var files = Directory.GetFiles(info.ModDir, "*.*", SearchOption.AllDirectories);

            using var zf = new ZipArchive(target, ZipArchiveMode.Create, true);
            zf.Comment = $"Created by CvModManager on {DateTime.UtcNow:yyyy-MM-dd HH:ii:ss}";
            var rootDirLength = Path.GetDirectoryName(info.ModDir)!.Length + 1;
            foreach (var file in files)
            {
                var entry = file[rootDirLength..];
                //Always pack info file in enabled state
                if (IsInfoFile(info, entry))
                {
                    entry = Path.Combine(info.ModFolderName, "info.json");
                }

                logOutput.WriteLine(entry);
                Debug.Print("Packing: {0}", entry);
                zf.CreateEntryFromFile(file, entry);
            }
        }

        public static void Install(Stream source)
        {
            ArgumentNullException.ThrowIfNull(source);
            ZipArchive zf;
            try
            {
                zf = new ZipArchive(source, ZipArchiveMode.Read, true);
            }
            catch (Exception ex)
            {
                throw new Exception($"Data is not a valid zip file", ex);
            }
            using (zf)
            {
                //Zip traditionally uses backslashes but we try to also accept forward slashes
                //for zip files created on linux
                var infoFile = zf.Entries.FirstOrDefault(m => ZipInfoJsonMatcher().IsMatch(m.FullName))
                    ?? throw new InvalidDataException("Cannot find info.json in the zip file. Is this not a mod?");
                var dirName = infoFile.FullName.Split('\\')[0].Split('/')[0];

                var dest = Path.Combine(ModPath, dirName);
                if (File.Exists(dest))
                {
                    throw new IOException($"Unable to create mod directory because '{Path.GetFileName(dest)}' in the mods folder exists and is a file. This is almost certainly a mistake, and the file should be deleted");
                }
                if (Directory.Exists(dest))
                {
                    throw new IOException($"A mod with folder '{Path.GetFileName(dest)}' already exists. If overwriting is intended, delete the mod first");
                }
                Debug.Print("Extracting file to {0}...", ModPath);
                //Note: This method is protected against path traversal attacks
                zf.ExtractToDirectory(ModPath);
            }
        }

        public static void InstallUnpacked(string folderPath, TextWriter? logOutput = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(folderPath);
            logOutput ??= TextWriter.Null;
            if (!Directory.Exists(folderPath))
            {
                throw new IOException("Directory does not exist");
            }
            folderPath = Path.GetFullPath(folderPath);
            var infoFile = Path.Combine(folderPath, "info.json");
            if (!File.Exists(infoFile))
            {
                throw new IOException("Supplied folder is not a mod. It lacks info.json file");
            }
            var info = JsonSerializer.Deserialize<InternalModInfo>(File.ReadAllText(infoFile), jsonOpt)
                ?? throw new IOException("mod info.json is not valid. Deserialization failed");
            var modFolderName = SanitizeFolderName(info.Title);
            var modFolderPath = Path.Combine(ModPath, modFolderName);
            if (Directory.Exists(modFolderPath))
            {
                throw new IOException($"A mod with folder name '{modFolderName}' already exists");
            }
            foreach (var f in Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories))
            {
                var subSegment = f[(folderPath.Length + 1)..];
                var newPath = Path.Combine(modFolderPath, subSegment);
                logOutput.WriteLine("Copying {0}...", subSegment);
                Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);
                File.Copy(f, newPath);
            }
        }

        public static void Uninstall(ModInfo info)
        {
            ArgumentNullException.ThrowIfNull(info);
            Directory.Delete(info.ModDir, true);
        }

        private static bool IsInfoFile(ModInfo info, string fileName)
        {
            var basePath = Path.Combine(info.ModFolderName, "info.json");

            if (fileName.Equals(basePath, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            if (fileName.Equals(basePath + ".disabled", StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            return false;
        }

        private static void Print(string message)
        {
            if (VerboseMessage)
            {
                Debug.Print(message);
                Console.WriteLine(message);
            }
        }

        private static void Print(string format, params object[] args)
        {
            Print(string.Format(format, args));
        }

        private static string SanitizeFolderName(string folderName)
        {
            folderName = WhitespaceFinder().Replace(folderName.Trim(), "");
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                folderName = folderName.Replace(c, '_');
            }
            return folderName;
        }

        [GeneratedRegex(@"\A[^\\/]+[\\/]info\.json\z", RegexOptions.IgnoreCase)]
        private static partial Regex ZipInfoJsonMatcher();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceFinder();
    }
}
