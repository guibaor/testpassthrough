using UnityEngine;
using UnityEngine.SceneManagement;
using Holo.Pilots.Login;

public class CalibrationExample : MonoBehaviour {
    private enum State { Comfort, Mode, Translation, Rotation }
    private State       state = State.Comfort;

    public GameObject   cameraReference;
    public float        _rotationSlightStep = 1f;
    public float        _translationSlightStep = 0.01f;
    public string        prefix = "pcs";
    public GameObject   ComfortUI;
    public GameObject   CalibrationModeUI;
    public GameObject   TransalationUI;
    public GameObject   RotationUI;

    bool rightTrigger = false;
    bool oldRightTrigger = false;
    bool leftTrigger = false;
    bool oldLeftTrigger = false;

    bool IsDownRightTrigger { get { return rightTrigger && !oldRightTrigger; } }
    bool IsDownLeftTrigger { get { return leftTrigger && !oldLeftTrigger; } }

    HoloMITCustomControls m_Controls;
    private void Awake() {
        m_Controls = new HoloMITCustomControls();

    }

    private void Start() {
        cameraReference.transform.localPosition = new Vector3(PlayerPrefs.GetFloat(prefix + "_pos_x", 0), PlayerPrefs.GetFloat(prefix + "_pos_y", 0), PlayerPrefs.GetFloat(prefix + "_pos_z", 0));
        cameraReference.transform.localRotation = Quaternion.Euler(PlayerPrefs.GetFloat(prefix + "_rot_x", 0), PlayerPrefs.GetFloat(prefix + "_rot_y", 0), PlayerPrefs.GetFloat(prefix + "_rot_z", 0));
    }

    // Update is called once per frame
    void Update() {
        oldRightTrigger = rightTrigger;
        oldLeftTrigger = leftTrigger;
        rightTrigger = m_Controls.SelfCalibration.RightTrigger.ReadValue<float>() >= 0.9;
        leftTrigger = m_Controls.SelfCalibration.LeftTrigger.ReadValue<float>() >= 0.9;
        if (AutomaticLoginManager.Instance.State == Holo.Pilots.Login.State.Calibration) {
            switch (state) {
                case State.Comfort:
                    #region UI
                    ComfortUI.SetActive(true);
                    CalibrationModeUI.SetActive(false);
                    TransalationUI.SetActive(false);
                    RotationUI.SetActive(false);
                    #endregion
                    #region INPUT
                    // I'm Comfortabler
                    if (IsDownRightTrigger) {
                        Debug.Log("Calibration: User is happy, return to LoginManager");
                        //Application.Quit();
                        //SceneManager.LoadScene("LoginManager");
                        AutomaticLoginManager.Instance.ChangeStateButton(Holo.Pilots.Login.State.Config);
                    }
                    // I'm not comfortable
                    if (IsDownLeftTrigger) {
                        Debug.Log("Calibration: Starting calibration process");
                        state = State.Mode;
                    }
                    // ResetAxisTrigger
                    #endregion
                    break;
                case State.Mode:
                    #region UI
                    ComfortUI.SetActive(false);
                    CalibrationModeUI.SetActive(true);
                    TransalationUI.SetActive(false);
                    RotationUI.SetActive(false);
                    #endregion
                    #region INPUT
                    //Activate Translation
                    if (ButtonADown()) {
                        Debug.Log("Calibration: Translation Mode");
                        state = State.Translation;
                    }
                    //Activate Rotation (UpAxis)
                    if (ButtonXDown()) {
                        Debug.Log("Calibration: Rotation Mode");
                        state = State.Rotation;
                    }
                    if (IsDownRightTrigger || IsDownLeftTrigger) {
                        Debug.Log("Calibration: User is done");
                        state = State.Comfort;
                    }
                    #endregion
                    break;
                case State.Translation:
                    #region UI
                    ComfortUI.SetActive(false);
                    CalibrationModeUI.SetActive(false);
                    TransalationUI.SetActive(true);
                    RotationUI.SetActive(false);
                    #endregion
                    #region INPUT
                    // Movement
                    float xAxis = m_Controls.SelfCalibration.RightAxis.ReadValue<Vector2>().x;
                    float zAxis = m_Controls.SelfCalibration.RightAxis.ReadValue<Vector2>().y;
                    float yAxis = m_Controls.SelfCalibration.LeftAxis.ReadValue<Vector2>().y;
                    // Code added by Jack to allow resetting of position (mainly for non-HMD users)
                    if (ButtonXDown()) {
                        cameraReference.transform.localPosition = new Vector3(0, 0, 0);
                        Debug.Log("Calibration: Try translation 0,0,0");
                    }
                    cameraReference.transform.localPosition += new Vector3(xAxis, yAxis, zAxis) * _translationSlightStep;
                    // Save Translation
                    if (IsDownRightTrigger) {
                        var pos = cameraReference.transform.localPosition;
                        PlayerPrefs.SetFloat(prefix + "_pos_x", pos.x);
                        PlayerPrefs.SetFloat(prefix + "_pos_y", pos.y);
                        PlayerPrefs.SetFloat(prefix + "_pos_z", pos.z);
                        Debug.Log($"Calibration: Translation Saved: {pos.x},{pos.y},{pos.z}");
                        state = State.Mode;
                    }
                    // Back
                    if (IsDownLeftTrigger) {
                        cameraReference.transform.localPosition = new Vector3(
                            PlayerPrefs.GetFloat(prefix + "_pos_x", 0),
                            PlayerPrefs.GetFloat(prefix + "_pos_y", 0),
                            PlayerPrefs.GetFloat(prefix + "_pos_z", 0)
                        );
                        var pos = cameraReference.transform.localPosition;
                        Debug.Log($"Calibration: Translation Reset to: {pos.x},{pos.y},{pos.z}");
                        state = State.Mode;
                    }
                    #endregion
                    break;
                case State.Rotation:
                    #region UI
                    ComfortUI.SetActive(false);
                    CalibrationModeUI.SetActive(false);
                    TransalationUI.SetActive(false);
                    RotationUI.SetActive(true);
                    #endregion
                    #region INPUT
                    // Rotation
                    float yAxisR = m_Controls.SelfCalibration.RightAxis.ReadValue<Vector2>().x;
                    // Code added by Jack to allow resetting of rotation (mainly for non-HMD users)
                    if (ButtonADown()) {
                        Debug.Log("Calibration: Try rotation 0,0,0");
                        cameraReference.transform.localEulerAngles = new Vector3(0, 0, 0);
                    }
                    cameraReference.transform.localRotation = Quaternion.Euler(cameraReference.transform.localRotation.eulerAngles + Vector3.up * _rotationSlightStep * yAxisR);
                    // Save Translation
                    if (IsDownRightTrigger) {
                        var rot = cameraReference.transform.localRotation.eulerAngles;
                        PlayerPrefs.SetFloat(prefix + "_rot_x", rot.x);
                        PlayerPrefs.SetFloat(prefix + "_rot_y", rot.y);
                        PlayerPrefs.SetFloat(prefix + "_rot_z", rot.z);

                        Debug.Log($"Calibration: Rotation Saved: {rot.x},{rot.y},{rot.z}");
                        state = State.Mode;
                    }
                    // Back
                    if (IsDownLeftTrigger) {
                        cameraReference.transform.localRotation = Quaternion.Euler(
                            PlayerPrefs.GetFloat(prefix + "_rot_x", 0),
                            PlayerPrefs.GetFloat(prefix + "_rot_y", 0),
                            PlayerPrefs.GetFloat(prefix + "_rot_z", 0)
                        );
                        var rot = cameraReference.transform.localRotation;
                        Debug.Log($"Calibration: Rotation Reset to: {rot.x},{rot.y},{rot.z}");
                        state = State.Mode;
                    }
                    #endregion
                    break;
                default:
                    break;
            }
        }
    }

    private void OnEnable() {
        m_Controls.SelfCalibration.LeftAxis.Enable();
        m_Controls.SelfCalibration.RightAxis.Enable();
        m_Controls.SelfCalibration.LeftTrigger.Enable();
        m_Controls.SelfCalibration.RightTrigger.Enable();
        m_Controls.SelfCalibration.ButtonA.Enable();
        m_Controls.SelfCalibration.ButtonX.Enable();
    }

    private void OnDisable() {
        m_Controls.SelfCalibration.LeftAxis.Disable();
        m_Controls.SelfCalibration.RightAxis.Disable();
        m_Controls.SelfCalibration.LeftTrigger.Disable();
        m_Controls.SelfCalibration.RightTrigger.Disable();
        m_Controls.SelfCalibration.ButtonA.Disable();
        m_Controls.SelfCalibration.ButtonX.Disable();
    }
    public bool ButtonADown() {
        return m_Controls.SelfCalibration.ButtonA.WasPressedThisFrame();
    }
    public bool ButtonXDown() {
        return m_Controls.SelfCalibration.ButtonX.WasPressedThisFrame();
    }
}
