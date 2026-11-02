// Deliberately small managed substitutes; these do NOT exercise IL2CPP/Harmony.
namespace BepInEx
{
    public static class Paths { public static string GameRootPath=>AppContext.BaseDirectory; }
    public class BepInPluginAttribute(string id,string name,string version):Attribute {}
    public class BepInProcessAttribute(string name):Attribute {}
}
namespace BepInEx.Configuration
{
    public class ConfigEntry<T>(T value) { public T Value=value; }
    public class AcceptableValueRange<T>(T min,T max) {}
    public class ConfigDescription(string text,object range) {}
    public class ConfigFile { public ConfigEntry<T> Bind<T>(string s,string k,T v,object d)=>new(v); }
}
namespace BepInEx.Unity.IL2CPP
{
    public class TestLog { public void LogInfo(object s){} public void LogWarning(object s){} public void LogMessage(object s){} }
    public class BasePlugin
    {
        public readonly BepInEx.Configuration.ConfigFile Config=new(); public readonly TestLog Log=new();
        public virtual void Load(){} public virtual bool Unload()=>true;
        public T AddComponent<T>()=>(T)Activator.CreateInstance(typeof(T),IntPtr.Zero)!;
    }
}
namespace BepInEx.Unity.IL2CPP.Utils.Collections
{
    public static class CollectionExtensions { public static System.Collections.IEnumerator WrapToIl2Cpp(this System.Collections.IEnumerator e)=>e; }
}
namespace HarmonyLib
{
    public class Harmony(string id) { public void Patch(System.Reflection.MethodInfo m,HarmonyMethod? prefix=null,HarmonyMethod? postfix=null){} public void UnpatchSelf(){} }
    public class HarmonyMethod(System.Reflection.MethodInfo m) { public int priority; }
    public static class Priority { public const int First=800,Last=0; }
    public static class AccessTools { public static System.Reflection.MethodInfo? Method(Type t,string n)=>t.GetMethod(n); }
}
namespace UnityEngine
{
    public class Object
    {
        static int serial; public readonly IntPtr Pointer=new(++serial); public string name=""; public bool alive=true;
        public static readonly List<Object> Objects=new();
        public static implicit operator bool(Object? o)=>o is not null && o.alive;
        public static void Destroy(Object o)=>o.alive=false;
        public static T[] FindObjectsOfType<T>(bool include) where T:Object=>Objects.OfType<T>().Where(x=>x.alive).ToArray();
    }
    public class GameObject:Object { public GameObject(string n=""){name=n;} public bool activeInHierarchy=true; public Transform transform=new(); }
    public class MonoBehaviour:Object { public MonoBehaviour(IntPtr p){} public bool isActiveAndEnabled=true; public GameObject gameObject=new(); public object StartCoroutine(System.Collections.IEnumerator e)=>e; }
    public class WaitForEndOfFrame {}
    public struct Quaternion(float x,float y,float z,float w) { public float x=x,y=y,z=z,w=w; }
    public class Transform:Object { public Transform? parent; public Vector3 position,localPosition; public Quaternion rotation=new(0,0,0,1),localRotation=new(0,0,0,1); public Vector3 TransformPoint(Vector3 p)=>p;public Vector3 TransformVector(Vector3 p)=>p; }
    public struct Vector3(float x,float y,float z) { public float x=x,y=y,z=z; public static Vector3 zero=>new(0,0,0);public static Vector3 forward=>new(0,0,1);public Vector3 normalized=>this; }
    public static class Time { public static int frameCount; public static float unscaledTime; }
    public enum KeyCode { None,F6,J,F7 }
    public static class Input { public static KeyCode PressedKey; public static bool GetKeyDown(KeyCode key) => key!=KeyCode.None && key==PressedKey; }
}
namespace KeepLength
{
    // Visibility substitute for testing production Plugin lifecycle. Actual
    // geometry is tested separately with the real PlaneClip implementation.
    internal sealed class ClipDisplay:IDisposable
    {
        public static int Live,Applied;public static bool Available=true,ThrowOnApply;
        public static UnityEngine.Vector3 LastPoint;
        public bool Valid=true;public int Frame;public int TriangleCount=>12;
        readonly UnityEngine.Transform top;
        ClipDisplay(UnityEngine.Transform top){this.top=top;Live++;}
        public bool Matches(H.LookAtPenis l,UnityEngine.Transform t,UnityEngine.Transform b)=>Valid && top.Pointer==t.Pointer;
        public static ClipDisplay? Find(H.LookAtPenis l,UnityEngine.Transform t,UnityEngine.Transform b)=>Available?new(t):null;
        public void Apply(UnityEngine.Vector3 p,UnityEngine.Vector3 n){if(ThrowOnApply)throw new Exception("test bake failure");LastPoint=p;Applied++;Frame=UnityEngine.Time.frameCount;}
        public void Dispose(){if(Valid){Live--;Valid=false;}}
    }
}
namespace H
{
    public class HScene:UnityEngine.MonoBehaviour
    { public HScene():base(IntPtr.Zero){} public bool disposed,IsEnded,_isNowChangeAnim; public bool Ready=true; public DataClass.AnimationInfo.AnimationListInfo? NowAnimationInfo=new(){ID=1}; }
    public class MotionData { public bool IsTopStick=true,IsManual; public string LookAtNullName="k_f_kokan_00"; }
    public class LookAim { public UnityEngine.Transform? TrfLookAt,TrfUpAxis; }
    public class LookAtPenis:UnityEngine.MonoBehaviour
    {
        public LookAtPenis():base(IntPtr.Zero){} public MotionData? _currentData=new();
        public UnityEngine.Transform? _danTop,_danBase,_lookAtNullT; public LookAim? _penisInfo;
        public void LateUpdate(){} public void Release(){}
    }
}
namespace H.DataClass.AnimationInfo
{
    public class AnimationListInfo { public int ID,Mode,Kind; }
}
