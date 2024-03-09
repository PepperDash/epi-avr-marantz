
using System;

#if SERIES4
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#else
using PDT.Plugins.Marantz.Interfaces;
#endif

namespace PDT.Plugins.Marantz
{
    public class MarantzInput : IInput
    {
        private bool _isSelected;

        private readonly string _inputCommand;
        private readonly MarantzDevice _parent;

        public MarantzInput(string key, string name, MarantzDevice parent, string inputCommand)
        {
            Key = key;
            Name = name;
            _parent = parent;
            _inputCommand = inputCommand;
        }

        public string Key { get; private set; }
        public string Name { get; private set; }

        public event EventHandler InputUpdated;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                var oldValue = _isSelected;
                _isSelected = value;

                if (oldValue != _isSelected)
                {
                    var handler = InputUpdated;
                    if (handler != null)
                        handler(this, EventArgs.Empty);
                }
            }
        }

        public void Select()
        {
            _parent.SetInput(_inputCommand);
        }
    }
}