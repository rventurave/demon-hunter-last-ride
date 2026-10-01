using System;
using System.IO;
using System.Reflection;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace JapaneseDemonHunter.GameplayEditor
{
    /// <summary>
    /// Editor-only fallback when the HTTP pipeline is unreachable. Executes only explicit,
    /// short-lived local requests; never runs a build, reconstructs a scene or alters XR.
    /// </summary>
    [InitializeOnLoad]
    internal static class RideLocalVerificationBridge
    {
        const string RequestPath="Library/RideLocalRequest.json";
        const string ReplyPath="Library/RideLocalReply.json";
        const string PendingKey="JDH.LocalVerification.PendingPlay";
        const string LastKey="JDH.LocalVerification.LastRequest";
        static double nextPoll;
        static double playingSince;
        [Serializable] sealed class Request { public string id; public string action; public string file; public string entry; public long expiresUtcTicks; }
        [Serializable] sealed class Reply
        {
            public string id, action, project, scene, error;
            public string details;
            public int protocolVersion=2;
            public bool passed, playMode;
            public float zombieHealth, swordDamage, handDamage, firstHandHealth;
            public int swordHits, handHits;
            public int swordImpactSounds, deathSounds;
        }

        static RideLocalVerificationBridge() { EditorApplication.update+=Tick; }
        static void Tick()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup<nextPoll) return;
            nextPoll=EditorApplication.timeSinceStartup+.25;
            string pending=SessionState.GetString(PendingKey,"");
            if(!string.IsNullOrEmpty(pending))
            {
                if(!EditorApplication.isPlaying) return;
                if(playingSince==0) { playingSince=EditorApplication.timeSinceStartup; return; }
                if(EditorApplication.timeSinceStartup-playingSince<.5) return;
                var request=JsonUtility.FromJson<Request>(pending);
                SessionState.EraseString(PendingKey);
                Execute(request,true);
                EditorApplication.isPlaying=false;
                return;
            }
            if(!File.Exists(RequestPath)) return;
            Request item;
            try { item=JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath)); }
            catch { return; } // Ignore partially written requests.
            if(item==null || string.IsNullOrEmpty(item.id) || item.expiresUtcTicks<DateTime.UtcNow.Ticks ||
                item.id==SessionState.GetString(LastKey,"")) return;
            SessionState.SetString(LastKey,item.id);
            if(item.action=="verification") { RunVerification(item); return; }
            if(item.action=="balancePlay")
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                { Write(item,new Reply {error="Play test requires a stopped, saved scene."}); return; }
                if(SceneManager.GetActiveScene().path!="Assets/Scenes/JapanDemonHunter.unity")
                { Write(item,new Reply {error="Open JapanDemonHunter first."}); return; }
                SessionState.SetString(PendingKey,JsonUtility.ToJson(item));
                EditorApplication.isPlaying=true;
                return;
            }
            Execute(item,false);
        }
        static void Execute(Request item,bool play)
        {
            var reply=new Reply {playMode=play};
            try
            {
                switch(item.action)
                {
                    case "status": reply.passed=true; break;
                    case "refresh":
                        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before recompiling.");
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        reply.passed=true; break;
                    case "inspectHands":
                        var report=new System.Text.StringBuilder();
                        foreach(var visual in Object.FindObjectsByType<Oculus.Interaction.HandVisual>(FindObjectsInactive.Include))
                        {
                            report.AppendLine(visual.name);
                            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
                                foreach(var material in renderer.sharedMaterials)
                                    if(material!=null) report.AppendLine("  "+material.name+" shader="+material.shader.name);
                            foreach(var component in visual.GetComponentsInParent<MonoBehaviour>(true))
                                if(component!=null) report.AppendLine("  "+component.GetType().FullName);
                        }
                        reply.details=report.ToString(); reply.passed=true; break;
                    case "balanceEdit":
                        RideFeatureSetup.SmallZombieBalanceRound();
                        ValidateBalance(reply); break;
                    case "balancePlay": ValidateBalance(reply); break;
                    default: throw new InvalidOperationException("Unsupported local verification request.");
                }
            }
            catch(Exception error) { reply.passed=false; reply.error=(error.InnerException??error).ToString(); }
            Write(item,reply);
        }
        static void Write(Request request,Reply reply)
        {
            reply.id=request.id; reply.action=request.action;
            reply.project=Application.dataPath;
            reply.scene=SceneManager.GetActiveScene().path;
            File.WriteAllText(ReplyPath,JsonUtility.ToJson(reply,true));
        }
        static async void RunVerification(Request request)
        {
            var reply=new Reply {playMode=Application.isPlaying};
            try
            {
                string[] allowed={"VerifyNextRound.cs","VerifyRidePriorities.cs","VerifyQuestDesktopTransition.cs",
                    "VerifyDesktopControls.cs","InspectRequestedFeatures.cs","InspectNextRound.cs","SetupNextRound.cs"};
                string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Tools/Verification"));
                string path=Path.GetFullPath(Path.Combine(Application.dataPath,"..",request.file??""));
                string name=Path.GetFileName(path);
                if(Path.GetDirectoryName(path)!=directory || Array.IndexOf(allowed,name)<0 ||
                    request.entry==null || !request.entry.StartsWith(Path.GetFileNameWithoutExtension(name)+".",StringComparison.Ordinal))
                    throw new InvalidOperationException("Only the round's named verification/setup files are allowed.");
                Type command=null;
                foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
                { command=assembly.GetType("Unity.Pipeline.Editor.Commands.Scripts.RunScriptCommand"); if(command!=null) break; }
                if(command==null) throw new InvalidOperationException("Installed pipeline script compiler is unavailable.");
                var method=command.GetMethod("RunScript",BindingFlags.Public|BindingFlags.Static);
                var parameters=method.GetParameters(); var args=new object[parameters.Length];
                for(int i=0;i<args.Length;i++) args[i]=parameters[i].DefaultValue;
                args[0]=path; args[1]=request.entry;
                var task=(System.Threading.Tasks.Task)method.Invoke(null,args);
                await task; // Keep the Editor update loop alive for async Play checks.
                object result=task.GetType().GetProperty("Result").GetValue(task);
                // Pipeline response uses public properties, which JsonUtility does not serialize.
                var jsonType=command.Assembly.GetType("Newtonsoft.Json.JsonConvert");
                if(jsonType==null) foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())
                { jsonType=assembly.GetType("Newtonsoft.Json.JsonConvert"); if(jsonType!=null) break; }
                if(jsonType==null) throw new InvalidOperationException("Pipeline JSON serializer is unavailable.");
                reply.details=(string)jsonType.GetMethod("SerializeObject",new[]{typeof(object)}).Invoke(null,new[]{result});
                reply.passed=true;
            }
            catch(Exception error) { reply.error=(error.InnerException??error).ToString(); }
            Write(request,reply);
        }
        static float Field(object target,string name) => (float)target.GetType().GetField(name,
            BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        static void AwakeForEdit(MonoBehaviour target)
        {
            if(!Application.isPlaying) target.GetType().GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(target,null);
        }
        static void Require(bool result,string message) { if(!result) throw new InvalidOperationException(message); }
        static void ValidateBalance(Reply reply)
        {
            var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
            GameObject prefab=null;
            foreach(var entry in spawner.SpawnEntries)
                if(entry!=null && entry.prefab!=null && entry.movementType==MonsterMovementType.Ground && !entry.isFaceThreat)
                    {prefab=entry.prefab; break;}
            if(prefab==null) throw new InvalidOperationException("Scene has no normal zombie entry.");
            reply.zombieHealth=prefab.GetComponent<MonsterDamageable>().MaximumHealth;
            var weapon=Object.FindAnyObjectByType<GrabbableWeapon>();
            reply.swordDamage=Field(weapon.GetComponentInChildren<SwordDamage>(),"damage");
            reply.handDamage=Field(Object.FindAnyObjectByType<HandStrikeController>(),"punchDamage");
            Require(reply.zombieHealth==12 && reply.swordDamage==12 && reply.handDamage==8,"Balance must be 12 HP / 12 sword / 8 hand.");
            var victims=new[]{Object.Instantiate(prefab),Object.Instantiate(prefab)};
            var tip=new GameObject("TemporaryBalanceSweep");
            try
            {
                for(int i=0;i<victims.Length;i++)
                {
                    victims[i].transform.position=new Vector3(10000+i*4,0,0);
                    AwakeForEdit(victims[i].GetComponent<MonsterBase>());
                    AwakeForEdit(victims[i].GetComponent<MonsterDamageable>());
                }
                Physics.SyncTransforms();
                var sweep=tip.AddComponent<SwordDamage>();
                sweep.enabled=false;
                sweep.Configure(tip.transform,reply.swordDamage,.2f,~0);
                var soundscape=Object.FindAnyObjectByType<GameSoundscape>();
                var deathClip=(AudioClip)typeof(GameSoundscape).GetField("zombieDeathClip",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(soundscape);
                var emitters=new MonsterSoundEmitter[2];
                for(int i=0;i<victims.Length;i++)
                {
                    emitters[i]=victims[i].AddComponent<MonsterSoundEmitter>();
                    emitters[i].Configure(null,deathClip,null,Field(soundscape,"zombieDeathSoundVolume"));
                }
                var sceneFeedback=weapon.GetComponent<SwordSwingAudio>();
                var clips=(AudioClip[])typeof(SwordSwingAudio).GetField("swordSounds",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(sceneFeedback);
                var source=tip.AddComponent<AudioSource>(); source.playOnAwake=false; source.spatialBlend=1; source.maxDistance=10;
                var feedback=tip.AddComponent<SwordSwingAudio>(); feedback.Configure(sweep,source,clips);
                var blade=victims[0].GetComponent<MonsterDamageable>();
                Vector3 bladeContact=victims[0].GetComponentInChildren<Collider>().bounds.center;
                tip.transform.position=bladeContact;
                sweep.BeginAttackWindow();
                Require(sweep.Sweep(bladeContact,bladeContact)==1 && !blade.IsAlive,"One valid sword impact must kill.");
                Require(sweep.Sweep(bladeContact,bladeContact)==0,"Persistent sword contact repeats damage.");
                Require(feedback.PlayedSoundCount==1 && feedback.LastPlayedClip.name!="5","Fatal sword damage lost impact audio.");
                Require(emitters[0].DeathSoundCount==1,"Fatal sword damage lost death audio.");
                reply.swordImpactSounds=feedback.PlayedSoundCount;
                feedback.Configure(null,null,null);
                reply.swordHits=1;
                sweep.EndAttackWindow(); sweep.Configure(tip.transform,reply.handDamage,.2f,~0);
                var fist=victims[1].GetComponent<MonsterDamageable>();
                Vector3 fistContact=victims[1].GetComponentInChildren<Collider>().bounds.center;
                sweep.BeginAttackWindow();
                Require(sweep.Sweep(fistContact,fistContact)==1 && fist.IsAlive,"First valid hand impact must not kill.");
                reply.firstHandHealth=fist.CurrentHealth;
                Require(reply.firstHandHealth==4 && sweep.Sweep(fistContact,fistContact)==0,"Persistent hand contact repeats damage.");
                sweep.EndAttackWindow(); sweep.BeginAttackWindow();
                Require(sweep.Sweep(fistContact,fistContact)==1 && !fist.IsAlive,"Second valid hand impact must kill.");
                Require(emitters[1].DeathSoundCount==1,"Hand kill lost death audio.");
                reply.deathSounds=emitters[0].DeathSoundCount+emitters[1].DeathSoundCount;
                reply.handHits=2; reply.passed=true;
            }
            finally { Object.DestroyImmediate(tip); foreach(var victim in victims) Object.DestroyImmediate(victim); Physics.SyncTransforms(); }
        }
    }
}
