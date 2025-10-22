using Newtonsoft.Json;
using PepperDash.Core;
using PepperDash.Core.Logging;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDT.Plugins.Marantz
{
    public class MarantzSurroundModes : ISelectableItems<SurroundModes>, IKeyName
    {
        public string Key { get; private set; }

        public string Name { get; private set; }

        private Dictionary<SurroundModes, ISelectableItem> _items = new Dictionary<SurroundModes, ISelectableItem>();

        public Dictionary<SurroundModes, ISelectableItem> Items
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

        private SurroundModes _currentItem;

        public SurroundModes CurrentItem
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

        public MarantzSurroundModes(string key, string name)
        {
            Key = key;
            Name = name;
        }
    }

    public class MarantzSurroundMode : ISelectableItem
    {
        private bool _isSelected;

        private readonly string _command;
        private readonly string[] _matchStrings;
        private readonly MarantzDevice _parent;

        public string Key { get; private set; }
        public string Name { get; private set; }

        [JsonIgnore]
        public string Command
        {
            get
            {
                return _command;
            }
        }

        [JsonIgnore]
        public string[] MatchStrings => _matchStrings.ToArray();

        public MarantzSurroundMode(string key, string name, MarantzDevice parent, string command, string[] matchStrings = null)
        {
            Key           = key;
            Name          = name;
            _parent       = parent;
            _command = command;
            this.LogVerbose("MaranztSurroundMode: Setting MatchStrings for {name} to: {@matchStrings}", name, matchStrings);
            _matchStrings = matchStrings ?? new[] { command };
        }

        public event EventHandler ItemUpdated;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (value == _isSelected)
                    return;

                _isSelected = value;
                ItemUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Select()
        {
            _parent.SetSurroundSoundMode(_command);
        }
    }
}
