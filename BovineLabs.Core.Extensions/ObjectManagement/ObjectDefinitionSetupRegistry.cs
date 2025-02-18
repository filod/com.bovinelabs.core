﻿// <copyright file="ObjectDefinitionSetupRegistry.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Core.ObjectManagement
{
    using JetBrains.Annotations;
    using Unity.Entities;

    /// <summary> A buffer of all objects in the project where <see cref="ObjectManagement.ObjectDefinition.ID" /> maps to the index. </summary>
    [InternalBufferCapacity(0)]
    internal struct ObjectDefinitionSetupRegistry : IBufferElementData
    {
        [UsedImplicitly] // By ObjectDefinitionSystem
        public Entity Prefab;
    }

    internal struct ObjectDefinitionSetupRegistryInitialized : ICleanupComponentData
    {
    }
}
