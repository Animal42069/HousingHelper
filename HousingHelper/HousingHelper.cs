using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Housing;
using System;
using UnityEngine;
using System.Timers;

namespace HousingHelper
{
    [BepInPlugin(GUID, "Housing Helper", VERSION)]
    [BepInProcess("AI-Syoujyo")]

    public class HousingHelper : BaseUnityPlugin
    {
        public const string VERSION = "1.1.1.0";
        private const string GUID = "animal42069.aihousinghelper";

        internal static ConfigEntry<float> _moveSnap;
        internal static ConfigEntry<float> _rotationSnap;
        internal static ConfigEntry<bool> _randomManipulate;
        internal static ConfigEntry<bool> _placeAtSelection;
        internal static ConfigEntry<bool> _selectAddedObject;
        internal static ConfigEntry<bool> _allowNegativeY;
        internal static ConfigEntry<float> _cameraMoveSpeed;
        internal static ConfigEntry<bool> _orthographicCamera;

        private static Housing.CraftCamera craftCamera;
 //       private static Camera housingCamera;
        private static bool inHousingMode = false;

        private static float buttonDownTime = 0;

        private void Awake()
        {
            _moveSnap = Config.Bind("Settings", "Move Snap", 1f, "Grid snap for object movement");
            _rotationSnap = Config.Bind("Settings", "Rotation Snap", 15f, "Angle in degrees to snap X and Z rotations to");
            _allowNegativeY = Config.Bind("Settings", "Allow objects below world plane", true, "Allow objects to be moved/rotated below the world plane");
            _randomManipulate = Config.Bind("Settings", "Random Manipulation", false, "Changes the behavior of the two manipulate buttons so that they rotate a random amount instead of +/- 90 degrees");
            _placeAtSelection = Config.Bind("Settings", "Add Object to Active Location", true, "Places added objects at the selected object location, instead of the 0 position.");
            _selectAddedObject = Config.Bind("Settings", "Select Added Object", true, "Automatically select an object when it is added.");
            (
                _cameraMoveSpeed = Config.Bind("Settings", "Camera Movement Speed", 40f, "Amount to move camera when arrow keys are used.")).SettingChanged += (s, e) =>
            { AdjustCameraSettings(_cameraMoveSpeed.Value, _orthographicCamera.Value); };
            (_orthographicCamera = Config.Bind("Settings", "Orthographic Camera", true, "Orthographic Camera.")).SettingChanged += (s, e) =>
            { AdjustCameraSettings(_cameraMoveSpeed.Value, _orthographicCamera.Value); };

            Harmony.CreateAndPatchAll(typeof(HousingHelper));
        }

        private static float SnapRotation(float value)
        {
            bool negativeValue = value < 0f;
            return Mathf.RoundToInt(Mathf.Abs(value) / _rotationSnap.Value) * _rotationSnap.Value * ((!negativeValue) ? 1 : -1);
        }

        [HarmonyPrefix, HarmonyPatch(typeof(GuideManager), "CheckRot")]
        private static bool HousingGuideManager_CheckRot(ref bool __result)
        {
            if (!_allowNegativeY.Value)
                return true;

            __result = true;
            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(GuideManager), "CorrectPos")]
        private static bool HousingGuideManager_CorrectPos(ref bool __result)
        {
            if (!_allowNegativeY.Value)
                return true;

            __result = false;
            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GuideRotation), "OnDrag")]
        private static void HousingGuideRotation_OnDrag(ObjectCtrl ___objectCtrl)
        {
            Vector3 eulerAngles = ___objectCtrl.LocalEulerAngles;
            ___objectCtrl.LocalEulerAngles = new Vector3(SnapRotation(eulerAngles.x), SnapRotation(eulerAngles.y), SnapRotation(eulerAngles.z));
        }

        [HarmonyPrefix, HarmonyPatch(typeof(GuideObject), "Awake")]
        private static void HousingGuideObject_Awake(ref GuideBase[] ___guide)
        {
            if (___guide.Length != 5 || ___guide[3] == null)
                return;

    //        Housing.CraftCamera camera = new Housing.CraftCamera();
            /*
                        Cinemachine.LensSettings settings = camera.lens;
                    settings.Orthographic = true;

                        camera.lens = settings;
                        camera.moveSpeed = 15;
            */
            GameObject rotationX = Instantiate(___guide[3].gameObject);
            rotationX.name = "X";
            rotationX.transform.name = "X";
            rotationX.transform.localScale = new Vector3(5, 5, 5);
            rotationX.transform.localEulerAngles = new Vector3(0, 0, 0);
            rotationX.transform.parent = ___guide[3].gameObject.transform.parent;
            rotationX.GetComponent<MeshRenderer>().material.color = Color.red;
            GuideRotation guideRotationX = rotationX.GetComponent<GuideRotation>();
            guideRotationX.axis = GuideRotation.RotationAxis.X;
            guideRotationX.name = "X";

            GameObject rotationZ = Instantiate(___guide[3].gameObject);
            rotationZ.name = "Z";
            rotationZ.transform.name = "Z";
            rotationZ.transform.localScale = new Vector3(5, 5, 5);
            rotationZ.transform.localEulerAngles = new Vector3(0, 270, 0);
            rotationZ.transform.parent = ___guide[3].gameObject.transform.parent;
            rotationZ.GetComponent<MeshRenderer>().material.color = Color.blue;
            GuideRotation guideRotationZ = rotationZ.GetComponent<GuideRotation>();
            guideRotationZ.axis = GuideRotation.RotationAxis.Z;
            guideRotationZ.name = "Z";

            GuideBase[] replacementGuide = new GuideBase[___guide.Length + 2];

            for (int i = 0; i < ___guide.Length; i++)
                replacementGuide[i] = ___guide[i];

            replacementGuide[___guide.Length] = guideRotationX;
            replacementGuide[___guide.Length + 1] = guideRotationZ;

            ___guide = replacementGuide;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ObjectCtrl), nameof(ObjectCtrl.Position), MethodType.Setter)]
        private static bool HousingObjectCtrl_PositionSetter(ObjectCtrl __instance, Vector3 value)
        {
            if (!_allowNegativeY.Value)
                return true;

            __instance.Transform.position = value;
            __instance.LocalPosition = __instance.Transform.localPosition;

            return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ManipulateUICtrl), "Rotation")]
        private static bool HousingManipulateUICtrl_Rotation()
        {
            if (!_randomManipulate.Value)
                return true;

            var selectObjects = Singleton<Selection>.Instance.SelectObjects;
            foreach (var selectObject in selectObjects)
            {
                if (selectObject == null)
                    continue;

                Vector3 localEulerAngles = selectObject.LocalEulerAngles;
                localEulerAngles.y = SnapRotation((localEulerAngles.y + UnityEngine.Random.Range(-180f, 180f)) % 360f);
                selectObject.LocalEulerAngles = localEulerAngles;
            }

            return false;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Housing.Command.AddItemCommand), "Do")]
        private static void HousingCommand_AddItemCommandDo(ObjectCtrl ___objectCtrl)
        {
            if (_placeAtSelection.Value && Singleton<Selection>.Instance.SelectObject != null)
                ___objectCtrl.LocalPosition = Singleton<Selection>.Instance.SelectObject.LocalPosition;

            if (_selectAddedObject.Value)
            {
                ObjectCtrl[] objectCtrls = new ObjectCtrl[1];
                objectCtrls[0] = ___objectCtrl;

                Singleton<Selection>.Instance.SetSelectObjects(objectCtrls);
                Singleton<CraftScene>.Instance.UICtrl.ListUICtrl.VirtualizingTreeView.SelectedItems = objectCtrls;
                Singleton<CraftScene>.Instance.UICtrl.AddUICtrl.Reselect();
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CraftScene), "Start")]
        private static void HousingCraftScene_Start(Housing.CraftCamera ___craftCamera, Camera[] ___cameras)
        {
            Console.WriteLine("HousingCraftCamera_Start");

            craftCamera = ___craftCamera;
            if (___cameras.IsNullOrEmpty())
                return;
  //          housingCamera = ___cameras[0];

            AdjustCameraSettings(_cameraMoveSpeed.Value, _orthographicCamera.Value);
        }


        private static void AdjustCameraSettings(float moveSpeed, bool orthographic)
        {
            if (craftCamera == null)
                return;

            craftCamera.moveSpeed = moveSpeed;
            craftCamera.keyMoveSpeed = 2 * moveSpeed;
            craftCamera.xRotSpeed = moveSpeed;
            craftCamera.yRotSpeed = moveSpeed;
            /*
                        if (housingCamera == null)
                            return;

                        Cinemachine.LensSettings settings = craftCamera.lens;
                        settings.Orthographic = orthographic;
                        settings.OrthographicSize = 500f;
                        craftCamera.lens = settings;
                        housingCamera.orthographic = orthographic;
                        housingCamera.orthographicSize = 500f;

                        Console.WriteLine($"housingCamera.orthographicSize {housingCamera.orthographicSize}");*/
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Manager.Housing), "StartHousing")]
        private static void Housing_StartHousing()
        {
            inHousingMode = true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Manager.Housing), "EndHousing")]
        private static void Housing_EndHousing()
        {
            inHousingMode = false;
        }

        void Update()
        {
            if (!inHousingMode)
                return;

            if (Input.GetMouseButtonDown(0))
                buttonDownTime = Time.time;

            if (Input.GetKey(KeyCode.LeftAlt))
            {
                if (Singleton<Selection>.Instance.SelectObject == null)
                    return;

                if (Input.GetAxis("Mouse ScrollWheel") != 0f)
                    ScrollWheelRotateObjects(Input.GetAxis("Mouse ScrollWheel") > 0);
                else
                    MouseMoveObjects();
            }
            else if (Input.GetKey(KeyCode.LeftControl))
            {
                if (Singleton<Selection>.Instance.SelectObject == null)
                    return;

                if (Input.GetAxis("Mouse ScrollWheel") != 0f)
                    ScrollWheelMoveObjects(Input.GetAxis("Mouse ScrollWheel") > 0);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if ((Time.time - buttonDownTime) < 0.2)
                    MouseSelectObjects();
            }
        }

        private static void ScrollWheelMoveObjects(bool up)
        {
            var selectObjects = Singleton<Selection>.Instance.SelectObjects;

            foreach (var selectObject in selectObjects)
            {
                if (selectObject == null)
                    continue;

                selectObject.Position += up ? new Vector3(0, 1, 0) : new Vector3(0, -1, 0);
            }
        }
        private static void ScrollWheelRotateObjects(bool clockwise)
        {
            var selectedObject = Singleton<Selection>.Instance.SelectObject;
            float rotation = clockwise ? _rotationSnap.Value : -_rotationSnap.Value;

            Vector3 localEulerAngles = Singleton<Selection>.Instance.SelectObject.LocalEulerAngles;
            localEulerAngles.y = SnapRotation((localEulerAngles.y + rotation) % 360f);
            Singleton<Selection>.Instance.SelectObject.LocalEulerAngles = localEulerAngles;

            var selectObjects = Singleton<Selection>.Instance.SelectObjects;
            if (selectObjects.Length <= 1)
                return;
            
            foreach (var selectObject in selectObjects)
            {
                if (selectObject == null || selectObject == selectedObject)
                    continue;

                Vector2 newObjectPosition = RotatePoint(new Vector2(selectObject.Position.x, selectObject.Position.z),
                                                        new Vector2(selectedObject.Position.x, selectedObject.Position.z),
                                                        -rotation);

                Vector3 selectEulerAngles = selectObject.LocalEulerAngles;
                selectEulerAngles.y = SnapRotation((selectEulerAngles.y + rotation) % 360f);
                selectObject.LocalEulerAngles = selectEulerAngles;

                selectObject.Position = new Vector3((float)Math.Floor(newObjectPosition.x + 0.5f),
                                                    selectObject.Position.y,
                                                    (float)Math.Floor(newObjectPosition.y + 0.5f));
            }
        }

        private static Vector2 RotatePoint(Vector2 pointToRotate, Vector2 centerPoint, float angleInDegrees)
        {
            double angleInRadians = angleInDegrees * (Math.PI / 180);
            double cosTheta = Math.Cos(angleInRadians);
            double sinTheta = Math.Sin(angleInRadians);
            return new Vector2((float)(cosTheta * (pointToRotate.x - centerPoint.x) - sinTheta * (pointToRotate.y - centerPoint.y) + centerPoint.x),
                               (float)(sinTheta * (pointToRotate.x - centerPoint.x) + cosTheta * (pointToRotate.y - centerPoint.y) + centerPoint.y));
        }

        private static void MouseMoveObjects()
        {
            var selectedObject = Singleton<Selection>.Instance.SelectObject;
            var originalPosition = selectedObject.Position;
            Plane selectionPlane = new Plane(Vector3.up, selectedObject.Position.y);
            Ray mouseRay = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (!selectionPlane.Raycast(mouseRay, out float distanceToPlane))
                return;

            Vector3 movePosition = mouseRay.GetPoint(distanceToPlane);
            selectedObject.Position = new Vector3(Mathf.Floor(movePosition.x + 0.5f),
                selectedObject.Position.y,
                Mathf.Floor(movePosition.z + 0.5f));

            var selectObjects = Singleton<Selection>.Instance.SelectObjects;
            if (selectObjects.Length <= 1 || originalPosition == selectedObject.Position)    
                return;
            
            var objectMovement = selectedObject.Position - originalPosition;
            foreach (var selectObject in selectObjects)
            {
                if (selectObject == null || selectObject == selectedObject)
                    continue;

                selectObject.Position += objectMovement;
            }
        }

        private static void MouseSelectObjects()
        {
            var ui = Singleton<CraftScene>.Instance.GetComponentInChildren<RectTransform>();

            float resolutionWidth = 2 * ui.localPosition.x;
            float resolutionHeight= 2 * ui.localPosition.y;

            var borderWidth = resolutionWidth * 0.15f;
            var borderHeight = resolutionHeight * 0.1f;

            Vector3 mousePosition = Input.mousePosition;

            if (mousePosition.x < borderWidth || mousePosition.x > (resolutionWidth - borderWidth))
                return;
            if (mousePosition.y < borderHeight|| mousePosition.y > (resolutionHeight - borderHeight))
                return;

            if (!Physics.Raycast(Camera.main.ScreenPointToRay(mousePosition), out var hit, Mathf.Infinity))
                return;

            var itemComponent = hit.collider.GetComponentInParent<ItemComponent>();
            if (itemComponent == null)         
                return;

            ObjectCtrl selectedObject = null;
            foreach (var item in Singleton<CraftScene>.Instance.UICtrl.ListUICtrl.VirtualizingTreeView.Items)
            {
                if (item.GetType() != typeof(OCItem))
                    continue;

                var ocItem = item as OCItem;

                if (item != null && ocItem.ItemComponent == itemComponent)
                {
                    selectedObject = ocItem;
                    break;
                }
            }

            if (selectedObject == null)
                return;

            ObjectCtrl[] objectCtrls;
            if (Singleton<Selection>.Instance.SelectObject != null && Input.GetKey(KeyCode.LeftShift))
            {
                var selectObjects = Singleton<Selection>.Instance.SelectObjects;

                foreach (var selectObject in selectObjects)
                    if (selectObject == selectedObject)
                        return;

                objectCtrls = new ObjectCtrl[selectObjects.Length + 1];

                for (int ctrl = 0; ctrl < selectObjects.Length; ctrl++)
                    objectCtrls[ctrl] = selectObjects[ctrl];

                objectCtrls[selectObjects.Length] = selectedObject;
            }
            else
            {
                objectCtrls = new ObjectCtrl[1];
                objectCtrls[0] = selectedObject;
            }

            Singleton<Selection>.Instance.SetSelectObjects(objectCtrls);
            Singleton<CraftScene>.Instance.UICtrl.ListUICtrl.VirtualizingTreeView.SelectedItems = objectCtrls;
    //        Singleton<CraftScene>.Instance.UICtrl.AddUICtrl.Reselect();
        }
    }
}
