using System;
using JapaneseDemonHunter.Gameplay;
using Reins;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JapaneseDemonHunter.GameplayEditor
{
    /// <summary>Focused, non-destructive migrations; never calls the scene generator.</summary>
    public static class RideFeatureSetup
    {
        static void RequireScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != "Assets/Scenes/JapanDemonHunter.unity")
                throw new InvalidOperationException("Open JapanDemonHunter and stop Play Mode first.");
        }
        static void Set(UnityEngine.Object target, string field, object value)
        {
            Undo.RecordObject(target, "Configure ride feature");
            var settings = new SerializedObject(target);
            var property = settings.FindProperty(field) ?? throw new InvalidOperationException(field);
            if (value is bool b) property.boolValue = b;
            else if (value is float f) property.floatValue = f;
            else if (value is Vector3 v) property.vector3Value = v;
            else property.objectReferenceValue = value as UnityEngine.Object;
            settings.ApplyModifiedProperties();
        }
        static void Save()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new InvalidOperationException("Scene could not be saved.");
        }
        [MenuItem("Tools/Game/Ride Features/2 Enable Rein Steering")]
        public static void Priority2()
        {
            RequireScene();
            foreach (var handle in UnityEngine.Object.FindObjectsByType<ReinHandle>())
                Set(handle, "enableLaneGesture", true);
            Save();
        }

        [MenuItem("Tools/Game/Ride Features/3 Configure Sword Recovery")]
        public static void Priority3()
        {
            RequireScene();
            var weapon = UnityEngine.Object.FindAnyObjectByType<GrabbableWeapon>();
            var settings = new SerializedObject(weapon);
            if (settings.FindProperty("swordRespawnPoint").objectReferenceValue != null) return;
            var cart = UnityEngine.Object.FindAnyObjectByType<CarriageMotor>().transform;
            var point = cart.Find("SwordRespawnPoint");
            if (point == null)
            {
                var obj = new GameObject("SwordRespawnPoint");
                Undo.RegisterCreatedObjectUndo(obj, "Create sword recovery point");
                point = obj.transform; point.SetParent(cart,false);
                point.SetPositionAndRotation(weapon.transform.position,weapon.transform.rotation);
            }
            Undo.RecordObject(weapon,"Configure sword recovery");
            weapon.ConfigureRecovery(point,new Vector3(0f,1.2f,0f),new Vector3(2.2f,1.5f,3.2f));
            Save();
        }

        static Bounds WagonBounds(Transform cart)
        {
            var wagon = GameObject.Find("Wagon_Model") ?? throw new InvalidOperationException("Missing wagon");
            Bounds result = default; bool found = false;
            foreach (var renderer in wagon.GetComponentsInChildren<Renderer>(true))
            for (int x=-1;x<=1;x+=2) for (int y=-1;y<=1;y+=2) for (int z=-1;z<=1;z+=2)
            {
                Bounds b = renderer.localBounds;
                Vector3 corner = cart.InverseTransformPoint(renderer.transform.TransformPoint(
                    b.center + Vector3.Scale(b.extents,new Vector3(x,y,z))));
                if (!found) { result=new Bounds(corner,Vector3.zero); found=true; } else result.Encapsulate(corner);
            }
            if (!found) throw new InvalidOperationException("Wagon has no bounds");
            return result;
        }

        [MenuItem("Tools/Game/Ride Features/4 Place Sword On Deck")]
        public static void Priority4()
        {
            RequireScene();
            var weapon = UnityEngine.Object.FindAnyObjectByType<GrabbableWeapon>();
            var cart = UnityEngine.Object.FindAnyObjectByType<CarriageMotor>().transform;
            var point = new SerializedObject(weapon).FindProperty("swordRespawnPoint").objectReferenceValue as Transform;
            if (point == null) throw new InvalidOperationException("Apply sword recovery first");
            // Migrate the known former rest pose once; retain later Inspector adjustments.
            if (Vector3.Distance(weapon.transform.localPosition,new Vector3(.45f,1.3102493f,-.95f))>.03f) return;
            Bounds wagon=WagonBounds(cart);
            float floor=wagon.center.y;
            var rig=GameObject.Find("OVRCameraRig").transform;
            var localRig=cart.InverseTransformPoint(rig.position);
            Vector3 position=new Vector3(localRig.x+.55f,floor+.075f,localRig.z+.35f);
            Undo.RecordObject(weapon.transform,"Place sword clear of reins");
            weapon.transform.localPosition=position;
            Undo.RecordObject(point,"Match sword recovery pose");
            point.SetPositionAndRotation(weapon.transform.position,weapon.transform.rotation);
            Set(weapon,"deckLocalHeight",position.y);
            Undo.RecordObject(weapon,"Fit recovery zone to wagon");
            weapon.ConfigureRecovery(point,new Vector3(wagon.center.x,floor+.9f,wagon.center.z),
                new Vector3(wagon.extents.x+.15f,1.15f,wagon.extents.z+.15f));
            Save();
        }

        [MenuItem("Tools/Game/Ride Features/5 Configure Sword Grab And Impact Audio")]
        public static void Priority5()
        {
            RequireScene();
            var weapon=UnityEngine.Object.FindAnyObjectByType<GrabbableWeapon>();
            var feedback=weapon.GetComponent<SwordSwingAudio>();
            if (new SerializedObject(feedback).FindProperty("grabClip").objectReferenceValue != null) return;
            var grab=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/swordSound/5.wav");
            var clips=new AudioClip[4];
            for(int i=0;i<clips.Length;i++) clips[i]=AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Art/Audio/swordSound/{i+1}.wav");
            if (grab==null || System.Array.Exists(clips,c=>c==null)) throw new InvalidOperationException("Missing sword clips");
            Undo.RecordObject(feedback,"Reserve fifth sword sound for grab");
            feedback.Configure(weapon.GetComponentInChildren<JapaneseDemonHunter.Monsters.SwordDamage>(),
                weapon.GetComponent<AudioSource>(),clips);
            feedback.ConfigureGrabAudio(weapon,grab);
            Save();
        }

        [MenuItem("Tools/Game/Ride Features/6 Configure Hand Combat")]
        public static void Priority6()
        {
            RequireScene();
            var hands=UnityEngine.Object.FindAnyObjectByType<HandStrikeController>();
            var settings=new SerializedObject(hands);
            if (settings.FindProperty("handHitClip").objectReferenceValue != null) return;
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/golpe.mp3");
            if (clip==null) throw new InvalidOperationException("Missing golpe.mp3");
            var source=hands.GetComponent<AudioSource>();
            if (source==null) { source=Undo.AddComponent<AudioSource>(hands.gameObject); source.playOnAwake=false;
                source.spatialBlend=1; source.minDistance=.5f; source.maxDistance=10; source.dopplerLevel=0; }
            Set(hands,"handHitAudioSource",source); Set(hands,"handHitClip",clip);
            if (Mathf.Approximately(settings.FindProperty("punchDamage").floatValue,5f)) Set(hands,"punchDamage",8f);
            Save();
        }

        [MenuItem("Tools/Game/Ride Features/7 Verify Zombie Death Audio")]
        public static void Priority7()
        {
            RequireScene();
            var sound=UnityEngine.Object.FindAnyObjectByType<GameSoundscape>();
            var settings=new SerializedObject(sound);
            if (settings.FindProperty("zombieDeathClip").objectReferenceValue==null)
                Set(sound,"zombieDeathClip",AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/zombie_muerte_Mahaha.mp3"));
            Save();
        }

        [MenuItem("Tools/Game/Ride Features/8 Configure Intro Session")]
        public static void Priority8()
        {
            RequireScene();
            var victory=UnityEngine.Object.FindAnyObjectByType<LevelVictoryController>();
            var session=victory.GetComponent<GameSessionController>();
            if (session==null) session=Undo.AddComponent<GameSessionController>(victory.gameObject);
            var eye=GameObject.Find("OVRCameraRig").transform.Find("TrackingSpace/CenterEyeAnchor");
            if (new SerializedObject(session).FindProperty("presentationEye").objectReferenceValue==null)
                Set(session,"presentationEye",eye);
            if (Mathf.Approximately(new SerializedObject(session).FindProperty("introTitleSize").floatValue,.055f))
                Set(session,"introTitleSize",.035f);
            Save();
        }
        [MenuItem("Tools/Game/Ride Features/9 Position Result Presentation")]
        public static void Priority9()
        {
            RequireScene();
            var session=UnityEngine.Object.FindAnyObjectByType<GameSessionController>();
            if (session==null) throw new InvalidOperationException("Configure priority 8 first");
            Save();
        }
    }
}
