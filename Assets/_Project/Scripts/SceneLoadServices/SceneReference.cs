using System;
using UnityEngine;

namespace SceneLoadServices
{
    /// <summary>Inspectorで選んだSceneを名前変更に強いGUIDで保持する</summary>
    [Serializable]
    public sealed class SceneReference
    {
        [SerializeField] private string _guid;

        public string Guid => _guid;
        public bool IsAssigned => !string.IsNullOrEmpty(_guid);
    }
}
