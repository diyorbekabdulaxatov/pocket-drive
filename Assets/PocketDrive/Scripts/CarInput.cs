using UnityEngine;

namespace PocketDrive
{
    // Reads keyboard, touch and mouse, and hands plain values to the car.
    // Swap this component out when moving to the Input System package.
    [RequireComponent(typeof(ArcadeCar))]
    public sealed class CarInput : MonoBehaviour
    {
        public const int Left = 0, Right = 1, Brake = 2, Drive = 3;
        public static readonly string[] ButtonLabels = { "LEFT", "RIGHT", "BRAKE", "DRIVE" };

        ArcadeCar car;

        // On-screen button layout in GUI coordinates (origin top-left), inside the device safe area.
        public static Rect ButtonRect(int index)
        {
            Rect safe = Screen.safeArea;
            float size = Mathf.Min(110f, safe.width / 7f);
            const float margin = 16f, gap = 12f;
            float bottom = Screen.height - safe.y - size - margin;
            float x = index < 2
                ? safe.x + margin + index * (size + gap)
                : safe.xMax - margin - (4 - index) * size - (3 - index) * gap;
            return new Rect(x, bottom, size, size);
        }

        static int ButtonAt(Vector2 guiPoint)
        {
            for (int i = 0; i < ButtonLabels.Length; i++)
                if (ButtonRect(i).Contains(guiPoint)) return i;
            return -1;
        }

        void Awake() => car = GetComponent<ArcadeCar>();

        void Update()
        {
            float steer = Axis(KeyCode.D, KeyCode.RightArrow) - Axis(KeyCode.A, KeyCode.LeftArrow);
            float throttle = Axis(KeyCode.W, KeyCode.UpArrow) - Axis(KeyCode.S, KeyCode.DownArrow);

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;
                ApplyButton(ToGui(touch.position), ref steer, ref throttle);
            }
            if (Input.touchCount == 0 && Input.GetMouseButton(0))
                ApplyButton(ToGui(Input.mousePosition), ref steer, ref throttle);

            car.SetInput(throttle, steer);
            if (Input.GetKeyDown(KeyCode.R)) car.ResetCar();
        }

        static float Axis(KeyCode a, KeyCode b) => Input.GetKey(a) || Input.GetKey(b) ? 1 : 0;

        static Vector2 ToGui(Vector2 screen) => new(screen.x, Screen.height - screen.y);

        static void ApplyButton(Vector2 point, ref float steer, ref float throttle)
        {
            switch (ButtonAt(point))
            {
                case Left: steer -= 1; break;
                case Right: steer += 1; break;
                case Brake: throttle -= 1; break;
                case Drive: throttle += 1; break;
            }
        }

        void OnDisable()
        {
            if (car != null) car.SetInput(0, 0);
        }
    }
}
