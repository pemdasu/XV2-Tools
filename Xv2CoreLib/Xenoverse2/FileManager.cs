using System;
using System.Collections.Generic;
using System.Linq;
using Xv2CoreLib.CMS;
using Xv2CoreLib.CUS;
using Xv2CoreLib.MSG;
using Xv2CoreLib.BCS;
using Xv2CoreLib.BAC;
using Xv2CoreLib.BDM;
using Xv2CoreLib.BSA;
using Xv2CoreLib.CSO;
using Xv2CoreLib.EAN;
using Xv2CoreLib.ERS;
using Xv2CoreLib.IDB;
using Xv2CoreLib.PUP;
using Xv2CoreLib.BCM;
using Xv2CoreLib.ACB;
using Xv2CoreLib.BAI;
using Xv2CoreLib.AMK;
using Xv2CoreLib.BAS;
using Xv2CoreLib.ESK;
using Xv2CoreLib.EMD;
using Xv2CoreLib.PSC;
using Xv2CoreLib.EMM;
using Xv2CoreLib.EMB_CLASS;
using Xv2CoreLib.EffectContainer;
using Xv2CoreLib.Resource;
using System.IO;
using Xv2CoreLib.Resource.App;
using Xv2CoreLib.AFS2;
using Xv2CoreLib.SPM;
using Xv2CoreLib.FMP;
using Xv2CoreLib.NSK;
using Xv2CoreLib.Eternity;
using Xv2CoreLib.CBS;
using Xv2CoreLib.Resource.UndoRedo;
using System.Timers;

namespace Xv2CoreLib
{
    public class FileManager
    {
        #region Singleton
        private static Lazy<FileManager> instance = new Lazy<FileManager>(() => new FileManager());
        public static FileManager Instance => instance.Value;

        private FileManager() 
        {
            CleanUpTimer = new Timer();
            CleanUpTimer.Interval = 5.0 * 60.0 * 1000.0; //5 minutes
            CleanUpTimer.Elapsed += CleanUpTimer_Elapsed;
        }

        private void CleanUpTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            lock (_lock)
            {
                if (CachedFiles != null && CpkCachedFiles != null)
                {
                    RemoveDeadReferences();
                }
            }
        }
        #endregion

        public static string GameDir => Instance.fileIO?.GameDir;

        internal FileWatcher fileWatcher { get; private set; } = new FileWatcher();
        public Xv2FileIO fileIO { get; private set; }
        private Dictionary<string, CachedFile> CachedFiles;
        private Dictionary<string, CachedFile> CpkCachedFiles;
        private LimitedStack<CachedFile> StrongReferenceStack;
        private readonly Timer CleanUpTimer;

        private int _strongReferenceLimit = 0;

        /// <summary>
        /// Use direct references when caching loaded files, preventing them from being removed by the garbage collector.
        /// </summary>
        /// <remarks>These files can later be freed up by calling <see cref="ClearStrongReferneces"/>.</remarks>
        public bool UseStrongReferences { get; set; }
        /// <summary>
        /// Gets or sets the maximum number of strong references to retain.
        /// </summary>
        /// <remarks>Default is 0, which will be treated as no limit.</remarks>
        public int StrongReferenceLimit
        {
            get => _strongReferenceLimit;
            set
            {
                _strongReferenceLimit = value;
                InitStrongReferenceStack();
            }
        }
        /// <summary>
        /// When enabled, the file cache is ignored and files are always reloaded from loose files or CPK, with the cache entry being overwritten.
        /// </summary>
        public bool ForceReloadFiles { get; set; }
        public bool UseCpkCache { get; set; }

        private readonly object _lock = new object();

        //Events
        /// <summary>
        /// Raised when a file is not found and an exception is not thrown.
        /// </summary>
        public static event Xv2FileNotFoundEventHandler FileNotFoundEvent;

        #region Init
        internal void Init()
        {
            if (ShouldLoadFileIO())
            {
                string GameDir = SettingsManager.Instance.Settings.GameDirectory;

                if (!File.Exists(string.Format("{0}/bin/DBXV2.exe", GameDir)))
                    GameDir = FindGameDirectory();

                if (File.Exists(string.Format("{0}/bin/DBXV2.exe", GameDir)))
                {
                    fileIO = new Xv2FileIO(GameDir, false, new string[] { "data_d4_5_xv1.cpk", "data_d6_dlc.cpk", "data2.cpk", "data1.cpk", "data0.cpk", "data.cpk" });
                }
                else
                {
                    throw new FileNotFoundException("FileManager.Init: GameDirectory was not set or is not valid.");
                }

                bool largerCache = SettingsManager.Instance.CurrentApp == Application.XenoKit || SettingsManager.Instance.CurrentApp == Application.EepkOrganiser;
                CachedFiles = largerCache ? new Dictionary<string, CachedFile>(1024) : new Dictionary<string, CachedFile>(128);
                CpkCachedFiles = largerCache ? new Dictionary<string, CachedFile>(1024) : new Dictionary<string, CachedFile>(128);
                StrongReferenceStack = new(StrongReferenceLimit);

            }
        }

        internal static string FindGameDirectory()
        {
            List<string> alphabet = new List<string>() { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "O", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z" };

            foreach (var letter in alphabet)
            {
                string path = Path.GetFullPath(string.Format("{0}:{1}Program Files{1}Steam{1}steamapps{1}common{1}DB Xenoverse 2{1}bin{1}DBXV2.exe", letter, Path.DirectorySeparatorChar));
                if (File.Exists(path))
                {
                    return Path.GetFullPath(string.Format("{0}:{1}Program Files{1}Steam{1}steamapps{1}common{1}DB Xenoverse 2", letter, Path.DirectorySeparatorChar));
                }
            }

            foreach (var letter in alphabet)
            {
                string path = Path.GetFullPath(string.Format("{0}:{1}Steam{1}steamapps{1}common{1}DB Xenoverse 2{1}bin{1}DBXV2.exe", letter, Path.DirectorySeparatorChar));
                if (File.Exists(path))
                {
                    return Path.GetFullPath(string.Format("{0}:{1}Steam{1}steamapps{1}common{1}DB Xenoverse 2", letter, Path.DirectorySeparatorChar));
                }
            }

            foreach (var letter in alphabet)
            {
                string path = Path.GetFullPath(string.Format("{0}:{1}Games{1}Steam{1}steamapps{1}common{1}DB Xenoverse 2{1}bin{1}DBXV2.exe", letter, Path.DirectorySeparatorChar));
                if (File.Exists(path))
                {
                    return Path.GetFullPath(string.Format("{0}:{1}Games{1}Steam{1}steamapps{1}common{1}DB Xenoverse 2", letter, Path.DirectorySeparatorChar));
                }
            }

            foreach (var letter in alphabet)
            {
                string path = Path.GetFullPath(string.Format("{0}:{1}Games{1}SteamLibrary{1}steamapps{1}common{1}DB Xenoverse 2{1}bin{1}DBXV2.exe", letter, Path.DirectorySeparatorChar));
                if (File.Exists(path))
                {
                    return Path.GetFullPath(string.Format("{0}:{1}Games{1}SteamLibrary{1}steamapps{1}common{1}DB Xenoverse 2", letter, Path.DirectorySeparatorChar));
                }
            }

            foreach (var letter in alphabet)
            {
                string path = Path.GetFullPath(string.Format("{0}:{1}SteamLibrary{1}steamapps{1}common{1}DB Xenoverse 2{1}bin{1}DBXV2.exe", letter, Path.DirectorySeparatorChar));
                if (File.Exists(path))
                {
                    return Path.GetFullPath(string.Format("{0}:{1}SteamLibrary{1}steamapps{1}common{1}DB Xenoverse 2", letter, Path.DirectorySeparatorChar));
                }
            }

            return string.Empty;
        }

        private bool ShouldLoadFileIO()
        {
            if (fileIO == null) return true;
            return fileIO.GameDir != SettingsManager.Instance.Settings.GameDirectory;
        }

        private void CheckInitState()
        {
            if(fileIO == null)
            {
                //If FileManager is accessed without Xenoverse.Init() being called, then it must initialize itself.
                Init();
            }
        }
        #endregion

        #region Load
        public T LoadFile<T>(string path, bool onlyFromCpk = false, bool raiseEx = true, bool ignoreCache = false) where T : class
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            lock (_lock)
            {
                T file = (T)GetParsedFileFromGameInternal(path, onlyFromCpk, raiseEx, ignoreCache);

                if (SettingsManager.Instance.CurrentApp == Application.XenoKit && file != null)
                {
                    CustomEntryNames.LoadNames(path, file);
                }

                return file;
            }
        }

        [Obsolete("Use generic LoadFile<T> method")]
        public object GetParsedFileFromGame(string path, bool onlyFromCpk = false, bool raiseEx = true, bool ignoreCache = false)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            lock (_lock)
            {
                object file = GetParsedFileFromGameInternal(path, onlyFromCpk, raiseEx, ignoreCache);

                if (SettingsManager.Instance.CurrentApp == Application.XenoKit && file != null)
                {
                    CustomEntryNames.LoadNames(path, file);
                }

                return file;
            }
        }

        private object GetParsedFileFromGameInternal(string path, bool onlyFromCpk = false, bool raiseEx = true, bool ignoreCache = false)
        {
            CheckInitState();

            bool useCache = !ignoreCache && (!onlyFromCpk || (onlyFromCpk && UseCpkCache));

            //Check cache and return an existing file, if allowed
            if (!ForceReloadFiles && useCache)
            {
                object cached = GetCachedFile(path, onlyFromCpk);
                if (cached != null) return cached;
            }

            //Handle missing files for CPK (when only loading from CPK)
            if (onlyFromCpk)
            {
                if (!fileIO.FileExistsInCpk(path))
                {
                    if (raiseEx)
                        throw new FileNotFoundException(string.Format("The file \"{0}\" does not exist in the cpks.", path));
                    else
                        return null;
                }
            }

            //File loading
            object file;

            if (path.Equals(StageDefFile.PATH, StringComparison.OrdinalIgnoreCase))
            {
                byte[] stageDefBytes = GetBytesFromGame(path, false, false);
                file = stageDefBytes != null ? StageDefFile.Load(stageDefBytes) : StageDefFile.DefaultFile;
            }
            else
            {
                //Handle missing files
                if (!fileIO.FileExists(path))
                {
                    if (raiseEx)
                        throw new FileNotFoundException(string.Format("The file \"{0}\" does not exist in the game directory or cpks.", path));
                    else
                        return null;
                }

                switch (Path.GetExtension(path))
                {
                    case ".bac":
                        file = BAC_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".bcm":
                        file = BCM_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".bcs":
                        file = BCS_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".bdm":
                        file = BDM_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx), true);
                        break;
                    case ".bsa":
                        file = BSA_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".cms":
                        file = CMS_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".cso":
                        file = CSO_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".cus":
                        file = CUS_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".ean":
                        file = EAN_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx), true);
                        break;
                    case ".ers":
                        file = ERS_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".idb":
                        file = IDB_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".pup":
                        file = PUP_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".bas":
                        file = BAS_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".bai":
                        file = BAI_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".amk":
                        file = AMK_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".esk":
                        file = ESK_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".emd":
                        file = EMD_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".nsk":
                        file = NSK_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".emb":
                        file = EMB_File.LoadEmb(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".emm":
                        file = EMM_File.LoadEmm(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".msg":
                        file = MSG_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".psc":
                        file = PSC_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".eepk":
                        file = EffectContainerFile.Load(path, fileIO, onlyFromCpk);
                        break;
                    case ".emz":
                        file = EMZ.EMZ_File.LoadData(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".acb":
                        {
                            byte[] awbBytes = fileIO.GetFileFromGame(string.Format("{0}/{1}.awb", Path.GetFileNameWithoutExtension(path), Path.GetDirectoryName(path)), false, onlyFromCpk);
                            AFS2_File awbFile = awbBytes != null ? AFS2_File.LoadFromArray(awbBytes) : null;
                            file = new ACB_Wrapper(ACB_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx), awbFile));
                        }
                        break;
                    case ".spm":
                        file = SPM_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".map":
                        file = FMP_File.Load(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    case ".cbs":
                        file = CBS_File.Parse(GetBytesFromGame(path, onlyFromCpk, raiseEx));
                        break;
                    default:
                        throw new InvalidDataException(string.Format("FileManager.GetParsedFileFromGame: The filetype of \"{0}\" is not supported.", path));
                }
            }
            
            if(useCache)
                AddCachedFile(path, file, onlyFromCpk);

            return file;
        }

        public byte[] GetBytesFromGame(string path, bool onlyFromCpk = false, bool raiseEx = false)
        {
            CheckInitState();

            if (fileIO == null && raiseEx) throw new NullReferenceException("FileManager.GetBytesFromGame: fileIO is null.");
            if (fileIO == null) return null;

            var bytes = fileIO.GetFileFromGame(path, raiseEx, onlyFromCpk);
            if (bytes == null) FileNotFoundEvent?.Invoke(this, new Xv2FileNotFoundEventArgs(path));
            return bytes;
        }

        public string GetAbsolutePath(string relativePath)
        {
            CheckInitState();
            return (fileIO != null) ? fileIO.PathInGameDir(relativePath) : relativePath;
        }
        #endregion

        #region Save
        internal void SaveFileToGame(string path, object file)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GetAbsolutePath(path)));

            byte[] bytes = FileManager.GetBytesFromParsedFile(path, file);
            File.WriteAllBytes(GetAbsolutePath(path), bytes);
            fileWatcher.FileLoadedOrSaved(path);
        }

        internal void SaveMsgFilesToGame(string path, IList<MSG_File> files)
        {
            for (int i = 0; i < files.Count; i++)
            {
                SaveFileToGame(path + Xenoverse2.LanguageSuffix[i], files[i]);
            }
        }

        public static byte[] GetBytesFromParsedFile(string path, object data)
        {
            switch (Path.GetExtension(path))
            {
                case ".bac":
                    return ((BAC_File)data).SaveToBytes();
                case ".bcs":
                    return ((BCS_File)data).SaveToBytes();
                case ".bdm":
                    return ((BDM_File)data).SaveToBytes();
                case ".bsa":
                    return ((BSA_File)data).SaveToBytes();
                case ".cms":
                    return ((CMS_File)data).SaveToBytes();
                case ".cso":
                    return ((CSO_File)data).SaveToBytes();
                case ".cus":
                    return ((CUS_File)data).SaveToBytes();
                case ".ean":
                    return ((EAN_File)data).SaveToBytes();
                case ".ers":
                    return ((ERS_File)data).SaveToBytes();
                case ".idb":
                    return ((IDB_File)data).SaveToBytes(Path.GetFileNameWithoutExtension(path).Equals("skill_item", StringComparison.OrdinalIgnoreCase));
                case ".msg":
                    return ((MSG_File)data).SaveToBytes();
                case ".pup":
                    return ((PUP_File)data).SaveToBytes();
                case ".psc":
                    return ((PSC_File)data).SaveToBytes();
                case ".emb":
                    return ((EMB_File)data).SaveToBytes();
                case ".emd":
                    return ((EMD_File)data).SaveToBytes();
                case ".nsk":
                    return ((NSK_File)data).Write();
                case ".emm":
                    return ((EMM_File)data).SaveToBytes();
                case ".spm":
                    return ((SPM_File)data).Write();
                case ".fmp":
                    return ((FMP_File)data).Write();
                case ".esk":
                    return ((ESK_File)data).SaveToBytes();
                default:
                    throw new InvalidDataException(String.Format("Xenoverse2.GetBytesFromParsedFile: The filetype of \"{0}\" is not supported.", path));
            }
        }

        #endregion
        
        #region Cache
        private void InitStrongReferenceStack()
        {
            if(StrongReferenceStack == null)
            {
                StrongReferenceStack = new LimitedStack<CachedFile>(StrongReferenceLimit);
            }
            else
            {
                if(StrongReferenceLimit < StrongReferenceStack.Capacity)
                {
                    for(int i = 0; i < StrongReferenceStack.Capacity - StrongReferenceLimit; i++)
                    {
                        StrongReferenceStack.Pop().ClearStrongReference();
                    }
                }

                StrongReferenceStack.Resize(StrongReferenceLimit);
            }
        }

        private object GetCachedFile(string path, bool fromCpk)
        {
            RemoveDeadReferences();

            if (fromCpk && CpkCachedFiles.TryGetValue(path, out CachedFile cpkFile))
            {
                return cpkFile.ObjectReference.IsAlive ? cpkFile.ObjectReference.Target : null;
            }
            else if(CachedFiles.TryGetValue(path, out CachedFile file))
            {
                return file.ObjectReference.IsAlive ? file.ObjectReference.Target : null;
            }

            return null;
        }

        private void AddCachedFile(string path, object data, bool fromCpk)
        {
            path = Utils.SanitizePath(path);

            if (fromCpk)
            {
                AddCachedFileInternal(ref CpkCachedFiles, path, data);
            }
            else
            {
                AddCachedFileInternal(ref CachedFiles, path, data);
            }
        }

        private void AddCachedFileInternal(ref Dictionary<string, CachedFile> cache, string path, object data)
        {
            CachedFile newCachedFile = new CachedFile(path, data, UseStrongReferences);

            if (cache.TryGetValue(path, out CachedFile file))
            {
                cache[path] = newCachedFile;
            }
            else
            {
                cache.Add(path, newCachedFile);
            }

            if(UseStrongReferences && StrongReferenceLimit > 0)
            {
                var outCachedObject = StrongReferenceStack.Push(newCachedFile);
                outCachedObject?.ClearStrongReference();
            }
        }
    
        public void ClearStrongReferneces()
        {
            foreach(var file in CachedFiles)
            {
                file.Value?.ClearStrongReference();
            }

            foreach (var file in CpkCachedFiles)
            {
                file.Value?.ClearStrongReference();
            }

            StrongReferenceStack.Clear();
        }

        private void RemoveDeadReferences()
        {
            CachedFiles.RemoveAll((k, v) => !v.ObjectReference.IsAlive);
            CpkCachedFiles.RemoveAll((k, v) => !v.ObjectReference.IsAlive);
        }
        #endregion

    }

    internal class CachedFile
    {
        internal string Path { get; private set; }
        internal WeakReference ObjectReference { get; private set; }
        private object StrongReference { get; set; }

        internal CachedFile(string path, object file, bool strongReference)
        {
            Path = path;
            ObjectReference = new WeakReference(file);

            if (strongReference)
                StrongReference = file;
        }

        internal void ClearStrongReference()
        {
            StrongReference = null;
        }

    }

    public delegate void Xv2FileNotFoundEventHandler(object source, Xv2FileNotFoundEventArgs e);

    public class Xv2FileNotFoundEventArgs : EventArgs
    {
        private string EventInfo;
        public Xv2FileNotFoundEventArgs(string file)
        {
            EventInfo = file;
        }
        public string GetInfo()
        {
            return EventInfo;
        }
    }
}
