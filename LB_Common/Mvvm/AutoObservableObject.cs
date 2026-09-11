using CommunityToolkit.Mvvm.ComponentModel;

namespace LB_Common.Mvvm
{
    public class AutoObservableObject : ObservableObject
    {
        public AutoObservableObject() : base()
        {
            RelayCommandManager.RegisterAllProperties(this);
        }
    }
}
