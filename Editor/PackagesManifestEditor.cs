using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

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
        private bool hasMinimalUnityVersion = false;
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
                data = JsonUtility.FromJson<PackageJson>(json) ?? new PackageJson();
                data.dependencies ??= new List<Dependency>();
                InitializeDependenciesList();

                hasMinimalUnityVersion = !string.IsNullOrEmpty(data.unityRelease);
            }
            else
            {
                data = new PackageJson();
            }
            initialized = true;
        }

        private void InitializeDependenciesList()
        {
            dependenciesList = new ReorderableList(data.dependencies, typeof(Dependency), true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Dependencies"),
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var element = data.dependencies[index];
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
                data.dependencies.Add(new Dependency
                {
                    name = "com.unity.new-package",
                    version = "1.0.0"
                });
            };
        }

        private void Save()
        {
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

            scroll = EditorGUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar);
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));

            EditorGUILayout.LabelField("Information", EditorStyles.boldLabel);

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

            // Description with word wrapping
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Description");
            GUIStyle wordWrapStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            data.description = EditorGUILayout.TextArea(data.description, wordWrapStyle, GUILayout.MinHeight(80));

            EditorGUILayout.Space(10);

            if (dependenciesList == null) InitializeDependenciesList();
            dependenciesList.DoLayoutList();

            EditorGUILayout.Space(10);

            data.visibilityInEditor = (Visibility)EditorGUILayout.EnumPopup("Visibility in Editor", data.visibilityInEditor);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);
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
            public string unityRelease; // Now string, can be null
            public List<Dependency> dependencies = new List<Dependency>();
            public Visibility visibilityInEditor = Visibility.Default;

            public string ToJson()
            {
                // Temporarily remove unityRelease if null or empty
                string originalUnityRelease = unityRelease;
                if (string.IsNullOrWhiteSpace(unityRelease))
                    unityRelease = null;

                string json = JsonUtility.ToJson(this, true);

                // Restore the original value
                unityRelease = originalUnityRelease;
                return json;
            }
        }

        [Serializable]
        private class Dependency
        {
            public string name;
            public string version;
        }

        private enum Visibility
        {
            Default,
            Hidden,
            Visible
        }
    }
}