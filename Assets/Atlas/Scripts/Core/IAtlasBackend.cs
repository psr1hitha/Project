// The one seam between Team Headset and Team Brain.
//
// Today: LocalFakeBackend implements this (local JSON + a scripted agent).
// Hand-off #2: B1 writes SpacetimeBackend (see SpacetimeBackendTemplate.cs) that
// implements the same interface on top of the generated bindings. No UI code changes.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atlas
{
    public interface IAtlasBackend
    {
        // ----- state -----
        bool IsConnected { get; }
        string LocalIdentity { get; }
        UserInfo LocalUser { get; }                 // null until Join
        ArchitectureGraph Graph { get; }            // null until loaded
        MapLayer CurrentLayer { get; }
        IReadOnlyList<ChecklistItem> MyChecklist { get; }
        IReadOnlyList<UserInfo> Users { get; }
        AgentTask GetTask(ulong id);
        string GetSelection(string identity);       // node id or null

        // ----- events (fire on the main thread) -----
        event Action<ArchitectureGraph> GraphLoaded;
        event Action<string, Vector3> NodeMoved;            // node id, new local position
        event Action<string, string> SelectionChanged;      // identity, node id (null = cleared)
        event Action<MapLayer> LayerChanged;
        event Action<UserInfo> UserChanged;
        event Action<UserInfo> LocalUserJoined;
        event Action<AgentTask> TaskUpdated;                // insert or status change
        event Action<AgentStep> AgentStepAdded;
        event Action<AgentAnswer> AnswerPosted;
        event Action<SceneCommand> SceneCommandIssued;
        event Action<IReadOnlyList<ChecklistItem>> ChecklistChanged;

        // ----- reducers the headset calls -----
        void Join(string name, Role role);
        void SetRole(Role role);
        void SelectNode(string nodeId);             // null/empty clears
        void MoveNode(string nodeId, Vector3 localPosition);
        void AddTranscript(string text);
        void SetLayer(MapLayer layer);
    }

    /// <summary>Tiny service locator so every script can find the backend.</summary>
    public static class AtlasServices
    {
        static IAtlasBackend backend;
        static event Action<IAtlasBackend> ready;

        public static IAtlasBackend Backend => backend;

        public static void Register(IAtlasBackend b)
        {
            if (backend != null && backend != b)
                Debug.LogWarning("[Atlas] A second backend registered; replacing the first.");
            backend = b;
            var cb = ready;
            ready = null;
            cb?.Invoke(b);
        }

        public static void Unregister(IAtlasBackend b)
        {
            if (backend == b) backend = null;
        }

        /// <summary>Runs now if a backend exists, otherwise once one registers.</summary>
        public static void WhenReady(Action<IAtlasBackend> callback)
        {
            if (backend != null) callback(backend);
            else ready += callback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            backend = null;
            ready = null;
        }
    }
}
