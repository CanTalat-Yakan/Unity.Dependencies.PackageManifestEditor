using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Newtonsoft.Json;

namespace UnityEssentials
{
    public static class PackageManifestEditorOpener
    {
        [MenuItem("Assets/Edit Package Manifest", true)]
        private static bool ValidateOpenPackageEditor()
        {
            string path = GetSelectedPath();
            return Path.GetFileName(path) == "package.json";
        }

        [MenuItem("Assets/Edit Package Manifest", false, 1001)]
        private static void OpenPackageEditor()
        {
            string path = GetSelectedPath();
            PackageManifestEditor.Show(path);
        }

        private static string GetSelectedPath() =>
            AssetDatabase.GetAssetPath(Selection.activeObject);
    }

    public class PackageManifestEditor
    {
        public EditorWindowDrawer window;
        public Action Repaint;
        public Action Close;

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

        public PackageManifestEditor(string jsonPath) =>
            _jsonPath = jsonPath;

        public static void Show(string filePath)
        {
            var editor = new PackageManifestEditor(filePath);
            var window = new EditorWindowDrawer("Edit Package Manifest", new(700, 800), new(400, 500))
                .SetPreProcess(editor.PreProcess)
                .SetHeader(editor.Header)
                .SetBody(editor.Body)
                .SetFooter(editor.Footer)
                .GetRepaintEvent(out editor.Repaint)
                .GetCloseEvent(out editor.Close)
                .ShowUtility();
        }

        private void PreProcess()
        {
            if (string.IsNullOrEmpty(_jsonPath) || !File.Exists(_jsonPath))
                _jsonData = new();
            else
            {
                string json = File.ReadAllText(_jsonPath);
                _jsonData = JsonConvert.DeserializeObject<PackageJson>(json) ?? new PackageJson();

                _jsonData.dependencies ??= new();
                InitializeDependenciesList();

                _jsonData.keywords ??= new();
                InitializeKeywordsList();

                _jsonData.samples ??= new();
                InitializeSamplesList();

                _hasMinimalUnityVersion = !string.IsNullOrEmpty(_jsonData.unityRelease);
            }

            _initialized = true;
        }

        private void InitializeDependenciesList()
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

        private void InitializeKeywordsList()
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

        private void InitializeSamplesList()
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
                    displayName = "",
                    description = "",
                    path = "Samples~/"
                });
            };
        }

        private void Save()
        {
            _jsonData.dependencies.Clear();
            foreach (var dependency in _dependencies)
                _jsonData.dependencies[dependency.name] = dependency.version;

            File.WriteAllText(_jsonPath, _jsonData.ToJson());
            AssetDatabase.Refresh();
        }

        private void Header()
        {
            if (!_initialized)
            {
                EditorGUILayout.LabelField("Loading...");
                return;
            }

            if (_jsonData == null)
            {
                EditorGUILayout.LabelField("Invalid or missing package.json.");
                if (GUILayout.Button("Close"))
                    Close();
                return;
            }
        }

        private void Body()
        {
            EditorGUILayout.LabelField("Information", EditorStyles.boldLabel);

            EditorGUI.indentLevel++;
            {
                ParsePackageName(_jsonData.name, out string organizationName, out string packageName);

                // Name: sanitized, used for the package name
                packageName = EditorGUILayout.TextField("Name", packageName);
                organizationName = EditorGUILayout.TextField("Organization name", organizationName);

                // DisplayName: plain, user-editable, not sanitized
                _jsonData.displayName = EditorGUILayout.TextField("Display Name", _jsonData.displayName);

                packageName = SanitizeNamePart(packageName);
                organizationName = SanitizeNamePart(organizationName);

                _jsonData.name = ComposePackageName(organizationName, packageName);

                _jsonData.version = EditorGUILayout.TextField("Version", _jsonData.version);

                _hasMinimalUnityVersion = EditorGUILayout.Toggle("Minimal Unity Version", _hasMinimalUnityVersion);

                // Unity Version Section
                var unityVersionParts = (_jsonData.unity ?? "2022.1").Split('.');
                int unityMajor = 0, unityMinor = 0;
                int.TryParse(unityVersionParts[0], out unityMajor);
                if (unityVersionParts.Length > 1)
                    int.TryParse(unityVersionParts[1], out unityMinor);

                unityMajor = EditorGUILayout.IntField("Major", unityMajor);
                unityMinor = EditorGUILayout.IntField("Minor", unityMinor);
                _jsonData.unity = $"{unityMajor}.{unityMinor}";

                if (_hasMinimalUnityVersion)
                    _jsonData.unityRelease = EditorGUILayout.TextField("Release", _jsonData.unityRelease);
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Description");
            GUIStyle wordWrapStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            _jsonData.description = EditorGUILayout.TextArea(_jsonData.description, wordWrapStyle, GUILayout.MinHeight(80));

            EditorGUILayout.Space(10);

            if (_dependenciesList == null)
                InitializeDependenciesList();
            _dependenciesList.DoLayoutList();

            EditorGUILayout.Space();

            if (_keywordsList == null)
                InitializeKeywordsList();
            _keywordsList.DoLayoutList();

            EditorGUILayout.Space();

            if (_samplesList == null)
                InitializeSamplesList();
            _samplesList.DoLayoutList();

            EditorGUILayout.Space(10);

            // Author fields
            _authorFoldout = EditorGUILayout.Foldout(_authorFoldout, "Author", true);
            if (_authorFoldout)
            {
                EditorGUI.indentLevel++;
                _jsonData.author.name = EditorGUILayout.TextField("Name", _jsonData.author.name);
                _jsonData.author.email = EditorGUILayout.TextField("Email", _jsonData.author.email);
                _jsonData.author.url = EditorGUILayout.TextField("URL", _jsonData.author.url);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(10);

            // Links foldout
            _linksFoldout = EditorGUILayout.Foldout(_linksFoldout, "Links", true);
            if (_linksFoldout)
            {
                EditorGUI.indentLevel++;
                _jsonData.documentationUrl = EditorGUILayout.TextField("Documentation URL", _jsonData.documentationUrl);
                _jsonData.changelogUrl = EditorGUILayout.TextField("Changelog URL", _jsonData.changelogUrl);
                _jsonData.licensesUrl = EditorGUILayout.TextField("Licenses URL", _jsonData.licensesUrl);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);

            _advancedFoldout = EditorGUILayout.Foldout(_advancedFoldout, "Advanced", true);
            if (_advancedFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    "If unchecked, the assets in this package will always " +
                    "be visible in the Project window and Object Picker." +
                    "\n(Default: hidden)", MessageType.Info);
                _jsonData.hideInEditor = EditorGUILayout.Toggle(new GUIContent("Hide In Editor"), _jsonData.hideInEditor);
                EditorGUI.indentLevel--;
            }
        }

        private void Footer()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Revert", GUILayout.Width(100)))
            {
                PreProcess();
            }
            if (GUILayout.Button("Apply", GUILayout.Width(100)))
            {
                Save();
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ParsePackageName(string fullName, out string org, out string disp)
        {
            org = "";
            disp = "";
            if (string.IsNullOrEmpty(fullName)) return;
            var parts = fullName.Split('.');
            if (parts.Length >= 3 && parts[0] == "com")
            {
                org = parts[1];
                disp = string.Join(".", parts, 2, parts.Length - 2);
            }
        }

        private string ComposePackageName(string organization, string name)
        {
            organization = SanitizeNamePart(organization);
            name = SanitizeNamePart(name);
            return $"com.{organization}.{name}";
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