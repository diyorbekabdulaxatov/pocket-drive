using UnityEngine;

namespace PocketDrive
{
    public sealed class CoastalCityHud : MonoBehaviour
    {
        public ArcadeCar car;
        GUIStyle heading, detail, button, speed;
        Texture2D map;
        bool showMap = true;
        void Awake()
        {
            map = new Texture2D(160, 160);
            var pixels = new Color[160 * 160];
            for (int y = 0; y < 160; y++) for (int x = 0; x < 160; x++)
            {
                float wx = (x / 159f - .5f) * 1000, wz = (y / 159f - .5f) * 1000;
                bool street = false;
                foreach (float lane in new[] { -300f, -150f, 0f, 150f, 300f })
                    street |= Mathf.Abs(wx - lane) < 12 && Mathf.Abs(wz) < 340 || Mathf.Abs(wz - lane) < 12 && Mathf.Abs(wx) < 340;
                bool highway = Mathf.Abs(Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz)) - 430) < 18;
                pixels[y * 160 + x] = highway ? new Color(.72f, .62f, .38f) : street ? new Color(.45f, .51f, .52f) : new Color(.08f, .15f, .18f);
            }
            map.SetPixels(pixels); map.Apply();
        }
        void OnDestroy() { if (map != null) Destroy(map); }
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.M)) showMap = !showMap;
            if (car != null && (Mathf.Abs(car.transform.position.x) > 650 || Mathf.Abs(car.transform.position.z) > 650)) car.ResetCar();
        }
        void OnGUI()
        {
            if (car == null) return;
            float s = Mathf.Max(.7f, Screen.height / 900f);
            heading ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            detail ??= new GUIStyle(GUI.skin.label);
            speed ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            button ??= new GUIStyle(GUI.skin.button);
            heading.fontSize = Mathf.RoundToInt(25*s); detail.fontSize = Mathf.RoundToInt(14*s);
            speed.fontSize = Mathf.RoundToInt(38*s); button.fontSize = Mathf.RoundToInt(18*s);
            Rect safe = Screen.safeArea; float left = safe.x + 22*s, top = Screen.height-safe.yMax + 20*s;
            GUI.color = new Color(.04f,.09f,.12f,.86f); GUI.DrawTexture(new Rect(left-10*s,top-6*s,330*s,84*s),Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(left,top,320*s,40*s),"PACIFIC COAST",heading);
            GUI.Label(new Rect(left,top+36*s,320*s,28*s),"POCKET DRIVE   /   LOS ANGELES INSPIRED",detail);
            GUI.Label(new Rect(safe.xMax-200*s,top,175*s,60*s),$"{car.SpeedKph:0} km/h",speed);
            if (GUI.Button(new Rect(safe.xMax-145*s,top+60*s,120*s,38*s),"RESET",button)) car.ResetCar();
            if (GUI.Button(new Rect(left,top+92*s,95*s,30*s),"MAP",button)) showMap = !showMap;
            if (showMap && map != null)
            {
                Rect r = new Rect(left,top+134*s,160*s,160*s); GUI.DrawTexture(r,map);
                Vector3 p = car.transform.position;
                float px = r.x+(p.x/1000f+.5f)*r.width, py = r.y+(.5f-p.z/1000f)*r.height;
                GUI.color = new Color(1,.55f,.2f); GUI.DrawTexture(new Rect(px-4*s,py-4*s,8*s,8*s),Texture2D.whiteTexture); GUI.color = Color.white;
            }
            for (int i=0;i<CarInput.ButtonLabels.Length;i++) GUI.Box(CarInput.ButtonRect(i),CarInput.ButtonLabels[i],button);
        }
    }
}
