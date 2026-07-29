using System.Collections.Generic;
using System.Text;

namespace PascalScene
{
    public sealed class PascalSceneBuildReport
    {
        public int ImportedNodeCount { get; internal set; }
        public int SkippedNodeCount { get; internal set; }
        public bool CatalogVersionMismatch { get; internal set; }
        public List<string> MissingModelIds { get; } = new();
        public List<string> UnsupportedNodes { get; } = new();
        public List<string> Warnings { get; } = new();

        public override string ToString()
        {
            var summary = new StringBuilder();
            summary.Append($"Imported {ImportedNodeCount} node(s); skipped {SkippedNodeCount}.");
            if (MissingModelIds.Count > 0)
            {
                summary.Append($" Missing models: {string.Join(", ", MissingModelIds)}.");
            }

            if (UnsupportedNodes.Count > 0)
            {
                summary.Append($" Unsupported: {string.Join(", ", UnsupportedNodes)}.");
            }

            if (Warnings.Count > 0)
            {
                summary.Append($" Warnings: {string.Join(" | ", Warnings)}");
            }

            return summary.ToString();
        }
    }
}
