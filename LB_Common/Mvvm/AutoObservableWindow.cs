using MahApps.Metro.Controls;
using System.ComponentModel;

namespace LB_Common.Mvvm
{
    public partial class AutoObservableWindow : MetroWindow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    
        public AutoObservableWindow() : base()
        {
            RelayCommandManager.RegisterAllProperties(this);
        }
    }
}
