using System;
using System.Collections.Generic;
using Deviant.Definitions;

namespace Deviant.Presentation.Contracts
{
    // Capture validates required media without creating outputs. A running owner
    // resolves definitions and prepares views only through this captured version.
    public interface ICueCatalog
    {
        ICueCatalogSnapshot Capture(IReadOnlyList<string> cueIds);
    }
    public interface ICueCatalogSnapshot : ICueViewFactory
    {
        Guid Version { get; }
        ICueDefinition Read(string cueId);
    }
}
