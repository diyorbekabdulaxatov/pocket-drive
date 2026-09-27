using System;
using UnityEngine;

namespace PocketDrive
{
    // Parking challenge: drive from the car park entrance into the highlighted bay and stop inside it,
    // nose first, before the clock runs out. Stars depend on time taken and on hard hits.
    public sealed class ParkingChallenge : MonoBehaviour
    {
        [Serializable]
        public struct Bay
        {
            public Vector3 centre;
            public float heading; // degrees; the direction the car must face once parked
        }

        public enum State { FreeDrive, Running, Won, Failed }

        [SerializeField] ArcadeCar car;
        [SerializeField] FollowCamera followCamera;
        [SerializeField] Transform marker;
        [SerializeField] Bay[] bays;
        [SerializeField] Vector3 startPosition;
        [SerializeField] float startHeading = 180f;
        [SerializeField] Vector2 baySize = new(2.7f, 5.5f);
        [SerializeField] float timeLimit = 60f;
        [SerializeField] float threeStarTime = 25f;
        [SerializeField] float twoStarTime = 40f;
        [SerializeField] float headingTolerance = 20f;
        [SerializeField] float settleTime = 1f;

        public State Current { get; private set; } = State.FreeDrive;
        public float Elapsed { get; private set; }
        public int Hits { get; private set; }
        public int Stars { get; private set; }
        public int TargetIndex { get; private set; } = -1;

        float stillTime;
        CarInput input;
        GUIStyle big, small, button;

        void Awake()
        {
            if (car != null) input = car.GetComponent<CarInput>();
            if (marker != null) marker.gameObject.SetActive(false);
        }

        void OnEnable() { if (car != null) car.Hit += OnHit; }
        void OnDisable() { if (car != null) car.Hit -= OnHit; }

        void OnHit(float impulse)
        {
            if (Current == State.Running) Hits++;
        }

        public void Begin(int bayIndex = -1)
        {
            TargetIndex = bayIndex >= 0 ? bayIndex : UnityEngine.Random.Range(0, bays.Length);
            car.PlaceAt(startPosition, Quaternion.Euler(0, startHeading, 0));
            if (followCamera != null) followCamera.Snap();
            Bay bay = bays[TargetIndex];
            if (marker != null)
            {
                marker.SetPositionAndRotation(bay.centre, Quaternion.Euler(0, bay.heading, 0));
                marker.gameObject.SetActive(true);
            }
            Elapsed = 0;
            Hits = 0;
            Stars = 0;
            stillTime = 0;
            Current = State.Running;
            if (input != null) input.enabled = true;
        }

        public void Stop()
        {
            Current = State.FreeDrive;
            if (marker != null) marker.gameObject.SetActive(false);
            if (input != null) input.enabled = true;
        }

        void FixedUpdate()
        {
            if (Current != State.Running) return;
            Elapsed += Time.fixedDeltaTime;
            bool parked = IsInsideTarget() && car.SpeedKph < 1.5f;
            stillTime = parked ? stillTime + Time.fixedDeltaTime : 0;
            if (stillTime >= settleTime) Finish(true);
            else if (Elapsed >= timeLimit) Finish(false);
        }

        void Finish(bool won)
        {
            Current = won ? State.Won : State.Failed;
            Stars = !won ? 0 : Elapsed <= threeStarTime && Hits == 0 ? 3 : Elapsed <= twoStarTime && Hits <= 1 ? 2 : 1;
            if (input != null) input.enabled = false; // also zeroes the car's input
        }

        // All four corners of the car's footprint must be inside the bay, and the car must face the bay's heading.
        public bool IsInsideTarget()
        {
            if (TargetIndex < 0) return false;
            Bay bay = bays[TargetIndex];
            float headingError = Mathf.Abs(Mathf.DeltaAngle(car.transform.eulerAngles.y, bay.heading));
            if (headingError > headingTolerance) return false;
            var box = car.GetComponent<BoxCollider>();
            Vector3 half = box.size * .5f;
            var bayFrame = Quaternion.Euler(0, bay.heading, 0);
            foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
            {
                Vector3 world = car.transform.TransformPoint(box.center + new Vector3(corner.x * half.x, 0, corner.y * half.z));
                Vector3 local = Quaternion.Inverse(bayFrame) * (world - bay.centre);
                if (Mathf.Abs(local.x) > baySize.x * .5f || Mathf.Abs(local.z) > baySize.y * .5f) return false;
            }
            return true;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.P)) Begin();
            if (marker != null && marker.gameObject.activeSelf && marker.childCount > 0)
            {
                // The arrow above the bay bobs so it is easy to spot from the entrance.
                var arrow = marker.GetChild(marker.childCount - 1);
                arrow.localPosition = new Vector3(0, 2.6f + Mathf.Sin(Time.time * 3f) * .25f, 0);
            }
        }

        void OnGUI()
        {
            if (car == null) return;
            float s = Mathf.Max(.7f, Screen.height / 900f);
            big ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            small ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            button ??= new GUIStyle(GUI.skin.button);
            big.fontSize = Mathf.RoundToInt(34 * s);
            small.fontSize = Mathf.RoundToInt(18 * s);
            button.fontSize = Mathf.RoundToInt(20 * s);
            Rect safe = Screen.safeArea;
            float top = Screen.height - safe.yMax + 20 * s, centre = safe.center.x;

            if (Current == State.FreeDrive)
            {
                if (GUI.Button(new Rect(centre - 110 * s, top, 220 * s, 48 * s), "PARKING CHALLENGE", button)) Begin();
                return;
            }

            if (Current == State.Running)
            {
                float left = Mathf.Max(0, timeLimit - Elapsed);
                Panel(new Rect(centre - 170 * s, top, 340 * s, 86 * s));
                GUI.Label(new Rect(centre - 170 * s, top, 340 * s, 48 * s), $"{left:0.0}s", big);
                GUI.Label(new Rect(centre - 170 * s, top + 46 * s, 340 * s, 32 * s), $"Park in the green bay   •   Hits {Hits}", small);
                if (GUI.Button(new Rect(safe.xMax - 145 * s, top + 108 * s, 120 * s, 38 * s), "QUIT", button)) Stop();
                return;
            }

            Rect panel = new(centre - 210 * s, Screen.height * .5f - 130 * s, 420 * s, 260 * s);
            Panel(panel);
            bool won = Current == State.Won;
            GUI.Label(new Rect(panel.x, panel.y + 18 * s, panel.width, 50 * s), won ? "PARKED!" : "TIME'S UP", big);
            GUI.Label(new Rect(panel.x, panel.y + 70 * s, panel.width, 50 * s), won ? $"{Stars} / 3 STARS" : "", big);
            GUI.Label(new Rect(panel.x, panel.y + 124 * s, panel.width, 30 * s), $"Time {Elapsed:0.0}s   •   Hits {Hits}", small);
            if (GUI.Button(new Rect(panel.x + 30 * s, panel.y + 180 * s, 170 * s, 52 * s), "RETRY", button)) Begin(TargetIndex);
            if (GUI.Button(new Rect(panel.xMax - 200 * s, panel.y + 180 * s, 170 * s, 52 * s), "FREE DRIVE", button)) Stop();
        }

        static void Panel(Rect r)
        {
            GUI.color = new Color(.04f, .09f, .12f, .88f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
