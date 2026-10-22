using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Handline.EditorTools
{
    /// <summary>
    /// Editor helper that opens the camera scene in an empty project.
    /// Added so the first import lands on Camera_MoCap instead of an untitled scene.
    /// It does not run in a player build.
    /// </summary>
    [InitializeOnLoad]
    public static class OpenHandlineScene
    {
        /// <summary>Scene Play opens. Added as the default stage.</summary>
        const string ScenePath = "Assets/Scenes/Camera_MoCap.unity";

        /// <summary>
        /// Subscribes once when the editor loads.
        /// delayCall waits until the editor is idle. playModeStateChanged opens the Game view on Play.
        /// </summary>
        static OpenHandlineScene()
        {
            EditorApplication.delayCall += OpenIfNeeded;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        /// <summary>
        /// Focuses the Game view when Play starts.
        /// Added so the camera stage is visible without hunting for the window.
        /// </summary>
        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            Type gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView);
        }

        /// <summary>
        /// Opens Camera_MoCap only when the current scene has no path.
        /// A scene the user already opened is left alone. Compiling retries on the next editor tick.
        /// </summary>
        static void OpenIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += OpenIfNeeded;
                return;
            }

            var scene = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path) || !File.Exists(ScenePath))
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("Camera_MoCap is ready. Press Play. The stage shows the camera with the skeleton on top.");
        }
    }
}
