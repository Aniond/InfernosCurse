using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Builds four static directional idle clips from Benidito's approved walking
/// artwork and rewires the locomotion controller so releasing input preserves
/// the direction of the last movement.
/// </summary>
public static class BeniditoDirectionalIdleBuilder
{
    const string ControllerPath = "Assets/Characters/Benidito/Benidito.controller";
    const string GeneratedDir = "Assets/Characters/Benidito/Animations/GeneratedDirectionalIdles";

    sealed class Direction
    {
        public string name;
        public string walkState;
        public string idleState => "Idle_" + name;
        public AnimatorConditionMode axisMode;
        public string axis;
        public float threshold;
    }

    static readonly Direction[] Directions =
    {
        new Direction { name = "North", walkState = "Walk_North", axis = "MoveY", axisMode = AnimatorConditionMode.Greater, threshold = 0.1f },
        new Direction { name = "South", walkState = "Walk_South", axis = "MoveY", axisMode = AnimatorConditionMode.Less, threshold = -0.1f },
        new Direction { name = "East", walkState = "Walk_East", axis = "MoveX", axisMode = AnimatorConditionMode.Greater, threshold = 0.1f },
        new Direction { name = "West", walkState = "Walk_West", axis = "MoveX", axisMode = AnimatorConditionMode.Less, threshold = -0.1f },
    };

    [MenuItem("InfernosCurse/Player/Rebuild Directional Idles")]
    public static void Apply()
    {
        EnsureFolder(GeneratedDir);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) throw new InvalidOperationException($"Missing {ControllerPath}");
        var stateMachine = controller.layers[0].stateMachine;

        var walkStates = new Dictionary<string, AnimatorState>();
        var idleStates = new Dictionary<string, AnimatorState>();
        foreach (Direction direction in Directions)
        {
            AnimatorState walk = FindState(stateMachine, direction.walkState)
                ?? throw new InvalidOperationException($"Missing animator state {direction.walkState}");
            var walkClip = walk.motion as AnimationClip
                ?? throw new InvalidOperationException($"{direction.walkState} has no AnimationClip motion");
            walkStates[direction.name] = walk;

            AnimatorState idle = FindState(stateMachine, direction.idleState) ?? stateMachine.AddState(direction.idleState);
            idle.motion = BuildIdleClip(direction.name, walkClip);
            idle.writeDefaultValues = true;
            idleStates[direction.name] = idle;
        }

        AnimatorState legacyIdle = FindState(stateMachine, "Idle");
        foreach (Direction direction in Directions)
        {
            AnimatorState walk = walkStates[direction.name];
            foreach (AnimatorStateTransition transition in walk.transitions.ToArray())
                if (transition.conditions.Any(c => c.parameter == "Speed" && c.mode == AnimatorConditionMode.Less))
                    walk.RemoveTransition(transition);

            AnimatorStateTransition stop = walk.AddTransition(idleStates[direction.name]);
            stop.hasExitTime = false;
            stop.duration = 0f;
            stop.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        }

        foreach (AnimatorState idle in idleStates.Values)
        {
            foreach (AnimatorStateTransition transition in idle.transitions.ToArray()) idle.RemoveTransition(transition);
            foreach (Direction target in Directions)
            {
                AnimatorStateTransition move = idle.AddTransition(walkStates[target.name]);
                move.hasExitTime = false;
                move.duration = 0f;
                move.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
                move.AddCondition(target.axisMode, target.threshold, target.axis);
            }
        }

        stateMachine.defaultState = idleStates["South"];
        if (legacyIdle != null) stateMachine.RemoveState(legacyIdle);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[BeniditoDirectionalIdle] Four directional idle states generated and locomotion transitions rewired.");
    }

    [MenuItem("InfernosCurse/Player/Validate Directional Idles")]
    public static void Validate()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var stateMachine = controller != null ? controller.layers[0].stateMachine : null;
        var errors = new List<string>();
        if (stateMachine == null) errors.Add("controller or base state machine is missing");
        else
        {
            foreach (Direction direction in Directions)
            {
                AnimatorState idle = FindState(stateMachine, direction.idleState);
                if (idle == null) errors.Add($"missing {direction.idleState}");
                else if (idle.motion == null) errors.Add($"{direction.idleState} has no motion");
                AnimatorState walk = FindState(stateMachine, direction.walkState);
                if (walk == null || !walk.transitions.Any(t => t.destinationState == idle))
                    errors.Add($"{direction.walkState} does not stop in {direction.idleState}");
            }
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors) Debug.LogError("[BeniditoDirectionalIdle] " + error);
            throw new InvalidOperationException($"Directional idle validation failed with {errors.Count} error(s).");
        }
        Debug.Log("[BeniditoDirectionalIdle] Validation passed for North, South, East, and West.");
    }

    static AnimationClip BuildIdleClip(string direction, AnimationClip source)
    {
        string path = $"{GeneratedDir}/Benidito_Idle_{direction}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = $"Benidito_Idle_{direction}" };
            AssetDatabase.CreateAsset(clip, path);
        }

        foreach (EditorCurveBinding oldBinding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            AnimationUtility.SetObjectReferenceCurve(clip, oldBinding, null);

        EditorCurveBinding binding = AnimationUtility.GetObjectReferenceCurveBindings(source)
            .FirstOrDefault(b => b.type == typeof(SpriteRenderer) && b.propertyName == "m_Sprite");
        ObjectReferenceKeyframe[] sourceFrames = AnimationUtility.GetObjectReferenceCurve(source, binding);
        Sprite sprite = sourceFrames?.Select(k => k.value as Sprite).FirstOrDefault(s => s != null);
        if (sprite == null) throw new InvalidOperationException($"{source.name} contains no sprite frames");

        var frames = new[]
        {
            new ObjectReferenceKeyframe { time = 0f, value = sprite },
            new ObjectReferenceKeyframe { time = 1f, value = sprite }
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, frames);
        clip.frameRate = source.frameRate;
        clip.wrapMode = WrapMode.Loop;
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static AnimatorState FindState(AnimatorStateMachine stateMachine, string name) =>
        stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
    }
}
