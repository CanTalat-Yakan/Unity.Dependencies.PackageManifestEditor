#if UNITY_EDITOR
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UnityEssentials
{
    public partial class PackageManifest
    {
        private string _jsonPath;
        private PackageJson _jsonData;
        private ReorderableList _dependenciesList;
        private List<Dependency> _dependencies = new();
        private ReorderableList _keywordsList;
        private ReorderableList _samplesList;
        private bool _hasMinimalUnityVersion = false;
        private bool _authorFoldout = true;
        private bool _linksFoldout = true;
        private bool _advancedFoldout = false;
        private bool _initialized = false;

        public PackageManifest(string path) =>
            _jsonPath = path;

        public void Save()
        {
            _jsonData.dependencies.Clear();
            foreach (var dependency in _dependencies)
                _jsonData.dependencies[dependency.name] = dependency.version;

            File.WriteAllText(_jsonPath, _jsonData.ToJson());
            AssetDatabase.Refresh();

            Close();
        }

        public void InitializeDependenciesList()
        {
            if (!_initialized || _jsonData == null || _jsonData.dependencies == null)
            {
                _dependencies.Clear();
                foreach (var kvp in _jsonData.dependencies)
                    _dependencies.Add(new Dependency { name = kvp.Key, version = kvp.Value });
            }

            _dependenciesList = new ReorderableList(_dependencies, typeof(Dependency), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Dependencies"),
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var element = _dependencies[index];
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;

                    // Calculate widths based on available space
                    float nameWidth = rect.width * 0.6f;
                    float versionWidth = rect.width * 0.4f - 20;

                    // Name field
                    string displayName = element.name;
                    displayName = EditorGUI.TextField(
                        new Rect(rect.x, rect.y, nameWidth, rect.height),
                        GUIContent.none,
                        displayName);
                    element.name = displayName;

                    // Version field
                    element.version = EditorGUI.TextField(
                        new Rect(rect.x + nameWidth + 5, rect.y, versionWidth, rect.height),
                        GUIContent.none,
                        element.version);
                },
                elementHeight = EditorGUIUtility.singleLineHeight + 4
            };

            _dependenciesList.onAddCallback = list =>
            {
                _dependencies.Add(new Dependency
                {
                    name = "com.example.new-package",
                    version = "1.0.0"
                });
            };
        }

        public void InitializeKeywordsList()
        {
            _keywordsList = new ReorderableList(_jsonData.keywords, typeof(string), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Keywords"),
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    _jsonData.keywords[index] = EditorGUI.TextField(rect, _jsonData.keywords[index]);
                },
                elementHeight = EditorGUIUtility.singleLineHeight + 4
            };

            _keywordsList.onAddCallback = list =>
            {
                _jsonData.keywords.Add("");
            };
        }

        public void InitializeSamplesList()
        {
            _samplesList = new ReorderableList(_jsonData.samples, typeof(Sample), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Samples"),
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var element = _jsonData.samples[index];
                    float y = rect.y + 2;
                    float lineHeight = EditorGUIUtility.singleLineHeight;
                    float spacing = 2;

                    // Display Name
                    element.displayName = EditorGUI.TextField(
                        new Rect(rect.x, y, rect.width, lineHeight),
                        "Display Name", element.displayName);
                    y += lineHeight + spacing;

                    // Description
                    element.description = EditorGUI.TextField(
                        new Rect(rect.x, y, rect.width, lineHeight),
                        "Description", element.description);
                    y += lineHeight + spacing;

                    // Path
                    element.path = EditorGUI.TextField(
                        new Rect(rect.x, y, rect.width, lineHeight),
                        "Path", element.path);

                    // Save back
                    _jsonData.samples[index] = element;
                },
                elementHeightCallback = (index) =>
                {
                    // 3 lines + spacing
                    return (EditorGUIUtility.singleLineHeight + 2) * 3;
                }
            };

            _samplesList.onAddCallback = list =>
            {
                _jsonData.samples.Add(new Sample
                {
                    displayName = string.Empty,
                    description = string.Empty,
                    path = "Samples~/"
                });
            };
        }

        private void ParsePackageName(string fullName, out string organizationName, out string packageName)
        {
            organizationName = string.Empty;
            packageName = string.Empty;

            if (string.IsNullOrEmpty(fullName)) 
                return;

            var parts = fullName.Split('.');
            if (parts.Length >= 3 && parts[0] == "com")
            {
                organizationName = parts[1];
                packageName = string.Join(".", parts, 2, parts.Length - 2);
            }
        }

        private string ComposePackageName(string organizationName, string packageName)
        {
            organizationName = SanitizeNamePart(organizationName);
            packageName = SanitizeNamePart(packageName);
            return $"com.{organizationName}.{packageName}";
        }

        private string SanitizeNamePart(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
            input = input.ToLowerInvariant().Replace(" ", "-");
            input = Regex.Replace(input, @"[^a-z0-9\-]", "");
            return input;
        }

        [Serializable]
        public class PackageJson
        {
            public string name;
            public string version;
            public string displayName;
            public string description;
            public string unity;
            public string unityRelease;
            public Dictionary<string, string> dependencies = new();
            public List<string> keywords = new();
            public Author author = new Author();
            public string documentationUrl;
            public string changelogUrl;
            public string licensesUrl;
            public List<Sample> samples = new();
            public bool hideInEditor = true;

            public string ToJson()
            {
                var data = JsonConvert.SerializeObject(this, Formatting.Indented);
                return data;
            }
        }

        [Serializable]
        public class Author
        {
            public string name;
            public string email;
            public string url;
        }

        [Serializable]
        public class Dependency
        {
            public string name;
            public string version;
        }

        [Serializable]
        public class Sample
        {
            public string displayName;
            public string description;
            public string path;
        }
    }
}
#endif