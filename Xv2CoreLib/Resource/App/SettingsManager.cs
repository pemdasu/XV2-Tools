using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xv2CoreLib.Resource.UndoRedo;

namespace Xv2CoreLib.Resource.App
{
    public enum Application
    {
        NotSet,
        ModInstaller,
        EepkOrganiser,
        Ace,
        XenoKit
    }

    public class SettingsManager
    {
        //Singleton
        private static Lazy<SettingsManager> instance = new Lazy<SettingsManager>(() => new SettingsManager());
        public static SettingsManager Instance => instance.Value;
        public static Settings settings => instance.Value.Settings;

        //Settings
        public Settings Settings { get; set; }
        public Application CurrentApp { get; set; } = Application.NotSet;

        #region SharedSettings
        //These settings are shared between some applications and so must always be available

        public bool TextureImportMatchNames { get { return ReflectionHelper.GetBoolProp(Settings, "TextureReuse_NameMatch", true); } }
        public bool AutoContainerRename { get { return ReflectionHelper.GetBoolProp(Settings, "AutoContainerRename", false); } }
        public bool AssetReuseMatchName { get { return ReflectionHelper.GetBoolProp(Settings, "AssetReuse_NameMatch", true); } }

        #endregion

        //Version
        public string CurrentVersionString
        {
            get
            {
                string[] split = Assembly.GetEntryAssembly().GetName().Version.ToString().Split('.');
                if (split[2] == "0" && split[3] == "0")
                {
                    return String.Format("{0}.{1}", split[0], split[1]);
                }
                else if (split[3] == "0")
                {
                    return String.Format("{0}.{1}.{2}", split[0], split[1], split[2]);
                }
                else
                {
                    return String.Format("{0}.{1}.{2}.{3}", split[0], split[1], split[2], split[3]);
                }
            }
        }
        public Version CurrentVersion { get { return Assembly.GetEntryAssembly().GetName().Version; } }

        //FileWatcher
        private FileSystemWatcher watcher;
        private DateTime lastSaveTime = DateTime.Now;
        private bool reloadTriggered = false;

        //Events
        public static event EventHandler SettingsReloaded;
        public static event EventHandler SettingsSaved;

        private SettingsManager()
        {
            LoadSettings();

            //Init file watcher
            watcher = new FileSystemWatcher();
            watcher.Path = Path.GetDirectoryName(GetSetingsPath());
            watcher.Filter = Path.GetFileName(GetSetingsPath());
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Changed += SettingsChangedExternally;
            watcher.EnableRaisingEvents = true;
        }

        private async void SettingsChangedExternally(object sender, FileSystemEventArgs e)
        {
            if (reloadTriggered) return;

            //Reload settings becuase it was changed externally
            if (lastSaveTime < (DateTime.Now - new TimeSpan(0, 0, 5)) && !reloadTriggered)
            {
                reloadTriggered = true;

                //Delay for 30 seconds if settings file is locked
                await Task.Run(() =>
                {
                    int locked = 0;

                    while (Utils.IsFileWriteLocked(GetSetingsPath()))
                    {
                        if (locked >= 30000)
                        {
                            break;
                        }

                        Task.Delay(100);
                        locked += 100;
                    }
                });

                //If settings is still locked, do not attempt to load it
                if (Utils.IsFileWriteLocked(GetSetingsPath()))
                {
                    reloadTriggered = false;
                    return;
                }

                LoadSettings(true);

                reloadTriggered = false;
            }
        }

        private void LoadSettings(bool isReload = false)
        {
            //if (CurrentApp == Application.NotSet) throw new InvalidOperationException("SettingsManager.LoadSettings: CurrentApp has not been set.");

#if DEBUG
            //In release build, rely on the try/catch instead
            if (!File.Exists(GetSetingsPath()))
            {
                Settings = new Settings();

                Settings.InitSettings();

                SaveSettings();
                UpdateEepkToolInterlop();
                return;
            }
#endif


            try
            {

                //Try to load the settings
                var oldSettings = Settings;
                var settings = Settings.Load(GetSetingsPath());
                settings.InitSettings();
                settings.ValidateSettings();

                Settings = settings;
                SettingsReloaded?.Invoke(oldSettings, EventArgs.Empty);

            }
            catch
            {
                if (!isReload)
                {
                    //If it fails, create a new instance and save it to disk.
                    Settings = new Settings();

                    Settings.InitSettings();

                    SaveSettings();
                }
            }

            UpdateEepkToolInterlop();
        }

        public void SaveSettings(bool errorIfFail = true)
        {
            //if (CurrentApp == Application.NotSet) throw new InvalidOperationException("SettingsManager.LoadSettings: CurrentApp has not been set.");
            if (Settings == null) throw new Exception("SettingsManager.SaveSettings: No settings are loaded.");

            SettingsSaved?.Invoke(this, EventArgs.Empty);

#if !DEBUG
            try
            {
#endif
                Settings.ValidateSettings();
                Directory.CreateDirectory(Path.GetDirectoryName(GetSetingsPath()));

                Settings.Save(GetSetingsPath());
                lastSaveTime = DateTime.Now;
#if !DEBUG
            }
            catch (UnauthorizedAccessException)
            {
                if (errorIfFail)
                    MessageBox.Show("Failed to save settings.\n\nThe application does not have write access in the current directory. Try moving it somewhere else or running it as administrator.", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch
            {
                if (errorIfFail)
                    MessageBox.Show("Failed to save settings.", "Settings", MessageBoxButton.OK, MessageBoxImage.Error);
            }
#endif

            UpdateEepkToolInterlop();
        }

        public void UpdateEepkToolInterlop()
        {
            EffectContainer.EepkLoadSettings.TextureImportMatchNames = TextureImportMatchNames;
            EffectContainer.EepkLoadSettings.AutoRenameContainers = AutoContainerRename;
            EffectContainer.EepkLoadSettings.AssetReuseMatchName = AssetReuseMatchName;
        }

        /// <summary>
        /// Get current theme as a string formatted for MahApps.Metro 2.0+.
        /// </summary>
        public string GetTheme()
        {
            string baseTheme = settings.UseDarkTheme ? "Dark" : "Light";
            AppAccent accent = settings.UseDarkTheme ? settings.CurrentDarkAccent : settings.CurrentLightAccent;
            return $"{baseTheme}.{accent.ToString()}";
        }

        #region PathHelpers
        public static string GetAbsolutePathRelativeToExe(string relativePath)
        {
            return String.Format("{0}/{1}", Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), relativePath);
        }

        public string GetSetingsPath()
        {
            return $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/LBTools/settings.txt";

            /*
            switch (CurrentApp)
            {
                case Application.EepkOrganiser:
                case Application.Ace:
                case Application.XenoKit:
                case Application.ModInstaller:
                    return $"{GetAppFolder()}/settings.xml";
                default:
                    return "settings.xml";
            }
            */
        }

        public string GetErrorLogPath()
        {
            switch (CurrentApp)
            {
                case Application.EepkOrganiser:
                case Application.Ace:
                case Application.XenoKit:
                    return $"{GetAppFolder()}/error_log.txt";
                default:
                    return GetAbsolutePathRelativeToExe("error_log.txt");
            }
        }

        public string GetAppFolder()
        {
            return GetAppFolder(CurrentApp);
        }

        public string GetAppFolder(Application app)
        {
            switch (app)
            {
                case Application.EepkOrganiser:
                    return GetAbsolutePathRelativeToExe("eepk_tool");
                case Application.Ace:
                    return GetAbsolutePathRelativeToExe("ace");
                case Application.XenoKit:
                    return GetAbsolutePathRelativeToExe("XenoKit");
                case Application.ModInstaller:
                    return $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/LB Mod Installer 3";
                default:
                    return "";
            }
        }

        public string GetAbsPathInAppFolder(string relativePath)
        {
            return $"{GetAppFolder()}/{relativePath}";
        }

        public static bool IsGameDirValid(string path)
        {
            return (File.Exists(String.Format("{0}/bin/DBXV2.exe", path)));
        }
        #endregion

    }

}
