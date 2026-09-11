using System;
using System.Collections.Generic;
using System.Reflection;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace LB_Common.Mvvm
{
    public class RelayCommandManager
    {
        private static readonly RelayCommandManager _instance = new RelayCommandManager();

        //Stores a weak reference to the NotifyCanExecuteChanged method
        private readonly List<WeakReference<IRelayCommand>> _commands = new List<WeakReference<IRelayCommand>>(256);
        //private readonly Timer _timer;

        private RelayCommandManager()
        {
            //_timer = new Timer(1000);
            //_timer.Enabled = true;
            //_timer.Elapsed += _timer_Elapsed;
            CommandManager.RequerySuggested += CommandManager_RequerySuggested;
        }

        private void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            NotifyAll();
        }

        private void CommandManager_RequerySuggested(object sender, EventArgs e)
        {
            NotifyAll();
        }

        private void NotifyAll()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_commands)
                {
                    for (int i = _commands.Count - 1; i >= 0; i--)
                    {
                        if (_commands[i].TryGetTarget(out IRelayCommand command))
                        {
                            command.NotifyCanExecuteChanged();
                        }
                        else
                        {
                            _commands.RemoveAt(i);
                        }
                    }
                }
            });
        }

        public static void Register(IRelayCommand command)
        {
            lock (_instance._commands)
            {
                _instance._commands.Add(new WeakReference<IRelayCommand>(command));
            }
        }

        internal static void RegisterAllProperties(object objectInstance)
        {
            PropertyInfo[] props = objectInstance.GetType().GetProperties();
            Type relayCommandInterface = typeof(IRelayCommand);

            foreach(PropertyInfo property in props)
            {
                if (relayCommandInterface.IsAssignableFrom(property.PropertyType))
                {
                    Register((IRelayCommand)property.GetValue(objectInstance));
                }
            }
        }
    }
}
