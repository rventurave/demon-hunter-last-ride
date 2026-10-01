using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Reins;
using Oculus.Interaction.Input;
using Oculus.Interaction.HandGrab;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using Object = UnityEngine.Object;

public static class VerifyRidePriorities
{
    // Runs focused NUnit assertions directly, without the runner that previously stalled.
    public static object FocusedEditModeAssertions()
    {
        Check(!Application.isPlaying,"Stop Play first");
        var results=new System.Collections.Generic.List<string>();
        string[] names={"RequestedRideFeaturesTests","SwordSwingAudioTests"};
        foreach(string name in names)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().SelectMany(a=>a.GetTypes()).Single(t=>t.Name==name);
            foreach(var method in type.GetMethods())
            {
                var attributes=method.GetCustomAttributes(false);
                var cases=attributes.Where(a=>a.GetType().Name=="TestCaseAttribute").ToArray();
                bool test=attributes.Any(a=>a.GetType().Name=="TestAttribute");
                if(!test && cases.Length==0) continue;
                var arguments=cases.Length==0 ? new object[][]{Array.Empty<object>()} : cases.Select(a=>(object[])a.GetType().GetProperty("Arguments").GetValue(a)).ToArray();
                foreach(var args in arguments)
                {
                    var fixture=Activator.CreateInstance(type);
                    try { type.GetMethod("SetUp")?.Invoke(fixture,null); method.Invoke(fixture,args); results.Add(name+"."+method.Name+"("+string.Join(",",args)+")"); }
                    finally { type.GetMethod("TearDown")?.Invoke(fixture,null); }
                }
            }
        }
        return new {passed=true,count=results.Count,assertions=results};
    }
    public static object CapturePresentation()
    {
        var debug=Object.FindAnyObjectByType<DesktopDebugMode>();
        Check(Application.isPlaying && debug.IsActive,"Requires Desktop Play");
        var camera=debug.DebugCamera;
        var old=camera.targetTexture; var active=RenderTexture.active;
        var texture=new RenderTexture(1280,720,24); var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        string path="Docs/Verification/"+(Object.FindAnyObjectByType<GameSessionController>().Phase==RideSessionPhase.GameOver?"GameOver":"Intro")+"-2026-09-30.png";
        try { camera.targetTexture=texture; camera.Render(); RenderTexture.active=texture;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply(); System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG()); }
        finally { camera.targetTexture=old; RenderTexture.active=active; texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels); }
        return new {path};
    }
    public static object Priority9Play()
    {
        Check(Application.isPlaying,"Requires Play Mode");
        var session=Object.FindAnyObjectByType<GameSessionController>();
        var debug=Object.FindAnyObjectByType<DesktopDebugMode>();
        Check(debug.IsActive,"Requires temporary Desktop mode");
        var rig=GameObject.Find("OVRCameraRig").transform;
        var position=rig.localPosition; var rotation=rig.localRotation;
        session.TryStartFromGrip(true,true);
        Call(session,"HandleGiantDefeat");
        var panel=Read<GameObject>(session,"resultRoot");
        var eye=debug.DebugCamera.transform;
        float distance=Vector3.Distance(panel.transform.position,eye.position);
        Check(session.Phase==RideSessionPhase.GameOver && panel.activeSelf,"Game Over missing");
        Check(Mathf.Abs(distance-2.4f)<.02f,"Game Over distance incorrect");
        Check(Vector3.Dot(panel.transform.forward,(eye.position-panel.transform.position).normalized)>.99f,"Game Over faces away");
        Check(Read<TextMesh>(session,"resultText").text=="GAME OVER","Result text missing");
        Check(rig.localPosition==position && rig.localRotation==rotation,"Presentation moved XR rig");
        return new {passed=true,distance,facesViewer=true,rigUnchanged=true};
    }
    public static async System.Threading.Tasks.Task<object> Priority8Play()
    {
        Check(Application.isPlaying,"Requires Play Mode");
        var session=Object.FindAnyObjectByType<GameSessionController>();
        var motor=Object.FindAnyObjectByType<CarriageMotor>();
        var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
        var intro=GameObject.Find("RideIntroMenu");
        var position=motor.transform.position;
        Check(session.Phase==RideSessionPhase.WaitingToStart && intro!=null,"Initial menu missing");
        await System.Threading.Tasks.Task.Delay(350);
        Check(motor.transform.position==position && motor.EffectiveSpeed==0 && session.ElapsedSeconds==0,"Waiting advances gameplay");
        Check(!spawner.enabled && spawner.ActiveMonsters.Count==0,"Waiting spawns monsters");
        int events=0; session.OnGameStarted+=()=>events++;
        session.TryStartFromGrip(true,false); session.TryStartFromGrip(false,true);
        Check(session.Phase==RideSessionPhase.WaitingToStart,"One handle starts gameplay");
        session.TryStartFromGrip(true,true); session.TryStartFromGrip(true,true); session.TryStartFromGrip(false,false);
        Check(session.Phase==RideSessionPhase.Playing && session.StartCount==1 && events==1 && !intro.activeSelf,"Start is not one shot");
        Check(spawner.enabled && !motor.IsTravelPaused,"Gameplay remains paused");
        float before=motor.Speed; motor.RequestAcceleration();
        Check(Mathf.Approximately(motor.Speed-before,1.5f),"Acceleration changed after menu");
        await System.Threading.Tasks.Task.Delay(200);
        Check(motor.transform.position!=position && session.ElapsedSeconds>0,"Started gameplay does not advance");
        return new {passed=true,oneHandleWaits=true,twoHandlesStartOnce=true,timersPaused=true,spawnerPaused=true,originalAcceleration=1.5f};
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method,
        BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, args);
    static T Read<T>(object owner, string field) => (T)owner.GetType().GetField(field,
        BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    static void Write(object owner, string field, object value) => owner.GetType().GetField(field,
        BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
    public static object Priority1()
    {
        var sceneMotor = Object.FindAnyObjectByType<CarriageMotor>();
        Check(sceneMotor != null, "Main scene motor missing");
        var settings = new SerializedObject(sceneMotor);
        float acceleration = settings.FindProperty("accelerationPerStroke").floatValue;
        float maximum = settings.FindProperty("maximumSpeed").floatValue;
        float starting = settings.FindProperty("startingSpeed").floatValue;
        Check(Mathf.Approximately(acceleration, 1.5f) && Mathf.Approximately(maximum, 8f) &&
            Mathf.Approximately(starting, 3.5f), "Original speed balance changed");
        int combinations = 0;
        foreach (var handle in new[] {sceneMotor.LeftRein, sceneMotor.RightRein})
        {
            Check(handle != null, "Missing handle");
            foreach (var hand in new[] {Handedness.Left, Handedness.Right})
            {
                Check(handle.CanDriveHand(hand, true, true), handle.name + " rejects " + hand);
                Check(!handle.CanDriveHand(hand, true, false), "Invalid tracking accepted");
                var model = new BilateralReinGestureModel(handle.CreateGestureStateMachine());
                model.Step(true, true, Vector3.zero, Vector3.zero, .016f);
                model.Step(true, true, Vector3.up * .2f, Vector3.up * .2f, .1f);
                Check(model.Step(true, true, Vector3.zero, Vector3.zero, .1f).Kind == ReinGestureKind.Accelerate,
                    "Existing bilateral lash failed");
                Check(model.Step(true, false, Vector3.up, Vector3.up, .1f).Kind == ReinGestureKind.None,
                    "One missing handle drives");
                combinations++;
            }
        }
        var owner = new GameObject("Temporary motor verification");
        try
        {
            owner.SetActive(false);
            var motor = owner.AddComponent<CarriageMotor>();
            EditorUtility.CopySerialized(sceneMotor, motor);
            Call(motor, "Awake");
            var speeds = new float[5]; speeds[0] = motor.Speed;
            for (int i = 1; i < speeds.Length; i++) { motor.RequestAcceleration(); speeds[i] = motor.Speed; }
            Check(Mathf.Approximately(speeds[1],5f) && Mathf.Approximately(speeds[2],6.5f) &&
                Mathf.Approximately(speeds[3],8f) && Mathf.Approximately(speeds[4],8f), "Lash gain changed");
            Check(GameObject.Find("OVRCameraRig") != null, "Existing Meta rig missing");
            Check(Object.FindAnyObjectByType<DesktopDebugMode>() != null, "Desktop harness missing");
            return new { passed = true, combinations, speeds, acceleration, maximum,
                rigLocalPosition = GameObject.Find("OVRCameraRig").transform.localPosition.ToString("F3"),
                desktopConfigured = true, realTrackingTested = false };
        }
        finally { Object.DestroyImmediate(owner); }
    }

    public static object Priority2()
    {
        var sceneMotor = Object.FindAnyObjectByType<CarriageMotor>();
        foreach (int direction in new[] {-1, 1})
        {
            var model = new BilateralReinGestureModel(sceneMotor.LeftRein.CreateGestureStateMachine());
            Check(model.Step(true,true,Vector3.zero,Vector3.zero,.016f).Kind == ReinGestureKind.None, "Neutral steers");
            Check(model.Step(true,true,Vector3.right*.05f,Vector3.right*.05f,.016f).Kind == ReinGestureKind.None, "Dead zone missing");
            var pull = Vector3.right * direction * .3f;
            var command = model.Step(true,true,pull,pull,.016f);
            Check(command.Kind == ReinGestureKind.LanePull && command.Direction == direction, "Wrong lateral direction");
            for (int i=0;i<100;i++) Check(model.Step(true,true,pull,pull,.016f).Kind == ReinGestureKind.None, "Held pull spams command/audio");
            model.Step(true,true,Vector3.zero,Vector3.zero,.5f);
            Check(model.Step(true,true,-pull,-pull,.016f).Direction == -direction, "Recenter does not rearm");
            var owner = new GameObject("Temporary steering motor"); owner.SetActive(false);
            try
            {
                var motor=owner.AddComponent<CarriageMotor>(); EditorUtility.CopySerialized(sceneMotor,motor); Call(motor,"Awake");
                float speed=motor.Speed; Call(motor,"Apply",command);
                Check(motor.Lane==direction && motor.Speed==speed,"Steering modifies longitudinal speed");
                var transition=Read<LaneTransitionModel>(motor,"_laneTransition");
                Check(transition.CurrentX==0,"Steering teleports");
                float halfway=transition.Step(.4f), end=transition.Step(.4f);
                Check(Mathf.Sign(halfway)==direction && Mathf.Abs(halfway)<Mathf.Abs(end),"Transition is not smooth");
                Check(Mathf.Approximately(end,direction*2.8f),"Lane width changed");
                Check(new SerializedObject(sceneMotor).FindProperty("laneClip").objectReferenceValue != null,"Missing turn audio");
            }
            finally { Object.DestroyImmediate(owner); }
        }
        return new {passed=true, neutral=true, deadZoneMetres=.18f, smoothSeconds=.8f, directions=2, noHeldPullSpam=true};
    }

    public static object Priority3()
    {
        var cart = new GameObject("Recovery cart test");
        var obj = new GameObject("Recovery weapon test"); obj.SetActive(false);
        var point = new GameObject("SwordRespawnPoint test"); point.transform.SetParent(cart.transform);
        point.transform.localPosition = Vector3.up; point.transform.localRotation=Quaternion.Euler(0,40,0);
        try
        {
            var body=obj.AddComponent<Rigidbody>(); body.isKinematic=true;
            var damage=obj.AddComponent<SwordDamage>();
            var weapon=obj.AddComponent<GrabbableWeapon>();
            weapon.Configure(null,body,damage,cart.transform,1);
            weapon.ConfigureRecovery(point.transform,Vector3.up,new Vector3(2,2,2));
            obj.transform.position=new Vector3(1,1,0); var valid=obj.transform.position;
            weapon.TickRecovery(10); Check(obj.transform.position==valid,"Valid dropped weapon moved");
            obj.transform.position=Vector3.right*10; weapon.TickRecovery(.5f);
            Check(weapon.RecoveryCount==0,"Recovery ignores delay");
            Write(weapon,"held",true); weapon.TickRecovery(10);
            Check(weapon.RecoveryCount==0 && obj.transform.position.x==10,"Held weapon teleported");
            Write(weapon,"held",false); body.isKinematic=false; body.linearVelocity=Vector3.one*4; body.angularVelocity=Vector3.one;
            weapon.TickRecovery(.5f); Check(weapon.RecoveryCount==0,"Held time leaked into recovery timer");
            weapon.TickRecovery(.8f);
            Check(weapon.RecoveryCount==1 && obj.transform.parent==cart.transform,"Recovery failed");
            Check(obj.transform.position==point.transform.position && Quaternion.Angle(obj.transform.rotation,point.transform.rotation)<.001f,"Recovery pose wrong");
            Check(body.linearVelocity==Vector3.zero && body.angularVelocity==Vector3.zero && body.isKinematic,"Recovery velocity not cleared");
            return new {passed=true, insideStays=true, outsideReturns=true, heldNeverTeleports=true, delaySeconds=1.25f};
        }
        finally { Object.DestroyImmediate(obj); Object.DestroyImmediate(cart); }
    }

    public static object Priority4()
    {
        var weapon=Object.FindAnyObjectByType<GrabbableWeapon>();
        var motor=Object.FindAnyObjectByType<CarriageMotor>();
        var point=Read<Transform>(weapon,"swordRespawnPoint");
        Check(point!=null && point.position==weapon.transform.position,"Respawn point differs from rest");
        Check(weapon.transform.localScale==Vector3.one,"Sword scale changed");
        var collider=weapon.GetComponent<CapsuleCollider>();
        Check(Mathf.Approximately(collider.height,.98f) && Mathf.Approximately(collider.radius,.05f),"Sword hitbox changed");
        float left=Vector3.Distance(weapon.transform.position,motor.LeftRein.transform.position);
        float right=Vector3.Distance(weapon.transform.position,motor.RightRein.transform.position);
        Check(Mathf.Min(left,right)>.8f,"Sword still near reins");
        Check(weapon.IsInsideSafeZone(weapon.transform.position),"New rest is outside safe zone");
        return new {passed=true,localPosition=weapon.transform.localPosition.ToString("F3"),leftDistance=left,rightDistance=right,
            hitboxPreserved=true, respawnMatches=true,physicalReachTested=false};
    }

    public static object Priority5()
    {
        var obj=new GameObject("Sword audio test"); obj.SetActive(false);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Monsters/Zombie/Prefabs/ZombieDemon.prefab");
        var victim=Object.Instantiate(prefab); victim.transform.position=new Vector3(10000,0,0);
        try
        {
            var health=victim.GetComponent<MonsterDamageable>(); Call(health,"Awake"); health.Configure(10000,true);
            var damage=obj.AddComponent<SwordDamage>(); damage.Configure(obj.transform,12,.25f,~0);
            var weapon=obj.AddComponent<GrabbableWeapon>(); weapon.Configure(null,null,damage,null,1);
            var source=obj.AddComponent<AudioSource>(); source.playOnAwake=false;
            var feedback=obj.AddComponent<SwordSwingAudio>();
            var scene=Object.FindObjectsByType<SwordSwingAudio>().First(item=>item.name=="Knife");
            var clips=Read<AudioClip[]>(scene,"swordSounds");
            var grab=Read<AudioClip>(scene,"grabClip"); Check(grab!=null && grab.name=="5","Fifth clip is not reserved for grab");
            feedback.Configure(damage,source,clips); feedback.ConfigureGrabAudio(weapon,grab);
            Call(feedback,"OnEnable");
            Call(weapon,"PickUp"); Call(weapon,"PickUp");
            Check(feedback.GrabSoundCount==1 && feedback.PlayedSoundCount==0,"Grab audio repeats or plays impacts");
            Physics.SyncTransforms();
            damage.BeginAttackWindow(); damage.Sweep(Vector3.zero,Vector3.zero);
            Check(feedback.PlayedSoundCount==0,"Air swing plays sound"); damage.EndAttackWindow();
            Physics.SyncTransforms(); var position=victim.transform.position+Vector3.up;
            var sequence=new System.Collections.Generic.List<string>(); AudioClip previous=null;
            for(int i=0;i<16;i++)
            {
                float before=health.CurrentHealth; int sounds=feedback.PlayedSoundCount;
                damage.BeginAttackWindow(); Check(damage.Sweep(position,position)==1,"Real zombie hit not registered");
                for(int j=0;j<8;j++) damage.Sweep(position,position);
                Check(health.CurrentHealth==before-12 && feedback.PlayedSoundCount==sounds+1,"Collider contact spams damage/audio");
                Check(feedback.LastPlayedClip!=grab && feedback.LastPlayedClip!=previous,"Impact clip includes grab/repeats");
                previous=feedback.LastPlayedClip; sequence.Add(previous.name); damage.EndAttackWindow();
            }
            Check(feedback.GrabSoundCount==1,"Hit replays fifth sound");
            return new {passed=true,grabOnlyOnce=true,airSilent=true,oneImpactPerWindow=true,impactClips=sequence.ToArray(),fifthClipExcluded=true};
        }
        finally { Object.DestroyImmediate(obj); Object.DestroyImmediate(victim); Physics.SyncTransforms(); }
    }

    sealed class TestHand : IHand
    {
        public Handedness Handedness {get;set;}
        public Vector3 Position;
        public bool Valid=true;
        public bool IsConnected=>Valid; public bool IsTrackedDataValid=>Valid;
        public bool IsHighConfidence=>Valid; public bool IsDominantHand=>false; public float Scale=>1;
        public bool IsPointerPoseValid=>Valid; public int CurrentDataVersion=>0;
        public event Action WhenHandUpdated {add{} remove{}}
        public bool GetJointPose(HandJointId id,out Pose pose){pose=new Pose(Position,Quaternion.identity);return Valid;}
        public bool GetJointPoseLocal(HandJointId id,out Pose pose)=>GetJointPose(id,out pose);
        public bool GetJointPoseFromWrist(HandJointId id,out Pose pose)=>GetJointPose(id,out pose);
        public bool GetPointerPose(out Pose pose)=>GetJointPose(default,out pose);
        public bool GetRootPose(out Pose pose)=>GetJointPose(default,out pose);
        public bool GetPalmPoseLocal(out Pose pose)=>GetJointPose(default,out pose);
        public bool GetJointPosesLocal(out ReadOnlyHandJointPoses poses){poses=default;return false;}
        public bool GetJointPosesFromWrist(out ReadOnlyHandJointPoses poses){poses=default;return false;}
        public bool GetFingerIsPinching(HandFinger finger)=>false;
        public bool GetIndexFingerIsPinching()=>false;
        public bool GetFingerIsHighConfidence(HandFinger finger)=>Valid;
        public float GetFingerPinchStrength(HandFinger finger)=>0;
    }

    public static object Priority6()
    {
        var scene=Object.FindAnyObjectByType<HandStrikeController>();
        Check(Read<AudioClip>(scene,"handHitClip")?.name=="golpe","Wrong hand clip");
        var cart=new GameObject("Hand combat test cart");
        var victim=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Monsters/Zombie/Prefabs/ZombieDemon.prefab"));
        victim.transform.position=new Vector3(10000,0,0);
        var interactors=new GameObject[2];
        try
        {
            var health=victim.GetComponent<MonsterDamageable>(); Call(health,"Awake"); Call(victim.GetComponent<MonsterBase>(),"Awake");
            var hands=cart.AddComponent<HandStrikeController>(); EditorUtility.CopySerialized(scene,hands);
            Write(hands,"cartRoot",cart.transform); Write(hands,"handHitAudioSource",cart.AddComponent<AudioSource>());
            var data=new TestHand[2];
            for(int i=0;i<2;i++)
            {
                interactors[i]=new GameObject("Test hand "+i); var interactor=interactors[i].AddComponent<HandGrabInteractor>();
                data[i]=new TestHand{Handedness=(Handedness)i,Position=victim.transform.position+Vector3.up};
                interactor.InjectHand(data[i]);
            }
            hands.DiscoverHands(); Check(hands.HasBothHands,"Both hands not discovered");
            Physics.SyncTransforms(); Call(hands,"LateUpdate");
            var sweeps=interactors.Select(go=>go.GetComponentInChildren<SwordDamage>()).ToArray();
            foreach(var sweep in sweeps) { Call(sweep,"OnEnable"); Call(sweep,"Update"); }
            Check(health.CurrentHealth==30 && hands.PlayedHitSoundCount==0,"Gentle contact hurts/plays sound");
            for(int i=0;i<2;i++)
            {
                data[i].Position+=Vector3.right*2f; Call(hands,"LateUpdate"); Call(sweeps[i],"Update");
                Check(health.CurrentHealth==30-8*(i+1) && hands.PlayedHitSoundCount==i+1,"Fast hand hit not registered");
                for(int j=0;j<10;j++) Call(sweeps[i],"Update");
                Check(hands.PlayedHitSoundCount==i+1,"Repeated contact spams sound");
                sweeps[i].EndAttackWindow(); data[i].Position-=Vector3.right*2f; Call(hands,"LateUpdate"); Call(sweeps[i],"Update");
                Check(hands.PlayedHitSoundCount==i+1,"Cooldown allows rapid second hit");
            }
            Check(health.IsAlive,"Zombie died in fewer than several hand hits");
            foreach(var sweep in sweeps) { Write(sweep,"nextVelocityWindow",Time.time-1); sweep.EndAttackWindow(); }
            for(int i=0;i<2;i++)
            {
                data[i].Position+=Vector3.right*2f; Call(hands,"LateUpdate"); Call(sweeps[i],"Update");
            }
            Check(!health.IsAlive && health.CurrentHealth==0 && hands.PlayedHitSoundCount==4,"Four balanced hand hits did not kill");
            data[0].Valid=false; Call(hands,"LateUpdate"); Check(!sweeps[0].enabled,"Invalid hand tracking can damage");
            return new {passed=true,handDamage=8,minimumVelocity=2,cooldown=.35,hitSounds=hands.PlayedHitSoundCount,hitsToKill=4,invalidTrackingDisabled=true};
        }
        finally { foreach(var go in interactors) if(go!=null) Object.DestroyImmediate(go); Object.DestroyImmediate(cart); Object.DestroyImmediate(victim); Physics.SyncTransforms(); }
    }
    public static object AudioInventory() => new [] {"zombie_muerte_Mahaha.mp3","golpe.mp3"}
        .Select(name=> {var clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Art/Audio/"+name);
            return new {name,seconds=clip!=null?clip.length:0};}).ToArray();

    public static object Priority7()
    {
        var scene=Object.FindAnyObjectByType<GameSoundscape>();
        var clip=Read<AudioClip>(scene,"zombieDeathClip"); var volume=Read<float>(scene,"zombieDeathSoundVolume");
        Check(clip!=null && clip.name=="zombie_muerte_Mahaha","Wrong death clip");
        Check(Mathf.Approximately(volume,.32f*.8f*2),"Death volume is not doubled");
        var victim=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Monsters/Zombie/Prefabs/ZombieDemon.prefab"));
        try
        {
            var monster=victim.GetComponent<MonsterBase>(); Call(monster,"Awake");
            var health=victim.GetComponent<MonsterDamageable>(); Call(health,"Awake");
            var sound=victim.AddComponent<MonsterSoundEmitter>(); sound.Configure(null,clip,null,volume);
            health.ApplyDamage(8,null); Check(sound.DeathSoundCount==0,"Damage plays death audio");
            health.ApplyDamage(100,null);
            Check(sound.DeathSoundCount==1 && monster.IsDead && !health.IsAlive,"Death transition/audio missing");
            Check(!health.ApplyDamage(8,null),"Dead zombie still receives damage");
            Call(sound,"OnKilled",health); Call(sound,"OnKilled",health);
            Check(sound.DeathSoundCount==1,"Death audio repeats");
            Check(monster.Attachment==null || !monster.Attachment.IsAttached,"Dead zombie remains attached");
            Check(Read<float>(monster,"deathDisableDelay")>=clip.length,"Death audio gets cut off");
            return new {passed=true,once=true,volume,formerVolume=.256f,clipLength=clip.length,deadCannotReceiveDamage=true,attachmentReleased=true};
        }
        finally { Object.DestroyImmediate(victim); }
    }
}
