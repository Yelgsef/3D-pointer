using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConfigureRobotObserver
{
    private const string ScenePath = "Assets/Scenes/HumanoidSimulation.unity";
    private const string RenderTexturePath = "Assets/RobotVision/RenderTextures/RobotEyeRT.renderTexture";

    [MenuItem("Humanoid Simulation/Configure Robot Observer")]
    public static void Configure()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            throw new InvalidOperationException("Open " + ScenePath + " before configuring the observer.");

        var human = GameObject.Find("Humanoid") ?? GameObject.Find("HumanTarget");
        var mainCameraObject = GameObject.Find("Main Camera");
        var obstacle = GameObject.Find("Obstacle");
        if (human == null || mainCameraObject == null || obstacle == null)
            throw new InvalidOperationException("Expected Humanoid, Main Camera, and Obstacle scene objects.");

        human.name = "HumanTarget";
        human.transform.SetPositionAndRotation(new Vector3(0f, 0f, -4f), Quaternion.Euler(0f, 180f, 0f));
        RemoveIfPresent<RosAnimationController>(human);
        RemoveIfPresent<RobotPosePublisher>(human);

        var robot = GameObject.Find("RobotObserver");
        if (robot == null)
            robot = new GameObject("RobotObserver");
        robot.transform.SetPositionAndRotation(new Vector3(0f, 0f, -9f), Quaternion.identity);

        var character = robot.GetComponent<CharacterController>();
        if (character == null)
            character = Undo.AddComponent<CharacterController>(robot);
        if (character == null)
            throw new InvalidOperationException("Unity could not add a CharacterController to RobotObserver.");
        character.height = 0.9f;
        character.radius = 0.38f;
        character.center = new Vector3(0f, 0.48f, 0f);
        character.skinWidth = 0.04f;
        character.stepOffset = 0.2f;
        character.slopeLimit = 45f;

        var bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RobotBody.mat");
        var accentMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RobotAccent.mat");
        var eyeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RobotEyes.mat");
        var jointsMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/RobotJoints.mat");
        RemoveChildIfPresent(robot.transform, "RobotBody");
        RemoveChildIfPresent(robot.transform, "UpperChassis");
        RemoveChildIfPresent(robot.transform, "FrontSensor");
        RemoveChildIfPresent(robot.transform, "LeftWheel");
        RemoveChildIfPresent(robot.transform, "RightWheel");
        RemoveChildIfPresent(robot.transform, "CameraMount");

        CreateShape("RobotBody", PrimitiveType.Cylinder, robot.transform, new Vector3(0f, 0.24f, 0f), new Vector3(0.76f, 0.23f, 0.86f), bodyMaterial);
        CreateShape("UpperChassis", PrimitiveType.Cube, robot.transform, new Vector3(0f, 0.61f, 0f), new Vector3(0.60f, 0.34f, 0.62f), bodyMaterial);
        CreateShape("FrontSensor", PrimitiveType.Sphere, robot.transform, new Vector3(0f, 0.65f, 0.33f), new Vector3(0.23f, 0.13f, 0.09f), eyeMaterial);
        CreateShape("LeftWheel", PrimitiveType.Cylinder, robot.transform, new Vector3(-0.40f, 0.20f, 0f), new Vector3(0.20f, 0.13f, 0.30f), jointsMaterial);
        CreateShape("RightWheel", PrimitiveType.Cylinder, robot.transform, new Vector3(0.40f, 0.20f, 0f), new Vector3(0.20f, 0.13f, 0.30f), jointsMaterial);

        var mount = new GameObject("CameraMount").transform;
        mount.SetParent(robot.transform, false);
        mount.localPosition = new Vector3(0f, 1.4f, 0f);
        CreateShape("MountStem", PrimitiveType.Cylinder, mount, new Vector3(0f, -0.13f, 0f), new Vector3(0.09f, 0.15f, 0.09f), jointsMaterial);

        var eye = new GameObject("RobotEyeCamera");
        eye.transform.SetParent(mount, false);
        eye.transform.localPosition = new Vector3(0f, 0f, 0.12f);
        var robotCamera = eye.AddComponent<Camera>();
        robotCamera.orthographic = false;
        robotCamera.fieldOfView = 34.6f;
        robotCamera.aspect = 16f / 9f;
        robotCamera.nearClipPlane = 0.03f;
        robotCamera.farClipPlane = 100f;
        robotCamera.clearFlags = CameraClearFlags.SolidColor;
        robotCamera.backgroundColor = new Color(0.18f, 0.22f, 0.24f);
        robotCamera.depth = 1f;

        EnsureFolder("Assets/RobotVision");
        EnsureFolder("Assets/RobotVision/RenderTextures");
        var renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
        if (renderTexture == null)
        {
            renderTexture = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32)
            {
                name = "RobotEyeRT",
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false
            };
            renderTexture.Create();
            AssetDatabase.CreateAsset(renderTexture, RenderTexturePath);
        }
        robotCamera.targetTexture = renderTexture;

        var follow = mainCameraObject.GetComponent<ThirdPersonFollowCamera>();
        if (follow != null)
            follow.Target = robot.transform;
        var mainCamera = mainCameraObject.GetComponent<Camera>();
        if (mainCamera != null)
        {
            mainCamera.orthographic = false;
            mainCamera.nearClipPlane = 0.1f;
            mainCamera.farClipPlane = 120f;
        }

        EnsureComponent<RosAnimationController>(robot);
        EnsureComponent<RobotPosePublisher>(robot);
        var controls = EnsureComponent<RobotObserverScenarioControls>(robot);
        var serializedControls = new SerializedObject(controls);
        serializedControls.FindProperty("robotObserver").objectReferenceValue = robot.transform;
        serializedControls.FindProperty("humanTarget").objectReferenceValue = human.transform;
        serializedControls.FindProperty("obstacle").objectReferenceValue = obstacle.transform;
        serializedControls.FindProperty("humanAnimator").objectReferenceValue = human.GetComponent<Animator>();
        serializedControls.ApplyModifiedPropertiesWithoutUndo();

        CreatePreview(renderTexture);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Robot Observer configured: independent RobotObserver, HumanTarget, RobotEyeRT, and live POV preview.");
    }

    private static void CreatePreview(RenderTexture renderTexture)
    {
        var old = GameObject.Find("RobotPOVCanvas");
        if (old != null)
            UnityEngine.Object.DestroyImmediate(old);

        var canvasObject = new GameObject("RobotPOVCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        var panel = new GameObject("RobotPOVPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-24f, 24f);
        panelRect.sizeDelta = new Vector2(376f, 231f);
        var panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.035f, 0.07f, 0.09f, 0.94f);
        panelImage.raycastTarget = false;

        var title = new GameObject("Title", typeof(RectTransform), typeof(Text));
        title.transform.SetParent(panel.transform, false);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -3f);
        titleRect.sizeDelta = new Vector2(-16f, 20f);
        var titleText = title.GetComponent<Text>();
        titleText.text = "ROBOT POV";
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 13;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.raycastTarget = false;

        var preview = new GameObject("LivePreview", typeof(RectTransform), typeof(RawImage));
        preview.transform.SetParent(panel.transform, false);
        var previewRect = preview.GetComponent<RectTransform>();
        previewRect.anchorMin = Vector2.zero;
        previewRect.anchorMax = Vector2.zero;
        previewRect.pivot = Vector2.zero;
        previewRect.anchoredPosition = new Vector2(8f, 6f);
        previewRect.sizeDelta = new Vector2(360f, 202f);
        var rawImage = preview.GetComponent<RawImage>();
        rawImage.texture = renderTexture;
        rawImage.raycastTarget = false;
    }

    private static void CreateShape(string name, PrimitiveType kind, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(kind);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        var renderer = part.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;
        var collider = part.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.DestroyImmediate(collider);
    }

    private static void RemoveIfPresent<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        if (component != null)
            UnityEngine.Object.DestroyImmediate(component);
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        var component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void RemoveChildIfPresent(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
            UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        var name = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
