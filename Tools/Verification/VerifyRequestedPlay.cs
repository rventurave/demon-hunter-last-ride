using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using Reins;
using Object = UnityEngine.Object;

public static class VerifyRequestedPlay
{
 static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
 static void Set(object obj,string name,object value) => obj.GetType().GetField(name,Private).SetValue(obj,value);
 static T Get<T>(object obj,string name) => (T)obj.GetType().GetField(name,Private).GetValue(obj);
 static void Check(bool valid,string message) { if(!valid) throw new Exception(message); }

 public static async Task<object> Run()
 {
  Check(EditorApplication.isPlaying,"Requires Play Mode");
  var results = new Dictionary<string,object>();
  var mainCart = GameObject.Find("VehicleRoot").transform;
  var rig = GameObject.Find("OVRCameraRig").transform;
  var knife = GameObject.Find("Knife");
  var sword = knife.GetComponentInChildren<SwordDamage>();
  var audio = knife.GetComponent<SwordSwingAudio>();
  var clips = Get<AudioClip[]>(audio,"swordSounds");
  Check(clips.Length==5 && clips.All(c=>c!=null),"Five clips not assigned");
  var horse = (GameObject.Find("Horse_Model_Left").transform.position + GameObject.Find("Horse_Model_Right").transform.position)*.5f;
  Check(Vector3.Dot(Vector3.ProjectOnPlane(rig.forward,Vector3.up).normalized,Vector3.ProjectOnPlane(horse-rig.position,Vector3.up).normalized)>.99f,"Rig faces away from horses");
  Check(Mathf.Abs(rig.localPosition.y-.56f)<.02f,"Rig floor differs from measured mesh floor");
  float weaponReach = Vector3.Distance(rig.InverseTransformPoint(knife.transform.position),new Vector3(0,.75f,.35f));
  Check(weaponReach<.6f,"Weapon is not near the starting hand position");
  results["player"] = new {rigLocal=rig.localPosition.ToString("F3"),forward=rig.forward.ToString(),weaponLocal=knife.transform.localPosition.ToString("F3"),weaponReach,artificialLocomotion=Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Any(b=>b!=null&&b.GetType().Name=="FirstPersonLocomotor"&&b.enabled)};

  var testMotorObject = new GameObject("Verification motor WITHOUT XR rig");
  testMotorObject.SetActive(false);
  var testMotor = testMotorObject.AddComponent<CarriageMotor>();
  Set(testMotor,"maximumSpeed",20f); Set(testMotor,"startingSpeed",20f); Set(testMotor,"decelerationRate",1f); Set(testMotor,"followRoadCurvature",false);
  testMotorObject.SetActive(true);
  try {
   await Task.Yield();
   float initial=testMotor.Speed,start=Time.time;
   var speeds=new List<float>{initial};
   for(int i=0;i<3;i++){await Task.Delay(1000);speeds.Add(testMotor.Speed);}
   float elapsed=Time.time-start;
   Check(Mathf.Abs(testMotor.Speed-(initial-elapsed))<.12f,"Deceleration did not follow rate * deltaTime");
   Check(speeds.Zip(speeds.Skip(1),(a,b)=>b<a).All(v=>v),"Speed did not decrease progressively");
   results["deceleration"] = new {rate=1,speeds,elapsed,actualLoss=initial-testMotor.Speed,mainRate=Get<float>(mainCart.GetComponent<CarriageMotor>(),"decelerationRate"),mainLashGain=Get<float>(mainCart.GetComponent<CarriageMotor>(),"accelerationPerStroke"),mainMaximum=Get<float>(mainCart.GetComponent<CarriageMotor>(),"maximumSpeed")};
  } finally {Object.Destroy(testMotorObject);}

  var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
  var entry=spawner.SpawnEntries.First(e=>e.movementType==MonsterMovementType.Ground);
  Check(entry.spawnDirection==MonsterSpawnDirection.FrontDispersed,"Zombies are not configured front dispersed");
  var placements=new List<Vector3>();
  for(int i=0;i<50;i++) if(spawner.TryFindSpawnPosition(entry,out var p))placements.Add(mainCart.InverseTransformPoint(p));
  Check(placements.Count>=10 && placements.All(p=>p.z<0),"Front ground placements failed");
  Check(placements.Any(p=>p.x<-.3f)&&placements.Any(p=>p.x>.3f),"Spawn dispersion lacks left/right positions");
  results["spawn"] = new {count=placements.Count,left=placements.Count(p=>p.x<0),right=placements.Count(p=>p.x>0),minX=placements.Min(p=>p.x),maxX=placements.Max(p=>p.x),minZ=placements.Min(p=>p.z),maxZ=placements.Max(p=>p.z)};

  var originalPoints=Object.FindAnyObjectByType<CartAttachmentPoints>().Points.ToArray();
  var root=new GameObject("Verification cart and terrain WITHOUT XR rig");
  root.transform.position=new Vector3(1000,0,0);
  var terrain=GameObject.CreatePrimitive(PrimitiveType.Cube); terrain.name="Temporary verification ground";terrain.transform.SetParent(root.transform);terrain.transform.localPosition=new Vector3(0,-.5f,0);terrain.transform.localScale=new Vector3(100,1,200);terrain.AddComponent<MonsterGroundSurface>();
  var cart=new GameObject("Test cart").transform;cart.SetParent(root.transform,false);
  var targetObject=new GameObject("Test hunter target WITHOUT camera");targetObject.transform.SetParent(cart,false);targetObject.transform.localPosition=new Vector3(0,1.7f,-.6f);
  var target=targetObject.AddComponent<PrototypeHunterMonsterTarget>();
  var targets=cart.gameObject.AddComponent<MonsterTargetRegistry>();targets.Configure(new[]{target});
  var pointRegistry=cart.gameObject.AddComponent<CartAttachmentPoints>();Set(pointRegistry,"rearPointsOnly",false);
  var points=new List<MonsterAttachmentPoint>();
  foreach(var original in originalPoints.Where(p=>p.AcceptedKinds==MonsterAttachmentKind.Ground)) {
   var o=new GameObject(original.name);o.transform.SetParent(cart,false);o.transform.localPosition=mainCart.InverseTransformPoint(original.transform.position);
   var p=o.AddComponent<MonsterAttachmentPoint>();p.Configure(MonsterAttachmentKind.Ground,1,cart.InverseTransformDirection(original.WorldFacingDirection));points.Add(p);
  }
  pointRegistry.Configure(points);
  var load=cart.gameObject.AddComponent<CartMonsterLoad>();load.Configure(60,.3f,null);
  var rear=new GameObject("Test rear direction").transform;rear.SetParent(cart,false);rear.localPosition=Vector3.forward*3;
  var context=new MonsterSpawnContext{cartTransform=cart,rearReachPoint=rear,patrolCenter=cart.position,targetRegistry=targets,attachmentPoints=pointRegistry,cartLoad=load,hunterTarget=target};
  var spawned=new List<MonsterBase>();
  Func<Vector3,MonsterBase> zombie = local => {
   var obj=Object.Instantiate(entry.prefab,cart.TransformPoint(local),Quaternion.identity,root.transform);
   var monster=obj.GetComponent<MonsterBase>();monster.enabled=false;monster.ConfigureSpawnDirection(MonsterSpawnDirection.FrontDispersed);monster.Initialize(context);spawned.Add(monster);return monster;
  };
  try {
   Physics.SyncTransforms();
   var pursuer=zombie(new Vector3(5,0,-8));
   for(int i=0;i<5;i++)pursuer.Tick(.1f);
   Vector3 oldPosition=pursuer.transform.position;
   cart.position+=Vector3.back*20; // Only a test cart with no rig, head, camera or XR Origin.
   for(int i=0;i<10;i++)pursuer.Tick(.05f);
   var movement=pursuer.GetComponent<MonsterMovement>();
   Check(Vector3.Distance(oldPosition,pursuer.transform.position)>.1f,"Passed zombie remained stopped");
   Check(Vector3.Dot(pursuer.transform.forward,(pursuer.Attachment.Destination-pursuer.transform.position).normalized)>.7f,"Passed zombie failed to turn towards cart");
   results["pursuit"] = new {state=pursuer.State.ToString(),distanceMoved=Vector3.Distance(oldPosition,pursuer.transform.position),velocity=movement.Velocity.ToString(),turned=true};
   pursuer.Retire();
   foreach(var p in points) {
    var m=zombie(p.transform.localPosition+Vector3.up*.02f);
    for(int i=0;i<12;i++)m.Tick(.1f);
    Check(m.State==MonsterState.Attached && m.Attachment.IsAttached,"Zombie failed to attach at "+p.name);
    Check(m.Attachment.ReservedPoint==p,"Zombie reserved wrong point");
    Check(m.transform.parent==p.transform,"Attached zombie is not parented to cart point");
    Check(m.GetComponent<MonsterMovement>().Velocity.sqrMagnitude<.001f,"Attached zombie is still chasing");
    Check(m.GetComponent<MonsterAnimationController>().HasAttackAnimation,"No existing attack animation on zombie");
    var attack=m.GetComponent<AttachedMonsterAttack>();attack.TickAttack(Time.time);Check(attack.AnimationCycleCount>0,"No attack/bite cycle after attachment");
   }
   Check(points.Count==5 && points.All(p=>p.OccupiedCount==1),"Attachment occupancy is not exclusive");
   var overflow=zombie(points[0].transform.localPosition);
   for(int i=0;i<5;i++)overflow.Tick(.1f);
   Check(!overflow.Attachment.HasReservedPoint,"Sixth zombie reused an occupied point");
   var attached=spawned.Where(m=>m.Attachment.IsAttached).ToArray();
   var locals=attached.Select(m=>m.transform.localPosition).ToArray();
   cart.position+=Vector3.back*3;cart.rotation=Quaternion.Euler(0,20,0);
   for(int i=0;i<attached.Length;i++)Check(Vector3.Distance(attached[i].transform.localPosition,locals[i])<.0001f,"Attached zombie lost cart-relative pose");
   results["attachments"] = new {points=points.Select(p=>p.name).ToArray(),attached=attached.Length,exclusive=true,attackAnimation=true,followTranslationAndRotation=true};
  } finally {foreach(var m in spawned)if(m!=null)m.Retire();Object.Destroy(root);}

  var ambience=mainCart.GetComponent<CarriageLampAmbience>();
  Check(Get<float>(ambience,"frontIntensity")==7 && Get<float>(ambience,"rearIntensity")==6,"Lamp settings not applied");
  Check(mainCart.GetComponentsInChildren<Light>().All(l=>l.shadows==UnityEngine.LightShadows.None),"Carriage lamps have costly real-time shadows");
  results["lighting"] = new {front=7,rear=6,ambient=RenderSettings.ambientLight.ToString(),existingLightCount=mainCart.GetComponentsInChildren<Light>().Length,noNewShadows=true};
  var capsule=knife.GetComponent<CapsuleCollider>();
  Check(Mathf.Abs(capsule.height-.98f)<.01f,"Weapon collider not extended");
  Check(Mathf.Abs(sword.transform.localPosition.z-.69f)<.01f,"Sword sweep tip not extended");
  results["reach"] = new {colliderLength=capsule.height,tip=sword.transform.localPosition.ToString(),grabComponentPreserved=knife.GetComponentsInChildren<Component>().Any(c=>c!=null&&c.GetType().Name=="HandGrabInteractable")};

  int idleBefore=audio.PlayedSoundCount;await Task.Delay(500);Check(audio.PlayedSoundCount==idleBefore,"Idle weapon continuously emits audio");
  var sequence=new List<string>();
  AudioClip previous=null;
  for(int i=0;i<50 && (i<10 || sequence.Distinct().Count()<5);i++) {
   int before=audio.PlayedSoundCount;
   sword.BeginAttackWindow();
   for(int j=0;j<10;j++){sword.BeginAttackWindow();sword.Sweep(sword.transform.position,sword.transform.position);}
   Check(audio.PlayedSoundCount==before+1,"More than one audio emission in a swing");
   Check(audio.LastPlayedClip!=previous,"Immediate audio repetition");
   previous=audio.LastPlayedClip;sequence.Add(previous.name);sword.EndAttackWindow();await Task.Delay(180);
  }
  Check(sequence.Distinct().Count()==5,"All five sounds were not observed in random attacks");
  int count=audio.PlayedSoundCount;
  audio.Configure(sword,knife.GetComponent<AudioSource>(),new[]{null,clips[2],null});sword.BeginAttackWindow();sword.EndAttackWindow();
  audio.Configure(sword,knife.GetComponent<AudioSource>(),new AudioClip[5]);sword.BeginAttackWindow();sword.EndAttackWindow();
  audio.Configure(sword,knife.GetComponent<AudioSource>(),null);sword.BeginAttackWindow();sword.EndAttackWindow();
  Check(audio.PlayedSoundCount==count+1,"Missing clips emitted invalid audio");
  audio.Configure(sword,knife.GetComponent<AudioSource>(),clips);
  results["audio"] = new {attacks=sequence.Count,sequence,allFiveUsed=true,onePerSwing=true,noImmediateRepeat=true,idleSilent=true,nullSafe=true,sourceCount=knife.GetComponents<AudioSource>().Length,oneShotVolume=Get<float>(audio,"volume")};
  var victimObject=Object.Instantiate(entry.prefab);
  var victim=victimObject.GetComponent<MonsterBase>();victim.enabled=false;
  try {
   victim.Initialize(new MonsterSpawnContext{cartTransform=mainCart,patrolCenter=mainCart.position,targetRegistry=Object.FindAnyObjectByType<MonsterTargetRegistry>(),attachmentPoints=Object.FindAnyObjectByType<CartAttachmentPoints>(),hunterTarget=Object.FindAnyObjectByType<PrototypeHunterMonsterTarget>()});
   victimObject.transform.position=sword.transform.position-Vector3.up*.95f;
   var hp=victimObject.GetComponent<MonsterDamageable>();
   float initialHealth=hp.CurrentHealth,amount=Get<float>(sword,"damage");
   Physics.SyncTransforms();
   int before=audio.PlayedSoundCount;
   sword.BeginAttackWindow();
   Check(sword.Sweep(sword.transform.position,sword.transform.position)==1,"Sword failed to damage zombie");
   for(int i=0;i<10;i++)sword.Sweep(sword.transform.position,sword.transform.position);
   Check(Mathf.Approximately(hp.CurrentHealth,initialHealth-amount),"Multiple damage activations within one swing");
   Check(!victim.IsDead,"Zombie died before health reached zero");
   Check(victimObject.GetComponent<MonsterSwordHitFeedback>()!=null,"Zombie lacked visual hit feedback");
   sword.EndAttackWindow();
   int hitCount=1;
   while(hp.CurrentHealth>0 && hitCount<10){sword.BeginAttackWindow();sword.Sweep(sword.transform.position,sword.transform.position);sword.EndAttackWindow();hitCount++;}
   Check(victim.IsDead && hp.CurrentHealth==0,"Zombie did not die at zero health");
   Check(audio.PlayedSoundCount==before+hitCount,"Impact produced duplicate sword audio");
   results["damage"] = new {initialHealth,damagePerHit=amount,hitsToDeath=hitCount,oneHitPerWindow=true,visualReaction=true,deathState=victim.State.ToString()};
  } finally {Object.Destroy(victimObject);}
  return results;
 }
}
