using UnityEngine;

public class ExampleCustomEvent : MonoBehaviour
{
    public NetworkTriggerExtension trigger;
    
    public void ShowLength(byte[] data) {
        Debug.Log($"You received a custom event with a byte[] of {data.Length} length");
    }

    private void OnGUI() {
        if (GUI.Button(new Rect(10, 50, 100, 50), "One")) {
            Debug.Log("Clicked the button with text One");
            byte[] data = new byte[1];
            trigger.Trigger(data);
        }
        if (GUI.Button(new Rect(10, 150, 100, 50), "Two")) {
            Debug.Log("Clicked the button with text Two");
            byte[] data = new byte[2];
            trigger.Trigger(data);
        }
        if (GUI.Button(new Rect(10, 250, 100, 50), "Three")) {
            Debug.Log("Clicked the button with text Three");
            byte[] data = new byte[3];
            trigger.Trigger(data);
        }
        if (GUI.Button(new Rect(10, 350, 100, 50), "Four")) {
            Debug.Log("Clicked the button with text Four");
            byte[] data = new byte[4];
            trigger.Trigger(data);
        }
    }

}
