// Selects TargetBoxScriptFlat based on its smallest local position
// and simulates click action when prompted

// SP v1.11.106.2

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class BetterTargetSelect : MonoBehaviour
{
    private Type TargetBoxType;

    private Transform TargetBoxesParent;
    private Component NextTargetBox;

    private Camera OverlayCamera;

    // Custom key functionality from AceRadar
    [SerializeField] private KeyCode[] NextTargetKeyboard;
    [SerializeField] private KeyCode[] NextTargetJoystick;
    private string ControlsPath;
    private string FileKey = "NTKEY.TXT";
    private string FileJoystick = "NTJOY.TXT";

    private void Awake()
    {
        ControlsPath = Path.Combine(Application.persistentDataPath, "NACHSAVE", "NEXTTGT");
        Directory.CreateDirectory(ControlsPath);

        KeyCodeImportFormat importKeyboard = ImportKeyCode(Path.Combine(ControlsPath, FileKey), KeyControls.keyboard);
        if (importKeyboard.success)
            NextTargetKeyboard = importKeyboard.keys;
        else
            ExportKeyCode(Path.Combine(ControlsPath, FileKey), NextTargetKeyboard);

        KeyCodeImportFormat importJoystick = ImportKeyCode(Path.Combine(ControlsPath, FileJoystick), KeyControls.joystick);
        if (importJoystick.success)
            NextTargetJoystick = importJoystick.keys;
        else
            ExportKeyCode(Path.Combine(ControlsPath, FileJoystick), NextTargetJoystick);
    }

    private void Start()
    {
        TargetBoxesParent = GameObject.Find("Targeting/Hud/Targets").transform;
        if (TargetBoxesParent == null)
            Debug.LogError("Cannot find TargetBoxesParent");
        else
            Debug.Log("Found TargetBoxesParent");

        StartCoroutine("FindOverlayCamera");
        StartCoroutine("CheckTargetBoxes");
    }

    private void Update()
    {
        if (GetKeyControlDown(KeyControls.joystick) || GetKeyControlDown(KeyControls.keyboard))
        {
            ClickNewTargetBox();
        }
    }

    public void ClickNewTargetBox()
    {
        if (NextTargetBox == null || ServiceProvider.Instance.GameState.IsPaused || OverlayCamera == null)
        {
            return;
        }

        Vector3 screenPos = OverlayCamera.WorldToScreenPoint(NextTargetBox.gameObject.transform.position);

        // Convert to the coordinate system used by Windows
        screenPos.x = screenPos.x / OverlayCamera.pixelWidth * Screen.width;
        screenPos.y = Screen.height -  (screenPos.y / OverlayCamera.pixelHeight * Screen.height);

        MouseOperations.SetCursorPosition(Mathf.RoundToInt(screenPos.x), Mathf.RoundToInt(screenPos.y));
        MouseOperations.MouseEvent(MouseOperations.MouseEventFlags.LeftDown | MouseOperations.MouseEventFlags.LeftUp);
    }

    private IEnumerator CheckTargetBoxes()
    {
        while (true)
        {
            if (TargetBoxType == null)
            {
                // Get the target box component type
                Component[] components = FindObjectsOfType<Component>();
                foreach (Component component in components)
                {
                    Type cType = component.GetType();
                    if (cType.Name == "TargetBoxScriptFlat")
                    {
                        TargetBoxType = cType;
                        Debug.Log("Found TargetBoxScriptFlat type");
                        break;
                    }
                }

                yield return new WaitForSecondsRealtime(1f);
            }
            else
            {
                float NextTargetBoxDistance = float.MaxValue;

                for (int i = 0; i < TargetBoxesParent.childCount; i++)
                {
                    Transform targetBox = TargetBoxesParent.GetChild(i);

                    // Check if the child has a target box component
                    bool hasTargetBox = false;
                    foreach (Component component in targetBox.gameObject.GetComponents<Component>())
                    {
                        if (component.GetType() == TargetBoxType)
                        {
                            hasTargetBox = true;
                            break;
                        }
                    }

                    // Only perform the distance check on child objects with a target box component
                    if (hasTargetBox && targetBox.localPosition.magnitude < NextTargetBoxDistance)
                    {
                        NextTargetBox = targetBox;
                        NextTargetBoxDistance = targetBox.localPosition.magnitude;
                    }
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }
        }
    }

    private IEnumerator FindOverlayCamera()
    {
        while (OverlayCamera == null)
        {
            /*
            Camera[] cameras = FindObjectsOfType<Camera>();
            foreach (Camera camera in cameras)
            {
                if (camera.gameObject.name == "/UI Root/Camera")
                    OverlayCamera = camera;
            }
            */

            OverlayCamera = GameObject.Find("/UI Root/Camera").GetComponent<Camera>();

            if (OverlayCamera != null)
                Debug.Log("Found UI Camera");

            yield return new WaitForSeconds(0.25f);
        }
    }

    // BEGIN Custom key functionality from AceRadar
    public bool GetKeyControlDown(KeyControls control)
    {
        KeyCode[] keys;
        switch (control)
        {
            case KeyControls.keyboard:
                keys = NextTargetKeyboard;
                break;
            case KeyControls.joystick:
                keys = NextTargetJoystick;
                break;
            default:
                Debug.LogError("Invalid or unimplemented KeyControls!");
                return false;
        }

        for (int i = 0; i < keys.Length; i++)
        {
            if (i < keys.Length - 1)
            {
                if (!Input.GetKey(keys[i]) && keys[i] != KeyCode.None)
                {
                    return false;
                }
            }
            else    // i == keys.Length - 1
            {
                if (!Input.GetKeyDown(keys[i]) && keys[i] != KeyCode.None)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private KeyCodeImportFormat ImportKeyCode(string fp, KeyControls control)
    {
        if (File.Exists(fp))
        {
            try
            {
                Stream s = File.OpenRead(fp);
                using (StreamReader reader = new StreamReader(s))
                {
                    List<KeyCode> keys = new List<KeyCode>();
                    string line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        KeyCode newKey = (KeyCode)Enum.Parse(typeof(KeyCode), line, true);
                        if (newKey != KeyCode.None)
                        {
                            keys.Add(newKey);
                        }
                    }
                    return new KeyCodeImportFormat(true, keys.ToArray());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
                Debug.LogError("Error loading KeyCode file for " + control.ToString() + ".");
                return new KeyCodeImportFormat(false, new KeyCode[] { });
            }
        }
        else
        {
            Debug.LogWarning("KeyCode file for " + control.ToString() + " does not exist! Created file with default values.");
            return new KeyCodeImportFormat(false, new KeyCode[] { });
        }
    }

    private void ExportKeyCode(string fp, KeyCode[] keys)
    {
        if (keys.Length < 1)
        {
            Debug.LogError("Invalid KeyCode array!");
            return;
        }
        Stream s = File.Create(fp);
        using (StreamWriter writer = new StreamWriter(s))
        {
            foreach (KeyCode k in keys)
            {
                writer.WriteLine(k.ToString());
            }
        }
    }

    public enum KeyControls { keyboard, joystick }

    private struct KeyCodeImportFormat
    {
        public KeyCode[] keys;
        public bool success;

        public KeyCodeImportFormat(bool s, KeyCode[] k)
        {
            success = s;
            if (success)
            {
                keys = k;
            }
            else
            {
                keys = new KeyCode[] { };
            }
        }
    }

    // END Custom key functionality from AceRadar

    // Set cursor position and generate click on Windows
    // https://stackoverflow.com/a/46203290
    public class MouseOperations
    {
        [Flags]
        public enum MouseEventFlags
        {
            LeftDown = 0x00000002,
            LeftUp = 0x00000004,
            MiddleDown = 0x00000020,
            MiddleUp = 0x00000040,
            Move = 0x00000001,
            Absolute = 0x00008000,
            RightDown = 0x00000008,
            RightUp = 0x00000010
        }

        [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out MousePoint lpMousePoint);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        public static void SetCursorPosition(int X, int Y)
        {
            SetCursorPos(X, Y);
        }

        public static void SetCursorPosition(MousePoint point)
        {
            SetCursorPos(point.X, point.Y);
        }

        public static MousePoint GetCursorPosition()
        {
            MousePoint currentMousePoint;
            var gotPoint = GetCursorPos(out currentMousePoint);
            if (!gotPoint) { currentMousePoint = new MousePoint(0, 0); }
            return currentMousePoint;
        }

        public static void MouseEvent(MouseEventFlags value)
        {
            MousePoint position = GetCursorPosition();

            mouse_event
                ((int)value,
                position.X,
                 position.Y,
                 0,
                 0)
                 ;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MousePoint
        {
            public int X;
            public int Y;

            public MousePoint(int x, int y)
            {
                X = x;
                Y = y;
            }
        }
    }
}
