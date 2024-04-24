using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Holo.Core;
using Holo.Pilots.Common;
using Holo.Pilots.Login;
using Holo.UserRepresentation.Voice;
using Holo.SessionManager;
using Holo.Manager.Clock;
using Holo.UserManager;
using Holo.EventManager;
using Holo.MediaManager;

public class AutomaticLoginManager : MonoBehaviour {

    private static AutomaticLoginManager instance;

    public static AutomaticLoginManager Instance { get { return instance; } }

    HoloMITControls m_Controls;
    EventSystem system = null;
    int ntpSyncThreshold = 4; // Warning about sync in experience (in seconds)

    [SerializeField] private string sceneToLoad;
    [SerializeField] private string sessionPreFix = "XRECO_";

    #region GUI Components

    public Font MenuFont = null;

    [HideInInspector] public bool isMaster = false;
    [HideInInspector] public string userID = "";

    public State State { get { return state; } set { state = value; } }

    private State state = State.Login;

    #region Info
    [Header("Info")]
    [SerializeField] private Text userName = null;
    [SerializeField] private GameObject ntpPanel = null;
    [SerializeField] private Text ntpText = null;
    [SerializeField] private Button ntpButton = null;
    #endregion

    #region LoginProperties
    [Header("Login")]
    [SerializeField] private GameObject loginPanel = null;
    [SerializeField] private Button loginButton = null;
    [SerializeField] private Button signupButton = null;
    [SerializeField] private Toggle rememberMeButton = null;
    [SerializeField] private InputField userNameLoginIF = null;
    [SerializeField] private InputField userPasswordLoginIF = null;
    #endregion

    #region RegisterProperties
    [Header("Register")]
    [SerializeField] private GameObject registerPanel = null;
    [SerializeField] private Button registerButton = null;
    [SerializeField] private Button exitRegisterButton = null;
    [SerializeField] private InputField userNameRegisterIF = null;
    [SerializeField] private InputField userEmailRegisterIF = null;
    [SerializeField] private InputField userPasswordRegisterIF = null;
    [SerializeField] private InputField confirmPasswordRegisterIF = null;
    #endregion

    #region SettingsProperties
    [Header("Settings")]
    [SerializeField] private GameObject configPanel = null;
    [SerializeField] private GameObject webcamInfoGO = null;
    [SerializeField] private Dropdown representationTypeConfigDropdown = null;
    [SerializeField] private Dropdown webcamDropdown = null;
    [SerializeField] private Dropdown microphoneDropdown = null;
    [SerializeField] private RectTransform VUMeter = null;
    [SerializeField] private Button calibButton = null;
    [SerializeField] private Button doneConfigButton = null;
    [SerializeField] private SelfRepresentationPreview selfRepresentationPreview = null;
    [SerializeField] private Text selfRepresentationDescription = null;
    #endregion

    #endregion

    #region Unity

    void Awake() {
        if (instance == null) {
            instance = this;
        }
        m_Controls = new HoloMITControls();
        m_Controls.UI.Tab.Enable();
    }

    void Start() {
        system = EventSystem.current;

        // Fill UserData representation dropdown according to UserRepresentationType enum declaration
        UpdateRepresentations();
        UpdateWebcams();
        UpdateMicrophones();

        InitialiseControllerEvents();
        ButtonListeners();
        DropdownListeners();

        if (PlayerPrefs.HasKey("representation"))
            representationTypeConfigDropdown.value = PlayerPrefs.GetInt("representation");

        if (CoreController.Instance.UserIsLogged) { // Comes from another scene
            FillSelfUserData();
            ChangeStateButton(State.Config);
            Debug.Log("Come from another Scene");
        } else { // Enter for first time
            ChangeStateButton(State.Login);
            // Get the NTP Time of Clock Manager to start knowing your difference and be able to Login
            GetNTPTime();
        }
    }

    void Update() {
        if (VUMeter && selfRepresentationPreview)
            VUMeter.sizeDelta = new Vector2(355 * Mathf.Min(1, selfRepresentationPreview.MicrophoneLevel), 20);

        TabShortcut();
    }

    void OnDestroy() {
        m_Controls.UI.Tab.Disable();
        TerminateControllerEvents();
    }

    #endregion

    #region Input Helpers
    void SelectFirstIF() {
        try {
            InputField[] inputFields = FindObjectsOfType<InputField>();
            if (inputFields != null) {
                inputFields[0].OnPointerClick(new PointerEventData(system));  //if it's an input field, also set the text caret
                inputFields[0].caretWidth = 2;
                //system.SetSelectedGameObject(first.gameObject, new BaseEventData(system));
            }
        } catch { }
    }

    void TabShortcut() {
        if (m_Controls.UI.Tab.WasPressedThisFrame()) {
            try {
                Selectable current = system.currentSelectedGameObject.GetComponent<Selectable>();
                if (current != null) {
                    Selectable next = current.FindSelectableOnDown();
                    if (next != null) {
                        InputField inputfield = next.GetComponent<InputField>();
                        if (inputfield != null) {
                            inputfield.OnPointerClick(new PointerEventData(system));  //if it's an input field, also set the text caret
                            inputfield.caretWidth = 2;
                        }

                        system.SetSelectedGameObject(next.gameObject, new BaseEventData(system));
                    } else {
                        // Select the first IF because no more elements exists in the list
                        SelectFirstIF();
                    }
                }
                //else Debug.Log("no selectable object selected in event system");
            } catch { }
        }
    }
    #endregion

    #region GUI Functions
    public void FillSelfUserData() {
        if (CoreController.Instance == null || CoreController.Instance.SelfUser == null)
            return;
        User user = CoreController.Instance.SelfUser;

        // Name
        userName.text = user.userName;
        // Config Info
        UserData userData = user.userData;
        if (PlayerPrefs.HasKey("representation"))
            representationTypeConfigDropdown.value = PlayerPrefs.GetInt("representation");
        //representationTypeConfigDropdown.value = (int)CoreController.Instance.SelfUser.player.playerRepresentationType;
        webcamDropdown.value = 0;
        for (int i = 0; i < webcamDropdown.options.Count; ++i) {
            if (webcamDropdown.options[i].text == userData.webcamName) {
                webcamDropdown.value = i;
                break;
            }
        }
        microphoneDropdown.value = 0;
        for (int i = 0; i < microphoneDropdown.options.Count; ++i) {
            if (microphoneDropdown.options[i].text == userData.microphoneName) {
                microphoneDropdown.value = i;
                break;
            }
        }
    }

    public void PanelChanger() {
        // Panels
        ntpPanel.SetActive(false);
        loginPanel.SetActive(false);
        registerPanel.SetActive(false);
        configPanel.SetActive(false);
        switch (state) {
            case State.Login:
                // Panels
                loginPanel.SetActive(true);
                CheckRememberMe();
                break;
            case State.Register:
                // Panels
                registerPanel.SetActive(true);
                break;
            case State.Config:
                // Panels
                configPanel.SetActive(true);
                // Behaviour
                SelfRepresentationChanger();
                break;
            default:
                break;
        }
        SelectFirstIF();
    }

    public void SelfRepresentationChanger() {
        // Dropdown Logic
        webcamInfoGO.SetActive(false);
        calibButton.gameObject.SetActive(false);
        if ((UserRepresentationType)representationTypeConfigDropdown.value == UserRepresentationType.PC_RS2) {
            calibButton.gameObject.SetActive(true);
        } else if ((UserRepresentationType)representationTypeConfigDropdown.value == UserRepresentationType.PC_KINECT) {
            calibButton.gameObject.SetActive(true);
        } else if ((UserRepresentationType)representationTypeConfigDropdown.value == UserRepresentationType.PC_SYNTH) {
            calibButton.gameObject.SetActive(true);
        } else if ((UserRepresentationType)representationTypeConfigDropdown.value == UserRepresentationType.WEBCAM) {
            webcamInfoGO.SetActive(true);
        } else if ((UserRepresentationType)representationTypeConfigDropdown.value == UserRepresentationType.HOLOCAPTURER) {
            calibButton.gameObject.SetActive(true);
        } else if ((UserRepresentationType)representationTypeConfigDropdown.value == UserRepresentationType.HOLOCAPTURER_DEPTH) {
            calibButton.gameObject.SetActive(true);
        }
        // Preview
        SetUserRepresentationDescription((UserRepresentationType)representationTypeConfigDropdown.value);
        selfRepresentationPreview.ChangeRepresentation((UserRepresentationType)representationTypeConfigDropdown.value,
            webcamDropdown.options[webcamDropdown.value].text);
        selfRepresentationPreview.ChangeMicrophone(microphoneDropdown.options[microphoneDropdown.value].text);
    }

    // Fill a scroll view with a text item
    private void AddTextComponentOnContent(Transform container, string value) {
        GameObject textGO = new GameObject();
        textGO.name = "Text-" + value;
        textGO.transform.SetParent(container);
        Text item = textGO.AddComponent<Text>();
        item.font = MenuFont;
        item.fontSize = 20;
        item.color = Color.white;

        ContentSizeFitter lCsF = textGO.AddComponent<ContentSizeFitter>();
        lCsF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform rectTransform;
        rectTransform = item.GetComponent<RectTransform>();
        rectTransform.localPosition = new Vector3(0, 0, 0);
        rectTransform.sizeDelta = new Vector2(2000, 30);
        rectTransform.localScale = Vector3.one;
        item.horizontalOverflow = HorizontalWrapMode.Wrap;
        item.verticalOverflow = VerticalWrapMode.Overflow;

        item.text = value;
    }

    private void AddPlayerComponentOnContent(Transform container, Player player) {
        GameObject playerGO = new GameObject();
        playerGO.name = "Player-" + player.playerName;
        playerGO.transform.SetParent(container);

        ContentSizeFitter lCsF = playerGO.AddComponent<ContentSizeFitter>();
        lCsF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Placeholder
        Text placeholderText = playerGO.AddComponent<Text>();
        placeholderText.font = MenuFont;
        placeholderText.fontSize = 20;
        placeholderText.color = Color.white;

        RectTransform rectGO;
        rectGO = placeholderText.GetComponent<RectTransform>();
        rectGO.localPosition = new Vector3(0, 0, 0);
        rectGO.sizeDelta = new Vector2(0, 30);
        rectGO.localScale = Vector3.one;
        placeholderText.horizontalOverflow = HorizontalWrapMode.Wrap;
        placeholderText.verticalOverflow = VerticalWrapMode.Overflow;

        placeholderText.text = " ";

        // TEXT
        Text textItem = new GameObject("Text-" + player.playerName).AddComponent<Text>();
        textItem.transform.SetParent(playerGO.transform);
        textItem.font = MenuFont;
        textItem.fontSize = 20;
        textItem.color = Color.white;

        RectTransform rectText;
        rectText = textItem.GetComponent<RectTransform>();
        rectText.anchorMin = new Vector2(0, 0.5f);
        rectText.anchorMax = new Vector2(1, 0.5f);
        rectText.localPosition = new Vector3(40, 0, 0);
        rectText.sizeDelta = new Vector2(0, 30);
        rectText.localScale = Vector3.one;
        textItem.horizontalOverflow = HorizontalWrapMode.Wrap;
        textItem.verticalOverflow = VerticalWrapMode.Overflow;

        textItem.text = player.playerName;

        Image imageItem = new GameObject("Image-" + player.playerName).AddComponent<Image>();
        imageItem.transform.SetParent(playerGO.transform);
        imageItem.type = Image.Type.Simple;
        imageItem.preserveAspect = true;

        RectTransform rectImage;
        rectImage = imageItem.GetComponent<RectTransform>();
        rectImage.anchorMin = new Vector2(0, 0.5f);
        rectImage.anchorMax = new Vector2(0, 0.5f);
        rectImage.localPosition = new Vector3(15, 0, 0);
        rectImage.sizeDelta = new Vector2(30, 30);
        rectImage.localScale = Vector3.one;
        // IMAGE
        switch (player.playerRepresentationType) {
            case UserRepresentationType.NONE:
                imageItem.sprite = Resources.Load<Sprite>("Icons/NoneIcon");
                textItem.text += " - (No Rep)";
                break;
            case UserRepresentationType.WEBCAM:
                imageItem.sprite = Resources.Load<Sprite>("Icons/WebCamIcon");
                textItem.text += " - (2D Video)";
                break;
            case UserRepresentationType.HOLOCAPTURER_DEPTH:
                imageItem.sprite = Resources.Load<Sprite>("Icons/PCIcon");
                textItem.text += " - (i2Cat HoloCapturer RGBD)";
                break;
            case UserRepresentationType.HOLOCAPTURER:
                imageItem.sprite = Resources.Load<Sprite>("Icons/PCIcon");
                textItem.text += " - (i2Cat HoloCapturer RGBPM)";
                break;
            case UserRepresentationType.AVATAR:
                imageItem.sprite = Resources.Load<Sprite>("Icons/AvatarIcon");
                textItem.text += " - (3D Avatar)";
                break;
            case UserRepresentationType.PC_RS2:
            case UserRepresentationType.PC_KINECT:
                imageItem.sprite = Resources.Load<Sprite>("Icons/PCIcon");
                textItem.text += " - (Simple PC)";
                break;
            case UserRepresentationType.PC_SYNTH:
                imageItem.sprite = Resources.Load<Sprite>("Icons/PCIcon");
                textItem.text += " - (Synthetic PC)";
                break;
            case UserRepresentationType.PC_PRERECORDED:
                imageItem.sprite = Resources.Load<Sprite>("Icons/PCIcon");
                textItem.text += " - (Prerecorded PC)";
                break;
            case UserRepresentationType.SPECTATOR:
                imageItem.sprite = Resources.Load<Sprite>("Icons/NoneIcon");
                textItem.text += " - (Spectator)";
                break;
            case UserRepresentationType.CAMERAMAN:
                imageItem.sprite = Resources.Load<Sprite>("Icons/OtherIcon");
                textItem.text += " - (Cameraman)";
                break;
            default:
                break;
        }
    }

    private void RemoveComponentsFromList(Transform container) {
        for (var i = container.childCount - 1; i >= 0; i--) {
            var obj = container.GetChild(i);
            obj.transform.SetParent(null);
            Destroy(obj.gameObject);
        }
    }

    private void UpdateRepresentations() {
        // Fill UserData representation dropdown according to UserRepresentationType enum declaration
        representationTypeConfigDropdown.ClearOptions();
        //dd.AddOptions(new List<string>(Enum.GetNames(typeof(UserRepresentationType))));
        List<string> finalNames = new List<string>();
        foreach (string type in Enum.GetNames(typeof(UserRepresentationType))) {
            string enumName;
            switch (type) {
                case "NONE":
                    enumName = "No Representation";
                    break;
                case "WEBCAM":
                    enumName = "2D Video";
                    break;
                case "HOLOCAPTURER_DEPTH":
                    enumName = "HoloCapturer RGBD";
                    break;
                case "AVATAR":
                    enumName = "3D Avatar";
                    break;
                case "HOLOCAPTURER":
                    enumName = "HoloCapturer RGBPM";
                    break;
                case "PC_RS2":
                    enumName = "Simple PointCloud (RealSense)";
                    break;
                case "PC_KINECT":
                    enumName = "Simple PointCloud (Kinect)";
                    break;
                case "PC_SYNTH":
                    enumName = "Synthetic PointCloud (development option)";
                    break;
                case "PC_PRERECORDED":
                    enumName = "Prerecorded PointCloud (development option)";
                    break;
                case "SPECTATOR":
                    enumName = "Spectator (with voice)";
                    break;
                case "CAMERAMAN":
                    enumName = "Cameraman";
                    break;
                case "VOICE":
                    enumName = null;
                    break;
                default:
                    enumName = type + " Not Defined";
                    break;
            }
            if (!string.IsNullOrEmpty(enumName))
                finalNames.Add(enumName);
        }
        representationTypeConfigDropdown.AddOptions(finalNames);
    }

    private void UpdateWebcams() {
        // Fill UserData representation dropdown according to UserRepresentationType enum declaration
        webcamDropdown.ClearOptions();
        WebCamDevice[] devices = WebCamTexture.devices;
        List<string> webcams = new List<string>();
        webcams.Add("None");
        foreach (WebCamDevice device in devices)
            webcams.Add(device.name);
        webcamDropdown.AddOptions(webcams);
    }

    private void UpdateMicrophones() {
        // Fill UserData representation dropdown according to UserRepresentationType enum declaration
        microphoneDropdown.ClearOptions();
        string[] devices = Microphone.devices;
        List<string> microphones = new List<string>();
        microphones.Add("None");
        foreach (string device in devices)
            microphones.Add(device);
        microphoneDropdown.AddOptions(microphones);
    }

    private void SetUserRepresentationDescription(UserRepresentationType _representationType) {
        // left change the icon 'userRepresentationLobbyImage'
        switch (_representationType) {
            case UserRepresentationType.NONE:
                selfRepresentationDescription.text = "No visual representation, and no audio communication. The user can only listen.";
                break;
            case UserRepresentationType.WEBCAM:
                selfRepresentationDescription.text = "2D video window from your camera, as in typical conferencing services.";
                break;
            case UserRepresentationType.HOLOCAPTURER_DEPTH:
                selfRepresentationDescription.text = "Realistic user representation, using the full i2Cat's HoloCapturing system with 1+ RGB-D cameras, as a RGBD.";
                break;
            case UserRepresentationType.HOLOCAPTURER:
                selfRepresentationDescription.text = "Realistic user representation, using the full i2Cat's HoloCapturing system with 1+ RGB-D cameras, as a RGBPM.";
                break;
            case UserRepresentationType.AVATAR:
                selfRepresentationDescription.text = "3D Synthetic Avatar.";
                break;
            case UserRepresentationType.PC_RS2:
                selfRepresentationDescription.text = "Realistic user representation, using a single RealSense RGB-D camera, as a PointCloud.";
                break;
            case UserRepresentationType.PC_KINECT:
                selfRepresentationDescription.text = "Realistic user representation, using a single Azure Kinect RGB-D camera, as a PointCloud.";
                break;
            case UserRepresentationType.PC_SYNTH:
                selfRepresentationDescription.text = "3D Synthetic PointCloud.";
                break;
            case UserRepresentationType.PC_PRERECORDED:
                selfRepresentationDescription.text = "3D Pre-recorded PointCloud.";
                break;
            case UserRepresentationType.SPECTATOR:
                selfRepresentationDescription.text = "No visual representation, but audio communication.";
                break;
            case UserRepresentationType.CAMERAMAN:
                selfRepresentationDescription.text = "Local video recorder.";
                break;
            default:
                break;
        }
    }
    #endregion

    #region Button Logics
    public void ButtonListeners() {
        ntpButton.onClick.AddListener(delegate { PanelChanger(); });

        loginButton.onClick.AddListener(delegate { Login(); });
        signupButton.onClick.AddListener(delegate { ChangeStateButton(State.Register); });

        registerButton.onClick.AddListener(delegate { RegisterButton(); });
        exitRegisterButton.onClick.AddListener(delegate { ChangeStateButton(State.Login); });

        calibButton.onClick.AddListener(delegate { CalibrationButton(); });
        doneConfigButton.onClick.AddListener(delegate { SaveConfigButton(); });
    }

    public void DropdownListeners() {
        representationTypeConfigDropdown.onValueChanged.AddListener(delegate { PanelChanger(); });
        webcamDropdown.onValueChanged.AddListener(delegate { PanelChanger(); });
        microphoneDropdown.onValueChanged.AddListener(delegate {
            selfRepresentationPreview.ChangeMicrophone(microphoneDropdown.options[microphoneDropdown.value].text);
        });
    }

    public void ChangeStateButton(State _state) {
        state = _state;
        PanelChanger();
    }

    public void RegisterButton() {
        if (userPasswordRegisterIF.text == confirmPasswordRegisterIF.text) {
            Register();
            confirmPasswordRegisterIF.textComponent.color = Color.white;
        } else {
            confirmPasswordRegisterIF.textComponent.color = Color.red;
        }
    }

    public void CalibrationButton() {
        // Load Calibration Stuff
        ChangeStateButton(State.Calibration);
    }

    public void SaveConfigButton() {
        selfRepresentationPreview.Stop();
        selfRepresentationPreview.StopMicrophone();

        UserData userData = new UserData {
            userRepresentationType = (UserRepresentationType)representationTypeConfigDropdown.value,
            webcamName = (webcamDropdown.options.Count <= 0) ? "None" : webcamDropdown.options[webcamDropdown.value].text,
            microphoneName = (microphoneDropdown.options.Count <= 0) ? "None" : microphoneDropdown.options[microphoneDropdown.value].text
        };
        CoreController.Instance.SelfUser.userData = userData;

        Config.Instance.debugMetrics = Config.Metric.File;

        Player tempPlayer = new Player {
            playerName = CoreController.Instance.SelfUser.userName,
            playerRepresentationType = (UserRepresentationType)representationTypeConfigDropdown.value
        };
        CoreController.Instance.SelfUser.player = tempPlayer;

        PlayerPrefs.SetInt("representation", (int)CoreController.Instance.SelfUser.player.playerRepresentationType);

        ChangeStateButton(State.Play);

        GetSessions();
    }

    public IEnumerator LoadSceneWaitingTime() {
        yield return new WaitForSeconds(1.0f);
        SceneManager.LoadSceneAsync(sceneToLoad);
    }
    #endregion

    #region Event Listeners
    // Subscribe to ManagersControllers Events
    private void InitialiseControllerEvents() {
        // Clock Manager
        ClockManagerController.Instance.OnGetNTPTimeEvent += OnGetNTPTimeResponse;
        ClockManagerController.Instance.OnGetSyncTimeEvent += OnGetSyncTimeResponse;
        // User Manager
        UserManagerController.Instance.OnCreateUserEvent += OnRegister;
        UserManagerController.Instance.OnLoginEvent += OnLogin;
        UserManagerController.Instance.OnLogoutEvent += OnLogout;
        // Session Manager
        SessionManagerController.Instance.OnGetSessionsEvent += OnGetSessionsHandler;
        SessionManagerController.Instance.OnCreateSessionEvent += OnCreateSessionHandler;
        SessionManagerController.Instance.OnJoinSessionEvent += OnJoinSessionHandler;
        SessionManagerController.Instance.OnGetSessionInfoEvent += OnGetSessionInfoHandler;
        SessionManagerController.Instance.OnPlayerJoinedSessionEvent += OnPlayerJoinedSessionHandler;
        SessionManagerController.Instance.OnPlayerLeavedSessionEvent += OnPlayerLeavedSessionHandler;
    }

    // Un-Subscribe to ManagersControllers Events
    private void TerminateControllerEvents() {
        // Clock Manager
        ClockManagerController.Instance.OnGetNTPTimeEvent -= OnGetNTPTimeResponse;
        ClockManagerController.Instance.OnGetSyncTimeEvent -= OnGetSyncTimeResponse;
        // User Manager
        UserManagerController.Instance.OnCreateUserEvent -= OnRegister;
        UserManagerController.Instance.OnLoginEvent -= OnLogin;
        UserManagerController.Instance.OnLogoutEvent -= OnLogout;
        // Session Manager
        SessionManagerController.Instance.OnGetSessionsEvent -= OnGetSessionsHandler;
        SessionManagerController.Instance.OnCreateSessionEvent -= OnCreateSessionHandler;
        SessionManagerController.Instance.OnJoinSessionEvent -= OnJoinSessionHandler;
        SessionManagerController.Instance.OnGetSessionInfoEvent -= OnGetSessionInfoHandler;
        SessionManagerController.Instance.OnPlayerJoinedSessionEvent -= OnPlayerJoinedSessionHandler;
        SessionManagerController.Instance.OnPlayerLeavedSessionEvent -= OnPlayerLeavedSessionHandler;
    }
    #endregion

    #region Commands

    #region ClockManager
    private void GetNTPTime() {
        ClockManagerController.Instance.GetNTPTime();
    }

    private void OnGetNTPTimeResponse(long ntpMillis) {
        int difference = (int)((ClockManagerController.Instance.GetNsNow() / 1000000) - ntpMillis) / 1000; //in seconds
                                                                                                           //if (difference >= ntpSyncThreshold || difference <= -ntpSyncThreshold) {
                                                                                                           //    ntpText.text = "You have a desynchronization of " + difference + " sec with the ClockManager.\nYou may suffer some problems as a result.";
                                                                                                           //    ntpPanel.SetActive(true);
                                                                                                           //    loginPanel.SetActive(false);
                                                                                                           //}
        Debug.Log($"[LoginManager][OnGetNTPTimeResponse] Difference: {difference}.");
    }

    private void GetSyncTime() {
        ClockManagerController.Instance.GetSyncTime();
    }

    private void OnGetSyncTimeResponse(long ntpMillis) {
        int difference = (int)((ClockManagerController.Instance.GetNsNow() / 1000000) - ntpMillis) / 1000; //in seconds
        if (difference >= ntpSyncThreshold || difference <= -ntpSyncThreshold) {
            ntpText.text = "You have a desynchronization of " + difference + " sec with the ClockManager.\nYou may suffer some problems as a result.";
            ntpPanel.SetActive(true);
            loginPanel.SetActive(false);
        }
        Debug.Log($"[LoginManager][OnGetSyncTimeResponse] Difference: {difference}.");
    }
    #endregion

    #region UserManager
    private void Register() {
        Debug.Log($"[LoginManager][Register] Send CreateUser request registration for user {userNameRegisterIF.text}.");
        UserManagerController.Instance.CreateUser(userNameRegisterIF.text, userEmailRegisterIF.text, userPasswordRegisterIF.text);
    }

    private void OnRegister() {
        Debug.Log($"[LoginManager][OnRegister] User {userNameRegisterIF.text} successfully registered.");
        userNameLoginIF.text = userNameRegisterIF.text;
        userPasswordLoginIF.text = userPasswordRegisterIF.text;
        ChangeStateButton(State.Login);
    }

    private void Login() {
        if (rememberMeButton.isOn) {
            PlayerPrefs.SetString("userNameLoginIF", userNameLoginIF.text);
            PlayerPrefs.SetString("userPasswordLoginIF", userPasswordLoginIF.text);
        } else {
            PlayerPrefs.DeleteKey("userNameLoginIF");
            PlayerPrefs.DeleteKey("userPasswordLoginIF");
        }
        Debug.Log($"[LoginManager][Login] Send Login request for user {userNameLoginIF.text}.");
        UserManagerController.Instance.Login(userNameLoginIF.text, userPasswordLoginIF.text);
    }

    private void CheckRememberMe() {
        if (PlayerPrefs.HasKey("userNameLoginIF") && PlayerPrefs.HasKey("userPasswordLoginIF")) {
            rememberMeButton.isOn = true;
            userNameLoginIF.text = PlayerPrefs.GetString("userNameLoginIF");
            userPasswordLoginIF.text = PlayerPrefs.GetString("userPasswordLoginIF");
        } else
            rememberMeButton.isOn = false;
    }

    private void OnLogin(User user) {
        Debug.Log($"[LoginManager][OnLogin] User {user.userName} with ID {user.userId} successfully logged in.");
        userName.text = user.userName;

        ChangeStateButton(State.Config);
    }

    private void Logout() {
        selfRepresentationPreview.Stop();
        selfRepresentationPreview.StopMicrophone();
        UserManagerController.Instance.Logout(CoreController.Instance.SelfUser.userId);
    }

    private void OnLogout() {
        Debug.Log($"[LoginManager][OnLogout] User {userName.text} successfully logged out.");
        userName.text = "";

        ChangeStateButton(State.Login);
    }
    #endregion

    #region SessionManager
    private void GetSessions() {
        SessionManagerController.Instance.GetSessions();
    }

    private void OnGetSessionsHandler(Session[] sessions) {
        Debug.Log($"[AutomaticLoginManager]OnGetSessionsHandler {sessions.Length}");
        if (sessions != null && sessions.Length > 0) {
            for (int i = 0; i < sessions.Length; ++i) {
                if (sessions[i].sessionName.StartsWith(sessionPreFix)) {
                    Debug.Log($"[NetController]JoinSession {sessions[i].sessionName}");
                    SessionManagerController.Instance.JoinSession(uint.Parse(sessions[i].sessionId), 
                        CoreController.Instance.SelfUser.userName, 
                        CoreController.Instance.SelfUser.player.playerRepresentationType);
                    return;
                }
            }
        }
        // Don't need ScenarioId because we're going to force the scenario to load with "sceneToLoad" field
        Architecture architecture = Config.Instance.MCUSetup.useMCU ? Architecture.MCU : Architecture.SFU;
        SessionManagerController.Instance.CreateSession($"{sessionPreFix}{UnityEngine.Random.Range(0, 1000)}",0 , architecture); // Don't need ScenarioId because we're going to force the scenario to load with "sceneToLoad" field

    }

    private void OnCreateSessionHandler() {
        Debug.Log($"[AutomaticLoginManager][OnCreateSessionHandler] Session created: {CoreController.Instance.MySession.sessionName} // {CoreController.Instance.MySession.sessionId}.");
        // Update the info in LobbyPanel
        CoreController.Instance.UserIsMaster = true;
        isMaster = CoreController.Instance.UserIsMaster;

        // Join to your own session
        SessionManagerController.Instance.JoinSession(uint.Parse(CoreController.Instance.MySession.sessionId),
                CoreController.Instance.SelfUser.player.playerName,
                CoreController.Instance.SelfUser.player.playerRepresentationType);
    }

    private void OnJoinSessionHandler() {
        Debug.Log($"[AutomaticLoginManager][OnJoinSessionHandler] Joined to session: {CoreController.Instance.MySession.sessionName} // {CoreController.Instance.MySession.sessionId}.");
        // Ask for all the info of the session
        SessionManagerController.Instance.GetSessionInfo(uint.Parse(CoreController.Instance.MySession.sessionId));
    }

    private void OnGetSessionInfoHandler() {
        Debug.Log($"[AutomaticLoginManager][OnGetSessionInfoHandler] Retrieved info: PlayerID {CoreController.Instance.SelfUser.player.playerId} " +
            $"// Type {CoreController.Instance.MySession.sessionType} " +
            $"// Arch {CoreController.Instance.MySession.architecture} " +
            $"// Players {CoreController.Instance.MySession.players.Length}.");

        // Set Master in Session & Create room on EventManager
        if (CoreController.Instance.UserIsMaster || CoreController.Instance.MySession.players.Length == 1) {
            CoreController.Instance.UserIsMaster = true;
            CoreController.Instance.MySession.sessionMaster = CoreController.Instance.SelfUser.userId;
            EventManagerController.Instance.CreateRoom(CoreController.Instance.MySession.sessionId);
            MediaManagerController.Instance.CreateRoom(CoreController.Instance.MySession.sessionId);
        }

        if (CoreController.Instance.UserIsMaster && CoreController.Instance.MySession.sessionType != SessionType.Running) {
            SessionManagerController.Instance.InitSession(uint.Parse(CoreController.Instance.MySession.sessionId));
        }

        Debug.Log($"Master: {CoreController.Instance.UserIsMaster} // Users: {CoreController.Instance.MySession.players.Length}");

        StartCoroutine(LoadSceneWaitingTime());
    }

    private void OnPlayerJoinedSessionHandler(Player player) {
    }

    private void OnPlayerLeavedSessionHandler(uint playerId) {
    }
    #endregion

    #endregion
}
