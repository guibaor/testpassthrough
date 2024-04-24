using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Holo.Core;
using Holo.EventManager;
using Holo.MediaManager;
using Holo.SessionManager;

public class PilotManager : MonoBehaviour
{
    public static PilotManager Instance { get; private set; }

    public GameObject pilotController;
    public GameObject rtcController;
    public GameObject mcuPipeline;

    #region GUI components
    [SerializeField] private Button exitButton = null;
    #endregion

    #region Unity
    private void Awake() {
        if (Instance == null) {
            Instance = this;
        }

        EventManagerController.Instance.ConnectNamespace(CoreController.Instance.MySession.sessionId);
        MediaManagerController.Instance.ConnectNamespace(CoreController.Instance.MySession.sessionId);
    }

    // Start is called before the first frame update
    void Start() {
        // Buttons listeners
        if (exitButton != null)
            exitButton.onClick.AddListener(delegate { LeaveButton(); });

        InitialiseControllerEvents();

        bool shouldWait = CoreController.Instance.MySession.architecture == Architecture.MCU ? true : false;
        StartCoroutine(InitAll(shouldWait));
    }

    private void OnDestroy() {
        TerminateControllerEvents();
    }
    #endregion

    #region Buttons
    public void LeaveButton() {
        LeaveSession();
    }
    #endregion

    #region Event Listeners
    // Subscribe to ManagersControllers Events
    private void InitialiseControllerEvents() {
        // Session Manager
        SessionManagerController.Instance.OnLeavePlayerEvent += OnLeavePlayerHandler;
        SessionManagerController.Instance.OnLeaveSessionEvent += OnLeaveSessionHandler;
        SessionManagerController.Instance.OnGetSessionInfoEvent += OnGetSessionInfoHandler;
        SessionManagerController.Instance.OnGlobalInitSessionEvent += OnGlobalInitSessionHandler;
        SessionManagerController.Instance.OnPlayerJoinedSessionEvent += OnPlayerJoinedSessionHandler;
        SessionManagerController.Instance.OnPlayerLeavedSessionEvent += OnPlayerLeavedSessionHandler;

        EventManagerController.Instance.RegisterMessageForwarder();
    }

    // Un-Subscribe to ManagersControllers Events
    private void TerminateControllerEvents() {
        // Session Manager
        SessionManagerController.Instance.OnLeavePlayerEvent -= OnLeavePlayerHandler;
        SessionManagerController.Instance.OnLeaveSessionEvent -= OnLeaveSessionHandler;
        SessionManagerController.Instance.OnGetSessionInfoEvent -= OnGetSessionInfoHandler;
        SessionManagerController.Instance.OnGlobalInitSessionEvent -= OnGlobalInitSessionHandler;
        SessionManagerController.Instance.OnPlayerJoinedSessionEvent -= OnPlayerJoinedSessionHandler;
        SessionManagerController.Instance.OnPlayerLeavedSessionEvent -= OnPlayerLeavedSessionHandler;

        EventManagerController.Instance.UnregisterMessageForwarder();
    }
    #endregion

    #region Commands

    #region Sessions

    private void LeavePlayer() {
        SessionManagerController.Instance.LeavePlayer(CoreController.Instance.SelfUser.player.playerId, CoreController.Instance.SelfUser.player.playerId);
    }

    private void OnLeavePlayerHandler() {
        Debug.Log($"[PilotManager][OnLeavePlayerHandler] Not Implemented.");
    }

    private void LeaveSession() {
        SessionManagerController.Instance.LeaveSession(uint.Parse(CoreController.Instance.MySession.sessionId), CoreController.Instance.SelfUser.player.playerId);
    }

    private void OnLeaveSessionHandler() {
        Debug.Log($"[PilotManager][OnLeaveSessionHandler] Session Leaved.");
        EventManagerController.Instance.DisconnectNamespace();
        MediaManagerController.Instance.DisconnectNamespace();
        SceneManager.LoadScene("Menu");
    }

    private void OnGetSessionInfoHandler() {
        Debug.Log($"[PilotManager][OnGetSessionInfoHandler] Not Implemented.");
    }

    private void OnGlobalInitSessionHandler() {
        Debug.Log($"[PilotManager][OnGlobalInitSessionHandler] Not Implemented.");
    }

    private void OnPlayerJoinedSessionHandler(Player player) {
        if (player != null) {
            Debug.Log($"[PilotManager][OnPlayerJoinedSessionHandler] Player joined: " + player.playerName);
        }
    }

    private void OnPlayerLeavedSessionHandler(uint userID) {
        Debug.Log($"[PilotManager][OnPlayerLeavedSessionHandler] Player left: " + userID);
    }

    #endregion

    #endregion

    IEnumerator InitAll(bool shouldWait) {
        if (shouldWait) {
            yield return new WaitForSeconds(6.0f);
            rtcController.SetActive(true);
            yield return new WaitForSeconds(2.0f);
            pilotController.SetActive(true);
            mcuPipeline.SetActive(true);
        } else {
            rtcController.SetActive(true);
            pilotController.SetActive(true);
            mcuPipeline.SetActive(true);
        }
    }

#if UNITY_STANDALONE_WIN
    void OnGUI() {
        if (GUI.Button(new Rect(Screen.width / 2, 5, 70, 20), "Open Log")) {
            var log_path = System.IO.Path.Combine(System.IO.Directory.GetParent(Environment.GetEnvironmentVariable("AppData")).ToString(), "LocalLow", Application.companyName, Application.productName, "Player.log");
            Debug.Log(log_path);
            Application.OpenURL(log_path);
        }
    }
#endif
}
