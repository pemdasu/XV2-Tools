using CommunityToolkit.Mvvm.Input;
using EEPK_Organiser.Misc;
using LB_Common.Mvvm;
using LB_Common.Numbers;
using LB_Common.Utils;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Xv2CoreLib.EEPK;
using Xv2CoreLib.EMP_NEW.Keyframes;
using Xv2CoreLib.ETR;
using Xv2CoreLib.Resource.UndoRedo;

namespace EEPK_Organiser.ViewModel
{
    public partial class EmpKeyframesViewModel : AutoObservableObject
    {
        private KeyframedBaseValue KeyframedValue = null;
        public DataGrid FloatKeyframeDataGrid = null;
        public DataGrid ColorKeyframeDataGrid = null;
        public DataGrid Vector2KeyframeDataGrid = null;
        public DataGrid Vector3KeyframeDataGrid = null;

        #region Visibilities
        public Visibility Vector2Visibile => KeyframedValue is KeyframedVector2Value ? Visibility.Visible : Visibility.Collapsed;
        public Visibility Vector3Visibile => KeyframedValue is KeyframedVector3Value ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ColorVisibile => KeyframedValue is KeyframedColorValue ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FloatVisibile => KeyframedValue is KeyframedFloatValue ? Visibility.Visible : Visibility.Collapsed;

        public KeyframedVector2Value Vector2Value => KeyframedValue as KeyframedVector2Value;
        public KeyframedVector3Value Vector3Value => KeyframedValue as KeyframedVector3Value;
        public KeyframedColorValue ColorValue => KeyframedValue as KeyframedColorValue;
        public KeyframedFloatValue FloatValue => KeyframedValue as KeyframedFloatValue;
        #endregion

        //Selected Keyframes
        private KeyframeVector2Value _selectedVector2 = null;
        private KeyframeVector3Value _selectedVector3 = null;
        private KeyframeColorValue _selectedColor = null;
        private KeyframeFloatValue _selectedFloat = null;

        public KeyframeVector2Value SelectedVector2
        {
            get => _selectedVector2;
            set
            {
                if (value != _selectedVector2)
                {
                    _selectedVector2 = value;

                    if(value != null)
                    {
                        NewVector.X = _selectedVector2.Value.X;
                        NewVector.Y = _selectedVector2.Value.Y;
                        NewTime = _selectedVector3.Time;
                        RaisePropertyChanged(nameof(NewTime));
                    }
                    RaisePropertyChanged(nameof(SelectedVector2));
                }
            }
        }
        public KeyframeVector3Value SelectedVector3
        {
            get => _selectedVector3;
            set
            {
                if (value != _selectedVector3)
                {
                    _selectedVector3 = value;

                    if(value != null)
                    {
                        NewVector.X = _selectedVector3.Value.X;
                        NewVector.Y = _selectedVector3.Value.Y;
                        NewVector.Z = _selectedVector3.Value.Z;
                        NewTime = _selectedVector3.Time;
                        RaisePropertyChanged(nameof(NewTime));
                    }
                    RaisePropertyChanged(nameof(SelectedVector3));
                }
            }
        }
        public KeyframeColorValue SelectedColor
        {
            get => _selectedColor;
            set
            {
                if (value != _selectedColor)
                {
                    _selectedColor = value;

                    if(value != null)
                    {
                        NewColor.R = _selectedColor.Value.R;
                        NewColor.G = _selectedColor.Value.G;
                        NewColor.B = _selectedColor.Value.B;
                        NewTime = _selectedColor.Time;
                        RaisePropertyChanged(nameof(NewTime));
                    }
                    RaisePropertyChanged(nameof(SelectedColor));
                }
            }
        }
        public KeyframeFloatValue SelectedFloat
        {
            get => _selectedFloat;
            set
            {
                if (value != _selectedFloat)
                {
                    _selectedFloat = value;

                    if(value != null)
                    {
                        NewFloat = _selectedFloat.Value;
                        NewTime = _selectedFloat.Time;
                        RaisePropertyChanged(nameof(NewTime));
                        RaisePropertyChanged(nameof(NewFloat));
                    }
                    RaisePropertyChanged(nameof(SelectedFloat));
                }
            }
        }


        //Add Keyframe
        public float NewTime { get; set; }
        public float NewFloat { get; set; }
        public CustomVector4 NewVector { get; set; } = new CustomVector4();
        public CustomColor NewColor { get; set; } = new CustomColor();

        //Options
        public bool Loop
        {
            get => KeyframedValue.Loop;
            set
            {
                if(KeyframedValue.Loop != value)
                {
                    UndoManager.Instance.AddUndo(new UndoablePropertyGeneric(nameof(KeyframedValue.Loop), KeyframedValue, KeyframedValue.Loop, value, "Keyframed Value -> Loop"));
                    KeyframedValue.Loop = value;
                }
            }
        }
        public bool Interpolate
        {
            get => KeyframedValue.Interpolate;
            set
            {
                if (KeyframedValue.Interpolate != value)
                {
                    UndoManager.Instance.AddUndo(new UndoablePropertyGeneric(nameof(KeyframedValue.Interpolate), KeyframedValue, KeyframedValue.Interpolate, value, "Keyframed Value -> Interpolate"));
                    KeyframedValue.Interpolate = value;
                }
            }
        }
        public bool IsAnimated
        {
            get => KeyframedValue.IsAnimated;
            set
            {
                if (KeyframedValue.IsAnimated != value)
                {
                    UndoManager.Instance.AddUndo(new UndoablePropertyGeneric(nameof(KeyframedValue.IsAnimated), KeyframedValue, KeyframedValue.IsAnimated, value, "Keyframed Value -> Is Animated"));
                    KeyframedValue.IsAnimated = value;
                    RaisePropertyChanged(nameof(IsAnimated));
                }
            }
        }
        public ETR_InterpolationType InterpolationType
        {
            get => KeyframedValue.ETR_InterpolationType;
            set
            {
                if (KeyframedValue.ETR_InterpolationType != value)
                {
                    UndoManager.Instance.AddUndo(new UndoablePropertyGeneric(nameof(KeyframedValue.ETR_InterpolationType), KeyframedValue, KeyframedValue.IsAnimated, value, "Keyframed Value -> Interpolation Type"));
                    KeyframedValue.ETR_InterpolationType = value;
                    RaisePropertyChanged(nameof(InterpolationType));
                }
            }
        }

        public void SetContext(KeyframedBaseValue value)
        {
            KeyframedValue = value;

            if(value != null)
                UpdateProperties();
        }

        public void UpdateProperties()
        {
            RaisePropertyChanged(nameof(Vector2Visibile));
            RaisePropertyChanged(nameof(Vector3Visibile));
            RaisePropertyChanged(nameof(ColorVisibile));
            RaisePropertyChanged(nameof(FloatVisibile));
            RaisePropertyChanged(nameof(Vector2Value));
            RaisePropertyChanged(nameof(Vector3Value));
            RaisePropertyChanged(nameof(ColorValue));
            RaisePropertyChanged(nameof(FloatValue));

            RaisePropertyChanged(nameof(Loop));
            RaisePropertyChanged(nameof(Interpolate));
            RaisePropertyChanged(nameof(IsAnimated));
            RaisePropertyChanged(nameof(InterpolationType));
        }


        #region FloatCommands
        [RelayCommand(CanExecute = nameof(IsFloatValue))]
        private void AddFloatKeyframe()
        {
            UndoManager.Instance.AddUndo(FloatValue.AddKeyframe(NewTime, NewFloat));
        }

        [RelayCommand(CanExecute = nameof(IsFloatSelected))]
        private void DeleteFloatKeyframe()
        {
            List<IUndoRedo> undos = new List<IUndoRedo>();
            List<KeyframeFloatValue> selectedFloatKeyframes = FloatKeyframeDataGrid.SelectedItems.Cast<KeyframeFloatValue>().ToList();

            foreach (var keyframe in selectedFloatKeyframes)
            {
                undos.Add(FloatValue.RemoveKeyframe(keyframe));
            }

            UndoManager.Instance.AddCompositeUndo(undos, "EMP -> Remove Keyframes");
        }

        [RelayCommand(CanExecute = nameof(IsFloatSelected))]
        private void CopyFloatKeyframe()
        {
            List<KeyframeFloatValue> selectedFloatKeyframes = FloatKeyframeDataGrid.SelectedItems.Cast<KeyframeFloatValue>().ToList();

            if(selectedFloatKeyframes.Count > 0)
            {
                CustomClipboard.SetData(KeyframedFloatValue.CLIPBOARD_ID, selectedFloatKeyframes);
            }
        }

        [RelayCommand(CanExecute = nameof(IsFloatInClipboard))]
        private void PasteFloatKeyframe()
        {
            if (!CustomClipboard.TryGetData(KeyframedFloatValue.CLIPBOARD_ID, out List<KeyframeFloatValue> keyframes))
                return;

            List<IUndoRedo> undos = new List<IUndoRedo>();

            foreach(var keyframe in keyframes)
            {
                undos.Add(FloatValue.AddKeyframe(keyframe.Time, keyframe.Value));
            }

            UndoManager.Instance.AddCompositeUndo(undos, "EMP -> Paste Keyframe");
        }

        private bool IsFloatInClipboard()
        {
            return CustomClipboard.ContainsData(KeyframedFloatValue.CLIPBOARD_ID) && IsFloatValue();
        }

        private bool IsFloatValue()
        {
            return KeyframedValue is KeyframedFloatValue;
        }

        private bool IsFloatSelected()
        {
            return SelectedFloat != null;
        }

        #endregion

        #region ColorCommands
        [RelayCommand(CanExecute = nameof(IsColorValue))]
        private void AddColorKeyframe()
        {
            UndoManager.Instance.AddUndo(ColorValue.AddKeyframe(NewTime, NewColor.R, NewColor.G, NewColor.B));
        }

        [RelayCommand(CanExecute = nameof(IsColorSelected))]
        private void DeleteColorKeyframe()
        {
            List<IUndoRedo> undos = new List<IUndoRedo>();
            List<KeyframeColorValue> selectedKeyframes = ColorKeyframeDataGrid.SelectedItems.Cast<KeyframeColorValue>().ToList();

            foreach (var keyframe in selectedKeyframes)
            {
                undos.Add(ColorValue.RemoveKeyframe(keyframe));
            }

            UndoManager.Instance.AddCompositeUndo(undos, "Remove Keyframes");
        }

        [RelayCommand(CanExecute = nameof(IsColorSelected))]
        private void CopyColorKeyframe()
        {
            List<KeyframeColorValue> selectedKeyframes = ColorKeyframeDataGrid.SelectedItems.Cast<KeyframeColorValue>().ToList();

            if (selectedKeyframes.Count > 0)
            {
                CustomClipboard.SetData(KeyframedColorValue.CLIPBOARD_ID, selectedKeyframes);
            }
        }

        [RelayCommand(CanExecute = nameof(IsColorInClipboard))]
        private void PasteColorKeyframe()
        {
            if (!CustomClipboard.TryGetData(KeyframedColorValue.CLIPBOARD_ID, out List<KeyframeColorValue> keyframes))
                return;

            List<IUndoRedo> undos = new List<IUndoRedo>();

            foreach (var keyframe in keyframes)
            {
                undos.Add(ColorValue.AddKeyframe(keyframe.Time, keyframe.Value.R, keyframe.Value.G, keyframe.Value.B));
            }

            UndoManager.Instance.AddCompositeUndo(undos, "EMP -> Paste Keyframe");
        }

        private bool IsColorInClipboard()
        {
            return CustomClipboard.ContainsData(KeyframedColorValue.CLIPBOARD_ID) && IsColorValue();
        }

        private bool IsColorValue()
        {
            return KeyframedValue is KeyframedColorValue;
        }

        private bool IsColorSelected()
        {
            return SelectedColor != null;
        }
        #endregion

        #region Vector2Commands
        [RelayCommand(CanExecute = nameof(IsVector2Value))]
        private void AddVector2Keyframe()
        {
            UndoManager.Instance.AddUndo(Vector2Value.AddKeyframe(NewTime, NewVector.X, NewVector.Y));
        }

        [RelayCommand(CanExecute = nameof(IsVector2Selected))]
        private void DeleteVector2Keyframe()
        {
            List<IUndoRedo> undos = new List<IUndoRedo>();
            List<KeyframeVector2Value> selectedKeyframes = Vector2KeyframeDataGrid.SelectedItems.Cast<KeyframeVector2Value>().ToList();

            foreach (var keyframe in selectedKeyframes)
            {
                undos.Add(Vector2Value.RemoveKeyframe(keyframe));
            }

            UndoManager.Instance.AddCompositeUndo(undos, "Remove Keyframes");
        }

        [RelayCommand(CanExecute = nameof(IsVector2Selected))]
        private void CopyVector2Keyframe()
        {
            List<KeyframeVector2Value> selectedKeyframes = Vector2KeyframeDataGrid.SelectedItems.Cast<KeyframeVector2Value>().ToList();

            if (selectedKeyframes.Count > 0)
            {
                CustomClipboard.SetData(KeyframedVector2Value.CLIPBOARD_ID, selectedKeyframes);
            }
        }

        [RelayCommand(CanExecute = nameof(IsVector2InClipboard))]
        private void PasteVector2Keyframe()
        {
            if (!CustomClipboard.TryGetData(KeyframedVector2Value.CLIPBOARD_ID, out List<KeyframeVector2Value> keyframes))
                return;

            List<IUndoRedo> undos = new List<IUndoRedo>();

            foreach (var keyframe in keyframes)
            {
                undos.Add(Vector2Value.AddKeyframe(keyframe.Time, keyframe.Value.X, keyframe.Value.Y));
            }

            UndoManager.Instance.AddCompositeUndo(undos, "EMP -> Paste Keyframe");
        }

        private bool IsVector2InClipboard()
        {
            return CustomClipboard.ContainsData(KeyframedVector2Value.CLIPBOARD_ID) && IsVector2Value();
        }

        private bool IsVector2Value()
        {
            return KeyframedValue is KeyframedVector2Value;
        }

        private bool IsVector2Selected()
        {
            return SelectedVector2 != null;
        }
        #endregion

        #region Vector3Commands
        [RelayCommand(CanExecute = nameof(IsVector3Value))]
        private void AddVector3Keyframe()
        {
            UndoManager.Instance.AddUndo(Vector3Value.AddKeyframe(NewTime, NewVector.X, NewVector.Y, NewVector.Z));
        }

        [RelayCommand(CanExecute = nameof(IsVector3Selected))]
        private void DeleteVector3Keyframe()
        {
            List<IUndoRedo> undos = new List<IUndoRedo>();
            List<KeyframeVector3Value> selectedKeyframes = Vector3KeyframeDataGrid.SelectedItems.Cast<KeyframeVector3Value>().ToList();

            foreach (var keyframe in selectedKeyframes)
            {
                undos.Add(Vector3Value.RemoveKeyframe(keyframe));
            }

            UndoManager.Instance.AddCompositeUndo(undos, "Remove Keyframes");
        }

        [RelayCommand(CanExecute = nameof(IsVector3Selected))]
        private void CopyVector3Keyframe()
        {
            List<KeyframeVector3Value> selectedKeyframes = Vector3KeyframeDataGrid.SelectedItems.Cast<KeyframeVector3Value>().ToList();

            if (selectedKeyframes.Count > 0)
            {
                CustomClipboard.SetData(KeyframedVector3Value.CLIPBOARD_ID, selectedKeyframes);
            }
        }

        [RelayCommand(CanExecute = nameof(IsVector3InClipboard))]
        private void PasteVector3Keyframe()
        {
            if (!CustomClipboard.TryGetData(KeyframedVector3Value.CLIPBOARD_ID, out List<KeyframeVector3Value> keyframes))
                return;

            List<IUndoRedo> undos = new List<IUndoRedo>();

            foreach (var keyframe in keyframes)
            {
                undos.Add(Vector3Value.AddKeyframe(keyframe.Time, keyframe.Value.X, keyframe.Value.Y, NewVector.Z));
            }

            UndoManager.Instance.AddCompositeUndo(undos, "EMP -> Paste Keyframe");
        }

        private bool IsVector3InClipboard()
        {
            return CustomClipboard.ContainsData(KeyframedVector3Value.CLIPBOARD_ID) && IsVector3Value();
        }

        private bool IsVector3Value()
        {
            return KeyframedValue is KeyframedVector3Value;
        }

        private bool IsVector3Selected()
        {
            return SelectedVector3 != null;
        }
        #endregion

    }
}
