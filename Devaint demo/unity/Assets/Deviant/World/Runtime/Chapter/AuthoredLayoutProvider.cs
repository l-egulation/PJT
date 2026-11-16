using System;
using Deviant.World.Contracts;

namespace Deviant.World
{
    public sealed class AuthoredLayoutProvider : IChapterLayoutProvider
    {
        readonly ChapterLayout layout;
        public AuthoredLayoutProvider(ChapterLayout layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            if (layout.Origin.Kind != LayoutOriginKind.Authored) throw new ArgumentException("Expected authored provenance.", nameof(layout));
        }
        public LayoutSupply Provide(LayoutRequest request)
        {
            if (request == null || request.ChapterId != layout.Id || request.LayoutVersion != layout.Version)
                return LayoutSupply.Failure("Requested chapter/layout version is unavailable.");
            // An authored result does not change with a seed or movement edits.
            return LayoutSupply.Success(layout);
        }
    }
}
