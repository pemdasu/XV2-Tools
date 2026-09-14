using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Application = System.Windows.Application;
using Microsoft.Win32;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using LB_Common.Forms;
using LB_Common.Mvvm;
using Xv2CoreLib;
using Xv2CoreLib.ECF;
using Xv2CoreLib.EEPK;
using Xv2CoreLib.EffectContainer;
using Xv2CoreLib.EMB_CLASS;
using Xv2CoreLib.EMM;
using Xv2CoreLib.EMP_NEW;
using Xv2CoreLib.ETR;
using Xv2CoreLib.Resource.App;
using Xv2CoreLib.Resource.UndoRedo;
using EEPK_Organiser.Forms;
using EEPK_Organiser.Forms.Editors;
using EEPK_Organiser.Misc;
using EEPK_Organiser.ViewModel;

#if XenoKit
using XenoKit;
using XenoKit.Engine;
using XenoKit.Engine.Model;
using XenoKit.Editor;
using XenoKit.Views;
#endif

namespace EEPK_Organiser.View
{
    public partial class EepkEditor : AutoObservableUserControl
    {

        internal enum Tabs
        {
            //Number matches tab indexes.
            Effect = 0,
            Pbind = 1,
            Tbind = 2,
            Cbind = 3,
            Emo = 4,
            Light = 5
        }

        public event EventHandler SelectedEffectTabChanged;

        #region DependencyProperty
        public static readonly DependencyProperty EepkInstanceProperty = DependencyProperty.Register(
            nameof(effectContainerFile), typeof(EffectContainerFile), typeof(EepkEditor), new PropertyMetadata(OnEepkChanged));

        private static DependencyPropertyChangedEventHandler EepkChanged;

        private static void OnEepkChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (EepkChanged != null)
                EepkChanged.Invoke(sender, e);
        }

        private void EepkInstanceChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            NotifyPropertyChanged(nameof(effectContainerFile));
            NotifyPropertyChanged(nameof(IsFileLoaded));

#if !XenoKit
            if(effectContainerFile != null)
                nameListManager.EepkLoaded(effectContainerFile);
#endif
        }
#endregion

        public EffectContainerFile effectContainerFile
        {
            get { return (EffectContainerFile)GetValue(EepkInstanceProperty); }
            set
            {
                SetValue(EepkInstanceProperty, value);
                NotifyPropertyChanged("EepkInstance");
                NotifyPropertyChanged(nameof(effectContainerFile));
            }
        }
        public bool IsFileLoaded => effectContainerFile != null;

        //ViewModel
        public EffectPartViewModel EffectPartViewModel { get; private set; }

        //Effect View
        private bool editModeCancelling = false;

        //Selected Effect
        public ObservableCollection<object> SelectedItems { get; private set; } = new ObservableCollection<object>();
        private Effect _selectedEffect = null;
        public Effect SelectedEffect
        {
            get => _selectedEffect;
            set
            {
                _selectedEffect = value;
                NotifyPropertyChanged(nameof(SelectedEffect));
                NotifyPropertyChanged(nameof(SelectedEffectID));
            }
        }
        public int SelectedEffectID
        {
            get => SelectedEffect != null ? SelectedEffect.IndexNum : 0;
            set
            {
                if(SelectedEffect != null && SelectedEffect?.IndexNum != value)
                {
                    if(effectContainerFile.Effects.FirstOrDefault(x => x.IndexNum == value) == null)
                    {
                        List<IUndoRedo> undos = new List<IUndoRedo>();
                        undos.Add(new UndoablePropertyGeneric(nameof(SelectedEffect.IndexNum), SelectedEffect, SelectedEffect.IndexNum, (ushort)value));
                        undos.Add(new UndoActionDelegate(effectContainerFile, nameof(EffectContainerFile.UpdateEffectFilter), true));

                        SelectedEffect.IndexNum = (ushort)value;

                        UndoManager.Instance.AddCompositeUndo(undos, "Effect ID");
                        NotifyPropertyChanged(nameof(SelectedEffectID));
                        effectContainerFile.UpdateEffectFilter();

                        effectDataGrid.ScrollIntoView(SelectedEffect);
                    }
                    else
                    {
                        MessagePrompt.Show($"This ID ({value}) is already used for another effect.", "ID Used", MessagePromptButtons.OK, MessagePromptIcon.Error);
                    }
                }

            }
        }


#if !XenoKit
        //NameLists
        private NameList.NameListManager _nameListManager = null;
        public NameList.NameListManager nameListManager
        {
            get
            {
                return this._nameListManager;
            }
            set
            {
                if (value != this._nameListManager)
                {
                    this._nameListManager = value;
                    NotifyPropertyChanged("nameListManager");
                }
            }
        }
#endif

        //Cache
        public CacheManager cacheManager { get; set; } = new CacheManager();

        private LoadFromGameHelper _loadHelper = null;
        public LoadFromGameHelper loadHelper
        {
            get
            {
                if (Xenoverse2.Instance.IsInitialized && _loadHelper == null)
                {
                    loadHelper = new LoadFromGameHelper();
                }
                if (Xenoverse2.Instance.IsInitialized)
                {
                    return this._loadHelper;
                }
                else
                {
                    return null;
                }
            }
            set
            {
                if (value != this._loadHelper)
                {
                    this._loadHelper = value;
                    NotifyPropertyChanged(nameof(loadHelper));
                }
            }
        }

        //Filtering
        private string _searchFilter = null;
        public string SearchFilter
        {
            get
            {
                return this._searchFilter;
            }
            set
            {
                if (value != this._searchFilter)
                {
                    this._searchFilter = value;
                    NotifyPropertyChanged(nameof(SearchFilter));
                }
            }
        }

#if XenoKit
        public Visibility XenoKitVisible => Visibility.Visible;
#else
        public Visibility XenoKitVisible => Visibility.Collapsed;
#endif

        public EepkEditor()
        {
            InitializeComponent();
            DataContext = this;
            rootGrid.DataContext = this;

            EepkChanged += EepkInstanceChanged;
            UndoManager.Instance.UndoOrRedoCalled += UndoManager_UndoOrRedoCalled;

#if XenoKit
            pbindDataGrid.SelectionChanged += PbindDataGrid_SelectionChanged;
            tbindDataGrid.SelectionChanged += TbindDataGrid_SelectionChanged;
            cbindDataGrid.SelectionChanged += CbindDataGrid_SelectionChanged;
            emoDataGrid.SelectionChanged += EmoDataGrid_SelectionChanged;
            lightDataGrid.SelectionChanged += LightDataGrid_SelectionChanged;
            toolButton.Visibility = Visibility.Visible;
#else
            toolButton.Visibility = Visibility.Hidden;

            //Load NameLists
            nameListManager = new NameList.NameListManager();
#endif
        }

        private void UndoManager_UndoOrRedoCalled(object sender, UndoEventRaisedEventArgs e)
        {
            if (effectContainerFile == null) return;

            emoDataGrid.Items.Refresh();
        }

        //Loading
        public async Task<EffectContainerFile> LoadEffectContainerFile(bool cacheFile = true)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Title = "Open EEPK file...";
            //openFile.Filter = "EEPK File | *.eepk; *.vfx2";
            //openFile.Filter = "EEPK File | *.eepk; |VFXPACKAGE File |*.vfxpackage";
            openFile.Filter = string.Format("EEPK File | *.eepk; |{1} File |*{0};", EffectContainerFile.ZipExtension, EffectContainerFile.ZipExtension.ToUpper().Remove(0, 1));
            openFile.ShowDialog();

            return await LoadEffectContainerFile(openFile.FileName, cacheFile);
        }

        public async Task<EffectContainerFile> LoadEffectContainerFile(string path, bool cacheFile = true)
        {
            if (File.Exists(path) && !string.IsNullOrWhiteSpace(path))
            {
                var loadedFile = await LoadFileAsync(path, false, false);


#if !XenoKit
                //Apply namelist
                nameListManager.EepkLoaded(loadedFile);
#endif

                //Cache the file
                if (cacheFile)
                {
                    cacheManager.CacheFile(path, loadedFile, "File");
                }
                else
                {
                    cacheManager.RemoveCachedFile(path);
                }

                return loadedFile;
            }

            return null;
        }

        public async Task<EffectContainerFile> LoadEepkFromGame(EntitySelector.EntityType type, bool cacheFile = true)
        {
            if (!SettingsManager.settings.ValidGameDir) throw new Exception("Game directory is not valid. Please set the game directory in the settings menu (File > Settings).");

            Forms.EntitySelector entitySelector = new Forms.EntitySelector(loadHelper, type, App.Current.MainWindow);
            entitySelector.ShowDialog();

            if (entitySelector.SelectedEntity != null)
            {
                var loadedFile = await LoadFileAsync(entitySelector.SelectedEntity.EepkPath, true, entitySelector.OnlyLoadFromCPK);


#if !XenoKit
                //Apply namelist
                nameListManager.EepkLoaded(loadedFile);
#endif

                //Cache the file
                if (cacheFile)
                {
                    cacheManager.CacheFile(entitySelector.SelectedEntity.EepkPath, loadedFile, type.ToString());
                }
                else
                {
                    cacheManager.RemoveCachedFile(entitySelector.SelectedEntity.EepkPath);
                }

                loadedFile.LoadedExternalFiles.Clear();
                loadedFile.Directory = string.Format("{0}/data/{1}", SettingsManager.settings.GameDirectory, loadedFile.Directory);

                return loadedFile;
            }

            return null;
        }

        private async Task<EffectContainerFile> LoadFileAsync(string path, bool fromGame, bool onlyFromCpk)
        {
            var controller = await ((MetroWindow)App.Current.MainWindow).ShowProgressAsync($"Loading...", $"", false, new MetroDialogSettings() { DialogTitleFontSize = 16, DialogMessageFontSize = 12, AnimateHide = false, AnimateShow = false });
            controller.SetIndeterminate();

            EffectContainerFile loadedFile = null;

            try
            {
                await Task.Run(() =>
                {
                    if (!fromGame)
                    {
                        //Load files directly
                        if (Path.GetExtension(path) == ".eepk")
                        {
                            loadedFile = EffectContainerFile.Load(path);
                        }
                        else if (Path.GetExtension(path) == EffectContainerFile.ZipExtension)
                        {
                            loadedFile = EffectContainerFile.LoadVfxPackage(path);
                        }
                    }
                    else
                    {
                        //Load from game
                        loadedFile = EffectContainerFile.Load(path, FileManager.Instance.fileIO, onlyFromCpk);
                    }
                });
            }
            finally
            {
                await controller.CloseAsync();
            }

            return loadedFile;
        }


        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedEffectTabChanged?.Invoke(this, EventArgs.Empty);
            if (effectContainerFile == null) return;

            switch ((Tabs)tabControl.SelectedIndex)
            {
                case Tabs.Effect:
                    SearchFilter = effectContainerFile.EffectSearchFilter;
                    break;
                case Tabs.Pbind:
                    SearchFilter = effectContainerFile.Pbind.AssetSearchFilter;
                    PlayAsset(GetSelectedAsset(AssetType.PBIND));
                    break;
                case Tabs.Tbind:
                    SearchFilter = effectContainerFile.Tbind.AssetSearchFilter;
                    PlayAsset(GetSelectedAsset(AssetType.TBIND));
                    break;
                case Tabs.Cbind:
                    SearchFilter = effectContainerFile.Cbind.AssetSearchFilter;
                    PlayAsset(GetSelectedAsset(AssetType.CBIND));
                    break;
                case Tabs.Emo:
                    SearchFilter = effectContainerFile.Emo.AssetSearchFilter;
                    PlayAsset(GetSelectedAsset(AssetType.EMO));
                    break;
                case Tabs.Light:
                    SearchFilter = effectContainerFile.LightEma.AssetSearchFilter;
                    PlayAsset(GetSelectedAsset(AssetType.LIGHT));
                    break;
            }

            e.Handled = true;
        }

        private void EffectPart_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CreateEffectPartViewModel();
            SetEffectPartGizmo();
        }

        private void EffectDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CreateEffectPartViewModel();
            PlaySelectedEffect();
            SetEffectPartGizmo();
        }

        private void CreateEffectPartViewModel()
        {
            if (SelectedEffect?.SelectedEffectPart != null)
            {
                if (EffectPartViewModel != null) EffectPartViewModel.Dispose();

                EffectPartViewModel = new EffectPartViewModel(SelectedEffect?.SelectedEffectPart);
            }
            else if (EffectPartViewModel != null)
            {
                EffectPartViewModel.Dispose();
                EffectPartViewModel = null;
            }

            NotifyPropertyChanged(nameof(EffectPartViewModel));
        }

        public static bool GameDirectoryCheck()
        {
            if (!SettingsManager.settings.ValidGameDir)
            {
                MessagePrompt.Show("Please set the game directory in the settings menu to use this option (File > Settings > Game Directory).", "Invalid Game Directory", MessagePromptButtons.OK, MessagePromptIcon.Information);
                return false;
            }
            return true;
        }

        //Selection
        private void SelectAsset(Asset asset)
        {
            switch (asset.assetType)
            {
                case AssetType.PBIND:
                    pbindDataGrid.SelectedItem = asset;
                    pbindDataGrid.ScrollIntoView(asset);
                    break;
                case AssetType.TBIND:
                    tbindDataGrid.SelectedItem = asset;
                    tbindDataGrid.ScrollIntoView(asset);
                    break;
                case AssetType.CBIND:
                    cbindDataGrid.SelectedItem = asset;
                    cbindDataGrid.ScrollIntoView(asset);
                    break;
                case AssetType.LIGHT:
                    lightDataGrid.SelectedItem = asset;
                    lightDataGrid.ScrollIntoView(asset);
                    break;
                case AssetType.EMO:
                    emoDataGrid.SelectedItem = asset;
                    emoDataGrid.ScrollIntoView(asset);
                    break;
            }
        }
        
        private void SelectEffect(Effect effect)
        {
            effectDataGrid.SelectedItem = effect;
            effectDataGrid.ScrollIntoView(effect);
        }

        #region CachedFiles
        //TODO: Remove this

        public void PBIND_ImportAsset_MenuItem_FromCachedFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem selectedMenuItem = e.OriginalSource as MenuItem;

            if (selectedMenuItem != null)
            {
                CachedFile cachedFile = selectedMenuItem.DataContext as CachedFile;

                if (cachedFile != null)
                {
                    AssetContainer_ImportAssets(effectContainerFile.Pbind, AssetType.PBIND, cachedFile.effectContainerFile);
                }
                else
                {
                    MessagePrompt.Show("There are no cached files.", "From Cached Files", MessagePromptButtons.OK, MessagePromptIcon.Information);
                }
            }
        }

        public void EMO_ImportAsset_MenuItem_FromCachedFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem selectedMenuItem = e.OriginalSource as MenuItem;

            if (selectedMenuItem != null)
            {
                CachedFile cachedFile = selectedMenuItem.DataContext as CachedFile;

                if (cachedFile != null)
                {
                    AssetContainer_ImportAssets(effectContainerFile.Emo, AssetType.EMO, cachedFile.effectContainerFile);
                }
                else
                {
                    MessagePrompt.Show("There are no cached files.", "From Cached Files", MessagePromptButtons.OK, MessagePromptIcon.Information);
                }
            }
        }

        public void TBIND_ImportAsset_MenuItem_FromCachedFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem selectedMenuItem = e.OriginalSource as MenuItem;

            if (selectedMenuItem != null)
            {
                CachedFile cachedFile = selectedMenuItem.DataContext as CachedFile;

                if (cachedFile != null)
                {
                    AssetContainer_ImportAssets(effectContainerFile.Tbind, AssetType.TBIND, cachedFile.effectContainerFile);
                }
                else
                {
                    MessagePrompt.Show("There are no cached files.", "From Cached Files", MessagePromptButtons.OK, MessagePromptIcon.Information);
                }
            }
        }

        public void CBIND_ImportAsset_MenuItem_FromCachedFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem selectedMenuItem = e.OriginalSource as MenuItem;

            if (selectedMenuItem != null)
            {
                CachedFile cachedFile = selectedMenuItem.DataContext as CachedFile;

                if (cachedFile != null)
                {
                    AssetContainer_ImportAssets(effectContainerFile.Cbind, AssetType.CBIND, cachedFile.effectContainerFile);
                }
                else
                {
                    MessagePrompt.Show("There are no cached files.", "From Cached Files", MessagePromptButtons.OK, MessagePromptIcon.Information);
                }
            }
        }

        public void LIGHT_ImportAsset_MenuItem_FromCachedFiles_Click(object sender, RoutedEventArgs e)
        {
            MenuItem selectedMenuItem = e.OriginalSource as MenuItem;

            if (selectedMenuItem != null)
            {
                CachedFile cachedFile = selectedMenuItem.DataContext as CachedFile;

                if (cachedFile != null)
                {
                    AssetContainer_ImportAssets(effectContainerFile.LightEma, AssetType.LIGHT, cachedFile.effectContainerFile);
                }
                else
                {
                    MessagePrompt.Show("There are no cached files.", "From Cached Files", MessagePromptButtons.OK, MessagePromptIcon.Information);
                }
            }
        }

        public void EffectOptions_ImportEffectsFromCache_Click(object sender, RoutedEventArgs e)
        {
            MenuItem selectedMenuItem = e.OriginalSource as MenuItem;

            if (selectedMenuItem != null)
            {
                CachedFile cachedFile = selectedMenuItem.DataContext as CachedFile;

                if (cachedFile != null)
                {
                    if (cachedFile.effectContainerFile != null)
                        EffectOptions_ImportEffects(cachedFile.effectContainerFile.Effects);
                }
                else
                {
                    MessagePrompt.Show("There are no cached files.", "From Cached Files", MessagePromptButtons.OK, MessagePromptIcon.Information);
                }
            }
        }
        #endregion

        #region Forms
        public static EmpEditorWindow PBIND_OpenEditor(EMP_File empFile, AssetContainerTool assetContainer, string name)
        {
            EmpEditorWindow form = GetActiveEmpForm(empFile);

            if (form == null)
            {
                form = new EmpEditorWindow(empFile, assetContainer, name);
                form.Show();
            }

            form.Focus();
            return form;
        }

        public static EtrEditorWindow TBIND_OpenEditor(ETR_File etrFile, AssetContainerTool assetContainer, string name)
        {
            EtrEditorWindow form = GetActiveEtrForm(etrFile);

            if (form == null)
            {
                form = new EtrEditorWindow(etrFile, assetContainer, name);
                form.Show();
            }

            form.Focus();
            return form;
        }

        public static EcfEditorWindow CBIND_OpenEditor(ECF_File ecfFile, string name)
        {
            EcfEditorWindow form = GetActiveEcfForm(ecfFile);

            if (form == null)
            {
                form = new EcfEditorWindow(ecfFile, name);
                form.Show();
            }

            form.Focus();
            return form;
        }

        public static MaterialsEditorForm AssetContainerOpenMaterialForm(AssetContainerTool assetContainer, AssetType assetType)
        {
            var emmFile = assetContainer.File2_Ref;
            MaterialsEditorForm emmForm = GetActiveEmmForm(emmFile);

            if (emmForm == null)
            {
                emmForm = new MaterialsEditorForm(emmFile, assetContainer, assetType, assetType.ToString());
            }
            else
            {
                emmForm.Focus();
            }

            emmForm.Show();

            return emmForm;
        }

        public static EmbEditForm AssetContainerOpenTextureForm(AssetContainerTool assetContainer, AssetType assetType)
        {
            var embFile = assetContainer.File3_Ref;
            EmbEditForm embForm = GetActiveEmbForm(embFile);

            if (embForm == null)
            {
                embForm = new EmbEditForm(embFile, assetContainer, assetType, assetType.ToString());
            }
            else
            {
                embForm.Focus();
            }

            embForm.Show();

            return embForm;
        }
        
        //Helpers
        public static EmbEditForm GetActiveEmbForm(EMB_File _embFile)
        {
            foreach (object window in Application.Current.Windows)
            {
                if (window is EmbEditForm textureViewer)
                {
                    if (textureViewer.EmbFile == _embFile)
                        return textureViewer;
                }
            }

            return null;
        }

        public static MaterialsEditorForm GetActiveEmmForm(EMM_File _emmFile)
        {
            foreach (object window in Application.Current.Windows)
            {
                if (window is MaterialsEditorForm materialsEditor)
                {
                    if (materialsEditor.EmmFile == _emmFile)
                        return materialsEditor;
                }
            }

            return null;
        }

        public static EmpEditorWindow GetActiveEmpForm(EMP_File _empFile)
        {
            foreach (object window in Application.Current.Windows)
            {
                if (window is EmpEditorWindow empEditor)
                {
                    if (empEditor.EmpFile == _empFile)
                        return empEditor;
                }
            }

            return null;
        }

        public static void CloseEmpForm(EMP_File empFile)
        {
            var form = GetActiveEmpForm(empFile);

            if (form != null)
                form.Close();
        }

        public static EcfEditorWindow GetActiveEcfForm(ECF_File _ecfFile)
        {
            foreach (object window in Application.Current.Windows)
            {
                if (window is EcfEditorWindow empEditor)
                {
                    if (empEditor.EcfFile == _ecfFile)
                        return empEditor;
                }
            }

            return null;
        }

        public static void CloseEcfForm(ECF_File _ecfFile)
        {
            var form = GetActiveEcfForm(_ecfFile);

            if (form != null)
                form.Close();
        }

        public static EtrEditorWindow GetActiveEtrForm(ETR_File _etrFile)
        {
            foreach (object window in Application.Current.Windows)
            {
                if (window is EtrEditorWindow etrEditor)
                {
                    if (etrEditor.EtrFile == _etrFile)
                        return etrEditor;
                }
            }

            return null;
        }

        public static void CloseEtrForm(ETR_File _etrFile)
        {
            var form = GetActiveEtrForm(_etrFile);

            if (form != null)
                form.Close();
        }

        public static void CloseAllEditorForms()
        {
            foreach (object window in App.Current.Windows)
            {
                if (window is EmbEditForm ||
                    window is MaterialsEditorForm ||
                    window is EmpEditorWindow ||
                    window is EcfEditorWindow ||
                    window is EtrEditorWindow)
                {
                    (window as Window).Close();
                }
            }
        }

        #endregion

        //Not sure what these are for
        private void MenuItem_MouseMove(object sender, MouseEventArgs e)
        {
            MenuItem menuItem = (MenuItem)sender;
            menuItem.IsSubmenuOpen = true;
        }

        private void ListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled)
            {
                return;
            }
            Control control = sender as Control;
            if (control == null)
            {
                return;
            }
            e.Handled = true;
            var wheelArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent,
                Source = control
            };
            var parent = VisualTreeHelper.GetParent(control) as UIElement;
            //var parent = control.Parent as UIElement;
            parent?.RaiseEvent(wheelArgs);
        }

#if !XenoKit
        private void NameList_Item_Click(object sender, RoutedEventArgs e)
        {
            if (effectContainerFile == null) return;

            MenuItem selectedMenuItem = e.OriginalSource as MenuItem;

            if (selectedMenuItem != null)
            {
                NameList.NameListFile nameList = selectedMenuItem.DataContext as NameList.NameListFile;

                if (nameList != null)
                {
                    nameListManager.ApplyNameList(effectContainerFile.Effects, nameList.GetNameList());
                }
            }
        }

        private void NameList_Clear_Click(object sender, RoutedEventArgs e)
        {
            nameListManager.ClearNameList(effectContainerFile.Effects);
        }

        private void NameList_Save_Click(object sender, RoutedEventArgs e)
        {
            nameListManager.SaveNameList(effectContainerFile.Effects);
        }
#endif

        /// <summary>
        /// Returns the SelectedEffectParts for the first SelectedEffect, if it exists.
        /// </summary>
        /// <returns></returns>
        public ObservableCollection<EffectPart> GetSelectedEffectParts()
        {
            if (SelectedEffect != null)
            {
                return SelectedEffect.SelectedEffectParts;
            }

            return null;
        }

#if XenoKit
        
        private void PbindDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PlayAsset(GetSelectedAsset(AssetType.PBIND));
        }

        private void TbindDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PlayAsset(GetSelectedAsset(AssetType.TBIND));
        }

        private void CbindDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PlayAsset(GetSelectedAsset(AssetType.CBIND));
        }

        private void EmoDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PlayAsset(GetSelectedAsset(AssetType.EMO));
        }

        private void LightDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PlayAsset(GetSelectedAsset(AssetType.LIGHT));
        }

        private void PlayAsset(Asset asset)
        {
            if (Viewport.Instance != null && asset != null)
                Viewport.Instance.VfxPreview.PreviewAsset(asset);
        }

#else
        private void PlayAsset(Asset asset)
        {

        }
#endif


        //TODO: Move these to commands
        private void EMO_AssetContainer_RenameFile_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;

            if (menuItem != null)
            {
                var nestedListBox = ((ContextMenu)menuItem.Parent).PlacementTarget as ListBox;

                var selectedFile = nestedListBox.SelectedItem as EffectFile;

                if (selectedFile != null)
                {
                    var parentAsset = effectContainerFile.Emo.GetAssetByFileInstance(selectedFile);

                    AssetContainer_RenameFile(selectedFile, parentAsset, effectContainerFile.Emo);

                    if (parentAsset != null)
                    {
                        parentAsset.RefreshNamePreview();
                    }

                    emoDataGrid.Items.Refresh();
                    emoDataGrid.SelectedItem = parentAsset;
                    emoDataGrid.ScrollIntoView(parentAsset);
                }
            }
        }

        private void EMO_AssetContainer_ReplaceFile_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;

            if (menuItem != null)
            {
                var nestedListBox = ((ContextMenu)menuItem.Parent).PlacementTarget as ListBox;

                var selectedFile = nestedListBox.SelectedItem as EffectFile;

                if (selectedFile != null)
                {
                    OpenFileDialog openFile = new OpenFileDialog();
                    openFile.Title = "Add file...";
                    openFile.Filter = "XV2 effect files | *.emo; *.ema; *.emm; *.emb";
                    openFile.ShowDialog();

                    if (File.Exists(openFile.FileName) && !String.IsNullOrWhiteSpace(openFile.FileName))
                    {
                        if (EffectFile.GetExtension(openFile.FileName) != selectedFile.Extension)
                        {
                            MessagePrompt.Show(String.Format("The file type of the selected external file ({0}) does not match that of {1}.", openFile.FileName, selectedFile.FullFileName), "Replace", MessagePromptButtons.OK, MessagePromptIcon.Error);
                            return;
                        }

                        string originalNewFileName = Path.GetFileName(openFile.FileName);
                        byte[] bytes = File.ReadAllBytes(openFile.FileName);
                        string newFileName = effectContainerFile.Emo.GetUnusedName(originalNewFileName); //To prevent duplicates

                        object oldFile;

                        switch (selectedFile.fileType)
                        {
                            case EffectFile.FileType.EMM:
                                oldFile = selectedFile.EmmFile;
                                selectedFile.EmmFile = EMM_File.LoadEmm(bytes);
                                UndoManager.Instance.AddUndo(new UndoableProperty<EffectFile>(nameof(EffectFile.EmmFile), selectedFile, oldFile, selectedFile.EmmFile, "Replace File (EMO)"));
                                break;
                            case EffectFile.FileType.EMB:
                                oldFile = selectedFile.EmbFile;
                                selectedFile.EmbFile = EMB_File.LoadEmb(bytes);
                                UndoManager.Instance.AddUndo(new UndoableProperty<EffectFile>(nameof(EffectFile.EmbFile), selectedFile, oldFile, selectedFile.EmbFile, "Replace File (EMO)"));
                                break;
                            default:
                                oldFile = selectedFile.Bytes;
                                selectedFile.Bytes = bytes;
                                UndoManager.Instance.AddUndo(new UndoableProperty<EffectFile>(nameof(EffectFile.Bytes), selectedFile, oldFile, selectedFile.Bytes, "Replace File (EMO)"));
                                break;
                        }

                        if (MessagePrompt.Show("Do you want to keep the old file name?", "Keep Name?", MessagePromptButtons.YesNo, MessagePromptIcon.Question) == MessagePromptResult.No)
                        {
                            selectedFile.SetName(newFileName);

                            if (newFileName != originalNewFileName)
                            {
                                MessagePrompt.Show(String.Format("The added file was renamed to \"{0}\" because \"{1}\" was already used.", newFileName, originalNewFileName), "Add File", MessagePromptButtons.OK, MessagePromptIcon.Information);
                            }
                        }

                        var selectedItem = emoDataGrid.SelectedItem;
                        emoDataGrid.SelectedItem = selectedItem;
                        emoDataGrid.ScrollIntoView(selectedItem);
                    }
                }
            }
        }

        private void EMO_AssetContainer_DeleteFile_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;

            if (menuItem != null)
            {
                var nestedListBox = ((ContextMenu)menuItem.Parent).PlacementTarget as ListBox;

                var selectedFile = nestedListBox.SelectedItem as EffectFile;

                if (selectedFile != null)
                {
                    var parentAsset = effectContainerFile.Emo.GetAssetByFileInstance(selectedFile);

                    List<IUndoRedo> undos = new List<IUndoRedo>();

                    if (parentAsset != null)
                    {
                        if (parentAsset.Files.Count > 1)
                        {
                            parentAsset.RemoveFile(selectedFile, undos);
                        }
                        else
                        {
                            MessagePrompt.Show(string.Format("Cannot delete the last file."), "Error", MessagePromptButtons.OK, MessagePromptIcon.Error);
                        }

                        UndoManager.Instance.AddCompositeUndo(undos, "Delete File (EMO)");

                        emoDataGrid.SelectedItem = parentAsset;
                        emoDataGrid.ScrollIntoView(parentAsset);
                    }
                    else
                    {
                        MessagePrompt.Show(string.Format("Could not find the parent asset."), "Error", MessagePromptButtons.OK, MessagePromptIcon.Error);
                    }
                }
            }
        }

        private void EMO_AssetContainer_ExtractFile_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;

            if (menuItem != null)
            {
                var nestedListBox = ((ContextMenu)menuItem.Parent).PlacementTarget as ListBox;

                var selectedFile = nestedListBox.SelectedItem as EffectFile;

                if (selectedFile != null)
                {
                    SaveFileDialog saveDialog = new SaveFileDialog();
                    saveDialog.Title = "Extract file...";
                    saveDialog.AddExtension = false;
                    saveDialog.Filter = string.Format("{1} File | *{0}", System.IO.Path.GetExtension(selectedFile.Extension), System.IO.Path.GetExtension(selectedFile.Extension).Remove(0, 1).ToUpper());
                    saveDialog.FileName = selectedFile.FullFileName;

                    if (saveDialog.ShowDialog() == true)
                    {
                        File.WriteAllBytes(saveDialog.FileName, selectedFile.GetBytes());
                    }
                }
            }
        }

        private void EMO_AssetContainer_EditFile_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuItem;

            if (menuItem != null)
            {
                var nestedListBox = ((ContextMenu)menuItem.Parent).PlacementTarget as ListBox;

                var selectedFile = nestedListBox.SelectedItem as EffectFile;
                OpenEmoEffectFileEditor(selectedFile, true);
            }
        }

    }
}