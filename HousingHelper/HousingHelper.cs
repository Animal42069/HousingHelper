using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnityEngine;

namespace HousingHelper
{
    [BepInPlugin(GUID, "Housing Helper", VERSION)]
    [BepInProcess("AI-Syoujyo")]

    public class HousingHelper : BaseUnityPlugin
    {
        public const string VERSION = "0.1.0.0";
        private const string GUID = "animal42069.aihousinghelper";

        internal static ConfigEntry<float> _rotationSnap;
        internal static ConfigEntry<bool> _randomManipulate;
        internal static ConfigEntry<bool> _allowNegativeY;

        private void Awake()
        {
            _rotationSnap = Config.Bind("Settings", "Rotation Snap", 15f, "Angle in degrees to snap X and Z rotations to");
            _allowNegativeY = Config.Bind("Settings", "Allow objects below world plane", true, "Allow objects to be moved/rotated below the world plane");
            _randomManipulate = Config.Bind("Settings", "Random Manipulation", false, "Changes the behavior of the two manipulate buttons so that they rotate a random amount instead of +/- 90 degrees");

            Harmony.CreateAndPatchAll(typeof(HousingHelper));
        }

        private static float Round(float _value)
        {
            bool flag = _value < 0f;
            return Mathf.RoundToInt(Mathf.Abs(_value) / _rotationSnap.Value) * _rotationSnap.Value * ((!flag) ? 1 : -1);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Housing.GuideManager), "CheckRot")]
        private static bool HousingGuideManager_CheckRot(ref bool __result)
        {
            if (!_allowNegativeY.Value)
                return true;

            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Housing.GuideManager), "CorrectPos")]
        private static bool HousingGuideManager_CorrectPos(ref bool __result, ref Vector3 _pos)
        {
            if (!_allowNegativeY.Value)
                return true;

            __result = false;
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Housing.GuideRotation), "OnDrag")]
        private static void HousingGuideRotation_OnDrag(Housing.ObjectCtrl ___objectCtrl)
        {
            Vector3 eulerAngles = ___objectCtrl.LocalEulerAngles;
            ___objectCtrl.LocalEulerAngles = new Vector3(Round(eulerAngles.x), Round(eulerAngles.y), Round(eulerAngles.z));
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Housing.GuideObject), "Awake")]
        private static void HousingGuideObject_Awake(ref Housing.GuideBase[] ___guide)
        {
            if (___guide.Length != 5 || ___guide[3] == null)
                return;

            Console.WriteLine($"___guide.Length {___guide.Length}");

            GameObject rotationX = Instantiate(___guide[3].gameObject);
            rotationX.name = "X";
            rotationX.transform.name = "X";
            rotationX.transform.localScale = new Vector3(5, 5, 5);
            rotationX.transform.localEulerAngles = new Vector3(0, 0, 0);
            rotationX.transform.parent = ___guide[3].gameObject.transform.parent;
            rotationX.GetComponent<MeshRenderer>().material.color = Color.red;
            Housing.GuideRotation guideRotationX = rotationX.GetComponent<Housing.GuideRotation>();
            guideRotationX.axis = Housing.GuideRotation.RotationAxis.X;
            guideRotationX.name = "X";

            GameObject rotationZ = Instantiate(___guide[3].gameObject);
            rotationZ.name = "Z";
            rotationZ.transform.name = "Z";
            rotationZ.transform.localScale = new Vector3(5, 5, 5);
            rotationZ.transform.localEulerAngles = new Vector3(0, 270, 0);
            rotationZ.transform.parent = ___guide[3].gameObject.transform.parent;
            rotationZ.GetComponent<MeshRenderer>().material.color = Color.blue;
            Housing.GuideRotation guideRotationZ = rotationZ.GetComponent<Housing.GuideRotation>();
            guideRotationZ.axis = Housing.GuideRotation.RotationAxis.Z;
            guideRotationZ.name = "Z";

            Housing.GuideBase[] replacementGuide = new Housing.GuideBase[___guide.Length + 2];

            for (int i = 0; i < ___guide.Length; i++)
                replacementGuide[i] = ___guide[i];

            replacementGuide[___guide.Length] = guideRotationX;
            replacementGuide[___guide.Length + 1] = guideRotationZ;

            ___guide = replacementGuide;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Housing.ObjectCtrl), nameof(Housing.ObjectCtrl.Position), MethodType.Setter)]
        private static bool HousingObjectCtrl_PositionSetter(Housing.ObjectCtrl __instance, Vector3 value)
        {
            if (!_allowNegativeY.Value)
                return true;

            __instance.Transform.position = value;
            __instance.LocalPosition = __instance.Transform.localPosition;

            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Housing.ManipulateUICtrl), "Rotation")]
        private static void HousingManipulateUICtrl_Rotation(ref float _value)
        {
            if (!_randomManipulate.Value)
                return;

            _value = UnityEngine.Random.Range(-180f, 180f);
        }
    }
}
