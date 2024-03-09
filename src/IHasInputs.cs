using System;
using System.Collections.Generic;

namespace PDT.Plugins.Marantz.Interfaces
{
    public interface IHasInputs
    {
        event EventHandler InputsUpdated;
        IDictionary<string, IInput> Inputs { get; } 
    }
}