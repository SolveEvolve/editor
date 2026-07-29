using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PascalScene
{
    [Serializable]
    public sealed class PascalSceneDocument
    {
        public const int CurrentSchemaVersion = 1;

        [JsonProperty("schemaVersion")]
        public int? SchemaVersion { get; set; }

        [JsonProperty("catalogVersion")]
        public string CatalogVersion { get; set; }

        [JsonProperty("materialLibraryVersion")]
        public string MaterialLibraryVersion { get; set; }

        [JsonProperty("nodes")]
        public Dictionary<string, PascalSceneNode> Nodes { get; set; } = new();

        [JsonProperty("rootNodeIds")]
        public List<string> RootNodeIds { get; set; } = new();

        [JsonProperty("installedPlugins")]
        public List<string> InstalledPlugins { get; set; } = new();

        [JsonProperty("collections")]
        public JObject Collections { get; set; } = new();

        [JsonProperty("materials")]
        public Dictionary<string, PascalSceneMaterial> Materials { get; set; } = new();

        public bool IsLegacyDocument => !SchemaVersion.HasValue;

        public static PascalSceneDocument Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Pascal scene JSON is empty.", nameof(json));
            }

            var document = JsonConvert.DeserializeObject<PascalSceneDocument>(json);
            if (document?.Nodes == null || document.RootNodeIds == null)
            {
                throw new JsonSerializationException(
                    "Pascal scene JSON must contain 'nodes' and 'rootNodeIds'.");
            }

            if (!document.IsLegacyDocument)
            {
                if (document.SchemaVersion != CurrentSchemaVersion)
                {
                    throw new JsonSerializationException(
                        $"Unsupported Pascal scene schema version '{document.SchemaVersion}'.");
                }

                if (string.IsNullOrWhiteSpace(document.CatalogVersion) ||
                    string.IsNullOrWhiteSpace(document.MaterialLibraryVersion) ||
                    document.Collections == null ||
                    document.Materials == null)
                {
                    throw new JsonSerializationException(
                        "Versioned Pascal scene JSON is missing catalog, material, collection, or material-library data.");
                }
            }

            document.Collections ??= new JObject();
            document.Materials ??= new Dictionary<string, PascalSceneMaterial>();

            foreach (var pair in document.Nodes)
            {
                if (pair.Value == null)
                {
                    throw new JsonSerializationException($"Node '{pair.Key}' is null.");
                }

                if (string.IsNullOrWhiteSpace(pair.Value.Id))
                {
                    pair.Value.Id = pair.Key;
                }
                else if (!string.Equals(pair.Key, pair.Value.Id, StringComparison.Ordinal))
                {
                    throw new JsonSerializationException(
                        $"Node key '{pair.Key}' does not match node id '{pair.Value.Id}'.");
                }

                if (string.IsNullOrWhiteSpace(pair.Value.Type))
                {
                    throw new JsonSerializationException($"Node '{pair.Key}' has no type.");
                }
            }

            foreach (var rootNodeId in document.RootNodeIds)
            {
                if (!document.Nodes.ContainsKey(rootNodeId))
                {
                    throw new JsonSerializationException($"Root node reference '{rootNodeId}' does not exist.");
                }
            }

            ValidateChildReferencesAndCycles(document.Nodes);

            return document;
        }

        private static void ValidateChildReferencesAndCycles(
            IReadOnlyDictionary<string, PascalSceneNode> nodes)
        {
            var visiting = new HashSet<string>();
            var visited = new HashSet<string>();
            foreach (var nodeId in nodes.Keys)
            {
                ValidateNode(nodeId);
            }

            void ValidateNode(string nodeId)
            {
                if (visited.Contains(nodeId))
                {
                    return;
                }

                if (!visiting.Add(nodeId))
                {
                    throw new JsonSerializationException($"Cycle detected at node '{nodeId}'.");
                }

                foreach (var childId in nodes[nodeId].Children ?? new List<string>())
                {
                    if (!nodes.ContainsKey(childId))
                    {
                        throw new JsonSerializationException(
                            $"Child reference '{childId}' from node '{nodeId}' does not exist.");
                    }

                    ValidateNode(childId);
                }

                visiting.Remove(nodeId);
                visited.Add(nodeId);
            }
        }
    }

    [Serializable]
    public sealed class PascalSceneNode
    {
        [JsonProperty("object")]
        public string Object { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("parentId")]
        public string ParentId { get; set; }

        [JsonProperty("visible")]
        public bool Visible { get; set; } = true;

        [JsonProperty("children")]
        public List<string> Children { get; set; } = new();

        [JsonProperty("level")]
        public float? Level { get; set; }

        [JsonProperty("height")]
        public float? Height { get; set; }

        [JsonProperty("thickness")]
        public float? Thickness { get; set; }

        [JsonProperty("elevation")]
        public float? Elevation { get; set; }

        [JsonProperty("curveOffset")]
        public float? CurveOffset { get; set; }

        [JsonProperty("start")]
        public float[] Start { get; set; }

        [JsonProperty("end")]
        public float[] End { get; set; }

        [JsonProperty("position")]
        public float[] Position { get; set; }

        [JsonProperty("rotation")]
        public float[] Rotation { get; set; }

        [JsonProperty("scale")]
        public float[] Scale { get; set; }

        [JsonProperty("polygon")]
        public JToken Polygon { get; set; }

        [JsonProperty("holes")]
        public List<List<float[]>> Holes { get; set; } = new();

        [JsonProperty("asset")]
        public PascalAsset Asset { get; set; }

        public List<float[]> GetPolygonPoints()
        {
            if (Polygon == null || Polygon.Type == JTokenType.Null)
            {
                return new List<float[]>();
            }

            var pointsToken = Polygon.Type == JTokenType.Object
                ? Polygon["points"]
                : Polygon;
            return pointsToken?.ToObject<List<float[]>>() ?? new List<float[]>();
        }
    }

    [Serializable]
    public sealed class PascalAsset
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("src")]
        public string SourceUrl { get; set; }

        [JsonProperty("dimensions")]
        public float[] Dimensions { get; set; }

        [JsonProperty("offset")]
        public float[] Offset { get; set; }

        [JsonProperty("rotation")]
        public float[] Rotation { get; set; }

        [JsonProperty("scale")]
        public float[] Scale { get; set; }
    }

    [Serializable]
    public sealed class PascalSceneMaterial
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("material")]
        public JObject Material { get; set; }
    }

    [Serializable]
    public sealed class PascalCatalogManifest
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("catalogVersion")]
        public string CatalogVersion { get; set; }

        [JsonProperty("materialLibraryVersion")]
        public string MaterialLibraryVersion { get; set; }

        [JsonProperty("entries")]
        public List<PascalCatalogEntry> Entries { get; set; } = new();

        public static PascalCatalogManifest Parse(string json)
        {
            var manifest = JsonConvert.DeserializeObject<PascalCatalogManifest>(json);
            if (manifest == null || manifest.SchemaVersion != PascalSceneDocument.CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(manifest.CatalogVersion) || manifest.Entries == null)
            {
                throw new JsonSerializationException("Pascal catalog manifest is invalid.");
            }

            return manifest;
        }

        public PascalCatalogEntry Find(string assetId)
        {
            return Entries.Find(entry => entry.Id == assetId);
        }
    }

    [Serializable]
    public sealed class PascalCatalogEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("localPath")]
        public string LocalPath { get; set; }

        [JsonProperty("sha256")]
        public string Sha256 { get; set; }
    }
}
