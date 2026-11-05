using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LegacyMigrationWindow : EditorWindow
{
    private LegacyMigrationReport _report;
    private readonly Dictionary<string, bool> _approved = new Dictionary<string, bool>(StringComparer.Ordinal);
    private Vector2 _scroll;
    private string _resultMessage;
    private MessageType _resultType = MessageType.Info;
    private bool _analysisQueued;

    [MenuItem("Tools/Project Spear/Rooms/Migrate Legacy Connections…")]
    public static void Open()
    {
        LegacyMigrationWindow window = GetWindow<LegacyMigrationWindow>("Legacy Migration");
        window.minSize = new Vector2(620f, 460f);
        window.Show();
    }

    private void OnDisable()
    {
        EditorApplication.update -= RunQueuedAnalysis;
        _analysisQueued = false;
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Analyze Registered Scenes"))
            Analyze();

        if (GUILayout.Button("Assign Missing Door IDs…"))
            AssignMissingDoorIds();

        if (GUILayout.Button("Validate Connections"))
        {
            RoomValidationReport validation = RoomConnectionValidator.Validate();
            _resultMessage = $"Validation: {validation.ErrorCount} error(s), {validation.WarningCount} warning(s).";
            _resultType = validation.HasErrors ? MessageType.Error : MessageType.Info;
            RoomConnectionValidator.ValidateMenu();
        }

        EditorGUILayout.EndHorizontal();

        if (_report == null)
        {
            EditorGUILayout.HelpBox(
                _analysisQueued
                    ? "Analysis is waiting for Unity to finish refreshing assets or compiling scripts."
                    : "Analyze the registered scenes to generate migration proposals.",
                MessageType.Info);
            return;
        }

        if (!string.IsNullOrEmpty(_resultMessage))
            EditorGUILayout.HelpBox(_resultMessage, _resultType);

        DrawDoorIdSummary();
        DrawMigrationSummary();

        int approvedCount = CountApproved();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(
                   approvedCount == 0 || _report.HasIncompleteDoorScan || _analysisQueued))
        {
            if (GUILayout.Button($"Apply Approved Proposals ({approvedCount})", GUILayout.Height(30f)))
                ApplyApproved();
        }

        EditorGUILayout.HelpBox(
            "Analysis and proposal generation never modify scenes or assets. " +
            "Only checked proposals are applied, using RoomConnectionService with rollback on failure. " +
            "Legacy fields (doorID, targetSceneName, targetDoorID) are never cleared.",
            MessageType.None);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        foreach (LegacyMigrationProposal proposal in _report.Proposals)
            DrawProposal(proposal);

        EditorGUILayout.EndScrollView();
    }

    private void DrawDoorIdSummary()
    {
        DoorIdAnalysisReport doorIds = _report.DoorIds;

        if (doorIds == null)
            return;

        string summary =
            $"Door IDs: {doorIds.ValidCount} valid, {doorIds.MissingCount} missing, {doorIds.DuplicateCount} duplicate.";

        EditorGUILayout.HelpBox(summary, doorIds.HasIssues ? MessageType.Warning : MessageType.Info);

        foreach (string message in doorIds.Messages)
            EditorGUILayout.HelpBox(message, MessageType.Warning);

        if (doorIds.DuplicateCount > 0)
        {
            EditorGUILayout.HelpBox(
                "Duplicate door IDs are reported, never regenerated. Resolve them manually or with a separate approved change.",
                MessageType.Warning);
        }
    }

    private void DrawMigrationSummary()
    {
        if (_report.HasIncompleteDoorScan)
        {
            EditorGUILayout.HelpBox(
                "Analysis is incomplete because one or more registered rooms could not be scanned. " +
                "Proposals cannot be applied until all rooms are analyzed successfully.",
                MessageType.Error);
        }
        else if (_analysisQueued)
        {
            EditorGUILayout.HelpBox(
                "Analysis is waiting for Unity to finish refreshing assets or compiling scripts.",
                MessageType.Info);
        }

        EditorGUILayout.HelpBox(
            $"Proposals: {_report.Proposals.Count} total, {_report.SafeCount} safe " +
            $"({_report.BidirectionalCount} bidirectional, {_report.OneWayCount} one-way), " +
            $"{_report.AlreadyMigratedCount} already migrated, {_report.UnsafeCount} unresolved or unsafe.",
            MessageType.Info);

        foreach (string message in _report.Messages)
            EditorGUILayout.HelpBox(message, MessageType.Warning);
    }

    private void DrawProposal(LegacyMigrationProposal proposal)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (proposal.IsSafeToApply)
        {
            _approved.TryGetValue(proposal.Key, out bool approved);
            bool newValue = EditorGUILayout.ToggleLeft(proposal.DisplayLabel, approved);
            if (newValue != approved)
                _approved[proposal.Key] = newValue;
        }
        else
        {
            EditorGUILayout.LabelField(proposal.DisplayLabel, EditorStyles.boldLabel);
        }

        EditorGUILayout.LabelField(proposal.ClassificationLabel, EditorStyles.miniBoldLabel);

        if (!string.IsNullOrEmpty(proposal.Reason))
            EditorGUILayout.LabelField(proposal.Reason, EditorStyles.wordWrappedMiniLabel);

        EditorGUILayout.EndVertical();
    }

    private void Analyze()
    {
        QueueAnalysis();
    }

    private void QueueAnalysis()
    {
        _analysisQueued = true;
        EditorApplication.update -= RunQueuedAnalysis;
        EditorApplication.update += RunQueuedAnalysis;
    }

    private void RunQueuedAnalysis()
    {
        if (!_analysisQueued)
        {
            EditorApplication.update -= RunQueuedAnalysis;
            return;
        }

        // isUpdating is Unity's AssetDatabase refresh/import state. Keep waiting rather
        // than relying on a fixed delay, then validate each registered SceneAsset below.
        if (EditorApplication.isUpdating || EditorApplication.isCompiling ||
            EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        _analysisQueued = false;
        EditorApplication.update -= RunQueuedAnalysis;
        _report = LegacyMigrationAnalyzer.Analyze();
        _approved.Clear();
        Repaint();
    }

    private void AssignMissingDoorIds()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Assign Missing Door IDs",
            "Generate stable door IDs for doors whose doorId is empty, then save the affected scenes.\n\n" +
            "Existing IDs, including duplicates, are never regenerated and legacy fields are not modified.",
            "Assign and Save",
            "Cancel");

        if (!confirmed)
            return;

        bool success = DoorIdMigrationService.AssignMissingDoorIds(out string message, out int assigned);

        _resultMessage = message;
        _resultType = success ? MessageType.Info : MessageType.Error;

        if (success)
            Debug.Log($"[LegacyMigration] Assigned {assigned} door ID(s).");
        else
            Debug.LogWarning($"[LegacyMigration] Door ID assignment reported problems. {message}");

        Analyze();
    }

    private void ApplyApproved()
    {
        if (_report == null || _report.HasIncompleteDoorScan || _analysisQueued)
            return;

        List<LegacyMigrationProposal> approved = new List<LegacyMigrationProposal>();

        foreach (LegacyMigrationProposal proposal in _report.Proposals)
        {
            if (!proposal.IsSafeToApply)
                continue;

            if (_approved.TryGetValue(proposal.Key, out bool isApproved) && isApproved)
                approved.Add(proposal);
        }

        if (approved.Count == 0)
            return;

        bool confirmed = EditorUtility.DisplayDialog(
            "Apply Legacy Migration",
            $"Create {approved.Count} RoomConnection(s) and save the affected scenes?\n\n" +
            "Legacy fields are preserved. Any failure rolls back the affected connection.",
            "Apply Approved",
            "Cancel");

        if (!confirmed)
            return;

        bool success = LegacyMigrationService.ApplyApproved(approved, out string summary);

        _resultMessage = summary;
        _resultType = success ? MessageType.Info : MessageType.Warning;

        if (success)
            Debug.Log($"[LegacyMigration] {summary}");
        else
            Debug.LogWarning($"[LegacyMigration] {summary}");

        Analyze();
    }

    private int CountApproved()
    {
        if (_report == null)
            return 0;

        int count = 0;

        foreach (LegacyMigrationProposal proposal in _report.Proposals)
        {
            if (!proposal.IsSafeToApply)
                continue;

            if (_approved.TryGetValue(proposal.Key, out bool approved) && approved)
                count++;
        }

        return count;
    }
}
