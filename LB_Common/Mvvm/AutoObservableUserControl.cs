using System.ComponentModel;
using System.Windows.Controls;

namespace LB_Common.Mvvm
{
    public class AutoObservableUserControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public AutoObservableUserControl() : base()
        {
            RelayCommandManager.RegisterAllProperties(this);
        }
    }
}
