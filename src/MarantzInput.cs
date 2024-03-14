
using System;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;


#if SERIES4
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#else
using PDT.Plugins.Marantz.Interfaces;
#endif

namespace PDT.Plugins.Marantz
{
    public class MarantzSurroundMode : ISelectableItem
    {
        private bool _isSelected;

        private readonly string _command;
        private readonly string _matchString;
        private readonly MarantzDevice _parent;

        public string Key { get; private set; }
        public string Name { get; private set; }

        public string Command { get
            {
                return _command;
            } 
        }
        public string MatchString
        {
            get
            {
                return _matchString;
            }
        }

        public MarantzSurroundMode(string key, string name, MarantzDevice parent, string command, string matchString = "")
        {
            Key = key;
            Name = name;
            _parent = parent;
            _command = command;
            _matchString = string.IsNullOrEmpty(matchString) ? command : matchString;
        }

        public event EventHandler IsSelectedChanged;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (value == _isSelected)
                    return;

                _isSelected = value;

                var handler = IsSelectedChanged;
                if (handler != null)
                    handler(this, EventArgs.Empty);  
            }
        }

        public void Select()
        {
            _parent.SetSurroundMode(_command);
        }
    }


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
                if (value == _isSelected)
                    return;

                _isSelected = value;
                var handler = InputUpdated;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        public void Select()
        {
            _parent.SetInput(_inputCommand);
        }
    }
}