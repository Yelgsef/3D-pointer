using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Robotics.ROSTCPConnector;

public static class BuildHumanoidScene
{
    private const string ScenePath = "Assets/Scenes/HumanoidSimulation.unity";
    private const string MaterialFolder = "Assets/Materials";
    private const string AnimationFolder = "Assets/Animations";

    [MenuItem("Humanoid Simulation/Build Demo Scene")]
    public static void Build()
    {
        EnsureFolder(MaterialFolder);
        EnsureFolder(AnimationFolder);

        Material floorMaterial = CreateMaterial("Floor", new Color(0.20f, 0.25f, 0.28f));
        Material bodyMaterial = CreateMaterial("RobotBody", new Color(0.10f, 0.38f, 0.52f));
        Material jointMaterial = CreateMaterial("RobotJoints", new Color(0.04f, 0.09f, 0.13f));
        Material accentMaterial = CreateMaterial("RobotAccent", new Color(1.00f, 0.54f, 0.18f));
        Material eyeMaterial = CreateMaterial("RobotEyes", new Color(0.40f, 0.88f, 0.96f), 0.1f);
        Material obstacleMaterial = CreateMaterial("Obstacle", new Color(0.82f, 0.25f, 0.18f));
        Material markingMaterial = CreateMaterial("LaneMarking", new Color(0.90f, 0.86f, 0.70f));

        AnimationClip idleClip = CreateClip("Idle", 1f, 0f, 0f);
        AnimationClip walkClip = CreateClip("Walk", 1f, 24f, 18f);
        AnimationClip runClip = CreateClip("Run", 0.58f, 38f, 30f);
        AnimatorController controller = CreateController(idleClip, walkClip, runClip);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "HumanoidSimulation";

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.56f, 0.61f, 0.65f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.48f, 0.56f, 0.60f);
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.008f;

        BuildGround(floorMaterial, markingMaterial);
        BuildObstacle(obstacleMaterial);
        GameObject robot = BuildHumanoid(bodyMaterial, jointMaterial, accentMaterial, eyeMaterial, controller);
        BuildCamera(robot.transform);
        BuildLighting();
        BuildRosConnection();

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(AssetDatabase.GUIDFromAssetPath(ScenePath), true)
        };

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Built and saved the ROS humanoid simulation scene at " + ScenePath);
    }

    private static GameObject BuildHumanoid(
        Material body,
        Material joints,
        Material accent,
        Material eyes,
        RuntimeAnimatorController controller)
    {
        GameObject root = new GameObject("Humanoid");
        root.transform.position = new Vector3(0f, 0f, -4f);
        root.tag = "Player";

        CharacterController characterController = root.AddComponent<CharacterController>();
        characterController.height = 1.9f;
        characterController.radius = 0.32f;
        characterController.center = new Vector3(0f, 0.95f, 0f);
        characterController.skinWidth = 0.04f;
        characterController.stepOffset = 0.3f;
        characterController.slopeLimit = 45f;
        characterController.minMoveDistance = 0f;

        Transform visuals = new GameObject("Visuals").transform;
        visuals.SetParent(root.transform, false);

        CreateShape("Pelvis", PrimitiveType.Cube, visuals, new Vector3(0f, 0.92f, 0f), new Vector3(0.48f, 0.24f, 0.3f), joints);
        CreateShape("Torso", PrimitiveType.Cube, visuals, new Vector3(0f, 1.34f, 0f), new Vector3(0.60f, 0.68f, 0.36f), body);
        CreateShape("ChestAccent", PrimitiveType.Cube, visuals, new Vector3(0f, 1.42f, 0.19f), new Vector3(0.24f, 0.10f, 0.035f), accent);
        CreateShape("Neck", PrimitiveType.Cylinder, visuals, new Vector3(0f, 1.77f, 0f), new Vector3(0.14f, 0.12f, 0.14f), joints);
        CreateShape("Head", PrimitiveType.Sphere, visuals, new Vector3(0f, 2.02f, 0f), new Vector3(0.43f, 0.43f, 0.40f), body);
        CreateShape("LeftEye", PrimitiveType.Sphere, visuals, new Vector3(-0.09f, 2.04f, 0.18f), new Vector3(0.075f, 0.075f, 0.045f), eyes);
        CreateShape("RightEye", PrimitiveType.Sphere, visuals, new Vector3(0.09f, 2.04f, 0.18f), new Vector3(0.075f, 0.075f, 0.045f), eyes);
        CreateShape("Antenna", PrimitiveType.Cylinder, visuals, new Vector3(0f, 2.31f, 0f), new Vector3(0.035f, 0.12f, 0.035f), accent);
        CreateShape("AntennaTip", PrimitiveType.Sphere, visuals, new Vector3(0f, 2.56f, 0f), Vector3.one * 0.10f, eyes);

        Transform leftArm = CreateLimb(visuals, "LeftArm", new Vector3(-0.39f, 1.58f, 0f), -1f, body, joints);
        Transform rightArm = CreateLimb(visuals, "RightArm", new Vector3(0.39f, 1.58f, 0f), 1f, body, joints);
        Transform leftLeg = CreateLeg(visuals, "LeftLeg", new Vector3(-0.15f, 0.84f, 0f), joints, body);
        Transform rightLeg = CreateLeg(visuals, "RightLeg", new Vector3(0.15f, 0.84f, 0f), joints, body);

        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        root.AddComponent<RosAnimationController>();
        root.AddComponent<RobotPosePublisher>();

        leftArm.name = "LeftArm";
        rightArm.name = "RightArm";
        leftLeg.name = "LeftLeg";
        rightLeg.name = "RightLeg";
        return root;
    }

    private static Transform CreateLimb(Transform parent, string name, Vector3 position, float side, Material material, Material joints)
    {
        GameObject pivot = new GameObject(name);
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = position;
        CreateShape("Shoulder", PrimitiveType.Sphere, pivot.transform, Vector3.zero, Vector3.one * 0.20f, joints);
        CreateShape("UpperArm", PrimitiveType.Capsule, pivot.transform, new Vector3(0f, -0.27f, 0f), new Vector3(0.17f, 0.31f, 0.17f), material);
        CreateShape("Hand", PrimitiveType.Sphere, pivot.transform, new Vector3(0f, -0.52f, 0.01f), new Vector3(0.16f, 0.17f, 0.16f), material);
        return pivot.transform;
    }

    private static Transform CreateLeg(Transform parent, string name, Vector3 position, Material joints, Material body)
    {
        GameObject pivot = new GameObject(name);
        pivot.transform.SetParent(parent, false);
        pivot.transform.localPosition = position;
        CreateShape("Hip", PrimitiveType.Sphere, pivot.transform, Vector3.zero, Vector3.one * 0.22f, joints);
        CreateShape("LegSegment", PrimitiveType.Capsule, pivot.transform, new Vector3(0f, -0.27f, 0f), new Vector3(0.21f, 0.31f, 0.21f), body);
        CreateShape("Foot", PrimitiveType.Cube, pivot.transform, new Vector3(0f, -0.52f, 0.09f), new Vector3(0.24f, 0.13f, 0.38f), joints);
        return pivot.transform;
    }

    private static GameObject BuildGround(Material floor, Material markings)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.12f, 1f);
        ground.transform.localScale = new Vector3(14f, 0.24f, 24f);
        ground.GetComponent<Renderer>().sharedMaterial = floor;

        for (int i = -4; i <= 4; i++)
        {
            GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.name = "LaneMarking_" + (i + 5);
            dash.transform.position = new Vector3(0f, 0.006f, -7f + i * 2f);
            dash.transform.localScale = new Vector3(0.08f, 0.012f, 0.9f);
            dash.GetComponent<Renderer>().sharedMaterial = markings;
            UnityEngine.Object.DestroyImmediate(dash.GetComponent<Collider>());
        }
        return ground;
    }

    private static void BuildObstacle(Material material)
    {
        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = "Obstacle";
        obstacle.transform.position = new Vector3(0f, 0.62f, 2.8f);
        obstacle.transform.localScale = new Vector3(1.6f, 1.24f, 0.8f);
        obstacle.GetComponent<Renderer>().sharedMaterial = material;

        GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stripe.name = "ObstacleStripe";
        stripe.transform.SetParent(obstacle.transform, false);
        stripe.transform.localPosition = new Vector3(0f, 0.12f, -0.51f);
        stripe.transform.localScale = new Vector3(0.84f, 0.13f, 0.04f);
        stripe.GetComponent<Renderer>().sharedMaterial = CreateMaterial("ObstacleStripe", new Color(0.98f, 0.76f, 0.30f));
        UnityEngine.Object.DestroyImmediate(stripe.GetComponent<Collider>());
    }

    private static void BuildCamera(Transform target)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 3.2f, -9.5f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 58f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 120f;
        cameraObject.AddComponent<AudioListener>();
        ThirdPersonFollowCamera follow = cameraObject.AddComponent<ThirdPersonFollowCamera>();
        follow.Target = target;
    }

    private static void BuildLighting()
    {
        GameObject lightObject = new GameObject("SimulationKeyLight");
        lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.color = new Color(1f, 0.91f, 0.78f);
        light.shadows = LightShadows.Soft;
    }

    private static void BuildRosConnection()
    {
        GameObject connectionObject = new GameObject("ROSConnection");
        ROSConnection connection = connectionObject.AddComponent<ROSConnection>();
        connection.RosIPAddress = "127.0.0.1";
        connection.RosPort = 10000;
        connection.ConnectOnStart = true;
        connection.ShowHud = true;
        connectionObject.AddComponent<ClickGoalPublisher>();
    }

    private static AnimationClip CreateClip(string clipName, float duration, float legAngle, float armAngle)
    {
        string path = AnimationFolder + "/" + clipName + ".anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = clipName, frameRate = 30f };
            AssetDatabase.CreateAsset(clip, path);
        }
        else
        {
            clip.ClearCurves();
            clip.frameRate = 30f;
        }

        if (legAngle > 0f)
        {
            AddSwingCurve(clip, "Visuals/LeftLeg", duration, legAngle);
            AddSwingCurve(clip, "Visuals/RightLeg", duration, -legAngle);
            AddSwingCurve(clip, "Visuals/LeftArm", duration, -armAngle);
            AddSwingCurve(clip, "Visuals/RightArm", duration, armAngle);
        }

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void AddSwingCurve(AnimationClip clip, string path, float duration, float amplitude)
    {
        var keys = new Keyframe[5];
        for (int i = 0; i < keys.Length; i++)
        {
            float time = duration * i / (keys.Length - 1);
            float radians = 2f * Mathf.PI * time / duration;
            keys[i] = new Keyframe(time, amplitude * Mathf.Sin(radians));
        }

        AnimationCurve curve = new AnimationCurve(keys);
        EditorCurveBinding binding = EditorCurveBinding.FloatCurve(
            path, typeof(Transform), "localEulerAnglesRaw.x");
        AnimationUtility.SetEditorCurve(clip, binding, curve);
    }

    private static AnimatorController CreateController(AnimationClip idle, AnimationClip walk, AnimationClip run)
    {
        const string path = AnimationFolder + "/HumanoidLocomotion.controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveDirection", AnimatorControllerParameterType.Float);
        controller.AddParameter("Turn", AnimatorControllerParameterType.Float);

        BlendTree tree = new BlendTree
        {
            name = "Locomotion",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            useAutomaticThresholds = false
        };
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 0.10f);
        tree.AddChild(run, 1f);
        AssetDatabase.AddObjectToAsset(tree, controller);
        AnimatorState state = controller.layers[0].stateMachine.AddState("Locomotion", new Vector3(260f, 120f));
        state.motion = tree;
        controller.layers[0].stateMachine.defaultState = state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static GameObject CreateShape(
        string name,
        PrimitiveType type,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material material)
    {
        GameObject shape = GameObject.CreatePrimitive(type);
        shape.name = name;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = localPosition;
        shape.transform.localScale = localScale;
        shape.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(shape.GetComponent<Collider>());
        return shape;
    }

    private static Material CreateMaterial(string name, Color color, float metallic = 0f)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", 0.35f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folder = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }
}
