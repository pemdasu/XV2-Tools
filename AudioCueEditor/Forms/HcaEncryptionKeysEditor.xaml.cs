using System.Windows;
using MahApps.Metro.Controls;
using CommunityToolkit.Mvvm.Input;
using AudioCueEditor.Data;

namespace AudioCueEditor.Forms
{
    /// <summary>
    /// Interaction logic for HcaEncryptionKeysEditor.xaml
    /// </summary>
    public partial class HcaEncryptionKeysEditor : MetroWindow
    {

        public HcaEncryptionKeys Keys { get; set; }
        public HcaEncryptionKey SelectedKey { get; set; }
        public ulong ThisAcbKey { get; set; }
        public string ThisAcbKeyText => $"Current ACB: {ThisAcbKey}";
        public bool HasThisKey => ThisAcbKey != 0;
        
        public HcaEncryptionKeysEditor(ulong thisAcbKey, Window parent)
        {
            Keys = HcaEncryptionKeysManager.Instance.EncryptionKeys;
            ThisAcbKey = thisAcbKey;
            Owner = parent;
            InitializeComponent();
        }

        [RelayCommand]
        private void AddNew()
        {
            HcaEncryptionKeysManager.Instance.AddKey(0);
        }

        [RelayCommand(CanExecute = nameof(CanRemoveKey))]
        private void RemoveKey()
        {
            Keys.Keys.Remove(SelectedKey);
            RemoveKeyCommand.NotifyCanExecuteChanged();
        }

        private bool CanRemoveKey()
        {
            return SelectedKey != null;
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            HcaEncryptionKeysManager.Instance.Save();
        }

        private void Key_Add(object sender, RoutedEventArgs e)
        {
            HcaEncryptionKeysManager.Instance.AddKey(ThisAcbKey);
        }

        private void Key_Copy(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ThisAcbKey.ToString());
        }

        private void DataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RemoveKeyCommand.NotifyCanExecuteChanged();
        }
    }
}
