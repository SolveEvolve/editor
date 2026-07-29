using UnityEngine;

namespace PascalScene
{
    [DisallowMultipleComponent]
    public sealed class PascalSceneIdentity : MonoBehaviour
    {
        [SerializeField] private string nodeId;
        [SerializeField] private string nodeType;

        public string NodeId => nodeId;
        public string NodeType => nodeType;

        public void Configure(string id, string type)
        {
            nodeId = id;
            nodeType = type;
        }
    }
}
