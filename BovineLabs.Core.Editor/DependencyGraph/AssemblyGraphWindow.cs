﻿// <copyright file="AssemblyGraphWindow.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Core.Editor.DependencyGraph
{
    using System;
    using System.Collections.Generic;
    using BovineLabs.Core.Editor.AssemblyBuilder;
    using BovineLabs.Core.Editor.UI;
    using UnityEditor;
    using UnityEditorInternal;
    using UnityEngine;
    using UnityEngine.UIElements;
    using Object = UnityEngine.Object;

    /// <summary> Based off the Dependency Graph for units DOTS shooter. </summary>
    public class AssemblyGraphWindow : EditorWindow
    {
        private const string RootUIPath = "Packages/com.bovinelabs.core/Editor Default Resources/AssemblyGraphWindow/";
        private static readonly UITemplate AssemblyGraphWindowTemplate = new(RootUIPath + "AssemblyGraphWindow");

        private readonly List<DependencyData> dependencyData = new();
        private ScrollView? content;
        private DropdownField? mode;

        [MenuItem("BovineLabs/Tools/Assembly Graph", priority = 1011)]
        private static void Execute()
        {
            GetWindow<AssemblyGraphWindow>(false, "Assembly Graph", true);
        }

        private static VisualElement CreateAssetButton(string label, Object asset)
        {
            var parent = new VisualElement();
            parent.AddToClassList("horizontal");

            var thumbnail = new VisualElement();
            var preview = AssetPreview.GetMiniThumbnail(asset);
            thumbnail.style.backgroundImage = preview;
            thumbnail.AddToClassList("thumbnail");
            parent.Add(thumbnail);

            var button = new Button(() => Selection.activeObject = asset) { text = label };
            button.AddToClassList("asset-button");
            parent.Add(button);

            return parent;
        }

        private static VisualElement CreateDependencyButton(string label, Object asset, bool isDependency)
        {
            var parent = new VisualElement();
            parent.AddToClassList("horizontal");

            var direction = new Label(isDependency ? "<" : ">");
            direction.AddToClassList("dependency-arrow");
            parent.Add(direction);

            var assetButton = CreateAssetButton(label, asset);
            parent.Add(assetButton);

            return parent;
        }

        private void OnEnable()
        {
            this.rootVisualElement.Clear();
            AssemblyGraphWindowTemplate.Clone(this.rootVisualElement);

            var findButton = this.rootVisualElement.Q<Button>("Find");
            findButton.clicked += this.Find;

            this.mode = this.rootVisualElement.Q<DropdownField>("Mode");
            this.mode.RegisterValueChangedCallback(_ => this.Clear());

            this.content = this.rootVisualElement.Q<ScrollView>("Content");
        }

        private void Find()
        {
            this.Clear();

            switch (this.mode!.index)
            {
                case 0:
                    this.FindAssetDependencies();
                    break;
                case 1:
                    this.FindAssetThatDependsOn();
                    break;
            }

            this.CreateContent();
        }

        private void Clear()
        {
            this.dependencyData.Clear();
            this.content!.Clear();
        }

        private void CreateContent()
        {
            // Sort dependency lists
            foreach (var data in this.dependencyData)
            {
                data.Dependencies.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));
            }

            foreach (var data in this.dependencyData)
            {
                this.content!.Add(CreateAssetButton(data.AssetPath, data.Asset));

                foreach (var (asset, path) in data.Dependencies)
                {
                    this.content.Add(CreateDependencyButton(path, asset, this.mode!.index == 0));
                }
            }
        }

        private void FindAssetDependencies()
        {
            // foreach (var selected in Selection.objects)
            // {
            //     if (!AssetDatabase.IsMainAsset(selected))
            //     {
            //         Debug.LogWarning($"Asset {selected} is not a main asset");
            //         continue;
            //     }
            //
            //     var path = AssetDatabase.GetAssetPath(selected);
            //     var dependencies = new List<string>(AssetDatabase.GetDependencies(path));
            //     dependencies.Remove(path); // GetDependencies returns itself
            //
            //     var data = new DependencyData
            //     {
            //         AssetPath = path,
            //         Asset = selected,
            //     };
            //     data.Dependencies.AddRange(dependencies.Select(s => (AssetDatabase.LoadAssetAtPath<Object>(s), s)));
            //
            //     this.dependencyData.Add(data);
            // }
        }

        private void FindAssetThatDependsOn()
        {
            var selectedObjectNames = new List<string>();
            var selectedObjectGUIDs = new List<string>();

            foreach (var selected in Selection.objects)
            {
                if (selected is not AssemblyDefinitionAsset assemblyDefinitionAsset)
                {
                    continue;
                }

                if (!AssetDatabase.IsMainAsset(selected))
                {
                    continue;
                }

                var path = AssetDatabase.GetAssetPath(selected);
                if (AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                var data = new DependencyData(assemblyDefinitionAsset, path);

                this.dependencyData.Add(data);

                selectedObjectNames.Add(assemblyDefinitionAsset.name);
                selectedObjectGUIDs.Add(AssetDatabase.GUIDFromAssetPath(path).ToString());
            }

            // Iterate all assets
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            for (var i = 0; i < guids.Length; i++)
            {
                var guid = guids[i];
                var path = AssetDatabase.GUIDToAssetPath(guid);
                EditorUtility.DisplayProgressBar("Searching", "AssemblyDefinitionAsset:" + path + " " + i + "/" + guids.Length, (float)i / guids.Length);

                if (selectedObjectGUIDs.Contains(path))
                {
                    continue;
                }

                var assemblyDefinition = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);

                var json = AssemblyDefinitionTemplate.New();
                JsonUtility.FromJsonOverwrite(assemblyDefinition.text, json);

                foreach (var dependency in json.references)
                {
                    var index = selectedObjectNames.IndexOf(dependency);
                    if (index != -1)
                    {
                        this.dependencyData[index].Dependencies.Add((AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path), path));
                        break;
                    }

                    index = selectedObjectGUIDs.IndexOf(dependency);
                    if (index != -1)
                    {
                        this.dependencyData[index].Dependencies.Add((AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path), path));
                        break;
                    }
                }
            }

            EditorUtility.ClearProgressBar();
        }

        private record DependencyData(AssemblyDefinitionAsset Asset, string AssetPath)
        {
            public List<(AssemblyDefinitionAsset Asset, string Path)> Dependencies { get; } = new();

            public AssemblyDefinitionAsset Asset { get; } = Asset;

            public string AssetPath { get; } = AssetPath;
        }
    }
}
