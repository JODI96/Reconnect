using Reconnect.Contracts.Buildings;
using UnityEngine;

namespace Reconnect.Client.City
{
    /// <summary>Placeholder block for a building (replaced by real 3D geometry in phase 6).</summary>
    public sealed class BuildingMarker : MonoBehaviour
    {
        public BuildingDto Building { get; private set; }
        public MeshRenderer Renderer { get; private set; }

        /// <summary>World position just above the roof – anchor for the name label.</summary>
        public Vector3 LabelAnchor { get; private set; }

        public void Initialize(BuildingDto building, MeshRenderer meshRenderer, float height)
        {
            Building = building;
            Renderer = meshRenderer;
            LabelAnchor = new Vector3(transform.position.x, height + 8f, transform.position.z);
        }
    }
}
