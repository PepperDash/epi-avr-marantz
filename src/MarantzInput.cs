
using System;
using System.Collections.Generic;

#if SERIES4
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
#else
using PDT.Plugins.Marantz.Interfaces;
#endif

namespace PDT.Plugins.Marantz
{
    public class MarantzInputs : ISelectableItems<string>
    {
        private Dictionary<string, ISelectableItem> _items = new Dictionary<string, ISelectableItem>();

        public Dictionary<string, ISelectableItem> Items
        {
            get
            {
                return _items;
            }
            set
            {
                if (_items == value)
                    return;

                _items = value;

                ItemsUpdated?.Invoke(this, null);
            }
        }

        private string _currentItem;

        public string CurrentItem
        {
            get
            {
                return _currentItem;
            }
            set
            {
                if (_currentItem == value)
                    return;

                _currentItem = value;

                CurrentItemChanged?.Invoke(this, null);
            }
        }

        public event EventHandler ItemsUpdated;
        public event EventHandler CurrentItemChanged;

    }

    public class MarantzInput : ISelectableItem
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

        public event EventHandler ItemUpdated;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (value == _isSelected)
                    return;

                _isSelected = value;
                var handler = ItemUpdated;
                if (handler != null)
                    handler(this, EventArgs.Empty);
            }
        }

        public void Select()
        {
            _parent.SetInput(_inputCommand);
        }
    }

    public class MarantzZone2Input : ISelectableItem
    {
        private bool _isSelected;

        private readonly string _inputCommand;
        private readonly MarantzZone2 _parent;

        public MarantzZone2Input(string key, string name, MarantzZone2 parent, string inputCommand)
        {
            Key = key;
            Name = name;
            _parent = parent;
            _inputCommand = inputCommand;
        }

        public string Key { get; private set; }
        public string Name { get; private set; }

        public event EventHandler ItemUpdated;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (value == _isSelected)
                    return;

                _isSelected = value;
                var handler = ItemUpdated;
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