using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;


#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI; // Keyboard, Mouse
#endif

public class SimpleCameraController : MonoBehaviour
{
    class CameraState
    {
        public float yaw;
        public float pitch;
        public float roll;
        public float x;
        public float y;
        public float z;

        public void SetFromTransform(Transform t)
        {
            pitch = t.eulerAngles.x;
            yaw = t.eulerAngles.y;
            roll = t.eulerAngles.z;
            x = t.position.x;
            y = t.position.y;
            z = t.position.z;
        }

        public void Translate(Vector3 translation)
        {
            Vector3 rotatedTranslation = Quaternion.Euler(pitch, yaw, roll) * translation;
            x += rotatedTranslation.x;
            y += rotatedTranslation.y;
            z += rotatedTranslation.z;
        }

        public void LerpTowards(CameraState target, float positionLerpPct, float rotationLerpPct)
        {
            yaw = Mathf.Lerp(yaw, target.yaw, rotationLerpPct);
            pitch = Mathf.Lerp(pitch, target.pitch, rotationLerpPct);
            roll = Mathf.Lerp(roll, target.roll, rotationLerpPct);

            x = Mathf.Lerp(x, target.x, positionLerpPct);
            y = Mathf.Lerp(y, target.y, positionLerpPct);
            z = Mathf.Lerp(z, target.z, positionLerpPct);
        }

        public void UpdateTransform(Transform t)
        {
            t.eulerAngles = new Vector3(pitch, yaw, roll);
            t.position = new Vector3(x, y, z);
        }
    }

    CameraState m_TargetCameraState = new CameraState();
    CameraState m_InterpolatingCameraState = new CameraState();

    [Header("Movement Settings")]
    [Tooltip("Exponential boost factor on translation, controllable by mouse wheel.")]
    public float boost = 3.5f;

    [Tooltip("Time it takes to interpolate camera position 99 percent to the target."), Range(0.001f, 1f)]
    public float positionLerpTime = 0.2f;

    [Header("Rotation Settings")]
    [Tooltip("X = Change in mouse position, Y = factor for camera rotation.")]
    public AnimationCurve mouseSensitivityCurve =
        new AnimationCurve(new Keyframe(0f, 0.5f, 0f, 5f), new Keyframe(1f, 2.5f, 0f, 0f));

    [Tooltip("Time it takes to interpolate camera rotation 99 percent to the target."), Range(0.001f, 1f)]
    public float rotationLerpTime = 0.01f;

    [Tooltip("Invert Y axis for mouse look.")]
    public bool invertY = false;

    void OnEnable()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (EventSystem.current != null)
        {
            GameObject go = EventSystem.current.gameObject;
            if (go != null)
            {
                StandaloneInputModule sim = go.GetComponent<StandaloneInputModule>();
                if (sim != null)
                {
                    Destroy(go.GetComponent<StandaloneInputModule>());
                    go.AddComponent<InputSystemUIInputModule>();
                }
            }
        }
#endif

        m_TargetCameraState.SetFromTransform(transform);
        m_InterpolatingCameraState.SetFromTransform(transform);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    Vector3 GetInputTranslationDirection()
    {
        Vector3 direction = Vector3.zero;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed) direction += Vector3.forward;
            if (kb.sKey.isPressed) direction += Vector3.back;
            if (kb.aKey.isPressed) direction += Vector3.left;
            if (kb.dKey.isPressed) direction += Vector3.right;
            if (kb.qKey.isPressed) direction += Vector3.down;
            if (kb.eKey.isPressed) direction += Vector3.up;
        }
#else
        if (Input.GetKey(KeyCode.W)) direction += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) direction += Vector3.back;
        if (Input.GetKey(KeyCode.A)) direction += Vector3.left;
        if (Input.GetKey(KeyCode.D)) direction += Vector3.right;
        if (Input.GetKey(KeyCode.Q)) direction += Vector3.down;
        if (Input.GetKey(KeyCode.E)) direction += Vector3.up;
#endif
        return direction;
    }

    bool GetRightMouseDown()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(1);
#endif
    }

    bool GetRightMouseUp()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.rightButton.wasReleasedThisFrame;
#else
        return Input.GetMouseButtonUp(1);
#endif
    }

    bool GetRightMouseHeld()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
        return Input.GetMouseButton(1);
#endif
    }

    Vector2 GetMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Mouse.current == null) return Vector2.zero;
        return Mouse.current.delta.ReadValue();
#else
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
    }

    float GetScrollY()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Mouse.current == null) return 0f;
        // New Input System scroll is in pixels, scale it to feel similar to Input.mouseScrollDelta
        return Mouse.current.scroll.ReadValue().y * 0.01f;
#else
        return Input.mouseScrollDelta.y;
#endif
    }

    bool GetShift()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var kb = Keyboard.current;
        return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
    }

    void Update()
    {
        // Lock on RMB press
        if (GetRightMouseDown())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Unlock on RMB release
        if (GetRightMouseUp())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Rotation while RMB held
        if (GetRightMouseHeld())
        {
            Vector2 mouseMovement = GetMouseDelta();
            mouseMovement.y *= invertY ? 1f : -1f;

            float mouseSensitivityFactor = mouseSensitivityCurve.Evaluate(mouseMovement.magnitude);
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            m_TargetCameraState.yaw += mouseMovement.x * mouseSensitivityFactor * 0.1f;
            m_TargetCameraState.pitch += mouseMovement.y * mouseSensitivityFactor * 0.1f;
#else
            m_TargetCameraState.yaw += mouseMovement.x * mouseSensitivityFactor;
            m_TargetCameraState.pitch += mouseMovement.y * mouseSensitivityFactor;
#endif
        }

        // Translation
        Vector3 translation = GetInputTranslationDirection() * Time.deltaTime;

        // Speed up when Shift is held
        if (GetShift())
            translation *= 10.0f;

        // Boost with scroll
        boost += GetScrollY() * 0.2f;
        translation *= Mathf.Pow(2.0f, boost);

        m_TargetCameraState.Translate(translation);

        // Framerate independent interpolation
        float positionLerpPct = 1f - Mathf.Exp((Mathf.Log(1f - 0.99f) / positionLerpTime) * Time.deltaTime);
        float rotationLerpPct = 1f - Mathf.Exp((Mathf.Log(1f - 0.99f) / rotationLerpTime) * Time.deltaTime);
        m_InterpolatingCameraState.LerpTowards(m_TargetCameraState, positionLerpPct, rotationLerpPct);

        m_InterpolatingCameraState.UpdateTransform(transform);
    }
}
