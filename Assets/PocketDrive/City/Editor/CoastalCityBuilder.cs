using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PocketDrive.Editor
{
    // Original, deterministic geometry. Rebuild only replaces the generated CoastalCity scene/assets.
    public static class CoastalCityBuilder
    {
        public const string ScenePath = "Assets/PocketDrive/Scenes/CoastalCity.unity";
        const string Root = "Assets/PocketDrive/City/Generated";
        static readonly Dictionary<string, Batch> batches = new();
        static readonly Dictionary<string, Material> mats = new();
        static System.Random random;
        static Transform colliders;
        static int palmCount, buildingCount;
        static float R(float a,float b) => a+(float)random.NextDouble()*(b-a);
        static Vector3 V(float x,float y,float z) => new(x,y,z);

        sealed class Batch
        {
            public readonly List<Vector3> vertices = new();
            public readonly List<Vector2> uv = new();
            public readonly List<int> triangles = new();
            public string material;
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float u=1,float v=1)
            {
                int n=vertices.Count; vertices.AddRange(new[]{a,b,c,d});
                float tile=RealisticLook.TileSize(material);
                uv.AddRange(tile>0?RealisticLook.QuadUVs(a,b,c,d,tile):new[]{new Vector2(0,0),new Vector2(u,0),new Vector2(u,v),new Vector2(0,v)});
                triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
        }
        static Batch Get(string material,Vector3 p)
        {
            string key=$"{material}_{Mathf.FloorToInt(p.x/120)}_{Mathf.FloorToInt(p.z/120)}";
            if(!batches.TryGetValue(key,out var batch)) batches[key]=batch=new Batch{material=material};
            return batch;
        }
        static void Box(string mat,Vector3 p,Vector3 size,bool solid=false,float tile=4)
        {
            var b=Get(mat,p); var h=size*.5f;
            Vector3 P(float x,float y,float z)=>p+V(x*h.x,y*h.y,z*h.z);
            b.Quad(P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1),size.y/tile,size.x/tile);
            b.Quad(P(1,-1,1),P(1,1,1),P(-1,1,1),P(-1,-1,1),size.y/tile,size.x/tile);
            b.Quad(P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1),size.y/tile,size.z/tile);
            b.Quad(P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1),size.y/tile,size.z/tile);
            b.Quad(P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1),size.x/tile,size.z/tile);
            b.Quad(P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1),P(1,-1,1),size.x/tile,size.z/tile);
            if(solid){var go=new GameObject(mat+" collision");go.transform.SetParent(colliders);go.transform.position=p;go.AddComponent<BoxCollider>().size=size;}
        }
        static void Cylinder(string mat,Vector3 a,Vector3 b,float ra,float rb,int sides=8)
        {
            Vector3 axis=(b-a).normalized, right=Vector3.Cross(axis,Vector3.forward).normalized;
            if(right.sqrMagnitude<.1f)right=Vector3.right;
            Vector3 front=Vector3.Cross(right,axis).normalized;var batch=Get(mat,a);
            for(int i=0;i<sides;i++)
            {
                float u=i*2*Mathf.PI/sides,w=(i+1)*2*Mathf.PI/sides;
                Vector3 n=right*Mathf.Cos(u)+front*Mathf.Sin(u),m=right*Mathf.Cos(w)+front*Mathf.Sin(w);
                batch.Quad(a+n*ra,b+n*rb,b+m*rb,a+m*ra,1,Vector3.Distance(a,b));
            }
        }
        static void Material(string name,Color color,float smooth=.1f,bool texture=false,bool facade=false)
        {
            string path=$"{Root}/{name}.mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(PocketDrive.Editor.RenderPipelineSetup.Lit);AssetDatabase.CreateAsset(m,path);}
            m.color=color;PocketDrive.Editor.RenderPipelineSetup.SetSmoothness(m,smooth);m.enableInstancing=true;
            if((texture||facade)&&!RealisticLook.HasSurface(name))
            {
                var tex=new Texture2D(128,128,TextureFormat.RGB24,true);var pixels=new Color[128*128];
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                {
                    float grain=R(.9f,1.04f);Color c=new Color(grain,grain,grain);
                    if(facade)
                    {
                        bool window=x>16&&x<106&&y>12&&y<116;
                        c=window?Color.Lerp(new Color(.16f,.28f,.33f),new Color(.58f,.69f,.7f),x/128f)*grain:new Color(.76f,.74f,.67f)*grain;
                        if(window&&(x==60||x==61||y==64))c=new Color(.1f,.15f,.17f);
                        if(window&&y>106)c*=.6f;
                    }
                    pixels[y*128+x]=c;
                }
                tex.SetPixels(pixels);tex.Apply();string tp=$"{Root}/{name}.png";File.WriteAllBytes(tp,tex.EncodeToPNG());Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(tp);var imp=(TextureImporter)AssetImporter.GetAtPath(tp);imp.wrapMode=TextureWrapMode.Repeat;imp.maxTextureSize=128;imp.mipmapEnabled=true;imp.SaveAndReimport();
                m.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            }
            RealisticLook.ApplySurface(m,name);
            EditorUtility.SetDirty(m);mats[name]=m;
        }
        [MenuItem("Pocket Drive/City/Create or Rebuild Coastal City")]
        public static void Generate()
        {
            if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();batches.Clear();mats.Clear();random=new System.Random(1705);palmCount=buildingCount=0;
            // Reuse the existing car and its input/tuning; never regenerate or modify the sandbox.
            EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            var source=Object.FindAnyObjectByType<ArcadeCar>();
            if(source==null)throw new Exception("Sandbox has no player car");
            var carCopy=Object.Instantiate(source.gameObject);// Preserve the cloned car in a temporary additive scene.
            // NewScene destroys scene objects, so preserve the car through a temporary additive scene.
            var temporary=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(carCopy,temporary);
            var old=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ProjectSetup.ScenePath);EditorSceneManager.CloseScene(old,true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(temporary);
            colliders=new GameObject("Collision geometry").transform;
            Material("asphalt",new Color(.22f,.235f,.25f),.12f,true);
            Material("concrete",new Color(.66f,.64f,.58f),.1f,true);
            Material("sand",new Color(.71f,.62f,.44f),.1f,true);
            Material("white",new Color(.87f,.86f,.75f));Material("yellow",new Color(.94f,.66f,.17f));
            Material("stucco",new Color(.87f,.79f,.64f),.1f,true);Material("terracotta",new Color(.55f,.24f,.12f),.05f,true);
            Material("glass",new Color(.23f,.42f,.49f),.7f);
            Material("office",Color.white,.4f,false,true);Material("warmoffice",new Color(.87f,.78f,.63f),.3f,false,true);
            Material("dark",new Color(.075f,.09f,.10f));Material("metal",new Color(.35f,.39f,.4f),.5f);
            Material("trunk",new Color(.34f,.25f,.14f),.1f,true);Material("palm",new Color(.24f,.38f,.12f));Material("palmLight",new Color(.37f,.45f,.15f));
            Material("grass",new Color(.37f,.40f,.19f),.05f,true);Material("water",new Color(.18f,.46f,.53f),.65f);
            Material("mountain",new Color(.50f,.48f,.39f));Material("sign",new Color(.045f,.25f,.17f));
            Material("carpaint",new Color(.63f,.19f,.065f),.7f);
            Material("parking",Color.white,.15f,true);
            Box("sand",V(0,-1,0),V(1250,2,1250),true,80);
            Box("water",V(-1250,-1.1f,0),V(1270,1,2600),false,200);
            Streets();Buildings();Freeway();Dressing();ParkingLot();Mountains();Flush();
            carCopy.name="Player Car";carCopy.transform.position=V(5,.42f,-280);carCopy.transform.rotation=Quaternion.identity;
            var car=carCopy.GetComponent<ArcadeCar>();
            string tuningPath=Root+"/CoastalCarTuning.asset";
            var cityTuning=AssetDatabase.LoadAssetAtPath<CarTuning>(tuningPath);
            if(cityTuning==null){cityTuning=Object.Instantiate(car.Tuning);AssetDatabase.CreateAsset(cityTuning,tuningPath);}
            cityTuning.groundProbe=.35f;EditorUtility.SetDirty(cityTuning);
            var carSettings=new SerializedObject(car);carSettings.FindProperty("tuning").objectReferenceValue=cityTuning;carSettings.ApplyModifiedPropertiesWithoutUndo();
            UpgradeCarVisual(carCopy.transform);
            var camObject=new GameObject("Main Camera");camObject.tag="MainCamera";
            var cam=camObject.AddComponent<Camera>();cam.fieldOfView=62;cam.farClipPlane=1400;cam.nearClipPlane=.15f;cam.allowHDR=true;
            camObject.AddComponent<AudioListener>();var follow=camObject.AddComponent<FollowCamera>();follow.target=carCopy.transform;follow.Snap();
            var so=new SerializedObject(follow);so.FindProperty("height").floatValue=3.4f;so.FindProperty("distance").floatValue=8;so.ApplyModifiedPropertiesWithoutUndo();follow.Snap();
            new GameObject("Coastal City HUD").AddComponent<CoastalCityHud>().car=car;
            CityPopulation.Add(carCopy.transform);
            Lighting();
            EditorSceneManager.SaveScene(temporary,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene(ProjectSetup.ScenePath,true)};
            AssetDatabase.SaveAssets();
            File.WriteAllText("outputs/coastal-city-statistics.txt",$"Original procedural city, seed 1705\nBuildings: {buildingCount}\nPalms: {palmCount}\nMesh chunks: {batches.Count}\nPlayable street grid: 600 x 600 metres\nElevated freeway footprint: 860 x 860 metres\n2 access ramps; highway deck 9m high\n");
            Debug.Log($"COASTAL_CITY_GENERATED buildings={buildingCount}, palms={palmCount}, chunks={batches.Count}");
        }
        static bool Junction(float p)=>Mathf.Abs(p/150-Mathf.Round(p/150))<.115f;
        static void Streets()
        {
            foreach(float k in new[]{-300f,-150f,0f,150f,300f})
            {
                Box("asphalt",V(k,.025f,0),V(27,.05f,675),true);Box("asphalt",V(0,.026f,k),V(675,.052f,27),true);
                for(int p=-330;p<335;p+=8)
                {
                    if(Junction(p))continue;
                    foreach(float offset in new[]{-4.6f,4.6f})
                    {Box("white",V(k+offset,.064f,p),V(.13f,.015f,3));Box("white",V(p,.065f,k+offset),V(3,.015f,.13f));}
                    foreach(float offset in new[]{-.18f,.18f})
                    {Box("yellow",V(k+offset,.065f,p),V(.12f,.015f,8));Box("yellow",V(p,.066f,k+offset),V(8,.015f,.12f));}
                }
                foreach(float j in new[]{-300f,-150f,0f,150f,300f})
                    for(int stripe=-5;stripe<=5;stripe++)foreach(float edge in new[]{-17f,17f})
                    {Box("white",V(k+stripe*2,.075f,j+edge),V(.8f,.015f,3));Box("white",V(k+edge,.075f,j+stripe*2),V(3,.015f,.8f));}
            }
            for(int ix=0;ix<4;ix++)for(int iz=0;iz<4;iz++)
            {
                float x=-225+ix*150,z=-225+iz*150;
                Box("concrete",V(x,.11f,z),V(122,.22f,122),true);
                Box("grass",V(x,.23f,z),V(109,.05f,109));
            }
            // Turnaround connections at the four boulevard ends.
            Box("asphalt",V(0,.025f,345),V(675,.05f,32),true);
            Box("asphalt",V(345,.025f,0),V(32,.05f,720),true);Box("asphalt",V(-345,.025f,0),V(32,.05f,720),true);
        }
        static void Buildings()
        {
            for(int ix=0;ix<4;ix++)for(int iz=0;iz<4;iz++)
            {
                float cx=-225+ix*150,cz=-225+iz*150;
                bool downtown=ix>=1&&ix<=2&&iz>=1;
                for(int a=0;a<2;a++)for(int b=0;b<2;b++)
                {
                    if(ix==2&&iz==2&&a==1&&b==1)continue;
                    float x=cx+(a==0?-31:31),z=cz+(b==0?-31:31);
                    float h=downtown?R(28,100):R(7,19),w=downtown?R(29,43):R(30,44),d=R(29,43);
                    Building(x,z,w,d,h,downtown);
                }
                // Small central paved courtyard and raised planters.
                Box("concrete",V(cx,.26f,cz),V(14,.1f,105));
                for(int t=-1;t<=1;t++)Box("grass",V(cx,.48f,cz+t*35),V(8,.5f,13),true);
            }
            Building(106,106,35,35,150,true); // skyline landmark, stepped crown
            for(int i=0;i<3;i++)Box("glass",V(106,151+i*5,106),V(29-i*7,10,29-i*7),true);
            Cylinder("metal",V(106,160,106),V(106,182,106),.3f,.12f);
        }
        static void Building(float x,float z,float w,float d,float h,bool tower)
        {
            buildingCount++;
            string facade=tower?(random.Next(2)==0?"office":"warmoffice"):"stucco";
            Box(facade,V(x,h/2+.3f,z),V(w,h,d),true,tower?3.5f:5f);
            Box("concrete",V(x,1.5f,z),V(w+1,3,d+1),true);
            // Storefront glazing and canopies face both boulevards.
            foreach(float side in new[]{-1f,1f})
            {
                Box("glass",V(x,2.1f,z+side*(d/2+.08f)),V(w-3,2.6f,.12f));
                Box(tower?"dark":"terracotta",V(x,3.8f,z+side*(d/2+1)),V(w+2,.3f,2.6f));
                if(!tower)
                    for(float floor=5;floor<h-1;floor+=3.2f)for(float window=-w/2+3;window<w/2-2;window+=4)
                    {Box("glass",V(x+window,floor,z+side*(d/2+.06f)),V(2,1.6f,.14f));Box("white",V(x+window,floor-.9f,z+side*(d/2+.2f)),V(2.4f,.16f,.6f));}
            }
            if(tower)
            {
                for(float y=7;y<h;y+=7)Box("concrete",V(x,y,z),V(w+.25f,.28f,d+.25f));
                for(float dx=-w/2;dx<=w/2+.1f;dx+=w/4)Box("metal",V(x+dx,h/2,z-d/2-.08f),V(.23f,h,.22f));
            }
            else Box("terracotta",V(x,h+.35f,z),V(w+1,.7f,d+1));
            Box("concrete",V(x,h+.5f,z),V(w*.6f,1,d*.55f));
            for(int i=0;i<3;i++)Box("metal",V(x-w*.22f+i*w*.2f,h+1.7f,z),V(3,2,3));
        }
        static List<Vector3> Ring()
        {
            var points=new List<Vector3>();
            // Counter-clockwise rounded rectangle, so inward = left of tangent.
            Vector2[] centers={new(360,-360),new(360,360),new(-360,360),new(-360,-360)};
            for(int corner=0;corner<4;corner++)
            {
                for(int i=0;i<=18;i++)
                {float angle=(-90+corner*90+i*5)*Mathf.Deg2Rad;points.Add(V(centers[corner].x+70*Mathf.Cos(angle),9,centers[corner].y+70*Mathf.Sin(angle)));}
                Vector3 end=points[^1];int next=(corner+1)%4;float angle2=(-90+next*90)*Mathf.Deg2Rad;
                Vector3 start=V(centers[next].x+70*Mathf.Cos(angle2),9,centers[next].y+70*Mathf.Sin(angle2));
                int steps=Mathf.CeilToInt(Vector3.Distance(end,start)/8);
                for(int i=1;i<steps;i++)points.Add(Vector3.Lerp(end,start,i/(float)steps));
            }
            points.Add(points[0]);return points;
        }
        static void Freeway()
        {
            var path=Ring();RoadRibbon("Pacific Freeway",path,31,true);
            for(int side=-1;side<=1;side+=2)
            {
                var ramp=new List<Vector3>();
                Vector3 a=V(side*310,9,-430),b=V(side*240,9,-430),c=V(side*170,.06f,-300),d=V(side*225,.06f,-300);
                for(int i=0;i<=64;i++)ramp.Add(RampPoint(side,i/64f));
                RoadRibbon("Coast access ramp "+side,ramp,10,false);
            }
            for(int x=-320;x<=320;x+=80)
            {
                foreach(int z in new[]{-430,430})Box("concrete",V(x,4.2f,z),V(4,8.4f,9),true);
                foreach(int z in new[]{-430,430})Box("concrete",V(z,4.2f,x),V(9,8.4f,4),true);
            }
            Sign(V(0,14,-430),"PACIFIC FWY    1\nDOWNTOWN  /  COAST",true);
            Sign(V(220,13,-405),"EXIT  2\nPALM BOULEVARD",true);
        }
        static Vector3 RampPoint(int side,float t)
        {
            float u=1-t;
            Vector3 p=u*u*u*V(side*310,9,-430)+3*u*u*t*V(side*240,9,-430)+3*u*t*t*V(side*170,.06f,-300)+t*t*t*V(side*225,.06f,-300);
            // Stay level until completely clear of the freeway deck, then descend.
            p.y=Mathf.Lerp(9,.06f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,1,t)));
            return p;
        }
        static void RoadRibbon(string name,List<Vector3> path,float width,bool freeway)
        {
            var mesh=new Batch();float distance=0;
            for(int i=0;i<path.Count-1;i++)
            {
                Vector3 a=path[i],b=path[i+1],forward=(b-a).normalized,right=Vector3.Cross(Vector3.up,forward).normalized;
                Vector3 before=i==0?(freeway?path[^2]:a):path[i-1];
                Vector3 after=i+2<path.Count?path[i+2]:(freeway?path[1]:b);
                Vector3 normalA=Vector3.Cross(Vector3.up,b-before).normalized,normalB=Vector3.Cross(Vector3.up,after-a).normalized;
                Vector3 leftA=a-normalA*width/2,leftB=b-normalB*width/2,rightA=a+normalA*width/2,rightB=b+normalB*width/2;
                Get("asphalt",a).Quad(leftA,leftB,rightB,rightA,1,1);
                mesh.Quad(leftA,leftB,rightB,rightA);
                float length=Vector3.Distance(a,b);
                // Outer retaining faces and solid guardrails, except ramp merge openings.
                for(int s=-1;s<=1;s+=2)
                {
                    Vector3 p=a+normalA*(width/2)*s,q=b+normalB*(width/2)*s;
                    Get("concrete",a).Quad(p-Vector3.up*.8f,q-Vector3.up*.8f,q,p,1,1);
                    bool opening=freeway&&a.z<-415&&Mathf.Abs(a.x)>170&&Mathf.Abs(a.x)<335;
                    if(!opening && (freeway || i>4&&i<path.Count-6))
                    {Beam("concrete",p+Vector3.up*.48f,q+Vector3.up*.48f,.4f,.95f,true);}
                }
                foreach(float offset in freeway?new[]{-10.3f,-5.15f,5.15f,10.3f}:new[]{0f})
                    if((int)(distance/5)%2==0)Stripe(a,b,right,offset,"white",.15f);
                if(freeway)foreach(float offset in new[]{-.15f,.15f})Stripe(a,b,right,offset,"yellow",.12f);
                distance+=length;
            }
            var collider=new GameObject(name+" road surface");collider.transform.SetParent(colliders);
            collider.AddComponent<MeshCollider>().sharedMesh=SaveMesh(name.Replace(" ","_"),mesh);
        }
        static void Stripe(Vector3 a,Vector3 b,Vector3 right,float offset,string mat,float w)
        {a+=Vector3.up*.035f+right*offset;b+=Vector3.up*.035f+right*offset;Get(mat,a).Quad(a-right*w/2,b-right*w/2,b+right*w/2,a+right*w/2);}
        static void Beam(string mat,Vector3 a,Vector3 b,float width,float height,bool solid=false)
        {
            Vector3 f=(b-a).normalized,r=Vector3.Cross(Vector3.up,f).normalized*width/2,u=Vector3.up*height/2;var batch=Get(mat,a);
            batch.Quad(a-r-u,a-r+u,b-r+u,b-r-u);batch.Quad(b+r-u,b+r+u,a+r+u,a+r-u);
            batch.Quad(a-r+u,a+r+u,b+r+u,b-r+u);batch.Quad(a+r-u,a-r-u,b-r-u,b+r-u);
            if(solid){var go=new GameObject("Guardrail");go.transform.SetParent(colliders);go.transform.position=(a+b)/2;go.transform.rotation=Quaternion.LookRotation(f);go.AddComponent<BoxCollider>().size=V(width,height,Vector3.Distance(a,b)+.1f);}
        }
        static void Palm(Vector3 origin,float h)
        {
            palmCount++;Vector3 crown=origin+V(R(-1,1),h,R(-1,1));
            for(int i=0;i<8;i++)
            {float t=i/8f,u=(i+1)/8f;Vector3 a=Vector3.Lerp(origin,crown,t),b=Vector3.Lerp(origin,crown,u);Cylinder("trunk",a,b,Mathf.Lerp(.35f,.15f,t),Mathf.Lerp(.35f,.15f,u));}
            for(int leaf=0;leaf<11;leaf++)
            {
                float angle=leaf*2*Mathf.PI/11+R(-.1f,.1f);Vector3 dir=V(Mathf.Cos(angle),0,Mathf.Sin(angle)),side=Vector3.Cross(Vector3.up,dir);
                float length=R(4,6);Vector3 Point(float t)=>crown+dir*(t*length)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*1.8f-t*t*2.1f);
                for(int j=0;j<10;j++)
                {
                    float t=j/10f,u=(j+1)/10f;Vector3 a=Point(t),b=Point(u);float w=Mathf.Sin((t+.05f)*Mathf.PI)*.65f;
                    var batch=Get(leaf%3==0?"palmLight":"palm",origin);
                    batch.Quad(a-side*w,b-side*w*.8f,b+side*w*.8f,a+side*w);
                    batch.Quad(a+side*w,b+side*w*.8f,b-side*w*.8f,a-side*w);
                    foreach(int sign in new[]{-1,1})
                    {Vector3 tip=a+side*sign*(w+.4f)+dir*.5f-Vector3.up*.15f;batch.Quad(a,tip,b,b);batch.Quad(b,tip,a,a);}
                }
            }
            var go=new GameObject("Palm trunk collision");go.transform.SetParent(colliders);go.transform.position=origin+Vector3.up*h/2;var capsule=go.AddComponent<CapsuleCollider>();capsule.radius=.32f;capsule.height=h;
            Box("concrete",origin+V(0,.18f,0),V(2,.36f,2));
        }
        // Surface car park at the south end of the north-south boulevard: four rows of 2.7 x 5.5 m bays.
        static void ParkingLot()
        {
            const float cz=-370,halfWidth=40,halfDepth=25,bay=2.7f,depth=5.5f;
            Box("parking",V(0,.03f,cz),V(halfWidth*2,.06f,halfDepth*2),true);
            Box("asphalt",V(0,.028f,cz+halfDepth+3.75f),V(12,.056f,7.5f),true);
            foreach(int side in new[]{-1,1})
            {
                Box("concrete",V(side*(halfWidth+.15f),.12f,cz),V(.3f,.24f,halfDepth*2+.6f),true);
                Box("concrete",V(side*(halfWidth+6)/2,.12f,cz+halfDepth+.15f),V(halfWidth-6,.24f,.3f),true);
            }
            Box("concrete",V(0,.12f,cz-halfDepth-.15f),V(halfWidth*2+.6f,.24f,.3f),true);
            float[] rows={cz+halfDepth-depth/2,cz+depth/2,cz-depth/2,cz-halfDepth+depth/2};
            foreach(float row in rows)
                for(int i=0;i<=28;i++)
                {
                    float x=-37.8f+i*bay;
                    if(row==rows[0]&&Mathf.Abs(x)<7)continue; // keep the entrance clear
                    Box("white",V(x,.07f,row),V(.12f,.012f,depth));
                }
            Box("white",V(0,.07f,cz),V(75.6f,.012f,.12f));
            foreach(int sx in new[]{-1,1})foreach(float z in new[]{cz+halfDepth-1,cz-halfDepth+1})
            {
                Vector3 p=V(sx*(halfWidth+1),.1f,z);Cylinder("metal",p,p+Vector3.up*8,.12f,.09f);
                Beam("metal",p+Vector3.up*8,p+V(-sx*2.5f,8,0),.12f,.12f);Box("white",p+V(-sx*2.5f,7.9f,0),V(1f,.15f,.45f));
            }
            Sign(V(9,3.2f,cz+halfDepth+6),"PARKING",false,true);
        }
        static void Dressing()
        {
            foreach(float road in new[]{-300f,-150f,0f,150f,300f})
                for(int k=-320;k<=320;k+=30)
                {
                    if(Junction(k))continue;
                    foreach(int side in new[]{-1,1})
                    {
                        Palm(V(road+side*17,.23f,k),R(10,16));
                        if(road==0||road==300)Palm(V(k,.23f,road+side*17),R(10,16));
                    }
                }
            foreach(float x in new[]{-300f,-150f,0f,150f,300f})foreach(float z in new[]{-300f,-150f,0f,150f,300f})
            {
                Vector3 p=V(x-16,.2f,z-16);Cylinder("metal",p,p+Vector3.up*7,.12f,.1f);
                Beam("metal",p+Vector3.up*7,p+V(12,7,0),.16f,.16f);
                Box("dark",p+V(11,6.4f,0),V(.45f,1.5f,.45f));
                Box("palmLight",p+V(11,6, -.24f),V(.2f,.2f,.03f));
            }
            for(int i=-300;i<=300;i+=60)foreach(int side in new[]{-1,1})
            {
                Vector3 p=V(side*15,.2f,i+25);Cylinder("metal",p,p+Vector3.up*8,.10f,.07f);
                Beam("metal",p+Vector3.up*8,p+V(-side*3,8.6f,0),.12f,.12f);Box("white",p+V(-side*3,8.6f,0),V(1.2f,.15f,.5f));
            }
            Sign(V(-18,3.5f,-260),"PALM BOULEVARD",false);
            Sign(V(160,4,-318),"PACIFIC FREEWAY\nACCESS  →",false);
            // Beach promenade and palm rows beyond the freeway.
            Box("concrete",V(-530,.04f,0),V(14,.08f,950),true);
            for(int z=-420;z<=420;z+=35)Palm(V(-518,.08f,z),R(11,16));
        }
        static void Sign(Vector3 p,string label,bool large,bool facesNorth=false)
        {
            float width=large?18:12,height=large?4:2;
            Box("sign",p,V(width,height,.2f));
            foreach(int side in new[]{-1,1})Cylinder("metal",V(p.x+side*width*.45f,0,p.z),V(p.x+side*width*.45f,p.y,p.z),.16f,.16f);
            var text=new GameObject(label.Replace('\n',' '));text.transform.position=p+V(0,0,facesNorth?.13f:-.13f);text.transform.rotation=facesNorth?Quaternion.Euler(0,180,0):Quaternion.identity;
            var tm=text.AddComponent<TextMesh>();tm.text=label;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.fontSize=64;tm.characterSize=large?.26f:.19f;tm.color=new Color(.93f,.93f,.82f);
        }
        static void Mountains()
        {
            Vector3 Point(float x,float z)
            {
                float ridge=Mathf.Max(0,1-Mathf.Abs(z-970)/280f);
                float noise=Mathf.PerlinNoise(x*.004f+10,z*.004f)*.7f+Mathf.PerlinNoise(x*.012f,z*.012f)*.3f;
                return V(x,Mathf.Pow(ridge,.6f)*(50+noise*170)-8,z);
            }
            for(int x=-1400;x<1400;x+=35)for(int z=700;z<1260;z+=35)
            {var b=Get("mountain",V(x,0,z));b.Quad(Point(x,z),Point(x,z+35),Point(x+35,z+35),Point(x+35,z));}
        }

        static Mesh SaveMesh(string name,Batch b)
        {
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(b.vertices);mesh.SetUVs(0,b.uv);mesh.SetTriangles(b.triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=$"{Root}/{name}.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old!=null){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);return old;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static void Flush()
        {
            var parent=new GameObject("City geometry • 120m chunks");
            foreach(var pair in batches)
            {
                var go=new GameObject(pair.Key);go.transform.SetParent(parent.transform);go.isStatic=true;
                go.AddComponent<MeshFilter>().sharedMesh=SaveMesh(pair.Key,pair.Value);
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mats[pair.Value.material];
                renderer.shadowCastingMode=pair.Value.material=="water"?ShadowCastingMode.Off:ShadowCastingMode.On;
            }
        }
        static void UpgradeCarVisual(Transform parent)
        {
            if(RealisticLook.AttachPorsche(parent))return;
            // Only this scene's cloned visual children are replaced.
            for(int i=parent.childCount-1;i>=0;i--)Object.DestroyImmediate(parent.GetChild(i).gameObject);
            GameObject Part(string name,Vector3 p,Vector3 scale,string mat,PrimitiveType type=PrimitiveType.Cube)
            {var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mats[mat];Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
            Part("Sports coupe body",V(0,0,0),V(1.75f,.45f,3.6f),"carpaint");
            Part("Lower sill",V(0,-.2f,0),V(1.8f,.12f,3.65f),"dark");
            Part("Cabin glass",V(0,.42f,-.1f),V(1.48f,.5f,1.55f),"glass");
            Part("Roof",V(0,.69f,-.17f),V(1.5f,.07f,1.4f),"carpaint");
            foreach(int side in new[]{-1,1})
            {
                Part("Headlight",V(side*.58f,.03f,1.82f),V(.45f,.14f,.05f),"white");
                Part("Tail light",V(side*.6f,.04f,-1.82f),V(.5f,.12f,.05f),"terracotta");
                Part("Mirror",V(side*.92f,.4f,.42f),V(.25f,.15f,.3f),"carpaint");
                foreach(float z in new[]{-1.13f,1.13f})
                {var wheel=Part("Wheel",V(side*.89f,-.16f,z),V(.62f,.13f,.62f),"dark",PrimitiveType.Cylinder);wheel.transform.localRotation=Quaternion.Euler(0,0,90);var hub=Part("Alloy wheel",V(side*.96f,-.16f,z),V(.39f,.02f,.39f),"metal",PrimitiveType.Cylinder);hub.transform.localRotation=Quaternion.Euler(0,0,90);}
            }
        }
        static void Lighting()
        {
            var sun=new GameObject("Late afternoon sun").AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.85f,.66f);sun.intensity=1.2f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(34,-32,0);RenderSettings.sun=sun;
            var sky=new Material(Shader.Find("Skybox/Procedural"));sky.SetFloat("_SunSize",.025f);sky.SetFloat("_AtmosphereThickness",.85f);sky.SetColor("_SkyTint",new Color(.48f,.58f,.68f));sky.SetColor("_GroundColor",new Color(.56f,.52f,.42f));
            string path=Root+"/CoastalSky.mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old!=null){EditorUtility.CopySerialized(sky,old);Object.DestroyImmediate(sky);sky=old;}else AssetDatabase.CreateAsset(sky,path);
            RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.35f,.42f,.51f);RenderSettings.ambientEquatorColor=new Color(.34f,.33f,.29f);RenderSettings.ambientGroundColor=new Color(.16f,.15f,.13f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.65f,.72f,.75f);RenderSettings.fogDensity=.00065f;
            QualitySettings.shadowDistance=140;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.Medium;QualitySettings.antiAliasing=2;
        }
        [MenuItem("Pocket Drive/City/Open Coastal City")]
        public static void OpenCity()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Preview()
        {
            EditorSceneManager.OpenScene(ScenePath);var cam=Camera.main;var follow=cam.GetComponent<FollowCamera>();follow.enabled=false;
            Capture(cam,cam.transform.position,cam.transform.position+cam.transform.forward*100,"outputs/coastal-city-driving.png");
            Capture(cam,V(5,4,-282),V(2,16,100),"outputs/coastal-city-street.png");
            Capture(cam,V(-520,220,-560),V(15,25,20),"outputs/coastal-city-overview.png");
            Capture(cam,V(30,14,-330),V(0,0,-372),"outputs/coastal-city-parking.png");
            var car=Object.FindAnyObjectByType<ArcadeCar>().transform;
            Capture(cam,car.position+V(4.5f,1.6f,5.5f),car.position+V(0,.4f,0),"outputs/coastal-city-car.png");
            Debug.Log("COASTAL_CITY_PREVIEW_OK");
        }
        static void Capture(Camera cam,Vector3 position,Vector3 look,string path)
        {
            cam.transform.position=position;cam.transform.LookAt(look);cam.fieldOfView=60;
            var rt=new RenderTexture(1600,900,24);cam.targetTexture=rt;cam.Render();cam.Render();RenderTexture.active=rt;// first render can miss textures still loading
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            cam.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(image);Object.DestroyImmediate(rt);
        }
        [MenuItem("Pocket Drive/City/Build Coastal City Android APK")]
        public static void BuildAndroid()
        {
            Directory.CreateDirectory("Builds/Android");EditorUserBuildSettings.buildAppBundle=false;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Builds/Android/PocketDrive-coastal-city.apk",target=BuildTarget.Android,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Coastal city Android build failed");
            Debug.Log("COASTAL_CITY_ANDROID_OK");
        }
        public static void GenerateAndPreview(){Generate();Checks();Preview();}
        public static void Checks()
        {
            EditorSceneManager.OpenScene(ScenePath);Physics.SyncTransforms();
            foreach(float x in new[]{-300f,-150f,0f,150f,300f})foreach(float z in new[]{-300f,-150f,0f,150f,300f})
                if(!Physics.Raycast(V(x,3,z),Vector3.down,out var hit,4)||hit.point.y>.15f)throw new Exception("Blocked street intersection");
            foreach(var p in Ring())if(!Physics.Raycast(p+Vector3.up*2,Vector3.down,out var hit,3)||Mathf.Abs(hit.point.y-9)>.1f)throw new Exception("Freeway has missing road collider");
            for(int side=-1;side<=1;side+=2)
                for(int i=1;i<64;i++)
                {
                    float t=i/64f,u=1-t;
                    Vector3 p=RampPoint(side,t);
                    if(!Physics.Raycast(p+Vector3.up*.3f,Vector3.down,out var hit,.5f)||Mathf.Abs(hit.point.y-p.y)>.12f)throw new Exception("Ramp road surface gap");
                }
            var car=Object.FindAnyObjectByType<ArcadeCar>();var body=car.GetComponent<Rigidbody>();
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;typeof(ArcadeCar).GetMethod("Awake",flags).Invoke(car,null);var tick=typeof(ArcadeCar).GetMethod("FixedUpdate",flags);
            var old=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            try
            {
                car.SetInput(1,0);for(int i=0;i<250;i++){tick.Invoke(car,null);Physics.Simulate(.02f);}
                if(body.position.z<-230||body.position.y<.2f)throw new Exception("City driving smoke check failed");
                foreach(int side in new[]{-1,1})
                {
                    Vector3 initial=RampPoint(side,1),toward=RampPoint(side,.94f)-initial;toward.y=0;
                    car.PlaceAt(initial+Vector3.up*.65f,Quaternion.LookRotation(toward));Physics.SyncTransforms();
                    car.SetInput(0,0);for(int i=0;i<40;i++){tick.Invoke(car,null);Physics.Simulate(.02f);}
                    int waypoint=61;
                    for(int frame=0;frame<2200&&waypoint>=0;frame++)
                    {
                        Vector3 delta=RampPoint(side,waypoint/64f)-body.position;delta.y=0;
                        if(delta.magnitude<3){waypoint--;continue;}
                        float angle=Vector3.SignedAngle(car.transform.forward,delta.normalized,Vector3.up);
                        car.SetInput(.28f,Mathf.Clamp(angle/35f,-1,1));tick.Invoke(car,null);Physics.Simulate(.02f);
                    }
                    if(waypoint>1||body.position.y<8)throw new Exception($"Ramp ascent failed side={side} waypoint={waypoint} position={body.position}");
                }
                car.SetInput(0,0);car.ResetCar();
            }finally{Physics.simulationMode=old;}
            Debug.Log("COASTAL_CITY_CHECKS_OK: 25 intersections, freeway loop and both ramp surfaces and physical ascents, spawn and driving");
        }
    }
}
