using System;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using JapaneseDemonHunter.Gameplay;
using JapaneseDemonHunter.Monsters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using System.Linq;
using Reins;
using UnityEngine;
using Object=UnityEngine.Object;

public static class SetupNextRound
{
    public static object SteeringSensitivity()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first");
        JapaneseDemonHunter.GameplayEditor.RideFeatureSetup.SteeringSensitivityRound();
        return new {passed=true,sensitivityMetres=.15f};
    }
    public static object Victory()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first");
        var goal=Object.FindAnyObjectByType<CastleGoalTrigger>();
        var victory=Object.FindAnyObjectByType<LevelVictoryController>();
        var root=goal.transform.parent.Find("VictoryFireworks");
        if(root==null) {root=new GameObject("VictoryFireworks").transform; root.SetParent(goal.transform.parent,false);}
        const string materialPath="Assets/Materials/Prototype/Mat_VictorySpark.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null) {material=new Material(Shader.Find("JapaneseDemonHunter/VictorySpark")); AssetDatabase.CreateAsset(material,materialPath);}
        var effects=new System.Collections.Generic.List<ParticleSystem>();
        for(int i=0;i<3;i++)
        {
            var emitter=root.Find("Firework_"+i);
            if(emitter==null) {emitter=new GameObject("Firework_"+i).transform; emitter.SetParent(root,false); emitter.gameObject.AddComponent<ParticleSystem>();}
            emitter.SetPositionAndRotation(goal.transform.position+goal.transform.rotation*new Vector3((i-1)*22f,20f,-45f),Quaternion.identity);
            var effect=emitter.GetComponent<ParticleSystem>(); effect.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=effect.main; main.duration=3f; main.loop=true; main.playOnAwake=false;
            main.simulationSpace=ParticleSystemSimulationSpace.World; main.startLifetime=new ParticleSystem.MinMaxCurve(1.6f,2.2f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(9f,14f); main.startSize=new ParticleSystem.MinMaxCurve(.18f,.35f);
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(1f,.75f,.2f),new Color(1f,.4f,.08f));
            main.gravityModifier=.35f; main.maxParticles=64;
            var emission=effect.emission; emission.rateOverTime=0f;
            emission.SetBursts(new[]{new ParticleSystem.Burst(i*.25f,24)});
            var shape=effect.shape; shape.shapeType=ParticleSystemShapeType.Sphere; shape.radius=.1f;
            var colors=effect.colorOverLifetime; colors.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)}); colors.color=gradient;
            var renderer=effect.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material;
            renderer.renderMode=ParticleSystemRenderMode.Stretch; renderer.velocityScale=.08f; renderer.lengthScale=2f;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
            effects.Add(effect);
        }
        var settings=new SerializedObject(victory);
        settings.FindProperty("castleGoal").objectReferenceValue=goal;
        var fireworks=settings.FindProperty("victoryFireworks"); fireworks.arraySize=effects.Count;
        for(int i=0;i<effects.Count;i++) fireworks.GetArrayElementAtIndex(i).objectReferenceValue=effects[i];
        settings.ApplyModifiedProperties();
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(victory.gameObject.scene); EditorSceneManager.SaveScene(victory.gameObject.scene);
        return new {emitters=3,burst=24,maximumParticles=192,arrivalTrigger=true};
    }
    public static object Castle()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first");
        var road=Object.FindAnyObjectByType<ForestRoad>();
        var motor=Object.FindAnyObjectByType<CarriageMotor>();
        var path=road.CreatePathModel();
        path.GetPoseAtDistance(road.LevelDistance-8f,out var gate,out float heading);
        gate=road.transform.TransformPoint(gate);
        var kingdom=road.transform.Find("Kingdom");
        if(kingdom==null) throw new InvalidOperationException("Existing kingdom not found");
        foreach(Transform child in kingdom)
            if(child.name!="Fortress_Model" && child.name!="CastleGoalTrigger" && child.name!="CastleEntryFloor" && child.name!="CastleWarmLights")
                child.gameObject.SetActive(false);
        var model=kingdom.Find("Fortress_Model");
        if(model==null)
        {
            model=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Fortress.fbx"),kingdom)).transform;
            model.name="Fortress_Model";
        }
        Quaternion rotation=Quaternion.Euler(0,heading,0);
        model.rotation=rotation; model.localScale=Vector3.one*320f;
        model.position=gate-rotation*new Vector3(-.0875f,.061f,.86f)*320f;
        const string materialPath="Assets/Materials/Prototype/Mat_CastleBeacon.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null) {material=new Material(Shader.Find("JapaneseDemonHunter/CastleBeacon")); AssetDatabase.CreateAsset(material,materialPath);}
        material.SetColor("_BaseColor",new Color(.52f,.4f,.21f));
        material.SetColor("_EmissionColor",new Color(.45f,.25f,.045f));
        foreach(var renderer in model.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>material).ToArray();
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
        {
            var collider=filter.GetComponent<MeshCollider>();
            if(collider==null) collider=filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh=filter.sharedMesh;
        }
        var floor=kingdom.Find("CastleEntryFloor");
        if(floor==null) {floor=GameObject.CreatePrimitive(PrimitiveType.Cube).transform; floor.name="CastleEntryFloor"; floor.SetParent(kingdom,false);}
        floor.SetPositionAndRotation(gate-rotation*Vector3.forward*10f-Vector3.up*.1f,rotation);
        floor.localScale=new Vector3(18f,.2f,32f);
        floor.GetComponent<Renderer>().sharedMaterial=material;
        var goal=kingdom.Find("CastleGoalTrigger");
        if(goal==null) {goal=new GameObject("CastleGoalTrigger").transform; goal.SetParent(kingdom,false); goal.gameObject.AddComponent<CastleGoalTrigger>();}
        path.GetPoseAtDistance(road.LevelDistance,out var goalPosition,out float goalHeading);
        goal.SetPositionAndRotation(road.transform.TransformPoint(goalPosition)+Vector3.up*2f,Quaternion.Euler(0,goalHeading,0));
        var box=goal.GetComponent<BoxCollider>(); box.isTrigger=true; box.size=new Vector3(11f,6f,3f);
        var body=goal.GetComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
        var goalSettings=new SerializedObject(goal.GetComponent<CastleGoalTrigger>());
        goalSettings.FindProperty("carriage").objectReferenceValue=motor; goalSettings.ApplyModifiedProperties();
        var lights=kingdom.Find("CastleWarmLights");
        if(lights==null) {lights=new GameObject("CastleWarmLights").transform; lights.SetParent(kingdom,false);}
        for(int side=-1;side<=1;side+=2)
        {
            var lamp=lights.Find("GateLight_"+side);
            if(lamp==null) {lamp=new GameObject("GateLight_"+side).transform; lamp.SetParent(lights,false); lamp.gameObject.AddComponent<Light>();}
            lamp.position=gate+rotation*new Vector3(side*7f,7f,3f);
            var light=lamp.GetComponent<Light>(); light.type=LightType.Point; light.color=new Color(1,.7f,.25f);
            light.intensity=3f; light.range=18f; light.shadows=LightShadows.None;
        }
        Object.FindAnyObjectByType<LevelVictoryController>().ConfigurePresentation(null,lights.GetComponentsInChildren<Light>());
        foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            if(camera.transform.IsChildOf(motor.transform) || camera.GetComponentInParent<DesktopDebugMode>()!=null)
                camera.farClipPlane=Mathf.Max(camera.farClipPlane,2000f);
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(road.gameObject.scene); EditorSceneManager.SaveScene(road.gameObject.scene);
        return new {asset="Assets/Art/Fortress.fbx",scale=320f,gatePosition=gate.ToString(),goalPosition=goal.position.ToString(),
            entryWidth=49.6f,entryHeight=34f,dynamicLights=2,shadowLights=0,fogBypass="Castle only, depth tested"};
    }
    public static object RidePacing()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first");
        var road=Object.FindAnyObjectByType<ForestRoad>();
        var serialized=new SerializedObject(road);
        serialized.FindProperty("totalTiles").intValue=80;
        serialized.ApplyModifiedProperties();
        var kingdom=road.transform.Find("Kingdom");
        if(kingdom!=null)
        {
            road.CreatePathModel().GetPoseAtDistance(road.LevelDistance,out var position,out float heading);
            Undo.RecordObject(kingdom,"Move goal to paced route end");
            kingdom.SetPositionAndRotation(road.transform.TransformPoint(position),Quaternion.Euler(0,heading+180f,0));
        }
        EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
        EditorSceneManager.SaveScene(road.gameObject.scene);
        return new {distance=road.LevelDistance,tiles=road.TotalTiles,motorBalanceUnchanged=true};
    }
    public static object EncounterPlacement()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first");
        var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
        var settings=new SerializedObject(spawner);
        settings.FindProperty("useEncounterGroups").boolValue=true;
        settings.FindProperty("minimumHordeSize").intValue=0;
        settings.FindProperty("forestRoot").objectReferenceValue=Object.FindAnyObjectByType<ForestRoad>().transform;
        settings.FindProperty("maximumPlacementAttempts").intValue=32;
        settings.ApplyModifiedProperties();
        string path=AssetDatabase.GetAssetPath(spawner.SpawnEntries.First(e=>e.movementType==MonsterMovementType.Ground).prefab);
        var runImporter=(ModelImporter)AssetImporter.GetAtPath("Assets/Art/Zombie/Zombie.fbx");
        var runClips=runImporter.clipAnimations.Length>0 ? runImporter.clipAnimations : runImporter.defaultClipAnimations;
        bool runChanged=false;
        foreach(var imported in runClips)
            if(imported.name.Contains("ZombieRun") && !imported.loopTime) {imported.loopTime=true; runChanged=true;}
        if(runChanged) {runImporter.clipAnimations=runClips; runImporter.SaveAndReimport();}
        var prefab=PrefabUtility.LoadPrefabContents(path);
        string clipName=""; string runState="";
        try
        {
            var controller=prefab.GetComponentInChildren<Animator>().runtimeAnimatorController as AnimatorController;
            var state=controller.layers[0].stateMachine.states.Select(s=>s.state)
                .First(s=>s.motion is AnimationClip clip && clip.name=="Zombie|ZombieRun");
            clipName=state.motion.name; runState=state.name;
            var animation=new SerializedObject(prefab.GetComponent<MonsterAnimationController>());
            animation.FindProperty("runState").stringValue=runState;
            animation.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(prefab,path);
        }
        finally {PrefabUtility.UnloadPrefabContents(prefab);}
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        EditorSceneManager.SaveScene(spawner.gameObject.scene);
        return new {frontMaximum=1,lateralMinimum=2,lateralMaximum=4,clip=clipName,runState};
    }
    public static object InitialSpawn()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first");
        var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
        Undo.RecordObject(spawner,"Initial spawn after gameplay starts");
        var serialized=new SerializedObject(spawner);
        serialized.FindProperty("initialSpawnDelay").floatValue=7f;
        serialized.FindProperty("spawnOneOfEachOnStart").boolValue=false;
        serialized.FindProperty("waitForFirstGallop").boolValue=false;
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        EditorSceneManager.SaveScene(spawner.gameObject.scene);
        return new {initialSpawnDelay=7f,startsAfterGameSession=true};
    }
    static Handedness ResolveSide(Object provider,HashSet<Object> seen)
    {
        if(provider==null || !seen.Add(provider)) throw new InvalidOperationException("Unresolved hand provider");
        var serialized=new SerializedObject(provider);
        var side=serialized.FindProperty("_handedness");
        if(side!=null) return (Handedness)side.intValue;
        var property=serialized.GetIterator();
        while(property.NextVisible(true))
            if(property.propertyType==SerializedPropertyType.ObjectReference && property.objectReferenceValue is MonoBehaviour)
                try { return ResolveSide(property.objectReferenceValue,seen); } catch(InvalidOperationException) { }
        throw new InvalidOperationException("Handedness unavailable for "+provider.name);
    }

    public static object HandFeedback()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorSceneManager.GetActiveScene().path!="Assets/Scenes/JapanDemonHunter.unity")
            throw new InvalidOperationException("Open stopped JapanDemonHunter");
        var controller=Object.FindAnyObjectByType<HandStrikeController>();
        var left=new List<MaterialPropertyBlockEditor>(); var right=new List<MaterialPropertyBlockEditor>();
        foreach(var visual in Object.FindObjectsByType<HandVisual>(FindObjectsInactive.Include))
        {
            if(visual.GetComponentInParent<DistanceHandGrabInteractor>()!=null) continue;
            var settings=new SerializedObject(visual);
            var side=ResolveSide(settings.FindProperty("_hand").objectReferenceValue,new HashSet<Object>());
            foreach(string field in new[]{"_handMaterialPropertyBlockEditor","_openXRHandMaterialPropertyBlockEditor"})
            {
                var material=settings.FindProperty(field)?.objectReferenceValue as MaterialPropertyBlockEditor;
                if(material!=null && !(side==Handedness.Left?left:right).Contains(material))
                    (side==Handedness.Left?left:right).Add(material);
            }
        }
        if(left.Count==0 || right.Count==0) throw new InvalidOperationException("Missing existing hand materials");
        Undo.RecordObject(controller,"Bind existing strike feedback materials");
        var serialized=new SerializedObject(controller);
        foreach(var pair in new[]{(name:"leftFeedbackMaterials",list:left),(name:"rightFeedbackMaterials",list:right)})
        {
            var array=serialized.FindProperty(pair.name); array.arraySize=pair.list.Count;
            for(int i=0;i<pair.list.Count;i++) array.GetArrayElementAtIndex(i).objectReferenceValue=pair.list[i];
        }
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        EditorSceneManager.SaveScene(controller.gameObject.scene);
        return new {left=left.Count,right=right.Count,duplicatedHands=0};
    }
    public static object Play() { EditorApplication.isPlaying=true; return new {requested=true}; }
    public static object Stop() { EditorApplication.isPlaying=false; return new {requested=true}; }
}




