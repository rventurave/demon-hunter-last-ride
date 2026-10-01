using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Reins;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using Object = UnityEngine.Object;

public static class VerifyNextRound
{
    public static object FinalAudit()
    {
        Check(!Application.isPlaying,"Stop Play first");
        var motor=Object.FindAnyObjectByType<CarriageMotor>();
        var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
        var hand=Object.FindAnyObjectByType<HandStrikeController>();
        var weapon=Object.FindAnyObjectByType<GrabbableWeapon>();
        var audio=weapon.GetComponentInChildren<SwordSwingAudio>();
        var soundscape=Object.FindAnyObjectByType<GameSoundscape>();
        var road=Object.FindAnyObjectByType<ForestRoad>();
        var debug=Object.FindAnyObjectByType<DesktopDebugMode>();
        Check(!debug.EnabledForDesktop,"Saved Desktop mode not zero");
        Check(UnityEngine.XR.Management.XRGeneralSettings.Instance.InitManagerOnStart,"XR startup disabled");
        Check(GameObject.Find("OVRCameraRig")!=null,"Meta rig missing");
        Check(Read<float>(motor,"accelerationPerStroke")==1.5f && Read<float>(motor,"decelerationRate")==.2f,"Longitudinal balance changed");
        Check(spawner.InitialSpawnDelay==7f && spawner.PostEncounterSpawnDelay==15f && spawner.UsesEncounterGroups,"Pacing lost");
        Check(spawner.SpawnEntries.First(e=>e.movementType==MonsterMovementType.Ground).prefab.GetComponent<MonsterDamageable>().MaximumHealth==12,"Live zombie prefab balance lost");
        Check(Read<CastleGoalTrigger>(Object.FindAnyObjectByType<LevelVictoryController>(),"castleGoal")!=null,"Victory trigger missing");
        return new {passed=true,desktopDebugMode=0,xrStartup=true,rigPreserved=true,
            zombiePrefab=AssetDatabase.GetAssetPath(spawner.SpawnEntries.First(e=>e.movementType==MonsterMovementType.Ground).prefab),
            zombieHP=12,swordDamage=Read<float>(weapon.GetComponentInChildren<SwordDamage>(),"damage"),handDamage=Read<float>(hand,"punchDamage"),
            lash=1.5f,deceleration=.2f,maximumSpeed=Read<float>(motor,"maximumSpeed"),
            leftSteeringThreshold=Read<float>(motor.LeftRein,"laneThreshold"),rightSteeringThreshold=Read<float>(motor.RightRein,"laneThreshold"),
            initialDelay=spawner.InitialSpawnDelay,postDelay=spawner.PostEncounterSpawnDelay,distance=road.LevelDistance,
            handClip=AssetDatabase.GetAssetPath(Read<AudioClip>(hand,"handHitClip")),handVolume=Read<float>(hand,"handHitVolume"),
            grabClip=AssetDatabase.GetAssetPath(Read<AudioClip>(audio,"grabClip")),
            deathClip=AssetDatabase.GetAssetPath(Read<AudioClip>(soundscape,"zombieDeathClip")),deathVolume=Read<float>(soundscape,"zombieDeathSoundVolume")};
    }
    public static async Task<object> FinalRuntimeRegression()
    {
        Check(Application.isPlaying,"Requires Play Mode");
        var session=Object.FindAnyObjectByType<GameSessionController>();
        var motor=Object.FindAnyObjectByType<CarriageMotor>();
        var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
        var victory=Object.FindAnyObjectByType<LevelVictoryController>();
        Check(session.Phase==RideSessionPhase.WaitingToStart && motor.EffectiveSpeed==0,"Menu does not pause ride");
        session.TryStartFromGrip(true,false); Check(session.StartCount==0,"One grip starts");
        session.TryStartFromGrip(false,true); Check(session.StartCount==0,"One other grip starts");
        session.TryStartFromGrip(true,true); session.TryStartFromGrip(true,true); session.TryStartFromGrip(false,false);
        Check(session.StartCount==1 && session.Phase==RideSessionPhase.Playing,"Start repeats or released grip resets menu");
        float speed=motor.Speed; motor.RequestAcceleration(); Check(Mathf.Approximately(motor.Speed-speed,1.5f),"Lash gain changed");
        float boosted=motor.Speed; await Task.Delay(350); Check(motor.Speed<boosted && motor.Speed>boosted-.2f,"Coasting changed");
        victory.Complete();
        Check(session.Phase==RideSessionPhase.Victory && spawner.SpawningStopped && !spawner.enabled,"Victory fails to stop spawning");
        Check(!spawner.TrySpawn() && spawner.TrySpawnEncounter()==0 && !spawner.TrySpawnFaceThreatRound(1),"Explicit spawn APIs bypass victory");
        speed=motor.Speed; motor.RequestAcceleration(); Call(session,"HandlePlayerHit",new object[]{null});
        Check(motor.Speed==speed && motor.EffectiveSpeed==0,"Victory accepts acceleration/damage");
        return new {passed=true,oneGripWaits=true,startCount=session.StartCount,lash=1.5f,coasting=true,victoryBlocksSpawnApis=true};
    }
    public static object GameOverPlay()
    {
        Check(Application.isPlaying,"Requires Play Mode");
        var session=Object.FindAnyObjectByType<GameSessionController>(); session.StartGame();
        Call(session,"HandleGiantDefeat");
        Check(session.Phase==RideSessionPhase.GameOver && session.Result==GameRunResult.Defeat,"GameOver became Victory");
        var title=Read<TextMesh>(session,"resultText"); Check(title.text=="GAME OVER" && title.color.r>title.color.g,"GameOver presentation changed");
        Check(Object.FindAnyObjectByType<MonsterSpawner>().SpawningStopped,"Defeat does not stop spawns");
        Check(Read<ParticleSystem[]>(Object.FindAnyObjectByType<LevelVictoryController>(),"victoryFireworks").All(p=>!p.isPlaying),"Defeat plays victory fireworks");
        var root=Read<GameObject>(session,"resultRoot");
        var eye=Read<Transform>(session,"presentationEye");
        Check(Mathf.Abs(Vector3.ProjectOnPlane(root.transform.position-eye.position,Vector3.up).magnitude-2.4f)<.02f,"GameOver too close");
        return new {passed=true,phase=session.Phase.ToString(),title=title.text,distance=2.4f,noFireworks=true};
    }
    public static object CastleGeometry()
    {
        var goal=Object.FindAnyObjectByType<CastleGoalTrigger>();
        var road=Object.FindAnyObjectByType<ForestRoad>();
        var model=road.transform.Find("Kingdom/Fortress_Model");
        Check(model!=null && Mathf.Approximately(model.localScale.x,320f),"Wrong fortress scale");
        Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(model.gameObject)=="Assets/Art/Fortress.fbx","Wrong fortress asset");
        Check(!ShaderUtil.ShaderHasError(model.GetComponentInChildren<Renderer>().sharedMaterial.shader),"Castle shader error");
        var mesh=model.GetComponentInChildren<MeshCollider>();
        int doorwayChecks=0;
        foreach(float lateral in new[]{-4.93f,-2.8f,0f,2.8f,4.93f})
            foreach(float height in new[]{.5f,1.6f,3f,4f})
            {
                var start=model.TransformPoint(new Vector3(-.0875f+lateral/320f,.061f+height/320f,1.2f));
                Check(!mesh.Raycast(new Ray(start,-model.forward),out var hit,256f),"Door geometry blocks cart at "+lateral+","+height);
                doorwayChecks++;
            }
        road.CreatePathModel().GetPoseAtDistance(road.LevelDistance,out var end,out float heading);
        Check(goal.ContainsCarriagePosition(road.transform.TransformPoint(end)),"Goal misses actual path");
        road.CreatePathModel().GetPoseAtDistance(road.LevelDistance-8,out var facade,out heading);
        Check(!goal.ContainsCarriagePosition(road.transform.TransformPoint(facade)),"Victory zone includes facade");
        Check(!goal.ContainsCarriagePosition(Object.FindAnyObjectByType<CarriageMotor>().transform.position),"Start already inside goal");
        return new {passed=true,doorwayChecks,clearEnvelopeWidth=9.86f,clearEnvelopeHeight=4f,
            approximateOpeningWidth=49.6f,goalBehindDoorMetres=8f,scale=320f,position=model.position.ToString(),
            lights=road.transform.Find("Kingdom/CastleWarmLights").GetComponentsInChildren<Light>().Length};
    }
    public static object CastleStartCapture()
        => CastleCapture(false);
    public static object CastleVisibilityTrial()
    {
        var model=Object.FindAnyObjectByType<ForestRoad>().transform.Find("Kingdom/Fortress_Model");
        var position=model.position; var scale=model.localScale;
        var gate=model.TransformPoint(new Vector3(-.0875f,.061f,.86f));
        try
        {
            model.localScale=Vector3.one*320;
            model.position=gate-model.rotation*new Vector3(-.0875f,.061f,.86f)*320;
            return CastleCapture(false);
        }
        finally {model.position=position; model.localScale=scale;}
    }
    public static object CastleNearCapture()
        => CastleCapture(true);
    static object CastleCapture(bool near)
    {
        Check(Application.isPlaying,"Requires Play Mode");
        var motor=Object.FindAnyObjectByType<CarriageMotor>();
        var road=Object.FindAnyObjectByType<ForestRoad>();
        var model=road.transform.Find("Kingdom/Fortress_Model");
        var cameraObject=new GameObject("Castle visibility diagnostic camera");
        var camera=cameraObject.AddComponent<Camera>(); camera.enabled=false;
        camera.transform.SetPositionAndRotation(motor.transform.position+Vector3.up*2.3f,Quaternion.Euler(0,180,0));
        if(near)
        {
            var center=model.GetComponentInChildren<Renderer>().bounds.center;
            camera.transform.position=center+model.forward*210+Vector3.up*30;
            camera.transform.LookAt(center);
        }
        camera.farClipPlane=2000f; camera.fieldOfView=65f;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.02f,.025f,.04f);
        var rt=new RenderTexture(1280,720,24); var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
        var old=RenderTexture.active;
        try
        {
            var point=camera.WorldToViewportPoint(model.GetComponentInChildren<Renderer>().bounds.center);
            Check(point.z>0 && point.x>0 && point.x<1 && point.y>0 && point.y<1,"Castle outside initial view");
            Check(model.gameObject.activeInHierarchy,"Distant castle culled");
            camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
            System.IO.File.WriteAllBytes(near?"Docs/Verification/NewRound8-CastleNear.png":"Docs/Verification/NewRound8-CastleStart.png",pixels.EncodeToPNG());
            Check(!ShaderUtil.ShaderHasError(model.GetComponentInChildren<Renderer>().sharedMaterial.shader),"Rendered castle shader failed");
            return new {passed=true,startDistance=Vector3.Distance(camera.transform.position,model.position),viewport=point.ToString(),
                image="Docs/Verification/NewRound8-CastleStart.png",xrCameraMoved=false};
        }
        finally {RenderTexture.active=old; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(pixels); Object.DestroyImmediate(cameraObject);}
    }
    public static async Task<object> RideDurationPlay()
    {
        Check(Application.isPlaying,"Requires Play Mode");
        var session=Object.FindAnyObjectByType<GameSessionController>();
        var motor=Object.FindAnyObjectByType<CarriageMotor>();
        var road=Object.FindAnyObjectByType<ForestRoad>();
        var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
        Check(road.LevelDistance==1440f,"Pacing migration missing");
        var goal=Object.FindAnyObjectByType<CastleGoalTrigger>();
        Check(Read<float>(motor,"accelerationPerStroke")==1.5f,"Lash changed");
        float oldScale=Time.timeScale,oldFixed=Time.fixedDeltaTime;
        var birth=new System.Collections.Generic.Dictionary<MonsterBase,float>();
        int encounters=0,strokes=0;
        Action<MonsterBase> handler=m=>{birth[m]=Time.time; if(m.MovementType==MonsterMovementType.Ground) encounters++;};
        spawner.MonsterSpawned+=handler;
        session.StartGame(); float started=Time.time,nextStroke=started;
        try
        {
            Time.timeScale=12f;
            // Real motor/path and encounter timers, accelerated Editor time; no position writes.
            while(!(goal!=null ? goal.HasArrived : road.HasReachedEnd) && Time.time-started<245f && session.Result==GameRunResult.Playing)
            {
                if(Time.time>=nextStroke) {motor.RequestAcceleration(); strokes++; nextStroke+=1.5f;}
                foreach(var pair in birth.ToArray())
                    if(pair.Key!=null && !pair.Key.IsDead && Time.time-pair.Value>=
                        (pair.Key.MovementType==MonsterMovementType.Ground?4f:2f))
                    {
                        var health=pair.Key.GetComponent<MonsterDamageable>();
                        if(health!=null && pair.Key.MovementType==MonsterMovementType.Ground) health.ApplyDamage(12f,null);
                        else pair.Key.Retire();
                    }
                await Task.Delay(25);
            }
            float duration=Time.time-started;
            Check(goal!=null ? goal.HasArrived : road.HasReachedEnd,"Representative ride did not reach destination");
            Check(duration>=175f && duration<=240f,"Ride outside duration target: "+duration);
            return new {passed=true,simulatedGameSeconds=duration,editorTimeScale=12,distance=road.LevelDistance,
                normalZombieCount=encounters,lashCount=strokes,lashCadence=1.5f,defenseAfterSeconds=4f,
                maximumSpeed=Read<float>(motor,"maximumSpeed"),deceleration=Read<float>(motor,"decelerationRate"),lash=1.5f};
        }
        finally {Time.timeScale=oldScale; Time.fixedDeltaTime=oldFixed; spawner.MonsterSpawned-=handler;}
    }

    public static async Task<object> VictoryJourneyPlay()
    {
        var duration=await RideDurationPlay();
        var session=Object.FindAnyObjectByType<GameSessionController>();
        var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
        var victory=Object.FindAnyObjectByType<LevelVictoryController>();
        var motor=Object.FindAnyObjectByType<CarriageMotor>();
        Check(session.Phase==RideSessionPhase.Victory && session.Result==GameRunResult.Victory,"Victory is GameOver");
        Check(victory.IsComplete && !spawner.enabled && !motor.enabled && motor.EffectiveSpeed==0,"Gameplay continues after victory");
        Check(!Object.FindAnyObjectByType<GiantZombieSpawner>().enabled && !Object.FindAnyObjectByType<FaceBatThreatController>().enabled,"Threat spawners still active");
        Check(spawner.ActiveMonsterCount==0,"Enemies remain after victory");
        var title=Read<TextMesh>(session,"resultText");
        Check(title.text=="CONGRATULATIONS" && title.color.r==1f && title.color.g>.7f,"Wrong victory text/color");
        var root=Read<GameObject>(session,"resultRoot"); Check(root.activeInHierarchy,"Victory panel hidden");
        var effects=Read<ParticleSystem[]>(victory,"victoryFireworks");
        Check(effects.Length==3 && effects.All(e=>e.isPlaying),"Fireworks absent");
        await Task.Delay(650);
        int particles=effects.Sum(e=>e.particleCount);
        Check(particles>0 && particles<=192,"Fireworks count invalid");
        var positions=effects.Select(e=>e.transform.position.ToString()).ToArray();
        int count=0; Action repeated=()=>count++; victory.LevelCompleted+=repeated;
        victory.Complete(); victory.LevelCompleted-=repeated;
        Call(session,"HandleGiantDefeat");
        Check(count==0 && session.Phase==RideSessionPhase.Victory,"Victory repeats/turns into GameOver");
        var cameraObject=new GameObject("Victory diagnostic camera"); var camera=cameraObject.AddComponent<Camera>(); camera.enabled=false;
        camera.transform.SetPositionAndRotation(root.transform.position+root.transform.forward*2.4f,Quaternion.LookRotation(-root.transform.forward));
        camera.farClipPlane=2000f;
        var rt=new RenderTexture(1280,720,24); var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false); var old=RenderTexture.active;
        try
        {
            camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0); pixels.Apply();
            System.IO.File.WriteAllBytes("Docs/Verification/NewRound9-Victory.png",pixels.EncodeToPNG());
        }
        finally {RenderTexture.active=old; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(pixels); Object.DestroyImmediate(cameraObject);}
        Check(effects.All(e=>!ShaderUtil.ShaderHasError(e.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader)),"Fireworks shader error");
        return new {passed=true,phase=session.Phase.ToString(),duration,particles,emitters=3,positions=string.Join(";",positions),
            title=title.text,textDistance=2.4f,spawnerStopped=true,attacksStopped=true,actualPhysicsTrigger=true};
    }
    public static async Task<object> EncounterCooldownPlay()
    {
        Check(Application.isPlaying,"Requires Play Mode");
        var session=Object.FindAnyObjectByType<GameSessionController>();
        var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
        session.StartGame(); Object.FindAnyObjectByType<CarriageMotor>().SetTravelPaused(true); await Task.Delay(100);
        int spawned=0; Action<MonsterBase> listener=m=>{if(m.MovementType==MonsterMovementType.Ground) spawned++;};
        spawner.MonsterSpawned+=listener;
        try
        {
            Check(spawner.TrySpawnEncounter(MonsterSpawnDirection.LeftForest)>=2,"Group unavailable");
            var group=spawner.ActiveMonsters.ToArray(); int initial=spawned;
            group[0].Kill();
            Check(spawner.EncounterInProgress,"One death ends living encounter");
            foreach(var monster in group.Skip(1)) monster.Kill();
            float defeated=Time.time;
            Check(!spawner.EncounterInProgress,"Dead encounter remains locked");
            Check(Mathf.Abs(spawner.SecondsUntilNextEncounter-15f)<.1f,"Cooldown not fifteen seconds");
            while(Time.time-defeated<14.85f) {Check(spawned==initial,"Spawn during respite"); await Task.Delay(100);}
            float remaining=spawner.SecondsUntilNextEncounter;
            while(spawned==initial && Time.time-defeated<18f) await Task.Delay(100);
            Check(spawned>initial,"Encounter never resumed");
            Check(Time.time-defeated>=15f,"Encounter resumed early");
            return new {passed=true,delay=spawner.PostEncounterSpawnDelay,measuredSeconds=Time.time-defeated,
                corpseRemovalDidNotResetTimer=remaining<.3f,newGroup=spawned-initial};
        }
        finally {spawner.MonsterSpawned-=listener;}
    }
    public static async Task<object> EncounterPlacementPlay()
    {
        Check(Application.isPlaying,"Requires Play Mode");
        var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
        Object.FindAnyObjectByType<GameSessionController>().StartGame();
        spawner.enabled=false;
        Object.FindAnyObjectByType<CarriageMotor>().SetTravelPaused(true);
        var random=UnityEngine.Random.state;
        UnityEngine.Random.InitState(20261001);
        int fronts=0,left=0,right=0,runChecks=0;
        var positions=new System.Collections.Generic.List<string>();
        try
        {
            foreach(var direction in new[]{MonsterSpawnDirection.FrontLane,MonsterSpawnDirection.LeftForest,MonsterSpawnDirection.RightForest})
            {
                for(int attempt=0;attempt<6;attempt++)
                {
                    int count=spawner.TrySpawnEncounter(direction);
                    Check(count<=(direction==MonsterSpawnDirection.FrontLane?1:4),"Encounter too large");
                    if(direction==MonsterSpawnDirection.FrontLane) fronts+=count;
                    else if(direction==MonsterSpawnDirection.LeftForest) left=Mathf.Max(left,count);
                    else right=Mathf.Max(right,count);
                    var monsters=spawner.ActiveMonsters.ToArray();
                    for(int i=0;i<monsters.Length;i++)
                    {
                        var p=monsters[i].transform.position;
                        var offset=spawner.CartTransform.InverseTransformPoint(p);
                        if(direction==MonsterSpawnDirection.LeftForest) Check(offset.x<-6,"Left spawn outside forest side");
                        if(direction==MonsterSpawnDirection.RightForest) Check(offset.x>6,"Right spawn outside forest side");
                        if(direction!=MonsterSpawnDirection.FrontLane)
                            Check((bool)spawner.GetType().GetMethod("IsOutsideTreeTrunks",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(spawner,new object[]{p}),"Spawn intersects tree trunk");
                        for(int j=0;j<i;j++) Check(Vector3.Distance(p,monsters[j].transform.position)>1f,"Overlapping group");
                        positions.Add(direction+":"+offset.ToString());
                        var animation=monsters[i].GetComponent<MonsterAnimationController>();
                        animation.Animator.Update(0f); animation.PlayRun(); animation.Animator.Update(.4f); animation.Animator.Update(.4f);
                        Check(animation.Animator.GetCurrentAnimatorStateInfo(0).IsName("LocomotionFast"),"Run state inactive: configured="+Read<string>(animation,"runState")+", has="+animation.Animator.HasState(0,Animator.StringToHash("Run"))+", hash="+animation.Animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                        Check(animation.Animator.runtimeAnimatorController.animationClips.Any(c=>c.name=="Zombie|ZombieRun" && c.isLooping),"Run clip not looping");
                        runChecks++;
                    }
                    foreach(var monster in monsters) monster.Retire();
                    await Task.Delay(50);
                }
            }
            Check(fronts>=4 && left>=2 && right>=2,"Unable to spawn requested regions/groups");
            return new {passed=true,frontalEvents=6,frontSuccesses=fronts,leftGroup=left,rightGroup=right,runChecks,positions=string.Join(";",positions)};
        }
        finally {UnityEngine.Random.state=random; foreach(var monster in spawner.ActiveMonsters.ToArray()) monster.Retire();}
    }
    public static async Task<object> InitialSpawnPlay()
    {
        Check(Application.isPlaying,"Requires Play Mode");
        var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
        var session=Object.FindAnyObjectByType<GameSessionController>();
        Check(session.Phase==RideSessionPhase.WaitingToStart && !spawner.enabled,"Menu does not pause spawner");
        Check(spawner.InitialSpawnDelay==7f,"Initial delay migration missing");
        await Task.Delay(1000);
        Check(spawner.ActiveMonsterCount==0,"Spawn during menu");
        float started=Time.time; float first=-1; int spawned=0;
        Action<MonsterBase> handler=monster=>{spawned++; if(first<0) first=Time.time-started;};
        spawner.MonsterSpawned+=handler;
        try
        {
            session.StartGame();
            Check(session.Phase==RideSessionPhase.Playing,"Gameplay did not start");
            while(Time.time-started<6.9f) { Check(spawned==0,"Spawn before seven seconds"); await Task.Delay(100); }
            while(first<0 && Time.time-started<9f) await Task.Delay(100);
            Check(first>=7f && first<9f,"First spawn missing or early");
            return new {passed=true,menuSpawnCount=0,firstSpawnSeconds=first,spawned,delay=spawner.InitialSpawnDelay};
        }
        finally {spawner.MonsterSpawned-=handler;}
    }
    public static object HandFeedback()
    {
        var controller=Object.FindAnyObjectByType<HandStrikeController>();
        int visuals=Object.FindObjectsByType<Oculus.Interaction.HandVisual>(FindObjectsInactive.Include).Length;
        int checks=0;
        foreach(var side in new[]{Oculus.Interaction.Input.Handedness.Left,Oculus.Interaction.Input.Handedness.Right})
        {
            var materials=Read<System.Collections.Generic.List<Oculus.Interaction.MaterialPropertyBlockEditor>>(controller,
                side==Oculus.Interaction.Input.Handedness.Left?"leftFeedbackMaterials":"rightFeedbackMaterials");
            Check(materials.Count>0,"Hand feedback references missing"); checks++;
            var original=materials.Select(m=>m.MaterialPropertyBlock.GetFloat("_ThumbGlowValue")).ToArray();
            try
            {
                controller.SetStrikeFeedback(side,true,false);
                foreach(var editor in materials) { Check(editor.MaterialPropertyBlock.GetFloat("_ThumbGlowValue")>=.6f,"Ready glow missing"); checks++; }
                controller.SetStrikeFeedback(side,false,true);
                foreach(var editor in materials)
                {
                    Check(editor.MaterialPropertyBlock.GetFloat("_ThumbGlowValue")==1f,"Hit glow missing");
                    Check(editor.MaterialPropertyBlock.GetColor("_FingerGlowColor").r>.9f,"Hit not gold"); checks+=2;
                }
            }
            finally { controller.SetStrikeFeedback(side,false,false); }
            for(int i=0;i<materials.Count;i++) { Check(Mathf.Approximately(original[i],materials[i].MaterialPropertyBlock.GetFloat("_ThumbGlowValue")),"Grab appearance not restored"); checks++; }
        }
        Check(visuals==Object.FindObjectsByType<Oculus.Interaction.HandVisual>(FindObjectsInactive.Include).Length,"Hands duplicated");
        Check(Read<float>(controller,"minimumSwingSpeed")==2f && Read<float>(controller,"punchDamage")==8f,"Combat threshold/balance changed");
        return new {passed=true,playMode=Application.isPlaying,checks=checks+2,existingHandVisuals=visuals,damage=8,minimumVelocity=2};
    }
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    static T Read<T>(object target,string name) => (T)target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(target);
    static void Call(object target,string name,params object[] arguments) => target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,arguments);

    public static async Task<object> SteeringPlay()
    {
        Check(Application.isPlaying,"Requires Play Mode");
        var motor=Object.FindAnyObjectByType<CarriageMotor>();
        foreach(var rein in new[]{motor.LeftRein,motor.RightRein})
            Check(Mathf.Approximately(new SerializedObject(rein).FindProperty("laneThreshold").floatValue,.15f),"Sensitivity migration pending");
        Object.FindAnyObjectByType<GameSessionController>().TryStartFromGrip(true,true);
        Check(!motor.IsTravelPaused,"Session did not start");
        var model=new BilateralReinGestureModel(motor.LeftRein.CreateGestureStateMachine());
        var neutral=Vector3.zero;
        Check(model.Step(true,true,neutral,neutral,.016f).Kind==ReinGestureKind.None,"Neutral steers");
        var noise=new Vector3(.05f,0,0);
        Check(model.Step(true,true,noise,noise,.016f).Kind==ReinGestureKind.None,"Noise steers");
        foreach(float direction in new[]{-1f,1f})
        {
            var probe=new BilateralReinGestureModel(motor.LeftRein.CreateGestureStateMachine());
            probe.Step(true,true,neutral,neutral,.016f);
            var below=new Vector3(direction*.14f,0,0);
            Check(probe.Step(true,true,below,below,.016f).Kind==ReinGestureKind.None,"Below threshold steers");
            var above=new Vector3(direction*.16f,0,0);
            var gesture=probe.Step(true,true,above,above,.016f);
            Check(gesture.Kind==ReinGestureKind.LanePull && gesture.Direction==(int)direction,"Moderate sensitivity fails");
        }
        var left=new Vector3(-.2f,0,.3f); var right=new Vector3(.2f,0,.3f);
        float speed=motor.Speed;
        Call(motor,"Apply",model.Step(true,true,left,left,.016f));
        Check(motor.Lane==-1 && Mathf.Approximately(speed,motor.Speed),"Left command failed/changes speed");
        await Task.Delay(150);
        var transition=Read<LaneTransitionModel>(motor,"_laneTransition"); float leftX=transition.CurrentX;
        model.Step(true,true,new Vector3(0,0,.3f),new Vector3(0,0,.3f),.016f);
        Call(motor,"Apply",model.Step(true,true,right,right,.016f));
        Check(motor.Lane==0 && Mathf.Approximately(transition.CurrentX,leftX),"Reversal jumps or locks");
        await Task.Delay(250);
        Check(transition.CurrentX>leftX,"Carriage does not move right after left");
        Call(motor,"Apply",model.Step(true,true,left,left,.016f));
        Check(motor.Lane==-1,"Right to left reversal locks");
        for(int i=0;i<30;i++) Check(model.Step(true,true,left,left,.016f).Kind==ReinGestureKind.None,"Held steering repeats");
        float before=motor.Speed; motor.RequestAcceleration();
        Check(Mathf.Approximately(motor.Speed-before,1.5f),"Lash acceleration changed");
        return new {passed=true,left=true,rightAfterLeft=true,leftAfterRight=true,deadZoneNoise=.05f,sensitivityMetres=.15f,originalAcceleration=1.5f};
    }

    public static object SmallZombieBalance()
    {
        Check(!Application.isPlaying,"Requires EditMode");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Monsters/Zombie/Prefabs/ZombieDemon.prefab");
        Check(prefab.GetComponent<MonsterDamageable>().MaximumHealth==12f,"Zombie balance migration pending");
        var sword=Object.FindAnyObjectByType<GrabbableWeapon>().GetComponentInChildren<SwordDamage>();
        float swordDamage=Read<float>(sword,"damage");
        float handDamage=Read<float>(Object.FindAnyObjectByType<HandStrikeController>(),"punchDamage");
        Check(swordDamage==12f && handDamage==8f,"Global damage changed");
        var victims=new[]{Object.Instantiate(prefab),Object.Instantiate(prefab)};
        try
        {
            foreach(var victim in victims) { Call(victim.GetComponent<MonsterBase>(),"Awake"); Call(victim.GetComponent<MonsterDamageable>(),"Awake"); }
            var blade=victims[0].GetComponent<MonsterDamageable>();
            Check(blade.ApplyDamage(swordDamage,sword) && !blade.IsAlive,"One sword strike does not kill");
            Check(!blade.ApplyDamage(swordDamage,sword),"Dead zombie accepts repeated damage");
            var fist=victims[1].GetComponent<MonsterDamageable>();
            Check(fist.ApplyDamage(handDamage,null) && fist.IsAlive && fist.CurrentHealth==4,"First hand strike kills");
            Check(fist.ApplyDamage(handDamage,null) && !fist.IsAlive,"Second hand strike does not kill");
            return new {passed=true,zombieHealth=12,swordDamage,handDamage,swordHits=1,handHits=2};
        }
        finally { foreach(var victim in victims) Object.DestroyImmediate(victim); }
    }
}





