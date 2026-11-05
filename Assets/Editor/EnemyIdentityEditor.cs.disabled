using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class EnemyIdentityEditor
{
    private const string IdentityPropertyName = "stableIdentity";
    private static bool _processingScenes;

    static EnemyIdentityEditor()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/Project Spear/Migrate Enemy Identities In Build Scenes")]
    private static void MigrateBuildScenes()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (ProcessBuildScenes(saveChangesToLoadedScenes: true))
            Debug.Log("Stable enemy identities are assigned in enabled build scenes.");
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (_processingScenes || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        ProcessScene(scene, out _);
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (_processingScenes || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        ProcessScene(scene, out _);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode || _processingScenes)
            return;

        if (!ProcessBuildScenes(saveChangesToLoadedScenes: false))
        {
            Debug.LogError("Play Mode was cancelled because one or more scene enemies do not have a persistent GlobalObjectId. Save those scenes and try again.");
            EditorApplication.isPlaying = false;
        }
    }

    private static bool ProcessBuildScenes(bool saveChangesToLoadedScenes)
    {
        Scene originalActiveScene = SceneManager.GetActiveScene();
        bool allIdentitiesValid = true;
        _processingScenes = true;

        try
        {
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled || string.IsNullOrEmpty(buildScene.path) || !File.Exists(buildScene.path))
                    continue;

                Scene scene = SceneManager.GetSceneByPath(buildScene.path);
                bool openedByThisPass = !scene.IsValid() || !scene.isLoaded;

                try
                {
                    if (openedByThisPass)
                        scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Additive);

                    bool sceneValid = ProcessScene(scene, out bool changed);
                    allIdentitiesValid &= sceneValid;

                    if (changed && (openedByThisPass || saveChangesToLoadedScenes) && !EditorSceneManager.SaveScene(scene))
                    {
                        allIdentitiesValid = false;
                        Debug.LogError($"Could not save enemy identities to scene '{buildScene.path}'.");
                    }
                }
                catch (Exception exception)
                {
                    allIdentitiesValid = false;
                    Debug.LogException(exception);
                }
                finally
                {
                    if (openedByThisPass && scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
        finally
        {
            if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
                SceneManager.SetActiveScene(originalActiveScene);

            _processingScenes = false;
        }

        return allIdentitiesValid;
    }

    private static bool ProcessScene(Scene scene, out bool changed)
    {
        changed = false;

        if (!scene.IsValid() || !scene.isLoaded)
            return false;

        List<IdentityRecord> records = new();
        Dictionary<string, int> identityCounts = new(StringComparer.Ordinal);
        bool allIdentitiesValid = true;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (BaseEnemy enemy in root.GetComponentsInChildren<BaseEnemy>(true))
            {
                if (PrefabUtility.IsPartOfPrefabAsset(enemy.gameObject) ||
                    PrefabStageUtility.GetPrefabStage(enemy.gameObject) != null)
                {
                    continue;
                }

                GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(enemy);
                if ((int)globalId.identifierType == 0)
                {
                    allIdentitiesValid = false;
                    Debug.LogError("Cannot assign a stable enemy identity until the scene object has a persistent GlobalObjectId.", enemy);
                    continue;
                }

                SerializedObject serializedEnemy = new(enemy);
                SerializedProperty identityProperty = serializedEnemy.FindProperty(IdentityPropertyName);
                if (identityProperty == null)
                {
                    allIdentitiesValid = false;
                    Debug.LogError($"BaseEnemy is missing serialized field '{IdentityPropertyName}'.", enemy);
                    continue;
                }

                string serializedIdentity = identityProperty.stringValue;
                string objectIdentity = globalId.ToString();
                records.Add(new IdentityRecord(enemy, serializedIdentity, objectIdentity));

                if (!string.IsNullOrEmpty(serializedIdentity))
                {
                    identityCounts.TryGetValue(serializedIdentity, out int count);
                    identityCounts[serializedIdentity] = count + 1;
                }
            }
        }

        foreach (KeyValuePair<string, int> identityCount in identityCounts)
        {
            if (identityCount.Value > 1)
                Debug.LogWarning($"Duplicate enemy identity '{identityCount.Key}' found in scene '{scene.path}'. Invalid instances will be repaired.");
        }

        HashSet<string> objectIdentities = new(StringComparer.Ordinal);
        HashSet<string> seenIdentities = new(StringComparer.Ordinal);
        HashSet<string> assignedIdentities = new(identityCounts.Keys, StringComparer.Ordinal);
        foreach (IdentityRecord record in records)
        {
            if (!objectIdentities.Add(record.ObjectIdentity))
            {
                allIdentitiesValid = false;
                Debug.LogError($"Unity returned a duplicate GlobalObjectId '{record.ObjectIdentity}' in scene '{scene.path}'.", record.Enemy);
                continue;
            }

            bool hasSerializedIdentity = !string.IsNullOrEmpty(record.SerializedIdentity);
            bool firstSerializedIdentity = hasSerializedIdentity && seenIdentities.Add(record.SerializedIdentity);
            if (firstSerializedIdentity)
                continue;

            if (hasSerializedIdentity &&
                identityCounts.TryGetValue(record.SerializedIdentity, out int count) &&
                count > 1)
            {
                Debug.LogWarning($"Repairing duplicate enemy identity on '{record.Enemy.name}'.", record.Enemy);
            }

            string newIdentity = record.ObjectIdentity;
            int suffix = 1;
            while (!assignedIdentities.Add(newIdentity))
                newIdentity = $"{record.ObjectIdentity}#{suffix++}";

            if (SetIdentity(record.Enemy, newIdentity))
                changed = true;
        }

        if (changed)
            EditorSceneManager.MarkSceneDirty(scene);

        return allIdentitiesValid;
    }

    private static bool SetIdentity(BaseEnemy enemy, string identity)
    {
        SerializedObject serializedEnemy = new(enemy);
        SerializedProperty identityProperty = serializedEnemy.FindProperty(IdentityPropertyName);
        if (identityProperty == null || identityProperty.stringValue == identity)
            return false;

        Undo.RecordObject(enemy, "Assign Stable Enemy Identity");
        identityProperty.stringValue = identity;
        bool changed = serializedEnemy.ApplyModifiedProperties();

        if (changed)
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemy);

        return changed;
    }

    private sealed class IdentityRecord
    {
        public readonly BaseEnemy Enemy;
        public readonly string SerializedIdentity;
        public readonly string ObjectIdentity;

        public IdentityRecord(BaseEnemy enemy, string serializedIdentity, string objectIdentity)
        {
            Enemy = enemy;
            SerializedIdentity = serializedIdentity;
            ObjectIdentity = objectIdentity;
        }
    }
}
