using System;
using System.Collections.Generic;
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

    public class PackageManifestEditor : EditorWindow
    {
        private string jsonPath;
        private PackageJson data;
        private Vector2 scroll;
        private ReorderableList dependenciesList;
        private List<Dependency> dependencies = new();
        private ReorderableList keywordsList;
        private ReorderableList samplesList;
        private bool hasMinimalUnityVersion = false;
        private bool authorFoldout = true;
        private bool linksFoldout = true;
        private bool advancedFoldout = false;
        private bool initialized = false;

        public static void Show(string filePath)
        {
            var window = CreateInstance<PackageManifestEditor>();
            window.titleContent = new GUIContent("Edit Package Manifest");
            window.jsonPath = filePath;
            window.minSize = new Vector2(400, 500); // Minimum reasonable size
            window.Load();
            window.ShowUtility();
        }

        private void Load()
        {
            if (!string.IsNullOrEmpty(jsonPath) && File.Exists(jsonPath))
            {
                string json = File.ReadAllText(jsonPath);
                data = JsonConvert.DeserializeObject<PackageJson>(json) ?? new PackageJson();

                data.dependencies ??= new();
                InitializeDependenciesList();

                data.keywords ??= new();
                InitializeKeywordsList();

                data.samples ??= new();
                InitializeSamplesList();

                hasMinimalUnityVersion = !string.IsNullOrEmpty(data.unityRelease);
            }
            else data = new();

            initialized = true;
        }

        private void InitializeDependenciesList()
        {
            if (!initialized || data == null || data.dependencies == null)
            {
                dependencies.Clear();
                foreach (var kvp in data.dependencies)
                    dependencies.Add(new Dependency { name = kvp.Key, version = kvp.Value });
            }

            dependenciesList = new ReorderableList(dependencies, typeof(Dependency), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Dependencies"),
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var element = dependencies[index];
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

            dependenciesList.onAddCallback = list =>
            {
                dependencies.Add(new Dependency
                {
                    name = "com.example.new-package",
                    version = "1.0.0"
                });
            };
        }

        private void InitializeKeywordsList()
        {
            keywordsList = new ReorderableList(data.keywords, typeof(string), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Keywords"),
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    rect.y += 2;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    data.keywords[index] = EditorGUI.TextField(rect, data.keywords[index]);
                },
                elementHeight = EditorGUIUtility.singleLineHeight + 4
            };

            keywordsList.onAddCallback = list =>
            {
                data.keywords.Add("");
            };
        }

        private void InitializeSamplesList()
        {
            samplesList = new ReorderableList(data.samples, typeof(Sample), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Samples"),
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var element = data.samples[index];
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
                    data.samples[index] = element;
                },
                elementHeightCallback = (index) =>
                {
                    // 3 lines + spacing
                    return (EditorGUIUtility.singleLineHeight + 2) * 3;
                }
            };

            samplesList.onAddCallback = list =>
            {
                data.samples.Add(new Sample
                {
                    displayName = "",
                    description = "",
                    path = "Samples~/"
                });
            };
        }

        private void Save()
        {
            data.dependencies.Clear();
            foreach (var dependency in dependencies)
                data.dependencies[dependency.name] = dependency.version;

            File.WriteAllText(jsonPath, data.ToJson());
            AssetDatabase.Refresh();
        }

        private void OnGUI()
        {
            if (!initialized)
            {
                EditorGUILayout.LabelField("Loading...");
                return;
            }

            if (data == null)
            {
                EditorGUILayout.LabelField("Invalid or missing package.json.");
                if (GUILayout.Button("Close")) Close();
                return;
            }

            EditorGUILayout.BeginVertical("box");
            {
                scroll = EditorGUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar);
                EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                {
                    EditorGUILayout.LabelField("Information", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    {
                        ParsePackageName(data.name, out string organizationName, out string packageName);

                        // Name: sanitized, used for the package name
                        packageName = EditorGUILayout.TextField("Name", packageName);
                        organizationName = EditorGUILayout.TextField("Organization name", organizationName);

                        // DisplayName: plain, user-editable, not sanitized
                        data.displayName = EditorGUILayout.TextField("Display Name", data.displayName);

                        packageName = SanitizeNamePart(packageName);
                        organizationName = SanitizeNamePart(organizationName);

                        data.name = ComposePackageName(organizationName, packageName);

                        data.version = EditorGUILayout.TextField("Version", data.version);

                        hasMinimalUnityVersion = EditorGUILayout.Toggle("Minimal Unity Version", hasMinimalUnityVersion);

                        // Unity Version Section
                        var unityVersionParts = (data.unity ?? "2020.1").Split('.');
                        int unityMajor = 0, unityMinor = 0;
                        int.TryParse(unityVersionParts[0], out unityMajor);
                        if (unityVersionParts.Length > 1)
                            int.TryParse(unityVersionParts[1], out unityMinor);

                        unityMajor = EditorGUILayout.IntField("Major", unityMajor);
                        unityMinor = EditorGUILayout.IntField("Minor", unityMinor);
                        data.unity = $"{unityMajor}.{unityMinor}";

                        if (hasMinimalUnityVersion)
                        {
                            if (!hasMinimalUnityVersion)
                                data.unityRelease = ""; // Create empty field if just enabled

                            EditorGUI.BeginChangeCheck();
                            data.unityRelease = EditorGUILayout.TextField("Release", data.unityRelease);
                            if (EditorGUI.EndChangeCheck())
                                if (string.IsNullOrWhiteSpace(data.unityRelease))
                                    data.unityRelease = null; // Remove if empty
                        }
                        else data.unityRelease = null; // Remove from JSON if unchecked
                    }
                    EditorGUI.indentLevel--;

                    EditorGUILayout.Space(10);

                    EditorGUILayout.LabelField("Description");
                    GUIStyle wordWrapStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
                    data.description = EditorGUILayout.TextArea(data.description, wordWrapStyle, GUILayout.MinHeight(80));

                    EditorGUILayout.Space(10);

                    if (dependenciesList == null)
                        InitializeDependenciesList();
                    dependenciesList.DoLayoutList();

                    EditorGUILayout.Space();

                    if (keywordsList == null)
                        InitializeKeywordsList();
                    keywordsList.DoLayoutList();

                    EditorGUILayout.Space();

                    if (samplesList == null)
                        InitializeSamplesList();
                    samplesList.DoLayoutList();

                    EditorGUILayout.Space(10);

                    // Author fields
                    authorFoldout = EditorGUILayout.Foldout(authorFoldout, "Author", true);
                    if (authorFoldout)
                    {
                        EditorGUI.indentLevel++;
                        data.author.name = EditorGUILayout.TextField("Name", data.author.name);
                        data.author.email = EditorGUILayout.TextField("Email", data.author.email);
                        data.author.url = EditorGUILayout.TextField("URL", data.author.url);
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.Space(10);

                    // Links foldout
                    linksFoldout = EditorGUILayout.Foldout(linksFoldout, "Links", true);
                    if (linksFoldout)
                    {
                        EditorGUI.indentLevel++;
                        data.documentationUrl = EditorGUILayout.TextField("Documentation URL", data.documentationUrl);
                        data.changelogUrl = EditorGUILayout.TextField("Changelog URL", data.changelogUrl);
                        data.licensesUrl = EditorGUILayout.TextField("Licenses URL", data.licensesUrl);
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.Space(10);

                    advancedFoldout = EditorGUILayout.Foldout(advancedFoldout, "Advanced", true);
                    if (advancedFoldout)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.HelpBox(
                            "If unchecked, the assets in this package will always " +
                            "be visible in the Project window and Object Picker." +
                            "\n(Default: hidden)", MessageType.Info);
                        data.hideInEditor = EditorGUILayout.Toggle(new GUIContent("Hide In Editor"),data.hideInEditor);
                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndScrollView();

            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Revert", GUILayout.Width(100)))
            {
                Load();
            }
            if (GUILayout.Button("Apply", GUILayout.Width(100)))
            {
                Save();
                Close();
            }
            EditorGUILayout.EndHorizontal();
        }

        private static readonly System.Text.RegularExpressions.Regex ValidNameRegex = new(@"^[a-z0-9\-]+$");

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
            input = System.Text.RegularExpressions.Regex.Replace(input, @"[^a-z0-9\-]", "");
            return input;
        }

        [Serializable]
        private class PackageJson
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
                var release = unityRelease;
                if (string.IsNullOrEmpty(release))
                    unityRelease = "";
                var data = JsonConvert.SerializeObject(this, Formatting.Indented);
                unityRelease = release;
                return data;
            }

            [Serializable]
            public class Author
            {
                public string name;
                public string email;
                public string url;
            }
        }

        [Serializable]
        private class Dependency
        {
            public string name;
            public string version;
        }

        [Serializable]
        private class Sample
        {
            public string displayName;
            public string description;
            public string path;
        }
    }
}