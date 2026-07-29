using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PascalScene
{
    [Serializable]
    public sealed class PascalSceneDocument
    {
        [JsonProperty("nodes")]
        public Dictionary<string, PascalSceneNode> Nodes { get; set; } = new();

        [JsonProperty("rootNodeIds")]
        public List<string> RootNodeIds { get; set; } = new();

        [JsonProperty("installedPlugins")]
        public List<string> InstalledPlugins { get; set; } = new();

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
            }

            return document;
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
}
