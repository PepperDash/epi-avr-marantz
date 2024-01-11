using System;
using PepperDash.Core;

namespace PDT.Plugins.Marantz
{
    public interface IInput : IKeyName
    {
        event EventHandler InputUpdated;
        bool IsSelected { get; }
        void Select();
    }
}