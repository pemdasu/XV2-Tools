using MahApps.Metro.Controls;
using System;
using System.ComponentModel;
using System.Linq.Expressions;

namespace LB_Common.Mvvm
{
    public partial class AutoObservableWindow : MetroWindow, INotifyPropertyChanged
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

        public AutoObservableWindow() : base()
        {
            RelayCommandManager.RegisterAllProperties(this);
        }
    }
}
