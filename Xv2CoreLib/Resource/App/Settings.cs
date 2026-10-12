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
    public class Settings : INotifyPropertyChanged
    {
        #region NotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        //Binary
        private SettingsFormat settingsBinary = null;
        private bool _isSaving = false;

        //Only basic value types are allowed + AppAccent. This is because SettingsFormat only supports these types, and adding more is not allowed because it will break backwards-compatibility.
        //All props with a Get/Setter will be serialized to a txt file, everything else is ignored

        //Helper props
        public bool ValidGameDir
        {
            get
            {
                return SettingsManager.IsGameDirValid(GameDirectory);
            }
        }


        #region Values
        //Common
        private string _gameDir = string.Empty;
        private string _savFile = string.Empty;
        private int _undoLimit = UndoManager.DefaultMaxCapacity;
        private AppTheme _currentTheme = AppTheme.Light;
        private AppAccent _currentLightAccent = AppAccent.Blue;
        private AppAccent _currentDarkAccent = AppAccent.Emerald;

        //EepkOrganiser
        private bool _assetReuseNameMatch = false;
        private EepkTextureReuse _textureReuse = EepkTextureReuse.Identical;
        private EepkFileCleanUp _fileCleanUp = EepkFileCleanUp.Prompt;
        private bool _autoContainerRename = true;
        private int _fileCacheLimit = 10;
        #endregion

        #region Common
        public int SettingsVersion { get; set; } = 3;

        public bool UpdateNotifications { get; set; } = true;
        public string GameDirectory
        {
            get => _gameDir;
            set
            {
                if (value != _gameDir)
                {
                    _gameDir = value;
                    NotifyPropertyChanged(nameof(GameDirectory));
                }
            }
        }
        public string SaveFile
        {
            get => _savFile;
            set
            {
                if (value != _savFile)
                {
                    _savFile = value;
                    NotifyPropertyChanged(nameof(SaveFile));
                }
            }
        }

        public bool UseLightTheme
        {
            get => _currentTheme == AppTheme.Light;
            set
            {
                if (value)
                    _currentTheme = AppTheme.Light;

                NotifyPropertyChanged(nameof(UseLightTheme));
            }
        }
        public bool UseDarkTheme
        {
            get => _currentTheme == AppTheme.Dark;
            set
            {
                if (value)
                    _currentTheme = AppTheme.Dark;

                NotifyPropertyChanged(nameof(UseDarkTheme));
            }
        }
        public int UndoLimit
        {
            get => _undoLimit;
            set
            {
                if (value != _undoLimit)
                {
                    _undoLimit = value;
                    NotifyPropertyChanged(nameof(UndoLimit));
                }
            }
        }

        public AppAccent CurrentLightAccent { get => _currentLightAccent; set { _currentLightAccent = value; NotifyPropertyChanged(nameof(CurrentLightAccent)); } }
        public AppAccent CurrentDarkAccent { get => _currentDarkAccent; set { _currentDarkAccent = value; NotifyPropertyChanged(nameof(CurrentDarkAccent)); } }
        public bool DefaultThemeSet { get; set; }
        #endregion

        #region EepkOrganiser
        public bool AssetReuse_NameMatch
        {
            get => _assetReuseNameMatch;
            set
            {
                if (value != _assetReuseNameMatch)
                {
                    _assetReuseNameMatch = value;
                    NotifyPropertyChanged(nameof(AssetReuse_NameMatch));
                }
            }
        }
        public bool TextureReuse_Identical
        {
            get => _textureReuse == EepkTextureReuse.Identical;
            set
            {
                if (value && !TextureReuse_Identical)
                {
                    _textureReuse = EepkTextureReuse.Identical;
                    NotifyPropertyChanged(nameof(TextureReuse_NameMatch));
                    NotifyPropertyChanged(nameof(TextureReuse_Identical));
                }
            }
        }
        public bool TextureReuse_NameMatch
        {
            get => _textureReuse == EepkTextureReuse.NameMatch;
            set
            {
                if (value && !TextureReuse_NameMatch)
                {
                    _textureReuse = EepkTextureReuse.NameMatch;
                    NotifyPropertyChanged(nameof(TextureReuse_NameMatch));
                    NotifyPropertyChanged(nameof(TextureReuse_Identical));
                }
            }
        }
        public bool FileCleanUp_Delete
        {
            get => _fileCleanUp == EepkFileCleanUp.Delete;
            set
            {
                if (value && !FileCleanUp_Delete)
                {
                    _fileCleanUp = EepkFileCleanUp.Delete;
                    NotifyPropertyChanged(nameof(FileCleanUp_Ignore));
                    NotifyPropertyChanged(nameof(FileCleanUp_Prompt));
                    NotifyPropertyChanged(nameof(FileCleanUp_Delete));
                }
            }
        }
        public bool FileCleanUp_Prompt
        {
            get => _fileCleanUp == EepkFileCleanUp.Prompt;
            set
            {
                if (value && !FileCleanUp_Prompt)
                {
                    _fileCleanUp = EepkFileCleanUp.Prompt;
                    NotifyPropertyChanged(nameof(FileCleanUp_Ignore));
                    NotifyPropertyChanged(nameof(FileCleanUp_Prompt));
                    NotifyPropertyChanged(nameof(FileCleanUp_Delete));
                }
            }
        }
        public bool FileCleanUp_Ignore
        {
            get => _fileCleanUp == EepkFileCleanUp.Ignore;
            set
            {
                if (value && !FileCleanUp_Ignore)
                {
                    _fileCleanUp = EepkFileCleanUp.Ignore;
                    NotifyPropertyChanged(nameof(FileCleanUp_Ignore));
                    NotifyPropertyChanged(nameof(FileCleanUp_Prompt));
                    NotifyPropertyChanged(nameof(FileCleanUp_Delete));
                }
            }
        }
        public bool AutoContainerRename
        {
            get => _autoContainerRename;
            set
            {
                if (value != _autoContainerRename)
                {
                    _autoContainerRename = value;
                    NotifyPropertyChanged(nameof(AutoContainerRename));
                }
            }
        }
        public int FileCacheLimit
        {
            get => _fileCacheLimit;
            set
            {
                if (value != _fileCacheLimit)
                {
                    _fileCacheLimit = value;
                    NotifyPropertyChanged(nameof(FileCacheLimit));
                }
            }
        }

        //EffectPart Tabs
        public bool EepkOrganiser_EffectPart_General_Expanded { get; set; } = true;
        public bool EepkOrganiser_EffectPart_Position_Expanded { get; set; } = true;
        public bool EepkOrganiser_EffectPart_Animation_Expanded { get; set; } = false;
        public bool EepkOrganiser_EffectPart_Flags_Expanded { get; set; } = true;
        public bool EepkOrganiser_EffectPart_UnkFlags_Expanded { get; set; }
        public bool EepkOrganiser_EffectPart_UnkValues_Expanded { get; set; }

        #endregion

        #region XenoKit
        public bool XenoKit_RetainActionPosition { get; set; }
        public bool XenoKit_ScdPhysicsEnabled { get; set; } = true;
        public bool XenoKit_ScdResetOnLoop { get; set; } = true;
        public float XenoKit_ScdGravityScale { get; set; } = 1f;
        public bool XenoKit_ScdShowColliders { get; set; }
        public bool XenoKit_SimulateBacMovement { get; set; } = true;
        public bool XenoKit_SimulateBacTracking { get; set; } = true;
        public bool XenoKit_SimulateBsaMovement { get; set; } = true;
        public float XenoKit_Scratches { get; set; }
        public float XenoKit_Blood { get; set; }
        public bool XenoKit_WindowMaximized { get; set; } = false;
        public int XenoKit_WindowSizeX { get; set; } = -1;
        public int XenoKit_WindowSizeY { get; set; } = -1;
        public int XenoKit_DelayedUpdateFrameInterval { get; set; } = 30;
        public bool XenoKit_HideEmptyBacEntries { get; set; } = true;
        public bool XenoKit_EnableCameraAnimations { get; set; } = true;
        public bool XenoKit_EnableVisualSkeleton { get; set; } = true;
        public bool XenoKit_AutoPlay { get; set; } = true;
        public bool XenoKit_Loop { get; set; } = false;
        public bool XenoKit_AudioSimulation { get; set; } = true;
        public bool XenoKit_HitboxSimulation { get; set; } = true;
        public bool XenoKit_ProjectileSimulation { get; set; } = true;
        public bool XenoKit_BpeSimulation { get; set; } = true;
        public bool XenoKit_VfxSimulation { get; set; } = true;
        public bool XenoKit_StageBgVfxSimulation { get; set; } = true;
        public bool XenoKit_CharacterAuraSimulation { get; set; } = true;
        public bool XenoKit_PreserveCameraState { get; set; } = true;
        public bool XenoKit_RenderBoneNames { get; set; } = true;
        public bool XenoKit_RenderBoneNamesMouseOverOnly { get; set; } = true;
        public bool XenoKit_HideLessImportantBones { get; set; } = true;
        public bool XenoKit_AutoResolvePasteReferences { get; set; } = false;
        public bool XenoKit_SuppressErrorsToLogOnly { get; set; } = false;
        public bool XenoKit_DelayLoadingCMN { get; set; } = false;

        //Render Settings:
        public bool XenoKit_RimLightingEnabled { get; set; } = true;
        public int XenoKit_SuperSamplingFactor { get; set; } = 2;
        public int XenoKit_ShadowMapRes { get; set; } = 2048;
        public bool XenoKit_FullLowRez { get; set; } = false;
        public bool XenoKit_UseOutlinePostEffect { get; set; } = true;
        public bool XenoKit_HalfRenderRate { get; set; }

        public bool XenoKit_AutoReloadShaders { get; set; } = false;
        public bool XenoKit_NextGenShaders { get; set; } = false;
        public int XenoKit_GlareLevel { get; set; } = 1; //DISABLE = -1, OFF = 0, LOW = 1, HIGH = 2
        public bool XenoKit_GlareLevelHighRes { get; set; } = false;


        #endregion

        public static Settings Load(string path)
        {
            Settings settings = new Settings();
            SettingsFormat format = SettingsFormat.Load(path);

            format.DeserializeProps(settings);
            settings.settingsBinary = format;

            return settings;
        }

        public void Save(string path)
        {
            if (settingsBinary == null)
                settingsBinary = new SettingsFormat();

            if (_isSaving) return;
            _isSaving = true;

            settingsBinary.SerializeProps(this);

            string settingsFile = settingsBinary.Write();

            try
            {
                File.WriteAllText(path, settingsFile);
            }
            catch 
            {
                //Wait a short while and then retry
                Thread.Sleep(200);
                File.WriteAllText(path, settingsFile);
            }
            finally
            {
                _isSaving = false;
            }
        }

        public virtual void InitSettings()
        {
            //Get game dir
            if (string.IsNullOrWhiteSpace(GameDirectory) || !ValidGameDir)
            {
                GameDirectory = FindGameDirectory();
            }

            //Setup Undo
            if (UndoLimit < 0) UndoLimit = 0;
            if (UndoLimit > UndoManager.MaximumMaxCapacity) UndoLimit = UndoManager.MaximumMaxCapacity;

            UndoManager.Instance.SetCapacity(UndoLimit);

            //Allowed SuperSamplingFactors: 1, 2, 4 and 8
            if (XenoKit_SuperSamplingFactor != 1 && XenoKit_SuperSamplingFactor != 2 &&
                XenoKit_SuperSamplingFactor != 4 && XenoKit_SuperSamplingFactor != 8)
            {
                XenoKit_SuperSamplingFactor = 2;
            }

            //Theme
            InitTheme();
        }

        public virtual void ValidateSettings()
        {
            //EEPK Settings
            if (_textureReuse != EepkTextureReuse.Identical && _textureReuse != EepkTextureReuse.NameMatch)
                _textureReuse = EepkTextureReuse.Identical;

            if (_fileCleanUp != EepkFileCleanUp.Delete && _fileCleanUp != EepkFileCleanUp.Ignore && _fileCleanUp != EepkFileCleanUp.Prompt)
                _fileCleanUp = EepkFileCleanUp.Prompt;

            //XenoKit
            XenoKit_DelayedUpdateFrameInterval = MathHelpers.Clamp(1, 60, XenoKit_DelayedUpdateFrameInterval);
        }

        private string FindGameDirectory()
        {
            List<string> alphabet = new List<string>() { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "O", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };

            foreach (var letter in alphabet)
            {
                string _path1 = String.Format(@"{0}:{1}Program Files (x86){1}Steam{1}steamapps{1}common{1}DB Xenoverse 2{1}bin{1}DBXV2.exe", letter, System.IO.Path.DirectorySeparatorChar);
                string _path2 = String.Format(@"{0}:{1}Games{1}Steam{1}steamapps{1}common{1}DB Xenoverse 2{1}bin{1}DBXV2.exe", letter, System.IO.Path.DirectorySeparatorChar);
                string _path3 = String.Format(@"{0}:{1}Steam{1}steamapps{1}common{1}DB Xenoverse 2{1}bin{1}DBXV2.exe", letter, System.IO.Path.DirectorySeparatorChar);

                if (File.Exists(_path1))
                {
                    return Path.GetDirectoryName(Path.GetDirectoryName(_path1));
                }
                else if (File.Exists(_path2))
                {
                    return Path.GetDirectoryName(Path.GetDirectoryName(_path2));
                }
                else if (File.Exists(_path3))
                {
                    return Path.GetDirectoryName(Path.GetDirectoryName(_path3));
                }
            }

            return null;
        }

        public AppTheme GetCurrentTheme()
        {
            return _currentTheme;
        }

        public void InitTheme(bool forceSet = false)
        {
            if (!DefaultThemeSet || forceSet)
            {
                //Check registry for the users Light/Dark mode preferences (Windows 10 only)
                var registryValue = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", "1");

                if (registryValue != null)
                    if (registryValue.ToString() == "0")
                        _currentTheme = AppTheme.Dark;
                    else
                        _currentTheme = AppTheme.Light;

                NotifyPropertyChanged(nameof(UseDarkTheme));
                NotifyPropertyChanged(nameof(UseLightTheme));

                DefaultThemeSet = true;
            }
        }

        #region Enums
        private enum EepkFileCleanUp
        {
            Delete,
            Prompt,
            Ignore
        }

        private enum EepkTextureReuse
        {
            Identical,
            NameMatch
        }


        #endregion
    }

    #region Enums
    public enum BacTypeSortMode
    {
        Default = 0, //Type Order
        StartTime = 1,
    }

    public enum AppTheme
    {
        Light,
        Dark
    }

    public enum AppAccent
    {
        Red,
        Green,
        Blue,
        Purple,
        Orange,
        Lime,
        Emerald,
        Teal,
        Cyan,
        Cobalt,
        Indigo,
        Violet,
        Pink,
        Magenta,
        Crimson,
        Amber,
        Yellow,
        Brown,
        Olive,
        Steel,
        Mauve,
        Taupe,
        Sienna
    }
    #endregion

}
