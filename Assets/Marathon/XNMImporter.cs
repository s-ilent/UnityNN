using UnityEngine;
using UnityEditor.Experimental.AssetImporters;
using System.Collections.Generic;
using System.IO;
using Marathon.Formats.Mesh.Ninja;
using UnityEditor;


namespace SilentTools
{

[CustomEditor(typeof(XNMImporter))]
[CanEditMultipleObjects]
public class XNMImporterEditor : ScriptedImporterEditor
{
}


[ScriptedImporter(1, "xnm")]
public class XNMImporter : ScriptedImporter
{
    public float m_Scale = 0.05f;

    [SerializeField] private MotionType m_Type;
    [SerializeField] private float m_StartFrame;
    [SerializeField] private float m_EndFrame;
    [SerializeField] private int m_SubMotionCount;
    [SerializeField] private float m_Framerate;
    [SerializeField] private uint m_Reserved0;
    [SerializeField] private uint m_Reserved1;

    [SerializeField] private List<SubMotionType> m_subMotionFlags;
    [SerializeField] private List<SubMotionInterpolationType> m_subMotionInterpolations;
    [SerializeField] private List<string> m_subMotionPaths;
    [SerializeField] private List<int> m_subMotionLengths;

    public string[] m_nodeHierarchyTarget;

    private float ConvertBinaryAngleToFloat(int inAngle)
    {
        // Used for both ANGLE16 and ANGLE32
        float outValue = (float)((double)inAngle * (180.0 / 32768.0));
        return outValue;
    }

    public override void OnImportAsset(AssetImportContext ctx)
    {
        var clip = new AnimationClip();

        string shortName = Path.GetFileNameWithoutExtension(ctx.assetPath);

        List<SubMotionType> subMotionFlags = new List<SubMotionType>();
        List<SubMotionInterpolationType> subMotionInterpolations = new List<SubMotionInterpolationType>();
        List<string> subMotionPaths = new List<string>();
        List<int> subMotionLengths = new List<int>();

        if (m_nodeHierarchyTarget == null) m_nodeHierarchyTarget = new string[0];

        // Call on the NinjaNext function to parse the XNM for us.
        NinjaNext loader = new NinjaNext();

        try
        {
            loader.Load(ctx.assetPath);
        }
        catch (System.NotImplementedException)
        {
            Debug.Log($"{shortName}: Failed with NotImplementedException");
            // End immediately.
            return;
        }

        // The Next class has a field Motion where the NinjaMotion data is stored.

        // For debug, place the data into the fields. 
        m_Type = loader.Data.Motion.Type;
        m_StartFrame = loader.Data.Motion.StartFrame;
        m_EndFrame = loader.Data.Motion.EndFrame;
        m_SubMotionCount = loader.Data.Motion.SubMotions.Count;
        m_Framerate = loader.Data.Motion.Framerate;
        m_Reserved0 = loader.Data.Motion.Reserved0;
        m_Reserved1 = loader.Data.Motion.Reserved1;

        // Parse the frames into Unity AnimationClips and AnimationCurves
        /*
        Each Ninja motion is composed of submotions, which contain keyframes.
        A submotion, then, is analogous to an AnimationClip path/property, and the keyframes form an AnimationCurve.
        The Flags of the submotion determine what properties it controls. For instance, it seems like a submotion
        can target node 0, and then have an animation with flags indicating it animates the rotation Y. 
        One issue is that we don't know the hierarchy of the animation. 
        */

        /* Framerates... 
        Unity has a user-facing Framerate value for animations, but if generating one via script,
        it is not possible to set the framerate. Instead we have no choice but to scale framerates
        using a formula like this: frameRate / desiredFramerate
        */
        float framerateTimeScale = 60.0f / m_Framerate;

        //string animationPrefix = Path.GetFileNameWithoutExtension(ctx.assetPath) + "_Bone_"; // To match XNJ Blender export.
        string animationPrefix = "";

        foreach (NinjaSubMotion subMotion in loader.Data.Motion.SubMotions)
        {
            // Each SubMotion will be imported as an AnimationCurve
            // So far all our test samples do one axis at a time, so we only need one set of keyframes.

            // Note the usage of localEulerAnglesRaw, as without it Unity will alter the values and break them.

            string targetProperty = "";
            if (subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_TRANSLATION_X)) targetProperty = "localPosition.x";
            if (subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_TRANSLATION_Y)) targetProperty = "localPosition.y";
            if (subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_TRANSLATION_Z)) targetProperty = "localPosition.z";
            if (subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_ROTATION_X)) targetProperty = "localEulerAnglesRaw.x";
            if (subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_ROTATION_Y)) targetProperty = "localEulerAnglesRaw.y";
            if (subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_ROTATION_Z)) targetProperty = "localEulerAnglesRaw.z";
            if (subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_SCALING_X)) targetProperty = "localScale.x";
            if (subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_SCALING_Y)) targetProperty = "localScale.y";
            if (subMotion.Type.HasFlag(SubMotionType.NND_SMOTTYPE_SCALING_Z)) targetProperty = "localScale.z";

            Keyframe[] smKFs = new Keyframe[subMotion.Keyframes.Count];

            string thisPath = animationPrefix + subMotion.NodeIndex.ToString("0000");
            if (subMotion.NodeIndex < m_nodeHierarchyTarget.Length) thisPath = m_nodeHierarchyTarget[subMotion.NodeIndex];
            int index = 0;
            foreach (var kf in subMotion.Keyframes)
            {
                // Handle each type of Keyframe from NinjaKeyframes
                if (kf is NinjaKeyframe.NNS_MOTION_KEY_VECTOR)
                {
                    // handle later
                    Debug.Log($"{shortName}: NNS_MOTION_KEY_VECTOR not supported");
                }
                if (kf is NinjaKeyframe.NNS_MOTION_KEY_ROTATE_A16)
                {
                    // handle later
                    Debug.Log($"{shortName}: NNS_MOTION_KEY_ROTATE_A16 not supported");
                }
                if (kf is NinjaKeyframe.NNS_MOTION_KEY_FLOAT)
                {
                    NinjaKeyframe.NNS_MOTION_KEY_FLOAT thisKF = (NinjaKeyframe.NNS_MOTION_KEY_FLOAT)kf;
                    smKFs[index] = (new Keyframe(thisKF.Frame / 60.0f, thisKF.Value));
                }
                if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT16)
                {
                    NinjaKeyframe.NNS_MOTION_KEY_SINT16 thisKF = (NinjaKeyframe.NNS_MOTION_KEY_SINT16)kf;
                    smKFs[index] = (new Keyframe(thisKF.Frame / 60.0f, ConvertBinaryAngleToFloat(thisKF.Value)));
                }
                if (kf is NinjaKeyframe.NNS_MOTION_KEY_SINT32)
                {
                    NinjaKeyframe.NNS_MOTION_KEY_SINT32 thisKF = (NinjaKeyframe.NNS_MOTION_KEY_SINT32)kf;
                    smKFs[index] = (new Keyframe(thisKF.Frame / 60.0f, ConvertBinaryAngleToFloat(thisKF.Value)));
                }

                // Apply scales
                smKFs[index].time *= framerateTimeScale;
                if (targetProperty.Contains("Position")) 
                {
                    smKFs[index].value *= m_Scale;
                }
                if (targetProperty.Contains("localPosition.x")) 
                {
                    smKFs[index].value *= -1;
                }
                if (targetProperty.Contains("localEulerAnglesRaw.y")) 
                {
                    smKFs[index].value *= -1;
                }

                index++;
            }

            //AnimationCurve thisCurve = new AnimationCurve(smKFs);
            AnimationCurve thisCurve = new AnimationCurve();
            thisCurve.keys = smKFs;

            // Apply repeat/wrap mode.
            WrapMode thisWrapMode = WrapMode.Default;
            if (subMotion.InterpolationType.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_NOREPEAT)) 
                // Once might be better than ClampForever, but haven't ran into a test case.
                thisWrapMode = WrapMode.ClampForever;
            if (subMotion.InterpolationType.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTREPEAT))
                // This might be better as Once, but not sure
                thisWrapMode = WrapMode.Loop;
            if (subMotion.InterpolationType.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_REPEAT)) 
                thisWrapMode = WrapMode.Loop;
            if (subMotion.InterpolationType.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_MIRROR)) 
                thisWrapMode = WrapMode.PingPong;
            if (subMotion.InterpolationType.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_OFFSET)) 
                Debug.Log($"{shortName}: Clip uses offset wrap mode, which is unsupported");
            
            thisCurve.preWrapMode = thisWrapMode;
            thisCurve.postWrapMode = thisWrapMode;

            // Apply interpolation.
            // So far I've only seen clips with Linear and Constant interpolation. 
            AnimationUtility.TangentMode thisTangentMode = AnimationUtility.TangentMode.ClampedAuto;
            if (subMotion.InterpolationType.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_LINEAR)) 
                thisTangentMode = AnimationUtility.TangentMode.Linear;
            if (subMotion.InterpolationType.HasFlag(SubMotionInterpolationType.NND_SMOTIPTYPE_CONSTANT)) 
                thisTangentMode = AnimationUtility.TangentMode.Constant;

            for (int kI = 0; kI < thisCurve.keys.Length; kI++)
            {
                AnimationUtility.SetKeyLeftTangentMode(thisCurve, kI, thisTangentMode);
                AnimationUtility.SetKeyRightTangentMode(thisCurve, kI, thisTangentMode);
            }

            // Apply the content of thisCurve to the Clip.
            clip.SetCurve(thisPath, typeof(Transform), targetProperty, thisCurve);

            // Apply motion repeat flags.
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            if (m_Type.HasFlag(MotionType.NND_MOTIONTYPE_TRIGGER)) 
                Debug.Log($"{shortName}: NND_MOTIONTYPE_TRIGGER is unsupported");
            if (m_Type.HasFlag(MotionType.NND_MOTIONTYPE_NOREPEAT)) 
                clipSettings.loopTime = false;
                clipSettings.loopBlend = false;
            if (m_Type.HasFlag(MotionType.NND_MOTIONTYPE_CONSTREPEAT)) 
                clipSettings.loopTime = true;
                clipSettings.loopBlend = false;
            if (m_Type.HasFlag(MotionType.NND_MOTIONTYPE_REPEAT)) 
                clipSettings.loopTime = true;
                clipSettings.loopBlend = true;
            if (m_Type.HasFlag(MotionType.NND_MOTIONTYPE_MIRROR)) 
                Debug.Log($"{shortName}: NND_MOTIONTYPE_MIRROR is unsupported");
            if (m_Type.HasFlag(MotionType.NND_MOTIONTYPE_OFFSET)) 
                // Can probably set from m_StartFrame, need test case
                Debug.Log($"{shortName}: NND_MOTIONTYPE_OFFSET is unsupported");

            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);

            subMotionFlags.Add(subMotion.Type);
            subMotionInterpolations.Add(subMotion.InterpolationType);
            subMotionPaths.Add($"{thisPath}/{targetProperty}");
            subMotionLengths.Add(subMotion.Keyframes.Count);
        }

        m_subMotionFlags = subMotionFlags;
        m_subMotionInterpolations = subMotionInterpolations;
        m_subMotionPaths = subMotionPaths;
        m_subMotionLengths = subMotionLengths;

        // I'm not sure if this should be used here, as it can alter tangents
        // but since switching to localEulerAnglesRaw I don't see it causing issues
        // with linear animations. 
        clip.EnsureQuaternionContinuity();

        ctx.AddObjectToAsset("main", clip);
        ctx.SetMainObject(clip);

    }
}

}