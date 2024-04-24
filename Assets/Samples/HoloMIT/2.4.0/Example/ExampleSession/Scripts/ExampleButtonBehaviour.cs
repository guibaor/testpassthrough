using UnityEngine;
using Holo.Pilots.Common;

public class ExampleButtonBehaviour : MonoBehaviour {
	public NetworkTrigger ExampleButtonTrigger;

	public float TimeOutBetweenTriggers = 1f;
	private float _ButtonLastTriggered;

	private void OnTriggerEnter(Collider other) {
		if (Time.realtimeSinceStartup - _ButtonLastTriggered > TimeOutBetweenTriggers) {
			string layer = LayerMask.LayerToName(other.gameObject.layer);
			if (layer != "TouchCollider") {
				return;
			}

			Debug.Log($"[Pilot0ButtonBehaviour] Triggered by {other.name} on layer {other.gameObject.layer}");

			ExampleButtonTrigger.Trigger();

			_ButtonLastTriggered = Time.realtimeSinceStartup;
		}
	}
}