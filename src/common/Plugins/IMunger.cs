using System;
using System.Collections.Generic;
using System.Text;

namespace ironmunge.Plugins
{
    public interface IMunger
    {
        string Name { get; }
        string Description { get; }

        int Execute();
    }
}
