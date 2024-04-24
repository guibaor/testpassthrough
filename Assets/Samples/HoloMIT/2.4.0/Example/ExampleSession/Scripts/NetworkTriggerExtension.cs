using UnityEngine;
using UnityEngine.Events;
using Holo.Core;
using Holo.Pilots.Common;
using Holo.EventManager;

public class NetworkTriggerExtension : NetworkIdBehaviour {
	// Class declaration
	[System.Serializable]
	public class MyEvent : UnityEvent<byte[]> { }

	public class NetworkByteData : BaseMessage {
		public string NetworkBehaviourId;
		public byte[] NetworkBehaviourInfo;
	}

	public bool MasterOnlyTrigger = false;

	public NetworkByteData triggerData;

	public MyEvent OnTrigger;

	public void Awake() {
		// Be carefull with the MessageTypeID. You should register once, what means that to extent more than one trigger you should
		// ask to the developers to add more than one MessageTypeID.
		EventManagerController.Instance.RegisterEventType(MessageTypeID.TID_NetworkCustomTriggerData, typeof(NetworkByteData));

	}
	public void OnEnable() {
		EventManagerController.Instance.Subscribe<NetworkByteData>(OnNetworkTriggerExtension);
	}


	public void OnDisable() {
		EventManagerController.Instance.Unsubscribe<NetworkByteData>(OnNetworkTriggerExtension);
	}

	public virtual void Trigger(byte[] _NetworkInfo) {
		if (MasterOnlyTrigger && !CoreController.Instance.UserIsMaster) {
			return;
		}

		Debug.Log($"[NetworkTrigger] Trigger called on NetworkTrigger with id = {NetworkId} and name = {gameObject.name}.");
		triggerData = new NetworkByteData() {
			NetworkBehaviourId = NetworkId,
			NetworkBehaviourInfo = _NetworkInfo,
		};

		if (!CoreController.Instance.UserIsMaster) {
			EventManagerController.Instance.SendSceneEventToMaster(triggerData);
		} else {
			OnTrigger.Invoke(triggerData.NetworkBehaviourInfo);
			BaseStats.Output("NetworkTrigger", $"name={name}, sessionId={CoreController.Instance.MySession.sessionId}");
			EventManagerController.Instance.SendSceneEventToAll(triggerData);
		}
	}

	void OnNetworkTriggerExtension(NetworkByteData data) {
		if (CoreController.Instance.UserIsMaster ||
		data.SenderId == CoreController.Instance.MySession.sessionMaster) {
			if (NeedsAction(data.NetworkBehaviourId)) {
				BaseStats.Output("NetworkTrigger", $"name={name}, sessionId={CoreController.Instance.MySession.sessionId}");

				OnTrigger.Invoke(data.NetworkBehaviourInfo);

				if (CoreController.Instance.UserIsMaster) {
					EventManagerController.Instance.SendSceneEventToAll(data);
				}
			}
		}
	}
}
