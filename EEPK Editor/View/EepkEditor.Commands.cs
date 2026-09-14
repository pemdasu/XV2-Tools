using CommunityToolkit.Mvvm.Input;
using EEPK_Organiser.Forms;
using EEPK_Organiser.Forms.Editors;
using EEPK_Organiser.Misc;
using EEPK_Organiser.ViewModel;
using LB_Common.Forms;
using LB_Common.Mvvm;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Xv2CoreLib;
using Xv2CoreLib.ECF;
using Xv2CoreLib.EEPK;
using Xv2CoreLib.EffectContainer;
using Xv2CoreLib.EMA;
using Xv2CoreLib.EMB_CLASS;
using Xv2CoreLib.EMD;
using Xv2CoreLib.EMM;
using Xv2CoreLib.EMO;
using Xv2CoreLib.EMP_NEW;
using Xv2CoreLib.ESK;
using Xv2CoreLib.ETR;
using Xv2CoreLib.Resource;
using Xv2CoreLib.Resource.App;
using Xv2CoreLib.Resource.UndoRedo;
using Application = System.Windows.Application;

namespace EEPK_Organiser.View
{
    public partial class EepkEditor : AutoObservableUserControl
    {

        #region XenoKit

        private void SetEffectPartGizmo()
        {
#if XenoKit
            if (Viewport.Instance == null) return;

            EffectPart effectPart = SelectedEffect?.SelectedEffectPart;

            if (effectPart != null)
            {
                Viewport.Instance.EffectPartGizmo.SetContext(effectPart);
            }
            else
            {
                Viewport.Instance.EffectPartGizmo.RemoveContext();
            }
#endif
        }

        //Effect_Play_Command
        [RelayCommand(CanExecute = nameof(IsEffectSelected))]
        private void PlaySelectedEffect()
        {
#if XenoKit
            if (SelectedEffect != null && Viewport.Instance != null)
            {
                Viewport.Instance.VfxPreview.PreviewEffect(SelectedEffect);
            }
#endif
        }
        #endregion

        #region ToolMenu
        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void ToolMenu_HueAdjust()
        {
            RecolorAll recolor = new RecolorAll(effectContainerFile, false, App.Current.MainWindow);

            if (recolor.Initialize())
                recolor.ShowDialog();
        }

        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void ToolMenu_HueSet()
        {
            RecolorAll recolor = new RecolorAll(effectContainerFile, true, App.Current.MainWindow);

            if (recolor.Initialize())
                recolor.ShowDialog();
        }

        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void ToolMenu_CreateSuperTexture()
        {
            if (MessagePrompt.Show($"This feature will attempt to optimize the number of textures used by this EEPK by combining them together. The result will be fewer, but larger individual textures. This should significantly increase the amount of textures that can be used.\n\nIt is advised to make backups of your files before using this feature.",
                "Optimize Textures (SuperTexture)", MessagePromptButtons.YesNo, MessagePromptIcon.Question) == MessagePromptResult.Yes)
            {
                int[] ret = effectContainerFile.MergeAllTexturesIntoSuperTextures_PBIND();

                MessagePrompt.Show($"{ret[0]} textures were merged together to create {ret[1]} Super Textures.", "Optimize Textures (SuperTexture)", MessagePromptButtons.OK, MessagePromptIcon.Information);

            }
        }

        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void ToolMenu_CleanAll()
        {
            if (MessagePrompt.Show($"Delete all unused or duplicate assets, texture and material from this {effectContainerFile.saveFormat}?", "Clean All",
                MessagePromptButtons.YesNo, MessagePromptIcon.Question) == MessagePromptResult.Yes)
            {
                List<IUndoRedo> undos = new List<IUndoRedo>();
                int[] totals = effectContainerFile.RemoveAllUnusedOrDuplicates(undos);

                int emp = totals[0];
                int etr = totals[1];
                int ecf = totals[2];
                int emo = totals[3];
                int light = totals[4];
                int empTextures = totals[5];
                int textures = totals[6];
                int materials = totals[7];
                int total = totals[8];

                UndoManager.Instance.AddCompositeUndo(undos, $"Clean All ({total})");
                MessagePrompt.Show($"{total} duplicate or unused references were purged.\n\nBreakdown by type:\nEMP: {emp}\nETR: {etr}\nECF: {ecf}\nEMO: {emo}\nLIGHT: {light}\nEMP Textures: {empTextures}\nTextures: {textures}\nMaterials: {materials}", "Clean All");
            }
        }

        public void ExportVfxPackage(IList<Effect> selEffects = null)
        {
            if (effectContainerFile == null) return;

            //if selEffects is null, then pass in all the effects.
            IList<Effect> effects = (selEffects != null) ? selEffects : effectContainerFile.Effects;

            EffectSelector effectSelector = new EffectSelector(effects, effectContainerFile, App.Current.MainWindow, EffectSelector.Mode.ExportEffect);
            effectSelector.ShowDialog();

            if (effectSelector.SelectedEffects != null)
            {
                EffectContainerFile vfxPackage = EffectContainerFile.New();
                vfxPackage.saveFormat = SaveFormat.VfxPackage;

                foreach (var effect in effectSelector.SelectedEffects)
                {
                    var newEffect = effect.Clone();
                    newEffect.IndexNum = effect.ImportIdIncrease;
                    vfxPackage.AddEffect(newEffect);
                }

                //Get path to save to
                SaveFileDialog saveDialog = new SaveFileDialog();
                saveDialog.Title = "Export to .vfxpackage";
                saveDialog.Filter = string.Format("{1} File |*{0}", EffectContainerFile.ZipExtension, EffectContainerFile.ZipExtension.ToUpper().Remove(0, 1));
                saveDialog.AddExtension = true;

                if (saveDialog.ShowDialog() == true)
                {
                    vfxPackage.Directory = string.Format("{0}/{1}", Path.GetDirectoryName(saveDialog.FileName), Path.GetFileNameWithoutExtension(saveDialog.FileName));
                    vfxPackage.Name = Path.GetFileNameWithoutExtension(saveDialog.FileName);
                    vfxPackage.SaveVfxPackage();

                    MessagePrompt.Show("Export successful.", "Export", MessagePromptButtons.OK, MessagePromptIcon.Information);
                }

            }
        }

        #endregion

        #region Search
        [RelayCommand]
        private void ClearSearch()
        {
            if (effectContainerFile == null) return;

            SearchFilter = string.Empty;

            switch ((Tabs)tabControl.SelectedIndex)
            {
                case Tabs.Effect:
                    effectContainerFile.EffectSearchFilter = string.Empty;
                    effectContainerFile.UpdateEffectFilter();
                    break;
                case Tabs.Pbind:
                    effectContainerFile.Pbind.AssetSearchFilter = string.Empty;
                    effectContainerFile.Pbind.UpdateAssetFilter();
                    break;
                case Tabs.Tbind:
                    effectContainerFile.Tbind.AssetSearchFilter = string.Empty;
                    effectContainerFile.Tbind.UpdateAssetFilter();
                    break;
                case Tabs.Cbind:
                    effectContainerFile.Cbind.AssetSearchFilter = string.Empty;
                    effectContainerFile.Cbind.UpdateAssetFilter();
                    break;
                case Tabs.Emo:
                    effectContainerFile.Emo.AssetSearchFilter = string.Empty;
                    effectContainerFile.Emo.UpdateAssetFilter();
                    break;
                case Tabs.Light:
                    effectContainerFile.LightEma.AssetSearchFilter = string.Empty;
                    effectContainerFile.LightEma.UpdateAssetFilter();
                    break;
            }
        }

        [RelayCommand]
        private void Search()
        {
            if (effectContainerFile == null) return;

            switch ((Tabs)tabControl.SelectedIndex)
            {
                case Tabs.Effect:
                    effectContainerFile.NewEffectFilter(SearchFilter);
                    effectContainerFile.UpdateEffectFilter();
                    break;
                case Tabs.Pbind:
                    effectContainerFile.Pbind.NewAssetFilter(SearchFilter);
                    effectContainerFile.Pbind.UpdateAssetFilter();
                    break;
                case Tabs.Tbind:
                    effectContainerFile.Tbind.NewAssetFilter(SearchFilter);
                    effectContainerFile.Tbind.UpdateAssetFilter();
                    break;
                case Tabs.Cbind:
                    effectContainerFile.Cbind.NewAssetFilter(SearchFilter);
                    effectContainerFile.Cbind.UpdateAssetFilter();
                    break;
                case Tabs.Emo:
                    effectContainerFile.Emo.NewAssetFilter(SearchFilter);
                    effectContainerFile.Emo.UpdateAssetFilter();
                    break;
                case Tabs.Light:
                    effectContainerFile.LightEma.NewAssetFilter(SearchFilter);
                    effectContainerFile.LightEma.UpdateAssetFilter();
                    break;
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Search();
        }

        #endregion

        #region Effect

        [RelayCommand(CanExecute = nameof(IsEffectSelected))]
        private void Effect_AddEffectPart()
        {
            var selectedEffect = effectDataGrid.SelectedItem as Effect;

            if (selectedEffect != null)
            {
                if (selectedEffect.EffectParts == null)
                    selectedEffect.EffectParts = new AsyncObservableCollection<EffectPart>();

                var newEffectPart = EffectPart.NewEffectPart();
                selectedEffect.EffectParts.Add(newEffectPart);

                UndoManager.Instance.AddUndo(new UndoableListAdd<EffectPart>(selectedEffect.EffectParts, newEffectPart, "New EffectPart"));
            }
        }

        [RelayCommand(CanExecute = nameof(IsEffectSelected))]
        private void Effect_Copy()
        {
            var selectedEffects = effectDataGrid.SelectedItems.Cast<Effect>().ToList();

            if (selectedEffects.Count > 0)
            {
                effectContainerFile.SaveDds();
                Clipboard.SetData(ClipboardDataTypes.Effect, selectedEffects);
            }
        }

        [RelayCommand(CanExecute = nameof(CanPasteEffect))]
        private void Effect_Paste()
        {
            List<Effect> effects = (List<Effect>)Clipboard.GetData(ClipboardDataTypes.Effect);

            if (effects != null)
            {
                //Add effects
                EffectOptions_ImportEffects(new AsyncObservableCollection<Effect>(effects));
            }
        }

        [RelayCommand(CanExecute = nameof(IsEffectSelected))]
        private void Effect_DeleteEffect()
        {
            var selectedEffects = effectDataGrid.SelectedItems.Cast<Effect>().ToList();

            if (selectedEffects.Count > 0)
            {
                List<IUndoRedo> undos = new List<IUndoRedo>();

                foreach (var _effect in selectedEffects)
                {
                    undos.Add(new UndoableListRemove<Effect>(effectContainerFile.Effects, _effect));
                    effectContainerFile.Effects.Remove(_effect);
                }

                UndoManager.Instance.AddUndo(new CompositeUndo(undos, "Delete Effects"));
            }
        }

        [RelayCommand(CanExecute = nameof(IsEffectSelected))]
        private void Effect_Duplicate()
        {
            var selectedEffect = effectDataGrid.SelectedItem as Effect;

            if (selectedEffect != null)
            {
                var copiedEffect = selectedEffect.Clone();
                copiedEffect.IndexNum = effectContainerFile.GetUnusedEffectId(copiedEffect.IndexNum);
                effectContainerFile.Effects.Add(copiedEffect);
                SelectEffect(copiedEffect);

                UndoManager.Instance.AddUndo(new UndoableListAdd<Effect>(effectContainerFile.Effects, copiedEffect, "Duplicate Effect"));
#if XenoKit
                Log.Add("Effect duplicated with ID: " + copiedEffect.IndexNum);
#endif
            }
        }

        [RelayCommand(CanExecute = nameof(CanPasteEffectPart))]
        private void EffectPart_Paste()
        {
            if (SelectedEffect != null)
            {
                ObservableCollection<EffectPart> effectParts = (ObservableCollection<EffectPart>)Clipboard.GetData(ClipboardDataTypes.EffectPart);

                if (effectParts != null)
                {
                    List<IUndoRedo> undos = new List<IUndoRedo>();

                    try
                    {
                        foreach (var effectPart in effectParts)
                        {
                            if (effectPart.AssetRef != null)
                                effectPart.AssetRef = effectContainerFile.AddAsset(effectPart.AssetRef, effectPart.AssetType, undos);

                            var newEffectPart = effectPart.Clone();
                            SelectedEffect.EffectParts.Add(newEffectPart);

                            undos.Add(new UndoableListAdd<EffectPart>(SelectedEffect.EffectParts, newEffectPart));
                        }
                    }
                    finally
                    {
                        if (undos.Count > 0)
                            UndoManager.Instance.AddUndo(new CompositeUndo(undos, (effectParts.Count > 1) ? "Paste EffectParts" : "Paste EffectPart"));
                    }
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsEffectPartSelected))]
        private void EffectPart_Copy()
        {
            if (SelectedEffect != null)
            {
                if (SelectedEffect.SelectedEffectParts != null)
                {
                    effectContainerFile.SaveDds();
                    Clipboard.SetData(ClipboardDataTypes.EffectPart, SelectedEffect.SelectedEffectParts);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsEffectPartSelected))]
        private void EffectPart_Delete()
        {
            var selectedEffect = SelectedEffect;

            if (selectedEffect != null)
            {
                if (selectedEffect.SelectedEffectParts != null)
                {
                    List<EffectPart> effectPartsToRemove = selectedEffect.SelectedEffectParts.ToList();
                    List<IUndoRedo> undos = new List<IUndoRedo>();

                    foreach (var effectPart in effectPartsToRemove)
                    {
                        undos.Add(new UndoableListRemove<EffectPart>(selectedEffect.EffectParts, effectPart));
                        selectedEffect.EffectParts.Remove(effectPart);
                    }

                    UndoManager.Instance.AddUndo(new CompositeUndo(undos, (selectedEffect.SelectedEffectParts.Count > 1) ? "Delete EffectParts" : "Delete EffectPart"));
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsEffectPartSelected))]
        private void EffectPart_Duplicate()
        {
            var selectedEffect = SelectedEffect;

            if (selectedEffect != null)
            {
                if (selectedEffect.SelectedEffectParts != null)
                {
                    List<IUndoRedo> undos = new List<IUndoRedo>();

                    foreach (var effectPart in selectedEffect.SelectedEffectParts)
                    {
                        var clone = effectPart.Clone();
                        selectedEffect.EffectParts.Add(clone);
                        undos.Add(new UndoableListAdd<EffectPart>(selectedEffect.EffectParts, clone));
                    }

                    UndoManager.Instance.AddCompositeUndo(undos, "EffectPart Duplicate");
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanGoToAsset))]
        private void EffectPart_GoToAsset()
        {
            var selectedEffectPart = GetSelectedEffectParts();

            if (selectedEffectPart != null)
            {
                if (selectedEffectPart.Count > 0)
                {
                    switch (selectedEffectPart[0].AssetType)
                    {
                        case AssetType.PBIND:
                            tabControl.SelectedIndex = (int)Tabs.Pbind;
                            pbindDataGrid.SelectedItem = selectedEffectPart[0].AssetRef;
                            pbindDataGrid.ScrollIntoView(selectedEffectPart[0].AssetRef);
                            break;
                        case AssetType.TBIND:
                            tabControl.SelectedIndex = (int)Tabs.Tbind;
                            tbindDataGrid.SelectedItem = selectedEffectPart[0].AssetRef;
                            tbindDataGrid.ScrollIntoView(selectedEffectPart[0].AssetRef);
                            break;
                        case AssetType.CBIND:
                            tabControl.SelectedIndex = (int)Tabs.Cbind;
                            cbindDataGrid.SelectedItem = selectedEffectPart[0].AssetRef;
                            cbindDataGrid.ScrollIntoView(selectedEffectPart[0].AssetRef);
                            break;
                        case AssetType.EMO:
                            tabControl.SelectedIndex = (int)Tabs.Emo;
                            emoDataGrid.SelectedItem = selectedEffectPart[0].AssetRef;
                            emoDataGrid.ScrollIntoView(selectedEffectPart[0].AssetRef);
                            break;
                        case AssetType.LIGHT:
                            tabControl.SelectedIndex = (int)Tabs.Light;
                            lightDataGrid.SelectedItem = selectedEffectPart[0].AssetRef;
                            lightDataGrid.ScrollIntoView(selectedEffectPart[0].AssetRef);
                            break;
                    }
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsEffectPartSelected))]
        private void EffectPart_ChangeAsset()
        {
            if (SelectedEffect != null)
            {
                if (SelectedEffect.SelectedEffectPart != null)
                {
                    Forms.AssetSelector assetSel = new Forms.AssetSelector(effectContainerFile, false, false, SelectedEffect.SelectedEffectPart.AssetType, this, SelectedEffect.SelectedEffectPart.AssetRef);
                    assetSel.ShowDialog();

                    if (assetSel.SelectedAsset != null)
                    {
                        List<IUndoRedo> undos = new List<IUndoRedo>();

                        foreach (var effectPart in SelectedEffect.SelectedEffectParts)
                        {
                            undos.Add(new UndoableProperty<EffectPart>(nameof(EffectPart.AssetType), effectPart, effectPart.AssetType, assetSel.SelectedAssetType));
                            undos.Add(new UndoableProperty<EffectPart>(nameof(EffectPart.AssetRef), effectPart, effectPart.AssetRef, assetSel.SelectedAsset));

                            effectPart.AssetType = assetSel.SelectedAssetType;
                            effectPart.AssetRef = assetSel.SelectedAsset;
                        }

                        UndoManager.Instance.AddUndo(new CompositeUndo(undos, "Change Asset(s)"));

                        if (EffectPartViewModel != null)
                            EffectPartViewModel.UpdateProperties();
                    }
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsEffectPartSelected))]
        private void EffectPart_Rescale()
        {
            if (SelectedEffect != null)
            {
                if (SelectedEffect.SelectedEffectParts != null)
                {
                    LB_Common.Forms.NumericInput inputForm = new LB_Common.Forms.NumericInput("Rescale", "Rescale Factor", 1f, 0, 50, 0.1, null, "The minimum and maximum sizes of the selected Effect Parts will be rescaled by the entered factor.");
                    inputForm.ShowDialog();

                    if (!inputForm.IsCancelled)
                    {
                        float scaleFactor = inputForm.GetValue<float>();

                        List<IUndoRedo> undos = new List<IUndoRedo>();

                        foreach (var effectPart in SelectedEffect.SelectedEffectParts)
                        {
                            float size1 = effectPart.ScaleMin * scaleFactor;
                            float size2 = effectPart.ScaleMax * scaleFactor;

                            undos.Add(new UndoableProperty<EffectPart>(nameof(EffectPart.ScaleMin), effectPart, effectPart.ScaleMin, size1));
                            undos.Add(new UndoableProperty<EffectPart>(nameof(EffectPart.ScaleMax), effectPart, effectPart.ScaleMax, size2));

                            effectPart.ScaleMin = size1;
                            effectPart.ScaleMax = size2;
                        }

                        UndoManager.Instance.AddCompositeUndo(undos, "Rescale EffectPart");
                        EffectPartViewModel?.UpdateProperties();
                    }
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanPasteEffectPartValues))]
        private void EffectPart_PasteValues()
        {
            if (SelectedEffect == null) return;
            if (SelectedEffect.SelectedEffectPart == null) return;

            ObservableCollection<EffectPart> effectParts = (ObservableCollection<EffectPart>)Clipboard.GetData(ClipboardDataTypes.EffectPart);

            if (effectParts == null) return;
            if (effectParts.Count != 1) return;

            List<IUndoRedo> undos = new List<IUndoRedo>();

            effectParts[0].AssetRef = effectContainerFile.AddAsset(effectParts[0].AssetRef, effectParts[0].AssetType, undos);
            SelectedEffect.SelectedEffectPart.CopyValues(effectParts[0], undos);

            UndoManager.Instance.AddUndo(new CompositeUndo(undos, "Paste Values"));

            if (EffectPartViewModel != null)
                EffectPartViewModel.UpdateProperties();
        }

        [RelayCommand]
        private void EffectPart_DoubleClick()
        {
            if (!CanGoToAsset()) return;

            if (Keyboard.IsKeyDown(Key.LeftAlt))
            {
                EffectPart_OpenEditor();
            }
            else
            {
                EffectPart_GoToAsset();
            }
        }

        private void EffectPartListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            //MouseBinding isn't working, so as a quick fix I'm using an event
            if (e.LeftButton == MouseButtonState.Pressed)
                EffectPart_DoubleClick();
        }

        //EffectPart_EditAsset_Command
        [RelayCommand(CanExecute = nameof(CanEditAsset))]
        private void EffectPart_OpenEditor()
        {
            ObservableCollection<EffectPart> selectedEffectPart = GetSelectedEffectParts();

            if (selectedEffectPart != null)
            {
                if (selectedEffectPart.Count > 0)
                {
                    switch (selectedEffectPart[0].AssetType)
                    {
                        case AssetType.PBIND:
                            PBIND_OpenEditor(selectedEffectPart[0].AssetRef.Files[0].EmpFile, effectContainerFile.Pbind, selectedEffectPart[0].AssetRef.Files[0].FileName);
                            break;
                        case AssetType.TBIND:
                            TBIND_OpenEditor(selectedEffectPart[0].AssetRef.Files[0].EtrFile, effectContainerFile.Tbind, selectedEffectPart[0].AssetRef.Files[0].FileName);
                            break;
                        case AssetType.CBIND:
                            CBIND_OpenEditor(selectedEffectPart[0].AssetRef.Files[0].EcfFile, selectedEffectPart[0].AssetRef.Files[0].FileName);
                            break;
                    }
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsEffectSelected))]
        private void Effect_Export()
        {
            var selectedEffects = effectDataGrid.SelectedItems.Cast<Effect>().ToList();

            if (selectedEffects.Count > 0)
            {
                ExportVfxPackage(selectedEffects);
            }
        }

        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void EffectNew()
        {
            Effect newEffect = new Effect();
            newEffect.IndexNum = effectContainerFile.GetUnusedEffectId(0);
            List<IUndoRedo> undos = effectContainerFile.AddEffect(newEffect, false, true);
            UndoManager.Instance.AddUndo(new CompositeUndo(undos, "New Effect"));

            SelectEffect(newEffect);

#if XenoKit
            Log.Add("Effect added with ID: " + newEffect.IndexNum);
#endif
        }


        //CanExecutes
        public bool CanGoToAsset()
        {
            if (IsEffectPartSelected())
            {
                return SelectedEffect.SelectedEffectPart.AssetRef != null;
            }
            return false;
        }

        public bool CanEditAsset()
        {
            if (IsEffectPartSelected())
            {
                return SelectedEffect.SelectedEffectPart.AssetType == AssetType.CBIND || SelectedEffect.SelectedEffectPart.AssetType == AssetType.TBIND || SelectedEffect.SelectedEffectPart.AssetType == AssetType.PBIND;
            }
            return false;
        }

        public bool IsEffectPartSelected()
        {
            return SelectedEffect?.SelectedEffectPart != null;
        }

        public bool IsEffectSelected()
        {
            return effectDataGrid.SelectedItem is Xv2CoreLib.EEPK.Effect;
        }

        public bool CanPasteEffect()
        {
            return Clipboard.ContainsData(ClipboardDataTypes.Effect);
        }

        public bool CanPasteEffectPart()
        {
            return Clipboard.ContainsData(ClipboardDataTypes.EffectPart);
        }

        public bool CanPasteEffectPartValues()
        {
            return (IsEffectPartSelected()) ? Clipboard.ContainsData(ClipboardDataTypes.EffectPart) : false;
        }

        #endregion

        #region EffectName

        [RelayCommand(CanExecute = nameof(CanRenameEffect))]
        private void EffectRenameBegin()
        {
            nameListColumn.IsReadOnly = false;
            effectDataGrid.CurrentColumn = nameListColumn;
            effectDataGrid.BeginEdit();
        }

        [RelayCommand]
        private void EffectRenameCommit()
        {
            if (!IsRenameInProgress()) return;
            nameListColumn.IsReadOnly = true;
            effectDataGrid.CurrentColumn = nameListColumn;
            effectDataGrid.CommitEdit();
        }

        private void EffectDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column == nameListColumn)
            {
                nameListColumn.IsReadOnly = true;
            }
        }

        private bool CanRenameEffect()
        {
            return nameListColumn.IsReadOnly && IsEffectSelected();
        }

        private bool IsRenameInProgress()
        {
            return !nameListColumn.IsReadOnly && IsEffectSelected();
        }
        #endregion

        #region ImportEffect
        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
		public async Task ImportEffectsFromFile()
		{
			var importEffectFile = await LoadEffectContainerFile();
			if (importEffectFile != null)
				EffectOptions_ImportEffects(importEffectFile.Effects);
        }

        [RelayCommand(CanExecute = nameof(CanImportFromGame))]
        public async Task ImportEffectsFromGame(EntitySelector.EntityType type)
        {
            var importEffectFile = await LoadEepkFromGame(type);
            if (importEffectFile != null)
                EffectOptions_ImportEffects(importEffectFile.Effects);
        }

        private void EffectOptions_ImportEffects(AsyncObservableCollection<Effect> effects)
        {
            if (effects != null)
            {
                EffectSelector effectSelector = new EffectSelector(effects, effectContainerFile, Application.Current.MainWindow);
                effectSelector.ShowDialog();
                List<IUndoRedo> undos = new List<IUndoRedo>();

                try
                {
                    if (effectSelector.SelectedEffects != null)
                    {
                        foreach (var effect in effectSelector.SelectedEffects)
                        {
                            var newEffect = effect.Clone();
                            newEffect.IndexNum = effect.ImportIdIncrease;
                            undos.AddRange(effectContainerFile.AddEffect(newEffect, true));
                            undos.Add(new UndoableListAdd<Effect>(effectContainerFile.Effects, newEffect));
                        }

                        //Update UI
                        if (effectSelector.SelectedEffects.Count > 0)
                        {
                            SelectEffect(effectSelector.SelectedEffects[0]);
                            effectContainerFile.UpdateEffectFilter();
                            undos.Add(new UndoActionDelegate(effectContainerFile, nameof(effectContainerFile.UpdateAllFilters), true));
                        }
                    }
                }
                finally
                {
                    if (undos.Count > 0)
                        UndoManager.Instance.AddUndo(new CompositeUndo(undos, "Import Effects"));
                }
            }
        }

        private bool CanImportFromGame()
        {
            return GameDirectoryCheck() && IsFileLoaded;
        }
        #endregion

        #region ImportAsset
        [RelayCommand(CanExecute = nameof(CanImportFromGame))]
        private async Task ImportPbindAssetsFromGame(EntitySelector.EntityType fromType)
        {
            await ImportAssetsFromGame(AssetType.PBIND, fromType);
        }

        [RelayCommand(CanExecute = nameof(CanImportFromGame))]
        private async Task ImportTbindAssetsFromGame(EntitySelector.EntityType fromType)
        {
            await ImportAssetsFromGame(AssetType.TBIND, fromType);
        }

        [RelayCommand(CanExecute = nameof(CanImportFromGame))]
        private async Task ImportCbindAssetsFromGame(EntitySelector.EntityType fromType)
        {
            await ImportAssetsFromGame(AssetType.CBIND, fromType);
        }

        [RelayCommand(CanExecute = nameof(CanImportFromGame))]
        private async Task ImportEmoAssetsFromGame(EntitySelector.EntityType fromType)
        {
            await ImportAssetsFromGame(AssetType.EMO, fromType);
        }

        [RelayCommand(CanExecute = nameof(CanImportFromGame))]
        private async Task ImportLightAssetsFromGame(EntitySelector.EntityType fromType)
        {
            await ImportAssetsFromGame(AssetType.LIGHT, fromType);
        }

        private async Task ImportAssetsFromGame(AssetType assetType, EntitySelector.EntityType fromType)
        {
            var effectFile = await LoadEepkFromGame(fromType);

            switch (assetType)
            {
                case AssetType.EMO:
                    await AssetContainer_ImportAssets(effectContainerFile.Emo, AssetType.EMO, effectFile);
                    break;
                case AssetType.PBIND:
                    await AssetContainer_ImportAssets(effectContainerFile.Pbind, AssetType.PBIND, effectFile);
                    break;
                case AssetType.TBIND:
                    await AssetContainer_ImportAssets(effectContainerFile.Tbind, AssetType.TBIND, effectFile);
                    break;
                case AssetType.CBIND:
                    await AssetContainer_ImportAssets(effectContainerFile.Cbind, AssetType.CBIND, effectFile);
                    break;
                case AssetType.LIGHT:
                    await AssetContainer_ImportAssets(effectContainerFile.LightEma, AssetType.LIGHT, effectFile);
                    break;
            }
        }

        private async Task AssetContainer_ImportAssets(AssetContainerTool container, AssetType type, EffectContainerFile importFile = null)
        {
            importFile ??= await LoadEffectContainerFile();
            
            List<IUndoRedo> undos = new List<IUndoRedo>();
            int renameCount = 0;
            int alreadyExistCount = 0;
            int addedCount = 0;

            if (importFile != null)
            {
                AssetSelector assetSelector = new AssetSelector(importFile, true, true, type, this);
                assetSelector.ShowDialog();

                if (assetSelector.SelectedAssets != null)
                {
                    var controller = await ((MetroWindow)App.Current.MainWindow).ShowProgressAsync($"Initializing...", $"", false, new MetroDialogSettings() { AnimateHide = false, AnimateShow = false, DialogTitleFontSize = 15, DialogMessageFontSize = 12 });
                    controller.Minimum = 0;
                    controller.Maximum = assetSelector.SelectedAssets.Count;
                    controller.SetMessage(String.Format("Importing assets: 0 of {0}.", assetSelector.SelectedAssets.Count));

                    try
                    {
                        await Task.Run(() =>
                        {
                            foreach (var newAsset in assetSelector.SelectedAssets)
                            {
                                Asset existingAsset = container.AssetExists(newAsset);

                                if (existingAsset == null)
                                {
                                    //First, regenerate the names
                                    foreach (var newFile in newAsset.Files)
                                    {
                                        newFile.SetName(container.GetUnusedName(newFile.FullFileName));

                                        if (newFile.FullFileName != newFile.OriginalFileName)
                                        {
                                            renameCount++;
                                        }

                                        try
                                        {
                                            switch (type)
                                            {
                                                case AssetType.PBIND:
                                                    container.AddPbindDependencies(newFile.EmpFile, undos);
                                                    break;
                                                case AssetType.TBIND:
                                                    container.AddTbindDependencies(newFile.EtrFile, undos);
                                                    break;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            //There was an overflow of textures
                                            //Since we haven't added the new asset to the main list yet we dont need to revert anything.
                                            throw new InvalidOperationException(String.Format("{0}\n\nThe asset was not imported.", ex.Message));
                                        }
                                    }

                                    newAsset.RefreshNamePreview();

                                    container.AddAsset(newAsset);
                                    undos.Add(new UndoableListAdd<Asset>(container.Assets, newAsset));

                                    controller.SetProgress(addedCount);
                                    controller.SetMessage(String.Format("Importing assets: {0} of {1}.", addedCount, assetSelector.SelectedAssets.Count));

                                    addedCount++;
                                }
                                else
                                {
                                    alreadyExistCount++;
                                }
                            }


                        });


                        await controller.CloseAsync();
                    }
                    catch (Exception ex)
                    {
                        await controller.CloseAsync();
                        ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    }

                }

                if (alreadyExistCount > 0)
                {
                    MessagePrompt.Show(String.Format("{0} assets were skipped because they already exist in this EEPK.\n\nHint: Use the duplicate function if that is what you want.", alreadyExistCount), "Add Asset(s)", MessagePromptButtons.OK, MessagePromptIcon.Warning);
                }

                if (renameCount > 0 && addedCount > 0)
                {
                    MessagePrompt.Show(String.Format("Assets imported.\n\nNote: {0} files were renamed during the add process due to existing file(s) having the same name.", renameCount), "Add Asset(s)", MessagePromptButtons.OK, MessagePromptIcon.Information);
                }
                else if (addedCount > 0)
                {
                    MessagePrompt.Show(String.Format("Assets imported."), "Add Asset(s)", MessagePromptButtons.OK, MessagePromptIcon.Information);
                }
                else
                {
                    MessagePrompt.Show("Operation aborted.", "Add Asset(s)", MessagePromptButtons.OK, MessagePromptIcon.Warning);
                }

                //Add Undos
                if (addedCount > 0)
                {
                    undos.Add(new UndoActionDelegate(container, nameof(container.UpdateAssetFilter), true));
                    UndoManager.Instance.AddUndo(new CompositeUndo(undos, "Import Asset"));
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private async Task ImportAssetsFromExternalFile(AssetType assetType)
        {
            switch ((AssetType)assetType)
            {
                case AssetType.EMO:
                    await AssetContainer_ImportAssets(effectContainerFile.Emo, AssetType.EMO);
                    break;
                case AssetType.PBIND:
                    await AssetContainer_ImportAssets(effectContainerFile.Pbind, AssetType.PBIND);
                    break;
                case AssetType.TBIND:
                    await AssetContainer_ImportAssets(effectContainerFile.Tbind, AssetType.TBIND);
                    break;
                case AssetType.CBIND:
                    await AssetContainer_ImportAssets(effectContainerFile.Cbind, AssetType.CBIND);
                    break;
                case AssetType.LIGHT:
                    await AssetContainer_ImportAssets(effectContainerFile.LightEma, AssetType.LIGHT);
                    break;
            }
        }
        #endregion

        #region Asset

        [RelayCommand(CanExecute = nameof(CanMergeAssets))]
        private void AssetMerge(AssetType assetType)
        {
            Asset primeAsset = GetSelectedAsset(assetType);
            List<Asset> selectedAssets = GetSelectedAssets(assetType);
            selectedAssets.Remove(primeAsset);
            AssetContainerTool container = effectContainerFile.GetAssetContainer(assetType);

            if (primeAsset != null && selectedAssets.Count > 0)
            {
                List<IUndoRedo> undos = new List<IUndoRedo>();

                int count = selectedAssets.Count + 1;

                if (MessagePrompt.Show(string.Format("All currently selected assets will be MERGED into {0}.\n\nAll other selected assets will be deleted, with all references to them changed to {0}.\n\nDo you wish to continue?", primeAsset.FileNamesPreview), string.Format("Merge ({0} assets)", count), MessagePromptButtons.OKCancel, MessagePromptIcon.Question) == MessagePromptResult.OK)
                {
                    foreach (var assetToRemove in selectedAssets)
                    {
                        effectContainerFile.AssetRefRefactor(assetToRemove, primeAsset, undos);
                        undos.Add(new UndoableListRemove<Asset>(container.Assets, assetToRemove));
                        container.Assets.Remove(assetToRemove);
                    }
                }

                UndoManager.Instance.AddUndo(new CompositeUndo(undos, "Merge"));
            }
            else
            {
                MessagePrompt.Show("Cannot merge with less than 2 assets selected.\n\nTip: Use Left Ctrl + Left Mouse Click to multi-select.", "Merge", MessagePromptButtons.OK, MessagePromptIcon.Warning);
            }
        }

        [RelayCommand(CanExecute = nameof(IsAssetTypeSelected))]
        private void AssetDelete(AssetType assetType)
        {
            List<Asset> assets = GetSelectedAssets(assetType);

            if (assets.Count > 0)
            {
                AssetContainerTool container = effectContainerFile.GetAssetContainer(assetType);

                List<IUndoRedo> undos = new List<IUndoRedo>();

                if (MessagePrompt.Show(string.Format("{0} asset(s) will be deleted. Any EffectParts that are linked to them will also be deleted.\n\nDo you want to continue?", assets.Count), "Delete Asset(s)", MessagePromptButtons.OKCancel, MessagePromptIcon.Question) == MessagePromptResult.OK)
                {
                    foreach (var asset in assets)
                    {
                        effectContainerFile.AssetRefRefactor(asset, null, undos); //Remove references to this asset
                        undos.Add(new UndoableListRemove<Asset>(container.Assets, asset));
                        container.Assets.Remove(asset);

                        if (asset.assetType == AssetType.PBIND)
                        {
                            CloseEmpForm(asset.Files[0].EmpFile);
                        }
                        else if (asset.assetType == AssetType.CBIND)
                        {
                            CloseEcfForm(asset.Files[0].EcfFile);
                        }
                        else if (asset.assetType == AssetType.TBIND)
                        {
                            CloseEtrForm(asset.Files[0].EtrFile);
                        }
                    }

                    UndoManager.Instance.AddUndo(new CompositeUndo(undos, "Delete Asset"));
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsAssetTypeSelected))]
        private void AssetDuplicate(AssetType assetType)
        {
            List<Asset> assets = GetSelectedAssets(assetType);

            if (assets.Count > 0)
            {
                AssetContainerTool container = effectContainerFile.GetAssetContainer(assetType);

                List<IUndoRedo> undos = new List<IUndoRedo>();

                foreach (var asset in assets)
                {
                    Asset newAsset = asset.Clone();
                    newAsset.InstanceID = Guid.NewGuid();
                    newAsset.assetType = assetType;

                    foreach (var file in newAsset.Files)
                    {
                        file.SetName(container.GetUnusedName(file.FullFileName));
                    }

                    container.Assets.Add(newAsset);

                    undos.Add(new UndoableListAdd<Asset>(container.Assets, newAsset));
                }

                UndoManager.Instance.AddUndo(new CompositeUndo(undos, "Duplicate Asset"));
            }
        }

        [RelayCommand(CanExecute = nameof(IsAssetTypeSelected))]
        private void AssetUsedBy(AssetType assetType)
        {
            var asset = GetSelectedAsset(assetType);
            bool showExt = assetType != AssetType.EMO;

            if (asset != null)
            {
                List<int> effects = effectContainerFile.AssetUsedBy(asset);
                effects.Sort();
                StringBuilder str = new StringBuilder();

                foreach (int effect in effects)
                {
                    str.Append(string.Format("Effect ID: {0}\r", effect));
                }

                string shownName = showExt ? asset.FileNamesPreviewWithExtension : asset.FileNamesPreview;

                MessagePrompt prompt = new MessagePrompt($"{shownName}: Used By", "The following effects use this asset:", str.ToString());
                prompt.Show();
            }
        }

        [RelayCommand(CanExecute = nameof(IsAssetTypeSelected))]
        private void AssetRename(AssetType assetType) //PBIND, TBIND, CBIND and LIGHT
        {
            if (assetType == AssetType.EMO) throw new ArgumentException("RenameAsset: EMO asset type cannot be renamed");
            var asset = GetSelectedAsset(assetType);

            if (asset != null)
            {
                AssetContainer_RenameFile(asset.Files[0], asset, effectContainerFile.GetAssetContainer(assetType));
                asset.RefreshNamePreview();
            }
        }

        [RelayCommand(CanExecute = nameof(IsAssetTypeSelected))]
        private void AssetCopy(AssetType assetType)
        {
            var selectedAsets = GetSelectedAssets(assetType);

            if(selectedAsets?.Count > 0)
            {
                effectContainerFile.SaveDds();
                Clipboard.SetData(ClipboardDataTypes.GetAssetFormat(assetType), selectedAsets);
            }
        }

        [RelayCommand(CanExecute = nameof(CanPasteAsset))]
        private void AssetPaste(AssetType assetType)
        {
            AssetContainerTool container = effectContainerFile.GetAssetContainer(assetType);

            List<Asset> assets = (List<Asset>)Clipboard.GetData(ClipboardDataTypes.GetAssetFormat(assetType));

            List<IUndoRedo> undos = new List<IUndoRedo>();

            if (assets != null)
            {
                int alreadyExistCount = 0;
                int copied = 0;

                foreach (Asset asset in assets)
                {
                    if (container.AssetExists(asset) == null)
                    {
                        copied++;
                        container.AddAsset(asset, undos);
                    }
                    else
                    {
                        alreadyExistCount++;
                    }
                }

                if (alreadyExistCount > 0 && copied == 0)
                {
                    MessagePrompt.Show("All copied assets already exist. Copy failed.", "Warning", MessagePromptButtons.OK, MessagePromptIcon.Warning);
                }
                else if (alreadyExistCount > 0 && copied > 0)
                {
                    MessagePrompt.Show(string.Format("{0} assets were skipped as they already exist.", alreadyExistCount), "Warning", MessagePromptButtons.OK, MessagePromptIcon.Warning);
                }

                UndoManager.Instance.AddUndo(new CompositeUndo(undos, "Paste"));
            }
        }

        [RelayCommand(CanExecute = nameof(CanPasteAsset))]
        private void AssetPasteOver(AssetType assetType)
        {
            var asset = GetSelectedAsset(assetType);

            if (asset != null)
            {
                AssetContainerTool container = effectContainerFile.GetAssetContainer(assetType);

                var newAsset = (List<Asset>)Clipboard.GetData(string.Format("{0}{1}", ClipboardDataTypes.Asset, assetType.ToString()));

                if (newAsset == null)
                {
                    MessagePrompt.Show("No asset was found in the clipboard. Cannot continue paste operation.", "Replace", MessagePromptButtons.OK, MessagePromptIcon.Warning);
                    return;
                }

                if (MessagePrompt.Show(string.Format("The asset \"{0}\" will be replaced with a copy of \"{1}\". This cannot be undone.\n\nDo you want to continue?", asset.FileNamesPreview, newAsset[0].FileNamesPreview), "Replace", MessagePromptButtons.YesNo, MessagePromptIcon.Question) == MessagePromptResult.No)
                {
                    return;
                }

                List<IUndoRedo> undos = new List<IUndoRedo>();

                if (newAsset.Count == 1)
                {
                    //back up of original asset data, so it can be restored in the event of an error
                    var oldFiles = asset.Files;
                    var selectedAsset = newAsset;

                    foreach (var file in selectedAsset[0].Files)
                    {
                        string newName = container.GetUnusedName(file.FullFileName);
                        undos.Add(new UndoableProperty<EffectFile>(nameof(file.FileName), file, file.FileName, Path.GetFileNameWithoutExtension(newName)));
                        file.SetName(newName);

                        try
                        {
                            switch (assetType)
                            {
                                case AssetType.PBIND:
                                    container.AddPbindDependencies(file.EmpFile, undos);
                                    break;
                                case AssetType.TBIND:
                                    container.AddTbindDependencies(file.EtrFile, undos);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            //Restore the previous asset
                            asset.Files = oldFiles;
                            MessagePrompt.Show($"Encountered an error while pasting the asset. \n\nThe asset has not been changed.", "Error", MessagePromptButtons.OK, MessagePromptIcon.Error, richText: ex.Message);
                            return;
                        }
                    }

                    undos.Add(new UndoableProperty<Asset>(nameof(asset.Files), asset, asset.Files, selectedAsset[0].Files));
                    undos.Add(new UndoActionDelegate(asset, nameof(asset.RefreshNamePreview), true));

                    asset.Files = selectedAsset[0].Files;
                    asset.RefreshNamePreview();

                    effectContainerFile.AssetRefDetailsRefresh(asset);

                    UndoManager.Instance.AddUndo(new CompositeUndo(undos, "Paste Over"));
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsAssetTypeSelected))]
        private void AssetHueAdjust(AssetType assetType)
        {
            AssetOpenRecolorDialog(GetSelectedAsset(assetType), false);
        }

        [RelayCommand(CanExecute = nameof(IsAssetTypeSelected))]
        private void AssetHueSet(AssetType assetType)
        {
            AssetOpenRecolorDialog(GetSelectedAsset(assetType), true);
        }

        [RelayCommand(CanExecute = nameof(IsAssetTypeSelected))]
        private void AssetOpenEditor(AssetType assetType)
        {
            if (assetType == AssetType.EMO || assetType == AssetType.LIGHT)
                throw new ArgumentException("AssetOpenEditor: No editors exist for EMO or LIGHT");

            var asset = GetSelectedAsset(assetType);

            switch (assetType)
            {
                case AssetType.PBIND:
                    PBIND_OpenEditor(asset.Files[0].EmpFile, effectContainerFile.Pbind, asset.Files[0].FileName);
                    break;
                case AssetType.TBIND:
                    TBIND_OpenEditor(asset.Files[0].EtrFile, effectContainerFile.Tbind, asset.Files[0].FileName);
                    break;
                case AssetType.CBIND:
                    CBIND_OpenEditor(asset.Files[0].EcfFile, asset.Files[0].FileName);
                    break;
            }
        }

        [RelayCommand(CanExecute = nameof(IsAssetTypeSelected))]
        private void AssetExtract(AssetType assetType)
        {
            if (assetType != AssetType.CBIND && assetType != AssetType.LIGHT)
                throw new ArgumentException("AssetExtract: Extraction only possible for CBIND and LIGHT asset types.");

            Asset asset = GetSelectedAsset(assetType);

            if (asset != null)
            {
                byte[] bytes;
                string filter;

                switch (assetType)
                {
                    case AssetType.CBIND:
                        bytes = asset.Files[0].EcfFile.SaveToBytes();
                        filter = "ECF File | *.ecf";
                        break;
                    case AssetType.LIGHT:
                        bytes = asset.Files[0].EmaFile.Write();
                        filter = "LIGHT EMA File | *.light.ema";
                        break;
                    default:
                        return;
                }

                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Title = "Extract file...",
                    FileName = asset.Files[0].FullFileName,
                    Filter = filter
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.WriteAllBytes(saveDialog.FileName, bytes);
                }
            }
        }


        //CanExecute
        private bool CanMergeAssets(AssetType assetType)
        {
            return GetSelectedAssetsCount(assetType) > 1;
        }

        private bool IsAssetTypeSelected(AssetType assetType)
        {
            return GetSelectedAsset(assetType) != null;
        }

        private bool CanPasteAsset(AssetType assetType)
        {
            return Clipboard.ContainsData(ClipboardDataTypes.GetAssetFormat(assetType));
        }

        //Helpers
        private void AssetOpenRecolorDialog(Asset asset, bool hueSet)
        {
            if (asset != null)
            {
                RecolorAll recolor = new RecolorAll(asset.assetType, asset, hueSet, Application.Current.MainWindow);

                if (recolor.Initialize())
                    recolor.ShowDialog();
            }
        }

        private Asset GetSelectedAsset(AssetType assetType)
        {
            switch (assetType)
            {
                case AssetType.PBIND:
                    return pbindDataGrid.SelectedItem as Asset;
                case AssetType.TBIND:
                    return tbindDataGrid.SelectedItem as Asset;
                case AssetType.CBIND:
                    return cbindDataGrid.SelectedItem as Asset;
                case AssetType.LIGHT:
                    return lightDataGrid.SelectedItem as Asset;
                case AssetType.EMO:
                    return emoDataGrid.SelectedItem as Asset;
                default:
                    throw new ArgumentException($"GetSelectedAssets: Bad AssetType ({assetType})");
            }
        }

        private List<Asset> GetSelectedAssets(AssetType assetType)
        {
            switch (assetType)
            {
                case AssetType.PBIND:
                    return pbindDataGrid.SelectedItems.Cast<Asset>().ToList();
                case AssetType.TBIND:
                    return tbindDataGrid.SelectedItems.Cast<Asset>().ToList();
                case AssetType.CBIND:
                    return cbindDataGrid.SelectedItems.Cast<Asset>().ToList();
                case AssetType.LIGHT:
                    return lightDataGrid.SelectedItems.Cast<Asset>().ToList();
                case AssetType.EMO:
                    return emoDataGrid.SelectedItems.Cast<Asset>().ToList();
                default:
                    throw new ArgumentException($"GetSelectedAssets: Bad AssetType ({assetType})");
            }
        }
        
        private int GetSelectedAssetsCount(AssetType assetType)
        {
            switch (assetType)
            {
                case AssetType.PBIND:
                    return pbindDataGrid.SelectedItems.Count;
                case AssetType.TBIND:
                    return tbindDataGrid.SelectedItems.Count;
                case AssetType.CBIND:
                    return cbindDataGrid.SelectedItems.Count;
                case AssetType.LIGHT:
                    return lightDataGrid.SelectedItems.Count;
                case AssetType.EMO:
                    return emoDataGrid.SelectedItems.Count;
            }

            return 0;
        }
        #endregion

        #region AssetPbind

        [RelayCommand(CanExecute = nameof(IsPbindSelected))]
        private void AssetPbindRemoveColorAnimations()
        {
            List<Asset> selectedAssets = GetSelectedAssets(AssetType.PBIND);

            if (selectedAssets != null)
            {
                List<IUndoRedo> undos = new List<IUndoRedo>();

                foreach (var asset in selectedAssets)
                    undos.AddRange(asset.Files[0].EmpFile.RemoveColorAnimations());

                if (undos.Count > 0)
                {
                    UndoManager.Instance.AddCompositeUndo(undos, "Remove Color Animations (PBIND)");
                    MessagePrompt.Show($"{undos.Count} animations were removed", "Remove Color Animations (PBIND)");
                }
                else
                {
                    MessagePrompt.Show("No animations found", "Remove Color Animations (PBIND)");
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsPbindSelected))]
        private void AssetPbindRemoveRandomColorRange()
        {
            List<Asset> selectedAssets = GetSelectedAssets(AssetType.PBIND);

            if (selectedAssets != null)
            {
                List<IUndoRedo> undos = new List<IUndoRedo>();

                foreach (var asset in selectedAssets)
                    undos.AddRange(asset.Files[0].EmpFile.RemoveRandomColorRange());

                if (undos.Count > 0)
                {
                    UndoManager.Instance.AddCompositeUndo(undos, "Remove Random Color Range (PBIND)");
                    MessagePrompt.Show($"{undos.Count} random instances were removed", "Remove Random Color Range (PBIND)");
                }
                else
                {
                    MessagePrompt.Show("No random instances found", "Remove Random Color Range (PBIND)");
                }
            }
        }

        private bool IsPbindSelected()
        {
            return GetSelectedAsset(AssetType.PBIND) != null;
        }

        #endregion

        #region AssetTbind

        [RelayCommand(CanExecute = nameof(IsTbindSelected))]
        private void AssetTbindScale()
        {
            Asset asset = GetSelectedAsset(AssetType.TBIND);

            float currentScale = asset.Files[0].EtrFile.GetAverageScale();
            LB_Common.Forms.NumericInput inputForm = new LB_Common.Forms.NumericInput("ETR Scale", "Scale Factor", currentScale, 0, 50, 0.01);
            inputForm.ShowDialog();

            if (!inputForm.IsCancelled)
            {
                List<IUndoRedo> undos = new List<IUndoRedo>();
                asset.Files[0].EtrFile.ScaleETRParts(Math.Max(0.001f, inputForm.GetValue<float>()) / Math.Max(0.001f, currentScale), undos);

                UndoManager.Instance.AddUndo(new CompositeUndo(undos, "ETR Scale"));
                UndoManager.Instance.ForceEventCall();
            }
        }

        private bool IsTbindSelected()
        {
            return GetSelectedAsset(AssetType.TBIND) != null;
        }

        #endregion

        #region AssetEmo

        [RelayCommand(CanExecute = nameof(IsEmoSelected))]
        private void AssetEmoAddFile()
        {
            Asset asset = GetSelectedAsset(AssetType.EMO);

            if (asset != null)
            {
                if (asset.Files.Count == 5)
                {
                    MessagePrompt.Show("An asset cannot have more than 5 files assigned to it.", "Add File", MessagePromptButtons.OK, MessagePromptIcon.Error);
                    return;
                }

                OpenFileDialog openFile = new OpenFileDialog();
                openFile.Title = "Add file...";
                openFile.Filter = "EMO effect files | *.emo; *.mat.ema; *.obj.ema; *.emm; *.emb";
                openFile.ShowDialog();

                if (File.Exists(openFile.FileName) && !String.IsNullOrWhiteSpace(openFile.FileName))
                {
                    string originalName = Path.GetFileName(openFile.FileName);
                    byte[] bytes = File.ReadAllBytes(openFile.FileName);
                    string newName = effectContainerFile.Emo.GetUnusedName(originalName); //To prevent duplicates

                    List<IUndoRedo> undos = new List<IUndoRedo>();

                    switch (EffectFile.GetExtension(openFile.FileName))
                    {
                        case ".emm":
                            asset.AddFile(EMM_File.LoadEmm(bytes), newName, EffectFile.FileType.EMM, undos);
                            break;
                        case ".emb":
                            asset.AddFile(EMB_File.LoadEmb(bytes), newName, EffectFile.FileType.EMB, undos);
                            break;
                        case ".light.ema":
                            asset.AddFile(EMA_File.Load(bytes), newName, EffectFile.FileType.EMA, undos);
                            break;
                        default:
                            asset.AddFile(bytes, newName, EffectFile.FileType.Other, undos);
                            break;
                    }

                    UndoManager.Instance.AddCompositeUndo(undos, "Add File (EMO)");

                    if (newName != originalName)
                    {
                        MessagePrompt.Show(string.Format("The added file was renamed to \"{0}\" because \"{1}\" was already used.", newName, originalName), "Add File", MessagePromptButtons.OK, MessagePromptIcon.Information);
                    }

                    SelectAsset(asset);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsEmoSelected))]
        private void AssetEmoAddEmdFile()
        {
            Asset asset = GetSelectedAsset(AssetType.EMO);

            if (asset.Files.Any(x => x.fileType == EffectFile.FileType.EMB) || asset.Files.Any(x => x.fileType == EffectFile.FileType.EMM) || asset.Files.Any(x => x.fileType == EffectFile.FileType.EMO))
            {
                MessagePrompt.Show($"An EMO, EMB or EMM already exists on the selected asset. Please delete it/them to complete the conversion.", "File Already Exists", MessagePromptIcon.Error);
                return;
            }

            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Title = "EMD + ESK -> EMO conversion";
            openFile.Filter = "EMD and ESK | *.emd; *.esk";
            openFile.Multiselect = true;

            if (openFile.ShowDialog() == true)
            {
                ESK_File eskFile = null;
                List<EMD_File> emdFiles = new List<EMD_File>();
                List<EMB_File> embFiles = new List<EMB_File>();
                List<EMB_File> dytFiles = new List<EMB_File>();
                List<EMM_File> emmFiles = new List<EMM_File>();

                bool hasDyt = false;


                foreach (var file in openFile.FileNames)
                {
                    if (Path.GetExtension(file) == ".emd")
                    {
                        string emmPath = string.Format("{0}/{1}.emm", Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file));
                        string embPath = string.Format("{0}/{1}.emb", Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file));
                        string dytPath = string.Format("{0}/{1}.dyt.emb", Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file));

                        if (!File.Exists(emmPath))
                        {
                            MessagePrompt.Show($"Could not find \"{emmPath}\".\n\nPlease note that each EMD file must have a EMM and EMB in the same directory.", "File Already Exists", MessagePromptIcon.Error);
                            return;
                        }

                        if (!File.Exists(embPath))
                        {
                            MessagePrompt.Show($"Could not find \"{embPath}\".\n\nPlease note that each EMD file must have a EMM and EMB in the same directory.", "File Already Exists", MessagePromptIcon.Error);
                            return;
                        }

                        if (!File.Exists(dytPath) && hasDyt)
                        {
                            MessagePrompt.Show($"Could not find \"{dytPath}\".\n\nIf at least one of the EMD files have a dyt file, then one is required for each EMD.", "File Already Exists", MessagePromptIcon.Error);
                            return;
                        }

                        EMD_File emd = EMD_File.Load(file);
                        EMB_File emb = EMB_File.LoadEmb(embPath);
                        EMM_File emm = EMM_File.LoadEmm(emmPath);
                        EMB_File dyt = null;

                        emdFiles.Add(emd);
                        embFiles.Add(emb);
                        emmFiles.Add(emm);

                        if (File.Exists(dytPath))
                        {
                            hasDyt = true;
                            dyt = EMB_File.LoadEmb(dytPath);
                            dytFiles.Add(dyt);
                        }

                    }
                    else if (Path.GetExtension(file) == ".esk")
                    {
                        eskFile = ESK_File.Load(file);
                    }

                }

                //Validation
                if (emdFiles.Count == 0)
                {
                    MessagePrompt.Show($"No EMD files were selected/loaded, so no EMO can be created.", "File Already Exists", MessagePromptIcon.Error);
                    return;
                }

                if (eskFile == null)
                {
                    MessagePrompt.Show($"No ESK file was selected/loaded, so no EMO can be created.", "File Already Exists", MessagePromptIcon.Error);
                    return;
                }

                EMB_File mergedEmb;
                EMM_File mergedEmm;
                EMO_File emoFile = EMO_File.ConvertToEmo(emdFiles.ToArray(), embFiles.ToArray(), dytFiles.ToArray(), emmFiles.ToArray(), eskFile, out mergedEmb, out mergedEmm);

                List<IUndoRedo> undos = new List<IUndoRedo>();

                string name = Path.GetFileNameWithoutExtension(openFile.FileName);

                asset.AddFile(emoFile, $"{name}.emo", EffectFile.FileType.EMO, undos);
                asset.AddFile(mergedEmb, $"{name}.emb", EffectFile.FileType.EMB, undos);
                asset.AddFile(mergedEmm, $"{name}.emm", EffectFile.FileType.EMM, undos);

                UndoManager.Instance.AddCompositeUndo(undos, "EMD -> EMO Conversion");
            }

        }

        [RelayCommand(CanExecute = nameof(IsEmoSelected))]
        private void AssetEmoAddEanFile()
        {
            //TODO: Make worky
            var asset = GetSelectedAsset(AssetType.EMO);

            if (asset.Files.Any(x => x.Extension == ".obj.ema"))
            {
                MessagePrompt.Show($"An OBJ.EMA already exists on the selected asset.", "File Already Exists", MessagePromptIcon.Warning);
                return;
            }

            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Title = "EAN -> EMA conversion";
            openFile.Filter = "EAN files | *.ean";

            if (openFile.ShowDialog() == true)
            {
                /*
                EAN_File eanFile = EAN_File.Load(openFile.FileName);
                EMA_File emaFile = EMA_File.ConvertToEma(eanFile);

                List<IUndoRedo> undos = new List<IUndoRedo>();
                asset.AddFile(emaFile, $"{Path.GetFileNameWithoutExtension(openFile.FileName)}.obj.ema", EffectFile.FileType.EMA, undos);

                UndoManager.Instance.AddCompositeUndo(undos, "EAN -> EMA");
                */
            }
        }

        [RelayCommand]
        private void AssetEmoDoubleClick(EffectFile file)
        {
            OpenEmoEffectFileEditor(file, false);
        }

        private bool IsEmoSelected()
        {
            return GetSelectedAsset(AssetType.EMO) != null;
        }


        //Helpers
        private async void OpenEmoEffectFileEditor(EffectFile selectedFile, bool showError)
        {
            if (selectedFile != null)
            {
                bool focus = false;
                Window window = null;

                switch (selectedFile.fileType)
                {
                    case EffectFile.FileType.EMB:
                        {
                            window = GetActiveEmbForm(selectedFile.EmbFile);

                            if (window == null)
                            {
                                window = new Forms.EmbEditForm(selectedFile.EmbFile, null, AssetType.EMO, selectedFile.FullFileName);
                            }
                            else
                            {
                                focus = true;
                            }
                        }
                        break;
                    case EffectFile.FileType.EMM:
                        {
                            window = GetActiveEmmForm(selectedFile.EmmFile);

                            if (window == null)
                            {
                                window = new Forms.MaterialsEditorForm(selectedFile.EmmFile, null, AssetType.EMO, selectedFile.FullFileName);
                            }
                            else
                            {
                                focus = true;
                            }
                        }
                        break;
#if XenoKit
                    case EffectFile.FileType.EMO:
                        if (emoDataGrid.SelectedItem is Asset asset)
                        {
                            EffectFile emb = asset.Files.FirstOrDefault(x => x.fileType == EffectFile.FileType.EMB);
                            EffectFile emm = asset.Files.FirstOrDefault(x => x.fileType == EffectFile.FileType.EMM);

                            if(emb == null || emb.EmbFile == null)
                            {
                                MessagePrompt.Show("The Model Editor requires a .emb file.", "No Textures", MessagePromptButtons.OK, MessagePromptIcon.Warning);
                                return;
                            }

                            if (emm == null || emm.EmmFile == null)
                            {
                                MessagePrompt.Show("The Model Editor requires a .emm file.", "No Materials", MessagePromptButtons.OK, MessagePromptIcon.Warning);
                                return;
                            }

                            Xv2ModelFile compiledModel = Viewport.Instance.CompiledObjectManager.GetCompiledObject<Xv2ModelFile>(selectedFile.EmoFile);
                            ModelScene modelScene = Viewport.Instance.CompiledObjectManager.GetCompiledObject<ModelScene>(compiledModel);

                            if (!TabManager.FocusTab(modelScene))
                            {
                                modelScene.SetFiles(XenoKit.Engine.Shader.ShaderType.Chara, emb.EmbFile, emm.EmmFile);
                                modelScene.SetPaths(true, $"{effectContainerFile.Directory}/{selectedFile.FullFileName}", $"{effectContainerFile.Directory}/{emb.FullFileName}", $"{effectContainerFile.Directory}/{emm.FullFileName}", null);

                                ModelSceneView modelSceneView = new ModelSceneView(modelScene);

                                TabManager.AddTab($"{Path.GetFileName(selectedFile.FullFileName)}", modelSceneView, modelScene, Files.Instance.SelectedItem, null);
                            }
                        }
                        break;
#endif
                    default:
                        if (showError)
                            MessagePrompt.Show(string.Format("Edit not possible for {0} files.", selectedFile.Extension), "Edit", MessagePromptButtons.OK, MessagePromptIcon.Stop);
                        return;
                }

                if (window != null)
                {
                    await Task.Delay(100);

                    if (focus)
                    {
                        window.Focus();
                    }
                    else
                    {
                        window.Show();
                    }
                }
            }
        }

        #endregion

        #region AssetContainer
        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void PbindCreateNewEmp()
        {
            var asset = effectContainerFile.Pbind.AddAsset(new EMP_File(), "NewEmp.emp");
            SelectAsset(asset);

            //Undos
            List<IUndoRedo> undos = new List<IUndoRedo>();
            undos.Add(new UndoableListAdd<Asset>(effectContainerFile.Pbind.Assets, asset));
            undos.Add(new UndoActionDelegate(effectContainerFile.Pbind, nameof(effectContainerFile.Pbind.UpdateAssetFilter), true));
            UndoManager.Instance.AddUndo(new CompositeUndo(undos, "New EMP"));
        }

        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void TbindCreateNewEtr()
        {
            ETR_File etrFile = new ETR_File();
            etrFile.Nodes.Add(ETR_Node.GetNew());

            Asset asset = effectContainerFile.Tbind.AddAsset(etrFile, "NewEtr.etr");
            SelectAsset(asset);

            //Undos
            List<IUndoRedo> undos = new List<IUndoRedo>();
            undos.Add(new UndoableListAdd<Asset>(effectContainerFile.Tbind.Assets, asset));
            undos.Add(new UndoActionDelegate(effectContainerFile.Tbind, nameof(effectContainerFile.Tbind.UpdateAssetFilter), true));
            UndoManager.Instance.AddUndo(new CompositeUndo(undos, "New ETR"));
        }

        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void CbindCreateNewEcf()
        {
            ECF_File ecfFile = new ECF_File();
            ecfFile.Nodes.Add(new ECF_Node());

            Asset asset = effectContainerFile.Cbind.AddAsset(ecfFile, "NewEcf.ecf");
            SelectAsset(asset);

            //Undos
            List<IUndoRedo> undos = new List<IUndoRedo>();
            undos.Add(new UndoableListAdd<Asset>(effectContainerFile.Cbind.Assets, asset));
            undos.Add(new UndoActionDelegate(effectContainerFile.Cbind, nameof(effectContainerFile.Cbind.UpdateAssetFilter), true));
            UndoManager.Instance.AddUndo(new CompositeUndo(undos, "New ECF"));
        }

        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void EmoCreateNewAsset()
        {
            List<IUndoRedo> undos = new List<IUndoRedo>();

            Asset asset = Asset.Create(AssetType.EMO);
            effectContainerFile.Emo.AddAsset(asset, undos);
            SelectAsset(asset);

            //Undos
            undos.Add(new UndoActionDelegate(effectContainerFile.Emo, nameof(effectContainerFile.Emo.UpdateAssetFilter), true));
            UndoManager.Instance.AddUndo(new CompositeUndo(undos, "New EMO Asset"));
        }


        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void CbindImportExternalEcf()
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Title = "Add file...";
            openFile.Filter = "ECF files | *.ecf";
            openFile.ShowDialog();

            if (!string.IsNullOrWhiteSpace(openFile.FileName) && File.Exists(openFile.FileName))
            {
                string newName = effectContainerFile.Cbind.GetUnusedName(System.IO.Path.GetFileName(openFile.FileName));

                Asset asset = new Asset()
                {
                    assetType = AssetType.CBIND,
                    Files = new AsyncObservableCollection<EffectFile>()
                        {
                            new EffectFile()
                            {
                                EcfFile = ECF_File.Load(openFile.FileName),
                                Extension = EffectFile.GetExtension(openFile.FileName),
                                fileType = EffectFile.FileType.ECF,
                                OriginalFileName = EffectFile.GetFileNameWithoutExtension(openFile.FileName),
                                FileName = EffectFile.GetFileNameWithoutExtension(newName)
                            }
                        }
                };

                effectContainerFile.Cbind.Assets.Add(asset);
                SelectAsset(asset);
            }
        }

        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void EmoImportExternalFiles()
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Title = "Import Asset...";
            openFile.Filter = "EMO effect files | *.emo; *.mat.ema; *.obj.ema; *.emm; *.emb";
            openFile.Multiselect = true;
            openFile.ShowDialog();

            if (openFile.FileNames.Length > 0)
            {
                if (openFile.FileNames.Length > 5)
                {
                    MessagePrompt.Show(string.Format("EMO assets cannot have more than 5 files. {0} were selected.\n\nImport cancelled.", openFile.FileNames.Length), "Import Asset", MessagePromptButtons.OK, MessagePromptIcon.Stop);
                    return;
                }

                Asset asset = new Asset()
                {
                    assetType = AssetType.EMO,
                    Files = new AsyncObservableCollection<EffectFile>()
                };

                foreach (string file in openFile.FileNames)
                {
                    string newName = effectContainerFile.Emo.GetUnusedName(System.IO.Path.GetFileName(file));

                    switch (EffectFile.GetFileType(file))
                    {
                        case EffectFile.FileType.EMB:
                            asset.AddFile(EMB_File.LoadEmb(file), newName, EffectFile.FileType.EMB);
                            break;
                        case EffectFile.FileType.EMM:
                            asset.AddFile(EMM_File.LoadEmm(file), newName, EffectFile.FileType.EMM);
                            break;
                        case EffectFile.FileType.EMA:
                            asset.AddFile(EMA_File.Load(file), newName, EffectFile.FileType.EMA);
                            break;
                        case EffectFile.FileType.EMO:
                            asset.AddFile(EMO_File.Load(file), newName, EffectFile.FileType.EMO);
                            break;
                        default:
                            throw new InvalidDataException(String.Format("EMO_ImportAsset_MenuItem_LoadEmoFiles_Click: FileType = {0} is not valid for EMO.", EffectFile.GetFileType(file)));
                    }
                }

                effectContainerFile.Emo.Assets.Add(asset);
                SelectAsset(asset);

                UndoManager.Instance.AddUndo(new UndoableListAdd<Asset>(effectContainerFile.Emo.Assets, asset, "Add EMO"));
            }
        }

        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void LightImportExternal()
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Title = "Add file...";
            openFile.Filter = "light.ema files | *.light.ema";
            openFile.ShowDialog();

            if (!string.IsNullOrWhiteSpace(openFile.FileName) && File.Exists(openFile.FileName))
            {
                string newName = effectContainerFile.LightEma.GetUnusedName(System.IO.Path.GetFileName(openFile.FileName));

                Asset asset = new Asset()
                {
                    assetType = AssetType.LIGHT,
                    Files = new AsyncObservableCollection<EffectFile>()
                        {
                            new EffectFile()
                            {
                                EmaFile = EMA_File.Load(openFile.FileName),
                                Extension = EffectFile.GetExtension(openFile.FileName),
                                fileType = EffectFile.FileType.EMA,
                                OriginalFileName = EffectFile.GetFileNameWithoutExtension(openFile.FileName),
                                FileName = EffectFile.GetFileNameWithoutExtension(newName)
                            }
                        }
                };

                effectContainerFile.LightEma.Assets.Add(asset);
                SelectAsset(asset);
            }
        }


        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void AssetRemoveUnused(AssetType assetType)
        {
            List<IUndoRedo> undos = new List<IUndoRedo>();

            int amountRemoved = effectContainerFile.RemoveUnusedAssets(assetType, undos);

            if (amountRemoved > 0)
            {
                UndoManager.Instance.AddUndo(new CompositeUndo(undos, $"Remove Unusued ({assetType})"));
                MessagePrompt.Show(String.Format("{0} unused assets were removed.", amountRemoved), "Remove Unused", MessagePromptButtons.OK, MessagePromptIcon.Information);
            }
            else
            {
                MessagePrompt.Show("There are no unused assets.", "Remove Unused", MessagePromptButtons.OK, MessagePromptIcon.Information);
            }
        }

        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void AssetOpenSettings(AssetType assetType)
        {
            AssetContainerSettings assetSettings = new AssetContainerSettings(effectContainerFile.GetAssetContainer(assetType));
            assetSettings.ShowDialog();
        }

        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void AssetOpenTextureForm(AssetType assetType)
        {
            if (assetType == AssetType.EMO || assetType == AssetType.LIGHT || assetType == AssetType.CBIND)
                throw new ArgumentException("AssetOpenEditor: No container level textures exists for this asset type");

            AssetContainerOpenTextureForm(effectContainerFile.GetAssetContainer(assetType), assetType);
        }

        [RelayCommand(CanExecute = nameof(IsFileLoaded))]
        private void AssetOpenMaterialForm(AssetType assetType)
        {
            if (assetType == AssetType.EMO || assetType == AssetType.LIGHT || assetType == AssetType.CBIND)
                throw new ArgumentException("AssetOpenEditor: No container level materials exists for this asset type");

            AssetContainerOpenMaterialForm(effectContainerFile.GetAssetContainer(assetType), assetType);
        }


        private void AssetContainer_RenameFile(EffectFile effectFile, Asset asset, AssetContainerTool container)
        {
            RenameForm renameForm = new RenameForm(effectFile.FileName, effectFile.Extension, string.Format("Renaming {0}", effectFile.FullFileName), container, App.Current.MainWindow);
            renameForm.ShowDialog();

            if (renameForm.WasNameChanged)
            {
                List<IUndoRedo> undos = new List<IUndoRedo>();
                undos.Add(new UndoableProperty<EffectFile>(nameof(effectFile.FileName), effectFile, effectFile.FileName, renameForm.NameValue));
                undos.Add(new UndoActionDelegate(asset, nameof(asset.RefreshNamePreview), true));
                UndoManager.Instance.AddUndo(new CompositeUndo(undos, "Rename Asset"));

                effectFile.FileName = renameForm.NameValue;

                effectContainerFile.AssetRefDetailsRefresh(container.GetAssetByFileInstance(effectFile));
            }
        }
        #endregion
    }

}
