using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Linq.Expressions;

namespace LB_Common.Mvvm
{
    public class AutoObservableObject : ObservableObject
    {
        public void NotifyPropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        public void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        public void RaisePropertyChanged<T>(Expression<Func<T>> propertyExpression)
        {
            if (propertyExpression.Body is MemberExpression memberExpression)
            {
                OnPropertyChanged(memberExpression.Member.Name);
            }
            else
            {
                throw new ArgumentException("The expression must refer to a property.", nameof(propertyExpression));
            }
        }

        public AutoObservableObject() : base()
        {
            RelayCommandManager.RegisterAllProperties(this);
        }
    }
}
