using CvModManager.Lib;
using System.Diagnostics;
using System.Text.RegularExpressions;
using static System.Console;

namespace CvModManager.Console
{
    internal partial class Program
    {
        static void Main(string[] args)
        {
            while (MainMenu())
            {
                Clear();
            }
        }

        private static void ShowTitle()
        {
            Title = "CV Mod Manager";
            WriteLine("CV Mod Manager");
            WriteLine("==============");
        }

        private static bool MainMenu()
        {
            Clear();
            ShowTitle();
            WriteLine("[L]ist mods");
            WriteLine("[T]oggle mod enabled/disabled");
            WriteLine("[I]nstall new mod");
            WriteLine("[U]ninstall existing mod");
            WriteLine("[P]ack mod for distribution");
            WriteLine("[O]pen mods folder");
            WriteLine("[Q]uit");
            var key = ReadKey(true).Key;
            switch (key)
            {
                case ConsoleKey.L:
                    ListMods();
                    Pause();
                    break;
                case ConsoleKey.T:
                    ToggleMod();
                    break;
                case ConsoleKey.I:
                    InstallMod();
                    break;
                case ConsoleKey.U:
                    UninstallMod();
                    break;
                case ConsoleKey.P:
                    PackMod();
                    break;
                case ConsoleKey.O:
                    OpenModsFolder();
                    break;
                case ConsoleKey.Q:
                    return false;
            }
            return true;
        }

        private static void OpenModsFolder()
        {
            if (!ModHelper.HasModPath())
            {
                ForegroundColor = ConsoleColor.Yellow;
                WriteLine("Mod path does not exist. Creating it now");
                Directory.CreateDirectory(ModHelper.GetModPath());
                ResetColor();
            }
            try
            {
                //This is likely not working on Linux
                Process.Start(new ProcessStartInfo(ModHelper.GetModPath())
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ForegroundColor = ConsoleColor.Red;
                WriteLine("Unable to open mod folder");
                WriteLine("[{0}] {1}", ex.GetType().Name, ex.Message);
                WriteLine("You can browse manually to: {0}", ModHelper.GetModPath());
                ResetColor();
                Pause();
            }
        }

        private static void PackMod()
        {
            var mod = SelectMod(null);
            if (mod == null)
            {
                return;
            }

            var fn = Path.Combine(AppContext.BaseDirectory, mod.ModFolderName + ".zip");
            using (var target = File.Create(fn))
            {
                ForegroundColor = ConsoleColor.Yellow;
                ModHelper.Pack(mod, target, Out);
                Out.Flush();
            }
            ForegroundColor = ConsoleColor.Green;
            WriteLine("Mod packed into {0}", fn);
            ResetColor();
            Pause();
        }

        private static void UninstallMod()
        {
            var mod = SelectMod(null);
            if (mod == null)
            {
                return;
            }
            ForegroundColor = ConsoleColor.Red;
            WriteLine("You're about to uninstall {0}", mod.Title);
            WriteLine("This action cannot be undone.");
            WriteLine("Pack or disable the mod instead if you need it later.");
            WriteLine("Press [Y] to uninstall or any other key to abort");
            FlushBuffer();
            if (ReadKey(true).Key == ConsoleKey.Y)
            {
                ForegroundColor = ConsoleColor.Yellow;
                WriteLine("Uninstalling mod...");
                ModHelper.Uninstall(mod);
                ForegroundColor = ConsoleColor.Green;
                WriteLine("Done");
            }
            else
            {
                WriteLine("Operation aborted");
            }
            ResetColor();
            Pause();
        }

        private static void InstallMod()
        {
            string p = "";
            bool reading = true;
            WriteLine("Drag a mod onto the console to install it");
            WriteLine("Supported formats:");
            WriteLine("* Zip files created with the \"pack\" option");
            WriteLine("* SteamCMD downloaded mod folder (folder that contains info.json)");
            FlushBuffer();
            //Wait for first data
            while (!KeyAvailable)
            {
                Thread.Sleep(100);
            }
            Stopwatch sw = Stopwatch.StartNew();
            while (reading)
            {
                if (KeyAvailable)
                {
                    p += ReadKey(true).KeyChar;
                    sw.Restart();
                }
                else if (sw.ElapsedMilliseconds > 500)
                {
                    reading = false;
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
            sw.Stop();
            if (string.IsNullOrWhiteSpace(p))
            {
                ForegroundColor = ConsoleColor.Red;
                WriteLine("Failed to parse file name from dropped file");
            }
            else if (File.Exists(p))
            {
                try
                {
                    using var source = File.OpenRead(p);
                    ModHelper.Install(source);
                    ForegroundColor = ConsoleColor.Green;
                    WriteLine("Mod installed and enabled successfully");
                }
                catch (Exception ex)
                {
                    ForegroundColor = ConsoleColor.Red;
                    WriteLine("Failed to install mod.");
                    WriteLine("[{0}] {1}", ex.GetType().Name, ex.Message);
                }
            }
            else if (Directory.Exists(p))
            {
                try
                {
                    ModHelper.InstallUnpacked(p, Out);
                    Out.Flush();
                    ForegroundColor = ConsoleColor.Green;
                    WriteLine("Mod installed and enabled successfully");
                }
                catch (Exception ex)
                {
                    ForegroundColor = ConsoleColor.Red;
                    WriteLine("Failed to install mod.");
                    WriteLine("[{0}] {1}", ex.GetType().Name, ex.Message);
                }
            }
            else
            {
                ForegroundColor = ConsoleColor.Red;
                WriteLine("'{0}' does not exist", p);
            }
            ResetColor();
            Pause();
        }

        private static void ToggleMod()
        {
            var mod = SelectMod(null);
            if (mod == null)
            {
                return;
            }
            if (mod.Enabled)
            {
                ModHelper.Disable(mod);
            }
            else
            {
                ModHelper.Enable(mod);
            }
            WriteLine("Mod state changed");
            Pause();
        }

        private static void ListMods()
        {
            foreach (var mod in ModHelper.GetInstalledMods())
            {
                ForegroundColor = mod.Enabled ? ConsoleColor.Green : ConsoleColor.Red;
                WriteLine("Title:   {0}", mod.Title);
                WriteLine("Detail:  {0}", SpaceReplacer().Replace(mod.Description.Trim(), " "));
                WriteLine("Folder:  {0}", mod.ModFolderName);
                WriteLine("Enabled: {0}", mod.Enabled ? "Yes" : "No");
                WriteLine();
            }
            ResetColor();
        }

        private static void Pause()
        {
            FlushBuffer();
            WriteLine("Press any key to continue");
            ReadKey(true);
        }

        private static void FlushBuffer()
        {
            while (KeyAvailable)
            {
                ReadKey(true);
            }
        }

        private static ModInfo? SelectMod(bool? state)
        {
            int index = 0;
            List<ModInfo> menu = [];
            foreach (var mod in ModHelper.GetInstalledMods())
            {
                if (state == null || state.Value == mod.Enabled)
                {
                    ForegroundColor = mod.Enabled ? ConsoleColor.Green : ConsoleColor.Red;
                    WriteLine("[{0}] {1}", ++index, mod.Title);
                    menu.Add(mod);
                }
            }
            ForegroundColor = ConsoleColor.Yellow;
            Write("Select mod: ");
            var input = ReadLine() ?? string.Empty;
            ResetColor();
            if (!int.TryParse(input, out index) || index < 1 || index > menu.Count)
            {
                return null;
            }

            return menu[index - 1];
        }

        [GeneratedRegex(@"\s+")]
        private static partial Regex SpaceReplacer();
    }
}
