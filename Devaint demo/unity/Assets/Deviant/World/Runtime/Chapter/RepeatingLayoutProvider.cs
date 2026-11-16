using System;
using Deviant.World.Contracts;

namespace Deviant.World
{
    // The supplied snapshot is already resolved authoring data. This adapter
    // neither chooses a procedural algorithm nor turns templates into regions.
    public sealed class RepeatingLayoutProvider : IChapterLayoutProvider
    {
        readonly ChapterLayout layout;
        public RepeatingLayoutProvider(ChapterLayout layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            if (layout.Repetition == null) throw new ArgumentException("A repeating terrain descriptor is required.", nameof(layout));
        }
        public LayoutSupply Provide(LayoutRequest request)
        {
            if (request == null || request.ChapterId != layout.Id || request.LayoutVersion != layout.Version)
                return LayoutSupply.Failure("The requested repeated source/version is not available.");
            return LayoutSupply.Success(layout);
        }
    }
}
