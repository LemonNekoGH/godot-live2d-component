/**
 * Copyright(c) Live2D Inc. All rights reserved.
 *
 * Use of this source code is governed by the Live2D Open Software license
 * that can be found at https://www.live2d.com/eula/live2d-open-software-license-agreement_en.html.
 */

using System.Collections.Generic;
using Live2D.Cubism.Framework.Json;

namespace Live2D.Cubism.Framework.UserData
{
    /// <summary>
    /// Target type for user data.
    /// </summary>
    public enum CubismUserDataTargetType
    {
        ArtMesh
    }

    /// <summary>
    /// A single user data entry associated with a drawable.
    /// </summary>
    public class CubismUserDataBody
    {
        public string Id { get; set; }
        public string Value { get; set; }
        public CubismUserDataTargetType TargetType { get; set; }
    }

    /// <summary>
    /// Manages user data for a model. Mirrors Unity's CubismUserDataTag system.
    /// </summary>
    public class CubismUserDataController
    {
        private readonly List<CubismUserDataBody> _entries = new();

        public IReadOnlyList<CubismUserDataBody> Entries => _entries;

        public void Initialize(CubismUserData3Json json)
        {
            _entries.Clear();
            if (json?.UserData == null) return;

            foreach (var entry in json.UserData)
            {
                _entries.Add(new CubismUserDataBody
                {
                    Id = entry.Id,
                    Value = entry.Value,
                    TargetType = entry.Target == "ArtMesh"
                        ? CubismUserDataTargetType.ArtMesh
                        : CubismUserDataTargetType.ArtMesh
                });
            }
        }

        public CubismUserDataBody FindById(string id)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Id == id) return _entries[i];
            return null;
        }
    }
}
