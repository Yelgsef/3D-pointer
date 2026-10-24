using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InstallRiggedHumanTarget
{
    private const string ModelPath = "Assets/Characters/HumanTarget_Rigged.fbx";
    private const string GestureControllerPath = "Assets/Animations/HumanTargetGestures.controller";

    [MenuItem("Humanoid Simulation/Install Rigged Human Target")]
    public static void Install()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/HumanoidSimulation.unity")
            throw new InvalidOperationException("Open the HumanoidSimulation scene before installing the human rig.");

        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("The generated human FBX is missing at " + ModelPath);

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.optimizeGameObjects = false;
        importer.SaveAndReimport();

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        if (model == null || avatar == null || !avatar.isValid || !avatar.isHuman)
            throw new InvalidOperationException("Unity could not create a valid Humanoid Avatar from the generated skeleton.");

        SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0 || renderers.Any(renderer => renderer.sharedMesh == null || renderer.bones.Length < 18))
            throw new InvalidOperationException("The generated character has no usable skinned humanoid mesh.");

        GameObject human = GameObject.Find("HumanTarget");
        if (human == null)
            throw new InvalidOperationException("The HumanTarget scene root was not found.");
        human.transform.rotation = Quaternion.identity;

        EnsureTarget("TargetObjectA", new Vector3(1.3f, 0.28f, -6f), new Color(0.96f, 0.43f, 0.16f), "Assets/Materials/TargetObjectA.mat");
        EnsureTarget("TargetObjectB", new Vector3(-1.3f, 0.28f, -6f), new Color(0.20f, 0.62f, 0.82f), "Assets/Materials/TargetObjectB.mat");

        Transform previous = human.transform.Find("HumanVisual");
        if (previous != null)
            UnityEngine.Object.DestroyImmediate(previous.gameObject);
        Transform oldPrimitives = human.transform.Find("Visuals");
        if (oldPrimitives != null)
            UnityEngine.Object.DestroyImmediate(oldPrimitives.gameObject);

        GameObject visual = PrefabUtility.InstantiatePrefab(model, scene) as GameObject;
        if (visual == null)
            throw new InvalidOperationException("Unity failed to instantiate the imported human rig.");
        visual.name = "HumanVisual";
        visual.transform.SetParent(human.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        Animator animator = human.GetComponent<Animator>();
        if (animator == null)
            animator = human.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.runtimeAnimatorController = GetGestureAnimatorController();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (!animator.isHuman ||
            animator.GetBoneTransform(HumanBodyBones.LeftUpperArm) == null ||
            animator.GetBoneTransform(HumanBodyBones.RightUpperArm) == null ||
            animator.GetBoneTransform(HumanBodyBones.Head) == null)
            throw new InvalidOperationException("The assigned Avatar does not resolve required head and arm bones.");

        HumanTargetGestureController gestures = human.GetComponent<HumanTargetGestureController>();
        if (gestures == null)
            gestures = human.AddComponent<HumanTargetGestureController>();
        SerializedObject serializedGestures = new SerializedObject(gestures);
        serializedGestures.FindProperty("animator").objectReferenceValue = animator;
        serializedGestures.ApplyModifiedPropertiesWithoutUndo();

        RobotObserverScenarioControls controls = GameObject.Find("RobotObserver").GetComponent<RobotObserverScenarioControls>();
        if (controls == null)
            throw new InvalidOperationException("RobotObserverScenarioControls is missing.");
        SerializedObject serializedControls = new SerializedObject(controls);
        serializedControls.FindProperty("humanAnimator").objectReferenceValue = animator;
        serializedControls.FindProperty("humanGestures").objectReferenceValue = gestures;
        serializedControls.FindProperty("targetObjectA").objectReferenceValue = GameObject.Find("TargetObjectA").transform;
        serializedControls.FindProperty("targetObjectB").objectReferenceValue = GameObject.Find("TargetObjectB").transform;
        serializedControls.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Installed generated skinned human rig. Avatar=" + avatar.name +
                  ", bones=" + animator.avatar.humanDescription.human.Length +
                  ", targets=TargetObjectA,TargetObjectB");
    }

    private static RuntimeAnimatorController GetGestureAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GestureControllerPath);
        if (controller == null)
        {
            AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Idle.anim");
            if (idleClip == null)
                throw new InvalidOperationException("The existing Idle animation clip is missing.");

            controller = AnimatorController.CreateAnimatorControllerAtPath(GestureControllerPath);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idle = stateMachine.AddState("Idle");
            idle.motion = idleClip;
            stateMachine.defaultState = idle;
        }

        AnimatorControllerLayer[] layers = controller.layers;
        layers[0].iKPass = true;
        controller.layers = layers;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void EnsureTarget(string name, Vector3 position, Color color, string materialPath)
    {
        GameObject target = GameObject.Find(name);
        if (target == null)
            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        target.name = name;
        target.transform.SetPositionAndRotation(position, Quaternion.identity);
        target.transform.localScale = new Vector3(0.42f, 0.56f, 0.42f);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            material = new Material(shader) { name = name + "Material", color = color };
            AssetDatabase.CreateAsset(material, materialPath);
        }
        target.GetComponent<Renderer>().sharedMaterial = material;
    }
}
