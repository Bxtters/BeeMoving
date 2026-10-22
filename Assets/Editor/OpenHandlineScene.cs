using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Handline.EditorTools
{
    [InitializeOnLoad]
    public static class OpenHandlineScene
    {
        const string ScenePath = "Assets/Scenes/Camera_MoCap.unity";

        static OpenHandlineScene()
        {
            EditorApplication.delayCall += OpenIfNeeded;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            Type gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView);
        }

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
            Debug.Log("Camera_MoCap is ready. Press Play. The stage draws skeletons and does not show the camera picture.");
        }
    }
}
