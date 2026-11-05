using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies explicitly approved legacy migration proposals. Every proposal is
/// re-analyzed immediately before application so stale or unsafe data is rejected.
/// Connection creation is delegated to RoomConnectionService.
/// </summary>
public static class LegacyMigrationService
{
    public static bool ApplyProposal(LegacyMigrationProposal approved, out string message)
    {
        if (approved == null)
        {
            message = "No proposal was provided.";
            return false;
        }

        if (!approved.IsSafeToApply)
        {
            message = $"Proposal '{approved.DisplayLabel}' is not safe to apply ({approved.ClassificationLabel}): {approved.Reason}";
            return false;
        }

        if (!TryGetRegisteredRoom(approved.A, out string roomMessage) ||
            !TryGetRegisteredRoom(approved.B, out roomMessage))
        {
            message = roomMessage;
            return false;
        }

        LegacyMigrationReport currentReport = LegacyMigrationAnalyzer.Analyze();
        LegacyMigrationProposal current = currentReport.FindByKey(approved.Key);

        if (current == null)
        {
            message = $"Proposal '{approved.DisplayLabel}' is stale: the legacy relationship no longer exists.";
            return false;
        }

        if (!current.IsSafeToApply)
        {
            message = $"Proposal '{approved.DisplayLabel}' is no longer safe to apply ({current.ClassificationLabel}): {current.Reason}";
            return false;
        }

        if (!IsSameProposal(approved, current))
        {
            message = $"Proposal '{approved.DisplayLabel}' is stale: the legacy relationship changed since analysis.";
            return false;
        }

        bool created = RoomConnectionService.CreateConnection(
            current.A.Room,
            current.A.DoorId,
            current.B.Room,
            current.B.DoorId,
            current.Direction,
            null,
            out string creationMessage);

        if (!created)
        {
            message = $"Migration of '{current.DisplayLabel}' was rejected: {creationMessage}";
            return false;
        }

        RoomValidationReport validation = RoomConnectionValidator.Validate();
        message =
            $"Migrated '{current.DisplayLabel}' as {current.Direction}. " +
            $"Legacy fields were preserved. Validation: {validation.ErrorCount} error(s), " +
            $"{validation.WarningCount} warning(s).";

        return true;
    }

    /// <summary>
    /// Applies only the proposals the developer explicitly approved. An empty list
    /// performs no analysis-driven migration and changes nothing.
    /// </summary>
    public static bool ApplyApproved(IReadOnlyList<LegacyMigrationProposal> approved, out string summary)
    {
        if (approved == null || approved.Count == 0)
        {
            summary = "No proposals are approved. Nothing was migrated.";
            return false;
        }

        int applied = 0;
        int failed = 0;
        List<string> lines = new List<string>();

        foreach (LegacyMigrationProposal proposal in approved)
        {
            bool success = ApplyProposal(proposal, out string message);

            if (success)
                applied++;
            else
                failed++;

            lines.Add((success ? "Applied: " : "Rejected: ") + message);
        }

        summary = $"Applied {applied} proposal(s); {failed} rejected.";
        foreach (string line in lines)
            summary += "\n" + line;

        return applied > 0 && failed == 0;
    }

    private static bool TryGetRegisteredRoom(LegacyEndpointSnapshot endpoint, out string message)
    {
        if (endpoint == null || endpoint.Room == null)
        {
            message = "A proposal endpoint has no RoomDefinition.";
            return false;
        }

        RoomRegistry registry = AssetDatabase.LoadAssetAtPath<RoomRegistry>(RoomRegistrationService.RegistryPath);
        if (registry == null || registry.Rooms == null)
        {
            message = "RoomRegistry is missing. Register rooms before migrating connections.";
            return false;
        }

        foreach (RoomDefinition room in registry.Rooms)
        {
            if (room == null)
                continue;

            if (ReferenceEquals(room, endpoint.Room) ||
                string.Equals(room.RoomId, endpoint.RoomId, StringComparison.Ordinal))
            {
                message = null;
                return true;
            }
        }

        message = $"Room '{endpoint.RoomId}' is no longer registered.";
        return false;
    }

    private static bool IsSameProposal(LegacyMigrationProposal approved, LegacyMigrationProposal current)
    {
        return approved.Direction == current.Direction &&
               approved.Classification == current.Classification &&
               SnapshotsMatch(approved.A, current.A) &&
               SnapshotsMatch(approved.B, current.B);
    }

    private static bool SnapshotsMatch(LegacyEndpointSnapshot expected, LegacyEndpointSnapshot current)
    {
        if (expected == null || current == null)
            return expected == null && current == null;

        if (!string.Equals(expected.RoomId, current.RoomId, StringComparison.Ordinal) ||
            !string.Equals(expected.DoorId, current.DoorId, StringComparison.Ordinal) ||
            !string.Equals(expected.LegacyDoorId, current.LegacyDoorId, StringComparison.Ordinal) ||
            !string.Equals(expected.TargetSceneName, current.TargetSceneName, StringComparison.Ordinal) ||
            !string.Equals(expected.TargetDoorId, current.TargetDoorId, StringComparison.Ordinal))
        {
            return false;
        }

        if (expected.Room == null || current.Room == null)
            return expected.Room == null && current.Room == null;

        return string.Equals(expected.Room.SceneGuid, current.Room.SceneGuid, StringComparison.Ordinal);
    }
}
