using System;
using System.ComponentModel;
using System.Linq.Expressions;
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

        protected void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void RaisePropertyChanged<T>(Expression<Func<T>> propertyExpression)
        {
            if (propertyExpression.Body is MemberExpression memberExpression)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(memberExpression.Member.Name));
            }
            else
            {
                throw new ArgumentException("The expression must refer to a property.", nameof(propertyExpression));
            }
        }

        public AutoObservableUserControl() : base()
        {
            RelayCommandManager.RegisterAllProperties(this);
        }
    }
}
