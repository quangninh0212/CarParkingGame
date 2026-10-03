using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarParkingGame.EditorTools
{
    // Gives the track's solid scenery the colliders it never had.
    //
    // The track pack ships one hand-made collision mesh group, "oval_complete_colliders",
    // which covers the walls, the garages and the ground - and nothing else. Every barrier,
    // tyre stack, plastic block, lamp post and bridge in the scene is a render-only mesh,
    // which is why the car drives straight through the barriers in every mission.
    //
    // Only the groups listed below are given colliders. Blanketing the scene would be
    // wrong twice over: trees and grandstands are scenery the car can never reach, and a
    // collider per tree is a cost an Android build should not pay.
    //
    // Colliders are non-convex MeshColliders. These objects never move, so a non-convex
    // mesh is both exact and the cheap option; convex hulls would round the barrier walls
    // out into the track.
    public static class TrackColliderTool
    {
        private const string ScenePath = "Assets/GameAssets/CartoonTracksPack1/Track1/Demo Scenes/complete_track_demo.unity";

        // Container names, matched exactly, under which every mesh becomes solid.
        //
        // oval_mod_walls is the big one and was missed the first time: 45 meshes of wall
        // and chain-link fence running the length of the track. "oval_complete_colliders"
        // looked like it covered them and does not, which is why the fences could still be
        // driven through after the first pass.
        private static readonly string[] SolidGroups =
        {
            "oval_mod_walls",
            "oval_mod_barriers",
            "oval_mod_tyres",
            "oval_mod_blocks",
            "oval_mod_lamps",
            "oval_mod_pitwall",
            "oval_mod_bridges",
            "oval_mod_startlights",
            "oval_mod_buildings",
            "oval_mod_seats",
            "oval_mod_tents",
            "oval_mod_turnsigns",
            "oval_mod_terrain"
        };

        // Under the terrain group only: a skirt mesh below the whole track, which would
        // become a second floor under everything if it were made solid.
        private static readonly HashSet<string> NeverSolid = new HashSet<string>
        {
            "underground"
        };

        [MenuItem("Tools/Car Parking/Make Track Scenery Solid")]
        public static void RunInOpenScene()
        {
            int added = AddColliders(out int alreadySolid);
            Debug.Log($"[TrackColliderTool] Added {added} collider(s); {alreadySolid} mesh(es) already had one. Save the scene to keep it.");
        }

        public static void RunFromCommandLine()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[TrackColliderTool] Could not open '{ScenePath}'.");
                EditorApplication.Exit(1);
                return;
            }

            int added = AddColliders(out int alreadySolid);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[TrackColliderTool] Added {added} collider(s); {alreadySolid} mesh(es) already had one. Saved '{ScenePath}'.");
            EditorApplication.Exit(0);
        }

        private static int AddColliders(out int alreadySolid)
        {
            alreadySolid = 0;
            int added = 0;

            var counts = new Dictionary<string, int>();

            foreach (string groupName in SolidGroups)
            {
                Transform group = FindGroup(groupName);

                if (group == null)
                {
                    Debug.LogWarning($"[TrackColliderTool] No group called '{groupName}' in the scene.");
                    continue;
                }

                int addedHere = 0;

                foreach (MeshFilter filter in group.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || NeverSolid.Contains(filter.name))
                    {
                        continue;
                    }

                    if (filter.GetComponent<Collider>() != null)
                    {
                        alreadySolid++;
                        continue;
                    }

                    var collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;

                    EditorUtility.SetDirty(filter.gameObject);
                    addedHere++;
                    added++;
                }

                counts[groupName] = addedHere;
            }

            foreach (KeyValuePair<string, int> entry in counts)
            {
                Debug.Log($"[TrackColliderTool] '{entry.Key}': {entry.Value} collider(s) added.");
            }

            return added;
        }

        // Matched by name anywhere in the scene, because the track's groups sit under
        // "Environment" in this scene but are root objects in the pack's own demos.
        private static Transform FindGroup(string groupName)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == groupName)
                {
                    return root.transform;
                }

                Transform found = FindRecursive(root.transform, groupName);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform FindRecursive(Transform parent, string groupName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                if (child.name == groupName)
                {
                    return child;
                }

                Transform found = FindRecursive(child, groupName);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
