using Newtonsoft.Json;
using PepperDash.Essentials.Core.DeviceTypeInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PDT.Plugins.Marantz
{
    public class MarantzSurroundModes : ISelectableItems<eSurroundModes>
    {
        private Dictionary<eSurroundModes, ISelectableItem> _items = new Dictionary<eSurroundModes, ISelectableItem>();

        public Dictionary<eSurroundModes, ISelectableItem> Items
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

    public class MarantzSurroundMode : ISelectableItem
    {
        private bool _isSelected;

        private readonly string _command;
        private readonly string _matchString;
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
            _parent.SetSurroundSoundMode(_command);
        }
    }
}
