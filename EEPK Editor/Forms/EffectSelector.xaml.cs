using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using LB_Common.Forms;
using Xv2CoreLib.EEPK;
using Xv2CoreLib.EffectContainer;
using LB_Common.Mvvm;

namespace EEPK_Organiser.Forms
{
    /// <summary>
    /// Interaction logic for EffectSelector.xaml
    /// </summary>
    public partial class EffectSelector : AutoObservableWindow
    {
        public enum Mode
        {
            ImportEffect,
            ExportEffect
        }

        private EffectContainerFile MainContainerFile { get; set; }
        public List<Effect> SelectedEffects = null;
        public List<EffectSelection> Effects { get; set; }

        private ushort _idIncreaseValue = 0;
        public ushort IdIncreaseValue
        {
            get => _idIncreaseValue;
            set
            {
                if (value != _idIncreaseValue)
                {
                   _idIncreaseValue = value;
                    NotifyPropertyChanged(nameof(IdIncreaseValue));
                }
            }
        }

        private readonly Mode CurrentMode;

        public EffectSelector(IList<Effect> effects, EffectContainerFile mainContainerFile, Window parent, Mode mode = Mode.ImportEffect)
        {
            CurrentMode = mode;
            Effects = new(effects.Count);

            foreach (var effect in effects)
                Effects.Add(new EffectSelection(effect));

            MainContainerFile = mainContainerFile;
            InitializeComponent();
            DataContext = this;
            Owner = parent;

            switch (CurrentMode)
            {
                case Mode.ImportEffect:
                    Title = "Import Effects";
                    break;
                case Mode.ExportEffect:
                    Title = "Export Effects";
                    break;
            }
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            var selectedEffects = GetSelectedEffects();

            if(selectedEffects.Count > 0)
            {
                if(CurrentMode == Mode.ImportEffect)
                {
                    bool wasError = false;
                    StringBuilder str = new StringBuilder();

                    foreach (var effect in selectedEffects)
                    {
                        if (MainContainerFile.IsEffectIdUsed(effect.NewEffectID))
                        {
                            wasError = true;
                            str.Append(string.Format("Effect ID: {0} > New ID: {1}\r", effect.Effect.IndexNum, effect.NewEffectID));
                        }
                    }

                    if (wasError)
                    {
                        MessagePrompt prompt = new MessagePrompt("ID Conflict", "The following effect IDs are conflicting.\nPlease change them to be unique.", str.ToString());
                        prompt.Show();
                    }
                    else
                    {
                        SelectedEffects = CreateOutputSelectedEffects();
                        Close();
                    }
                }
                else
                {
                    SelectedEffects = CreateOutputSelectedEffects();
                    Close();
                }
            }
        }

        private List<Effect> CreateOutputSelectedEffects()
        {
            var selection = Effects.Where(x => x.IsSelected).ToArray();
            List<Effect> effects = new(selection.Length);

            foreach(var effect in selection)
            {
                effects.Add(effect.Effect.ShallowClone(effect.NewEffectID));
            }

            return effects;
        }

        private void IdIncreaseValueButton_Click(object sender, RoutedEventArgs e)
        {
            if(Effects != null)
            {
                foreach(var effect in Effects)
                {
                    effect.NewEffectID += IdIncreaseValue;
                }
            }
        }

        private void IdDecreaseValueButton_Click(object sender, RoutedEventArgs e)
        {
            if (Effects != null)
            {
                foreach (var effect in Effects)
                {
                    effect.NewEffectID -= IdIncreaseValue;
                }
            }
        }

        public bool ImportEffectIdInceaseUsedByOtherEffects(ushort id, Effect effect)
        {
            foreach (var _effect in Effects)
            {
                if (_effect.Effect != effect && _effect.NewEffectID == id) return true;
            }

            return false;
        }

        private void UnselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach(var effect in Effects)
            {
                effect.IsSelected = false;
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var effect in Effects)
            {
                effect.IsSelected = true;
            }
        }

        private List<EffectSelection> GetSelectedEffects()
        {
            return Effects.Where(p => p.IsSelected == true).ToList();
        }

        private void ContextMenu_IncreaseID_Click(object sender, RoutedEventArgs e)
        {
            var selected = effectDataGrid.SelectedItems.Cast<EffectSelection>().ToList();

            if (selected != null)
            {
                foreach (var effect in selected)
                {
                    effect.NewEffectID += IdIncreaseValue;
                }
            }
        }

        private void ContextMenu_DecreaseID_Click(object sender, RoutedEventArgs e)
        {
            var selected = effectDataGrid.SelectedItems.Cast<EffectSelection>().ToList();

            if (selected != null)
            {
                foreach (var effect in selected)
                {
                    effect.NewEffectID -= IdIncreaseValue;
                }
            }
        }

        private void ContextMenu_Select_Click(object sender, RoutedEventArgs e)
        {
            SetSelectState(true);
        }

        private void ContextMenu_Unselect_Click(object sender, RoutedEventArgs e)
        {
            SetSelectState(false);
        }

        [RelayCommand]
        private void ToggleSelection()
        {
            //First set them as selected
            bool toggleState = SetSelectState(true);

            //If already selected, then unselect them
            if (toggleState)
                SetSelectState(false);
        }

        private bool SetSelectState(bool state)
        {
            bool alreadyThisState = true;

            List<EffectSelection> selected = effectDataGrid.SelectedItems.Cast<EffectSelection>().ToList();

            if (selected != null)
            {
                foreach (var pair in selected)
                {
                    if(pair.IsSelected != state)
                    {
                        pair.IsSelected = state;
                        alreadyThisState = false;
                    }
                }
            }

            return alreadyThisState;
        }


        public class EffectSelection : AutoObservableObject
        {
            private bool _isSelected = true;
            private ushort _newId;

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if(value != _isSelected)
                    {
                        _isSelected = value;
                        NotifyPropertyChanged(nameof(IsSelected));
                    }
                }
            }
            public ushort NewEffectID
            {
                get => _newId;
                set
                {
                    if (value != _newId)
                    {
                        _newId = value;
                        NotifyPropertyChanged(nameof(NewEffectID));
                    }
                }
            }
            public Effect Effect { get; set; }

            public EffectSelection(Effect effect)
            {
                Effect = effect;
                NewEffectID = effect.IndexNum;
                IsSelected = true;
            }
        }
    }

}
