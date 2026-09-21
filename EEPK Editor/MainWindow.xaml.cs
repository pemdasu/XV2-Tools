using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;
using System.Globalization;
using ControlzEx.Theming;
using MahApps.Metro.Controls.Dialogs;
using AutoUpdater;
using LB_Common.Forms;
using LB_Common.Mvvm;
using Xv2CoreLib.Resource.UndoRedo;
using Xv2CoreLib.EffectContainer;
using Xv2CoreLib.Resource.App;
using xv2 = Xv2CoreLib.Xenoverse2;
using EEPK_Organiser.View;

namespace EEPK_Organiser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AutoObservableWindow
    {
        public EepkEditor EepkView => eepkEditor;

        private EffectContainerFile _eepkFile = null;
        public EffectContainerFile EepkFile
        {
            get
            {
                return this._eepkFile;
            }
            set
            {
                if (value != _eepkFile)
                {
                    _eepkFile = value;
                    NotifyPropertyChanged(nameof(IsFileLoaded));
                    NotifyPropertyChanged(nameof(EepkFile));
                    NotifyPropertyChanged(nameof(CanSave));
                }
            }
        }
        public bool IsFileLoaded => _eepkFile != null;
        public bool CanSave
        {
            get
            {
                if (EepkFile == null) return false;
                return EepkFile.CanSave;
            }
        }

        //Version
        public bool IsVerDBXV2
        {
            get
            {
                if (EepkFile == null) return false;
                return (EepkFile.Version == Xv2CoreLib.EMP_NEW.VersionEnum.DBXV2);
            }
            set
            {
                if (EepkFile != null)
                {
                    EepkFile.Version = Xv2CoreLib.EMP_NEW.VersionEnum.DBXV2;
                    UpdateSelectedVersion();
                }
            }
        }
        public bool IsVerSDBH
        {
            get
            {
                if (EepkFile == null) return false;
                return (EepkFile.Version == Xv2CoreLib.EMP_NEW.VersionEnum.SDBH);
            }
            set
            {
                if (EepkFile != null)
                {
                    EepkFile.Version = Xv2CoreLib.EMP_NEW.VersionEnum.SDBH;
                    UpdateSelectedVersion();
                }
            }
        }

        //GameInterface
        public bool CanLoadFromGame
        {
            get
            {
                return eepkEditor?.loadHelper != null;
            }
        }
        
        public NameList.NameListManager nameListManager { get { return eepkEditor?.nameListManager; } }

        public MainWindow()
        {
            //Force en-US culture accross whole application to ensure error messages will always be in english
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            //Allows decimal points to be typed in float values with UpdateSourceTrigger=PropertyChanged
            FrameworkCompatibilityPreferences.KeepTextBoxDisplaySynchronizedWithTextProperty = false;

            //Tooltips
            ToolTipService.ShowDurationProperty.OverrideMetadata(
            typeof(DependencyObject), new FrameworkPropertyMetadata(Int32.MaxValue));

            //Init settings
            SettingsManager.SettingsReloaded += SettingsManager_SettingsReloaded;

            //Init UI
            InitializeComponent();
            DataContext = this;
            InitTheme();

            //Update title
            Title += $" ({SettingsManager.Instance.CurrentVersionString})";

            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            SettingsManager.Instance.SaveSettings();
        }

        private void SettingsManager_SettingsReloaded(object sender, EventArgs e)
        {
            InitTheme();

            if(sender is Settings oldSettings)
            {
                if(oldSettings.GameDirectory != SettingsManager.settings.GameDirectory && SettingsManager.settings.ValidGameDir)
                {
                    AsyncInit();
                }
            }
        }

        public void InitTheme()
        {
            Dispatcher.Invoke((() =>
            {
                ThemeManager.Current.ChangeTheme(System.Windows.Application.Current, SettingsManager.Instance.GetTheme());
            }));
        }

        public async Task AsyncInit()
        {
            var controller = await this.ShowProgressAsync($"Initializing...", $"", false, DialogSettings.Default);
            controller.SetIndeterminate();

            try
            {
                await Task.Run(() =>
                {
                    xv2.Instance.loadCharacters = true;
                    xv2.Instance.loadSkills = true;
                    xv2.Instance.loadCmn = false;
                    xv2.Instance.Init();
                });

                eepkEditor.loadHelper = null;
                NotifyPropertyChanged(nameof(CanLoadFromGame));
                await controller.CloseAsync();
            }
            catch (Exception ex)
            {
                await controller.CloseAsync();
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }

            NotifyPropertyChanged(nameof(CanLoadFromGame));
        }

        private async Task Load(string path = null)
        {
            //If a file is already loaded then ask for confirmation
            if (EepkFile != null)
            {
                var ret = MessagePrompt.Show(string.Format("Do you want to save the currently opened file first?", EepkFile.Name), "Open", MessagePromptButtons.YesNoCancel, MessagePromptIcon.Question);

                if (ret == MessagePromptResult.Yes)
                {
                    if (EepkFile.CanSave)
                    {
                        MenuSave();
                    }
                    else
                    {
                        MenuSaveAs();
                    }
                }
                else if (ret == MessagePromptResult.Cancel)
                {
                    return;
                }
            }

            //Clear Undo stack
            UndoManager.Instance.Clear();

            //Load the eepk + assets
            EffectContainerFile file = null;

            if (path == null)
            {
                file = await eepkEditor.LoadEffectContainerFile(false);
            }
            else
            {
                file = await eepkEditor.LoadEffectContainerFile(path, false);
            }

            if (file != null)
            {
                View.EepkEditor.CloseAllEditorForms();
                EepkFile = file;
                UpdateSelectedVersion();
            }

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NotifyPropertyChanged(nameof(EepkView));

            //Check startup args
            LoadOnStartUp();

            //Async Tasks
            AsyncStartUpTasks();
        }

        private async void AsyncStartUpTasks()
        {
            if (SettingsManager.Instance.Settings.ValidGameDir)
                AsyncInit();


            //Check for updates silently
#if !DEBUG
            CheckForUpdate(false);
#endif

        }

        private async void CheckForUpdate(bool userInitiated)
        {
            //Check for update
            AppUpdate appUpdate = default;

            await Task.Run(() =>
            {
                appUpdate = Update.CheckForUpdate(AutoUpdater.App.EEPK_Organiser);
            });

            await Task.Delay(1000);

            if(Update.UpdateState == UpdateState.XmlDownloadFailed && userInitiated)
            {
                await this.ShowMessageAsync("Update Failed", "The AppUpdate XML file failed to download.", MessageDialogStyle.Affirmative, DialogSettings.Default);
                return;
            }

            if (Update.UpdateState == UpdateState.XmlParseFailed && userInitiated)
            {
                await this.ShowMessageAsync("Update Failed", $"The AppUpdate XML file could not be parsed.\n\n{Update.FailedErrorMessage}", MessageDialogStyle.Affirmative, DialogSettings.Default);
                return;
            }

            if (!appUpdate.ForceUpdate && !SettingsManager.settings.UpdateNotifications && !userInitiated)
            {
                return;
            }

            if (appUpdate.HasUpdate)
            {
                MetroDialogSettings dialogSettings = DialogSettings.ScrollDialog;
                dialogSettings.FirstAuxiliaryButtonText = "Ignore";
                dialogSettings.AffirmativeButtonText = "Update";
                dialogSettings.NegativeButtonText = "Open in Browser";
                dialogSettings.DefaultButtonFocus = MessageDialogResult.Affirmative;

                MessageDialogResult messageResult = await this.ShowMessageAsync("Update Available", $"An update is available ({appUpdate.Version}). The application can automatically download and update itself (confirmation may be required), or you may also open the website in a browser and download the update manually. \n\nNote: All instances of the application will be closed and any unsaved work will be lost if Update is selected.\n\nChangelog:\n{appUpdate.Changelog}", MessageDialogStyle.AffirmativeAndNegativeAndSingleAuxiliary, dialogSettings);

                if (messageResult == MessageDialogResult.FirstAuxiliary)
                    return;

                //Check that the required runtime is installed on the users machine
                switch (Update.CheckRuntime())
                {
                    case RuntimeStatus.NotInstalled:
                        {
                            dialogSettings.AffirmativeButtonText = "Visit Site";
                            dialogSettings.NegativeButtonText = "Cancel";
                            dialogSettings.DefaultButtonFocus = MessageDialogResult.Affirmative;
                            MessageDialogResult result = await this.ShowMessageAsync(".NET Update Required", $"This update requires that the .NET {Update.GetRequiredRuntime()} runtime be installed on this machine. Please install this runtime and try again.\n\nDo you want to be directed towards the download page for .NET {Update.GetRequiredRuntime()}? (it will open in your browser). Alternatively, you may cancel the update.\n\nOnce on the download page, you need to download the runtime labeled \"Desktop Runtime\" for Windows x64.", MessageDialogStyle.AffirmativeAndNegative, dialogSettings);

                            if (result == MessageDialogResult.Affirmative)
                                Process.Start(Update.GetRuntimePageUrl());

                            return;
                        }
                    case RuntimeStatus.FolderNotFound:
                        {
                            dialogSettings.AffirmativeButtonText = "Visit Site";
                            dialogSettings.NegativeButtonText = "Continue Update";
                            dialogSettings.DefaultButtonFocus = MessageDialogResult.Affirmative;
                            MessageDialogResult result = await this.ShowMessageAsync(".NET Runtime Version Not Found", $"This update requires that the .NET {Update.GetRequiredRuntime()} runtime be installed on this machine, but it could not be automatically located. If it is not installed already, you must install it before the update will run. \n\nDo you want to cancel the update and be directed towards the download page for .NET {Update.GetRequiredRuntime()}? (it will open in your browser). Alternatively, you may continue with the update if you believe that this specific runtime version is actually installed.\n\nOnce on the download page, you need to download the runtime labeled \"Desktop Runtime\" for Windows x64.", MessageDialogStyle.AffirmativeAndNegative, dialogSettings);

                            if (result == MessageDialogResult.Affirmative)
                            {
                                Process.Start(Update.GetRuntimePageUrl());
                                return;
                            }

                            break;
                        }
                }

                if (messageResult == MessageDialogResult.Affirmative)
                {
                    var controller = await this.ShowProgressAsync("Update Available", "Downloading...", false, DialogSettings.Default);
                    controller.SetIndeterminate();

                    try
                    {
                        await Task.Run(() =>
                        {
                            Update.DownloadUpdate();
                        });
                    }
                    finally
                    {
                        await controller.CloseAsync();
                    }

                    if (Update.UpdateState == UpdateState.DownloadSuccess)
                    {
                        Update.UpdateApplication();
                    }
                    else if (Update.UpdateState == UpdateState.DownloadFail)
                    {
                        await this.ShowMessageAsync("Download Failed", "Received Error: " + Update.FailedErrorMessage, MessageDialogStyle.Affirmative, DialogSettings.Default);
                    }

                    if(Update.UpdateState == UpdateState.BootstrapperLaunchFailed)
                    {
                        await this.ShowMessageAsync("Update Failed", "Received Error: " + Update.FailedErrorMessage, MessageDialogStyle.Affirmative, DialogSettings.Default);
                    }

                }
                else if (messageResult == MessageDialogResult.Negative)
                {
                    Process.Start("https://github.com/LazyBone152/EEPKOrganiser/releases");
                }
            }
            else if (userInitiated)
            {
                await this.ShowMessageAsync("Update", $"No update is available.", MessageDialogStyle.Affirmative, DialogSettings.Default);
            }
        }

        private void LoadOnStartUp()
        {
            string[] args = Environment.GetCommandLineArgs();

            foreach(var arg in args)
            {
                if(System.IO.Path.GetExtension(arg) == ".eepk" || System.IO.Path.GetExtension(arg) == EffectContainerFile.ZipExtension)
                {
                    Load(arg);
                    return;
                }
            }
        }

        private void UpdateSelectedVersion()
        {
            NotifyPropertyChanged(nameof(IsVerDBXV2));
            NotifyPropertyChanged(nameof(IsVerSDBH));
        }

        private void FileCleanUp()
        {
            if (EepkFile.LoadedExternalFilesNotSaved.Count > 0 && !SettingsManager.Instance.Settings.FileCleanUp_Ignore)
            {
                bool fileCleanUp = SettingsManager.Instance.Settings.FileCleanUp_Delete;

                if (SettingsManager.Instance.Settings.FileCleanUp_Prompt)
                {
                    StringBuilder str = new StringBuilder();

                    foreach (string file in EepkFile.GetUnusedFilePaths())
                    {
                        str.Append(string.Format("{0}\r", file));
                    }

                    if(MessagePrompt.Show("The files listed below are no longer in any of the asset containers. Do you want to also delete them from disk?", "Save", 
                        MessagePromptButtons.YesNo, MessagePromptIcon.Question, str.ToString()) == MessagePromptResult.Yes)
                    {
                        fileCleanUp = true;
                    }
                    else
                    {
                        return;
                    }
                }
                
                if (fileCleanUp)
                {
                    try
                    {
                        foreach (string file in EepkFile.GetUnusedFilePaths())
                        {
                            if (File.Exists(file))
                                File.Delete(file);
                        }
                    }
                    catch { }
                }
            }
        }

        //NameList
        private void NameList_Item_Click(object sender, RoutedEventArgs e)
        {
            if (EepkFile == null) return;

            MenuItem selectedMenuItem = e.OriginalSource as MenuItem;

            if (selectedMenuItem != null)
            {
                NameList.NameListFile nameList = selectedMenuItem.DataContext as NameList.NameListFile;

                if (nameList != null)
                {
                    eepkEditor.nameListManager.ApplyNameList(EepkFile.Effects, nameList.GetNameList());
                }
            }
        }

        private void NameList_Clear_Click(object sender, RoutedEventArgs e)
        {
            eepkEditor.nameListManager.ClearNameList(EepkFile.Effects);
        }

        private void NameList_Save_Click(object sender, RoutedEventArgs e)
        {
            eepkEditor.nameListManager.SaveNameList(EepkFile.Effects);
        }

        private void MenuItem_MouseMove(object sender, MouseEventArgs e)
        {
            MenuItem menuItem = (MenuItem)sender;
            menuItem.IsSubmenuOpen = true;
        }
        
        //File Path Display
        private void FilePath_MenuItem_CopyFullPath_Click(object sender, RoutedEventArgs e)
        {
            if (EepkFile == null) return;

            try
            {
                Clipboard.SetText(EepkFile.FullFilePath, TextDataFormat.Text);
            }
            catch
            {

            }
        }

        private void FilePath_MenuItem_CopyDirectory_Click(object sender, RoutedEventArgs e)
        {
            if (EepkFile == null) return;

            try
            {
                Clipboard.SetText(EepkFile.Directory, TextDataFormat.Text);
            }
            catch
            {

            }
        }

        //File Dropped
        private void Grid_FilesDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] droppedFilePaths = e.Data.GetData(DataFormats.FileDrop, true) as string[];

                    if (droppedFilePaths.Length == 1)
                    {
                        switch (Path.GetExtension(droppedFilePaths[0]))
                        {
                            case EffectContainerFile.ZipExtension:
                            case ".eepk":
                                Load(droppedFilePaths[0]);
                                break;
                            case ".emp":
                            case ".etr":
                            case ".ecf":
                            case ".emb":
                            case ".emm":
                            case ".ema":
                            case ".emo":
                                MessagePrompt.Show(string.Format("\"{0}\" files are not supported directly. Please load a .eepk.", Path.GetExtension(droppedFilePaths[0])), "File Drop", MessagePromptButtons.OK, MessagePromptIcon.Error);
                                break;
                            default:
                                MessagePrompt.Show(string.Format("The filetype of the dropped file ({0}) is not supported.", Path.GetExtension(droppedFilePaths[0])), "File Drop", MessagePromptButtons.OK, MessagePromptIcon.Error);
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessagePrompt.Show(string.Format("The dropped file could not be opened.\n\nThe reason given by the system: {0}", ex.Message), "File Drop", MessagePromptButtons.OK, MessagePromptIcon.Error);
            }
        }

        //"Import" relay events
        
        private void EffectOptions_ImportEffectsFromCache_Click(object sender, RoutedEventArgs e)
        {
            eepkEditor.EffectOptions_ImportEffectsFromCache_Click(sender, e);
        }

        private void PBIND_ImportAsset_MenuItem_FromCachedFiles_Click(object sender, RoutedEventArgs e)
        {
            eepkEditor.PBIND_ImportAsset_MenuItem_FromCachedFiles_Click(sender, e);
        }

        private void TBIND_ImportAsset_MenuItem_FromCachedFiles_Click(object sender, RoutedEventArgs e)
        {
            eepkEditor.TBIND_ImportAsset_MenuItem_FromCachedFiles_Click(sender, e);
        }

        private void CBIND_ImportAsset_MenuItem_FromCachedFiles_Click(object sender, RoutedEventArgs e)
        {
            eepkEditor.CBIND_ImportAsset_MenuItem_FromCachedFiles_Click(sender, e);
        }

        private void EMO_ImportAsset_MenuItem_FromCachedFiles_Click(object sender, RoutedEventArgs e)
        {
            eepkEditor.EMO_ImportAsset_MenuItem_FromCachedFiles_Click(sender, e);
        }

        private void LIGHT_ImportAsset_MenuItem_FromCachedFiles_Click(object sender, RoutedEventArgs e)
        {
            eepkEditor.LIGHT_ImportAsset_MenuItem_FromCachedFiles_Click(sender, e);
        }

    }
}
