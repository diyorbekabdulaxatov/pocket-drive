using UnityEngine;

namespace PocketDrive
{
    public sealed class PrototypeHud : MonoBehaviour
    {
        public ArcadeCar car;
        public string title = "POCKET DRIVE  /  SANDBOX";
        GUIStyle titleStyle;
        GUIStyle button;
        void OnGUI()
        {
            if (car == null) return;
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            button ??= new GUIStyle(GUI.skin.button) { fontSize = 20 };
            Rect safe = Screen.safeArea;
            float top = Screen.height - safe.yMax + 16;
            GUI.Label(new Rect(safe.x + 20, top, 500, 36), title, titleStyle);
            GUI.Label(new Rect(safe.x + 20, top + 38, 500, 30), $"{car.SpeedKph:0} km/h   •   WASD / arrows   •   R to reset");
            if (GUI.Button(new Rect(safe.xMax - 130, top, 110, 44), "RESET", button)) car.ResetCar();
            for (int i = 0; i < CarInput.ButtonLabels.Length; i++)
                GUI.Box(CarInput.ButtonRect(i), CarInput.ButtonLabels[i], button);
        }
    }
}
